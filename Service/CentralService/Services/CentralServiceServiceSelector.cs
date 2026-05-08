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
            var prioritized = new List<(CandidateService Candidate, ServiceNetworkStatus Status)>(candidates.Count);
            var cacheMissCandidates = new List<CandidateService>();

            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // 心跳状态优先：发现阶段只优先处理“在线”实例。
                if (candidate.Service.Status != 1)
                {
                    continue;
                }

                var cachedStatus = _networkEvaluator.GetServiceNetworkStatus(candidate.Service.Id);
                if (cachedStatus != null)
                {
                    prioritized.Add((candidate, NormalizeStatusWithHeartbeat(candidate.Service, cachedStatus)));
                    continue;
                }

                cacheMissCandidates.Add(candidate);
            }

            if (cacheMissCandidates.Count > 0)
            {
                // 缓存缺失时走轻量评估（仅基于心跳数据/状态合成，不执行实时 Ping）。
                var missingStatuses = await Task.WhenAll(
                    cacheMissCandidates.Select(async candidate =>
                    {
                        var evaluated = await _networkEvaluator.EvaluateServiceNetworkAsync(candidate.Service.Id);
                        return new
                        {
                            Candidate = candidate,
                            Status = evaluated == null
                                ? BuildFallbackStatusFromHeartbeat(candidate.Service)
                                : NormalizeStatusWithHeartbeat(candidate.Service, evaluated),
                        };
                    }));

                prioritized.AddRange(missingStatuses.Select(x => (x.Candidate, x.Status)));
            }

            var available = prioritized
                .Where(x => x.Candidate.Service.Status == 1 && x.Status.IsAvailable)
                .OrderByDescending(x => x.Status.CalculateScore())
                .ThenByDescending(x => x.Candidate.EffectiveWeight)
                .Select(x => x.Candidate.Service)
                .ToList();

            var remaining = candidates
                .Where(candidate => available.All(x => !string.Equals(x.Id, candidate.Service.Id, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (available.Count == 0)
            {
                return RotateCandidates(serviceName, remaining.Count > 0 ? remaining : candidates)
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

    private static ServiceNetworkStatus NormalizeStatusWithHeartbeat(ServiceInfo service, ServiceNetworkStatus status)
    {
        if (service == null)
        {
            return status ?? new ServiceNetworkStatus
            {
                ServiceId = string.Empty,
                ResponseTime = 3000,
                PacketLoss = 100,
                LastCheckTime = DateTime.Now,
                ConsecutiveSuccesses = 0,
                ConsecutiveFailures = 1,
                IsAvailable = false,
            };
        }

        if (status == null)
        {
            return BuildFallbackStatusFromHeartbeat(service);
        }

        if (service.Status == 1)
        {
            return new ServiceNetworkStatus
            {
                ServiceId = service.Id,
                ResponseTime = status.ResponseTime > 0 ? status.ResponseTime : BuildFallbackStatusFromHeartbeat(service).ResponseTime,
                PacketLoss = 0,
                LastCheckTime = DateTime.Now,
                ConsecutiveSuccesses = status.ConsecutiveSuccesses > 0 ? status.ConsecutiveSuccesses : 1,
                ConsecutiveFailures = 0,
                IsAvailable = true,
            };
        }

        return new ServiceNetworkStatus
        {
            ServiceId = service.Id,
            ResponseTime = status.ResponseTime > 0 ? status.ResponseTime : 3000,
            PacketLoss = 100,
            LastCheckTime = DateTime.Now,
            ConsecutiveSuccesses = 0,
            ConsecutiveFailures = status.ConsecutiveFailures > 0 ? status.ConsecutiveFailures : 1,
            IsAvailable = false,
        };
    }

    private static ServiceNetworkStatus BuildFallbackStatusFromHeartbeat(ServiceInfo service)
    {
        if (service == null)
        {
            return new ServiceNetworkStatus
            {
                ServiceId = string.Empty,
                ResponseTime = 3000,
                PacketLoss = 100,
                LastCheckTime = DateTime.Now,
                ConsecutiveSuccesses = 0,
                ConsecutiveFailures = 1,
                IsAvailable = false,
            };
        }

        if (service.Status != 1)
        {
            return new ServiceNetworkStatus
            {
                ServiceId = service.Id,
                ResponseTime = 3000,
                PacketLoss = 100,
                LastCheckTime = DateTime.Now,
                ConsecutiveSuccesses = 0,
                ConsecutiveFailures = 1,
                IsAvailable = false,
            };
        }

        var responseTime = 200L;
        if (service.LastHeartbeatTime > DateTime.MinValue)
        {
            var elapsed = DateTime.Now - service.LastHeartbeatTime;
            if (elapsed > TimeSpan.Zero)
            {
                responseTime = (long)Math.Ceiling(elapsed.TotalMilliseconds);
                if (responseTime <= 0)
                {
                    responseTime = 1;
                }
                else if (responseTime > 3000)
                {
                    responseTime = 3000;
                }
            }
        }

        return new ServiceNetworkStatus
        {
            ServiceId = service.Id,
            ResponseTime = responseTime,
            PacketLoss = 0,
            LastCheckTime = DateTime.Now,
            ConsecutiveSuccesses = 1,
            ConsecutiveFailures = 0,
            IsAvailable = true,
        };
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
