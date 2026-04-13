using CentralService.Admin.Config;
using CentralService.Models;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace CentralService.Services;

public sealed class CentralServiceServiceSelector
{
    private readonly ServiceRegistry _serviceRegistry;
    private readonly ServiceNetworkEvaluator _networkEvaluator;
    private readonly CentralServiceRuntimeConfigProvider _configProvider;
    private readonly ILogger<CentralServiceServiceSelector> _logger;

    private readonly ConcurrentDictionary<string, int> _roundRobinCounters = new();

    public CentralServiceServiceSelector(
        ServiceRegistry serviceRegistry,
        ServiceNetworkEvaluator networkEvaluator,
        CentralServiceRuntimeConfigProvider configProvider,
        ILogger<CentralServiceServiceSelector> logger)
    {
        _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
        _networkEvaluator = networkEvaluator ?? throw new ArgumentNullException(nameof(networkEvaluator));
        _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ServiceInfo? DiscoverServiceRoundRobin(string serviceName, string? requesterIp = null)
    {
        var candidates = GetCandidates(serviceName, requesterIp, excludedServiceIds: null);
        if (candidates.Count == 0)
        {
            return null;
        }

        var rotated = RotateCandidates(serviceName, candidates);
        return rotated[0].Service;
    }

    public ServiceInfo? DiscoverServiceWeighted(string serviceName, string? requesterIp = null)
    {
        var candidates = GetCandidates(serviceName, requesterIp, excludedServiceIds: null);
        if (candidates.Count == 0)
        {
            return null;
        }

        var totalWeight = candidates.Sum(x => x.EffectiveWeight);
        if (totalWeight <= 0)
        {
            return DiscoverServiceRoundRobin(serviceName, requesterIp);
        }

        var randomWeight = Random.Shared.Next(1, totalWeight + 1);
        var current = 0;
        foreach (var candidate in candidates)
        {
            current += candidate.EffectiveWeight;
            if (randomWeight <= current)
            {
                return candidate.Service;
            }
        }

        return candidates[0].Service;
    }

    public async Task<ServiceInfo?> DiscoverBestServiceAsync(string serviceName, string? requesterIp = null)
    {
        var candidates = await GetPreferredCandidatesAsync(serviceName, requesterIp, null);
        return candidates.Count == 0 ? null : candidates[0];
    }

    public async Task<IReadOnlyList<ServiceInfo>> GetPreferredCandidatesAsync(
        string serviceName,
        string? requesterIp,
        IEnumerable<string>? excludedServiceIds,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var candidates = GetCandidates(serviceName, requesterIp, excludedServiceIds);
        if (candidates.Count == 0)
        {
            return Array.Empty<ServiceInfo>();
        }

        try
        {
            var statuses = await Task.WhenAll(
                candidates.Select(async candidate =>
                {
                    var status = await _networkEvaluator.EvaluateServiceNetworkAsync(candidate.Service.Id);
                    return new { Candidate = candidate, Status = status };
                }));

            var available = statuses
                .Where(x => x.Status != null && x.Status.IsAvailable)
                .OrderByDescending(x => x.Status!.CalculateScore())
                .ThenByDescending(x => x.Candidate.EffectiveWeight)
                .Select(x => x.Candidate.Service)
                .ToList();

            var remaining = statuses
                .Where(x => x.Status == null || !x.Status.IsAvailable)
                .Select(x => x.Candidate)
                .ToList();

            if (available.Count == 0)
            {
                return RotateCandidates(serviceName, remaining)
                    .Select(x => x.Service)
                    .ToArray();
            }

            var rotatedRemaining = RotateCandidates(serviceName, remaining)
                .Select(x => x.Service)
                .ToArray();

            return available
                .Concat(rotatedRemaining)
                .ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取服务类型 {ServiceName} 的优选候选实例失败，回退到轮询排序", serviceName);
            return RotateCandidates(serviceName, candidates)
                .Select(x => x.Service)
                .ToArray();
        }
    }

    private List<CandidateService> RotateCandidates(string serviceName, List<CandidateService> candidates)
    {
        if (candidates.Count <= 1)
        {
            return candidates;
        }

        var counter = _roundRobinCounters.AddOrUpdate(serviceName, 0, (_, old) => unchecked(old + 1));
        var startIndex = counter;
        if (startIndex < 0)
        {
            startIndex = -startIndex;
        }

        startIndex %= candidates.Count;
        if (startIndex == 0)
        {
            return candidates;
        }

        var rotated = new List<CandidateService>(candidates.Count);
        for (var index = 0; index < candidates.Count; index++)
        {
            rotated.Add(candidates[(startIndex + index) % candidates.Count]);
        }

        return rotated;
    }

    private List<CandidateService> GetCandidates(
        string serviceName,
        string? requesterIp,
        IEnumerable<string>? excludedServiceIds)
    {
        var services = _serviceRegistry.GetServicesByName(serviceName);
        if (services.Count == 0)
        {
            return new List<CandidateService>();
        }

        var excluded = excludedServiceIds == null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(excludedServiceIds, StringComparer.OrdinalIgnoreCase);

        var snapshot = _configProvider.Snapshot;
        if (snapshot.PoliciesByName.TryGetValue(serviceName, out var policy) && policy.PreferLocalNetwork)
        {
            var locals = services.Where(x => x.IsLocalNetwork).ToList();
            if (locals.Count > 0)
            {
                services = locals;
            }
        }

        var candidates = new List<CandidateService>();
        foreach (var service in services)
        {
            if (excluded.Contains(service.Id))
            {
                continue;
            }

            if (snapshot.OverridesById.TryGetValue(service.Id, out var @override) && @override.Disabled)
            {
                continue;
            }

            var discoveryService = ResolveDiscoveryService(service, requesterIp);
            if (discoveryService == null)
            {
                continue;
            }

            var effectiveWeight = service.Weight;
            if (@override?.Weight != null)
            {
                effectiveWeight = @override.Weight.Value;
            }

            if (effectiveWeight < 0)
            {
                effectiveWeight = 0;
            }

            candidates.Add(new CandidateService(discoveryService, effectiveWeight));
        }

        return candidates;
    }

    private ServiceInfo? ResolveDiscoveryService(ServiceInfo service, string? requesterIp)
    {
        if (service == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(requesterIp))
        {
            if (!string.IsNullOrWhiteSpace(service.LocalIp) && IsInSameSubnet(requesterIp, service.LocalIp))
            {
                return CloneWithEntryHost(service, service.LocalIp);
            }

            if (!string.IsNullOrWhiteSpace(service.OperatorIp)
                && string.Equals(service.OperatorIp, requesterIp, StringComparison.OrdinalIgnoreCase))
            {
                return CloneWithEntryHost(service, service.OperatorIp);
            }
        }

        if (!string.IsNullOrWhiteSpace(service.PublicIp))
        {
            return CloneWithEntryHost(service, service.PublicIp);
        }

        if (!string.IsNullOrWhiteSpace(service.OperatorIp))
        {
            return CloneWithEntryHost(service, service.OperatorIp);
        }

        if (!string.IsNullOrWhiteSpace(service.LocalIp))
        {
            return CloneWithEntryHost(service, service.LocalIp);
        }

        return null;
    }

    private static bool IsInSameSubnet(string ip1, string ip2)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ip1) || string.IsNullOrWhiteSpace(ip2))
            {
                return false;
            }

            var segments1 = ip1.Split('.');
            var segments2 = ip2.Split('.');
            if (segments1.Length != 4 || segments2.Length != 4)
            {
                return false;
            }

            return string.Equals(segments1[0], segments2[0], StringComparison.OrdinalIgnoreCase)
                   && string.Equals(segments1[1], segments2[1], StringComparison.OrdinalIgnoreCase)
                   && string.Equals(segments1[2], segments2[2], StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static ServiceInfo CloneWithEntryHost(ServiceInfo service, string entryHost)
    {
        return new ServiceInfo
        {
            Id = service.Id,
            Name = service.Name,
            Host = entryHost,
            LocalIp = service.LocalIp,
            OperatorIp = service.OperatorIp,
            PublicIp = service.PublicIp,
            Port = service.Port,
            ServiceType = service.ServiceType,
            Status = service.Status,
            HealthCheckUrl = service.HealthCheckUrl,
            HealthCheckPort = service.HealthCheckPort,
            HeartbeatIntervalSeconds = service.HeartbeatIntervalSeconds,
            RegisterTime = service.RegisterTime,
            LastHeartbeatTime = service.LastHeartbeatTime,
            Weight = service.Weight,
            Metadata = new Dictionary<string, string>(service.Metadata),
            IsLocalNetwork = service.IsLocalNetwork,
        };
    }

    private sealed record CandidateService(ServiceInfo Service, int EffectiveWeight);
}
