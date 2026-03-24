using CentralService.Internal;
using CentralService.Models;
using CentralService.Service.Models;
using ServiceRegistryInfo = CentralService.Models.ServiceInfo;

namespace CentralService.Services.ServiceCircuiting;

public sealed class ServiceAccessService
{
    private readonly ServiceRegistry _serviceRegistry;
    private readonly CentralServiceServiceSelector _serviceSelector;
    private readonly ServiceCircuitTomlStore _circuitStore;
    private readonly ServiceCircuitJsonStore _circuitJsonStore;
    private readonly ServiceCircuitRuntimeStateStore _runtimeStateStore;
    private readonly ILogger<ServiceAccessService> _logger;

    public ServiceAccessService(
        ServiceRegistry serviceRegistry,
        CentralServiceServiceSelector serviceSelector,
        ServiceCircuitTomlStore circuitStore,
        ServiceCircuitJsonStore circuitJsonStore,
        ServiceCircuitRuntimeStateStore runtimeStateStore,
        ILogger<ServiceAccessService> logger)
    {
        _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
        _serviceSelector = serviceSelector ?? throw new ArgumentNullException(nameof(serviceSelector));
        _circuitStore = circuitStore ?? throw new ArgumentNullException(nameof(circuitStore));
        _circuitJsonStore = circuitJsonStore ?? throw new ArgumentNullException(nameof(circuitJsonStore));
        _runtimeStateStore = runtimeStateStore ?? throw new ArgumentNullException(nameof(runtimeStateStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    internal async Task<ServiceAccessResolveOutcome> ResolveAsync(
        ServiceAccessResolveRequest? request,
        string? requesterIp,
        CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ServiceName))
        {
            return ServiceAccessResolveOutcome.Fail(
                StatusCodes.Status400BadRequest,
                "服务名称不能为空",
                ServiceAccessErrorKeys.NoAvailableInstance);
        }

        var clientKey = NormalizeClientIdentity(
            request.ClientName,
            request.ClientLocalIp,
            request.ClientOperatorIp,
            request.ClientPublicIp,
            requesterIp);

        if (clientKey.IsEmpty)
        {
            return ServiceAccessResolveOutcome.Fail(
                StatusCodes.Status400BadRequest,
                "客户端标识无效",
                ServiceAccessErrorKeys.InvalidClientIdentity);
        }

        var excludedServiceIds = new HashSet<string>(
            request.ExcludedServiceIds ?? Array.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);

        var candidates = await _serviceSelector.GetPreferredCandidatesAsync(
            request.ServiceName,
            requesterIp,
            excludedServiceIds,
            cancellationToken);

        if (candidates.Count == 0)
        {
            return ServiceAccessResolveOutcome.Fail(
                StatusCodes.Status503ServiceUnavailable,
                $"未找到可用的服务实例：{request.ServiceName}",
                ServiceAccessErrorKeys.NoAvailableInstance);
        }

        var nowUtc = DateTimeOffset.UtcNow;
        foreach (var candidate in candidates)
        {
            var persisted = await _circuitJsonStore.EnsureServiceAsync(
                candidate,
                _circuitStore.Defaults,
                cancellationToken);
            if (persisted == null)
            {
                continue;
            }

            if (_runtimeStateStore.IsOpen(clientKey, persisted.ServiceKey, nowUtc))
            {
                continue;
            }

            var ticket = _runtimeStateStore.CreateTicket(
                clientKey,
                persisted.ServiceKey,
                candidate.Id,
                candidate.Name,
                nowUtc);

            return ServiceAccessResolveOutcome.Success(new ServiceAccessResolveResponse
            {
                AccessTicket = ticket,
                MaxAttempts = persisted.CircuitBreaker.MaxAttempts,
                Service = CentralServiceServiceContractMapper.ToApiModel(candidate),
            });
        }

        return ServiceAccessResolveOutcome.Fail(
            StatusCodes.Status503ServiceUnavailable,
            $"服务 {request.ServiceName} 当前对该客户端均处于熔断状态",
            ServiceAccessErrorKeys.CircuitOpen);
    }

    internal async Task<ServiceAccessReportResponse> ReportAsync(
        ServiceAccessReportRequest? request,
        string? requesterIp,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return CreateDecision(ServiceAccessDecisionCodes.RetryResolve, "上报请求不能为空");
        }

        var clientKey = NormalizeClientIdentity(
            request.ClientName,
            request.ClientLocalIp,
            request.ClientOperatorIp,
            request.ClientPublicIp,
            requesterIp);

        if (!_runtimeStateStore.TryConsumeTicket(request.AccessTicket, DateTimeOffset.UtcNow, out var ticket) || ticket == null)
        {
            return CreateDecision(ServiceAccessDecisionCodes.RetryResolve, "访问票据无效或已过期");
        }

        if (!clientKey.IsEmpty && !string.Equals(clientKey.PersistentKey, ticket.ClientKey.PersistentKey, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "访问上报客户端标识与票据不一致，ticket={Ticket} client={Client}",
                ticket.Ticket,
                clientKey.PersistentKey);
        }

        var persisted = _circuitJsonStore.Find(ticket.ServiceKey);
        var circuitBreaker = persisted?.CircuitBreaker ?? _circuitStore.Defaults;
        var nowUtc = DateTimeOffset.UtcNow;

        if (request.Success)
        {
            _runtimeStateStore.ReportSuccess(ticket.ClientKey, ticket.ServiceKey, circuitBreaker);
            return CreateDecision(ServiceAccessDecisionCodes.Complete, null);
        }

        if (string.Equals(request.FailureKind, "Business", StringComparison.OrdinalIgnoreCase))
        {
            return CreateDecision(ServiceAccessDecisionCodes.Complete, request.FailureMessage);
        }

        var opened = _runtimeStateStore.ReportFailure(ticket.ClientKey, ticket.ServiceKey, circuitBreaker, nowUtc);
        if (!opened)
        {
            return CreateDecision(ServiceAccessDecisionCodes.RetryResolve, request.FailureMessage);
        }

        var alternatives = await _serviceSelector.GetPreferredCandidatesAsync(
            ticket.ServiceName,
            requesterIp,
            new[] { ticket.ServiceId },
            cancellationToken);

        foreach (var alternative in alternatives)
        {
            var alternativeEntry = _circuitJsonStore.Find(alternative);
            if (alternativeEntry == null)
            {
                continue;
            }

            if (!_runtimeStateStore.IsOpen(ticket.ClientKey, alternativeEntry.ServiceKey, nowUtc))
            {
                return CreateDecision(ServiceAccessDecisionCodes.TryNextInstance, request.FailureMessage);
            }
        }

        return CreateDecision(ServiceAccessDecisionCodes.RetryResolve, request.FailureMessage);
    }

    internal IReadOnlyList<ServiceCircuitInstanceAdminSnapshot> GetAdminSnapshots(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            return Array.Empty<ServiceCircuitInstanceAdminSnapshot>();
        }

        var nowUtc = DateTimeOffset.UtcNow;
        return _serviceRegistry.GetServicesByName(serviceName)
            .Select(service =>
            {
                var persisted = _circuitJsonStore.Find(service);
                if (persisted == null)
                {
                    return null;
                }

                return new ServiceCircuitInstanceAdminSnapshot(
                    service.Id,
                    persisted.ServiceKey,
                    persisted.LastSeenAtUtc,
                    persisted.CircuitBreaker.Clone(),
                    _runtimeStateStore.GetOpenClients(persisted.ServiceKey, nowUtc));
            })
            .Where(x => x != null)
            .Cast<ServiceCircuitInstanceAdminSnapshot>()
            .ToArray();
    }

    internal async Task<ServiceCircuitBreakerSettings?> UpdateCircuitBreakerAsync(
        string? serviceId,
        ServiceCircuitBreakerSettings? settings,
        CancellationToken cancellationToken = default)
    {
        var service = _serviceRegistry.GetServiceById(serviceId);
        var updated = await _circuitJsonStore.UpdateCircuitBreakerAsync(
            service,
            settings,
            _circuitStore.Defaults,
            cancellationToken);
        return updated?.CircuitBreaker;
    }

    internal bool ClearServiceState(string? serviceId)
    {
        var service = _serviceRegistry.GetServiceById(serviceId);
        if (service == null)
        {
            return false;
        }

        var persisted = _circuitJsonStore.Find(service);
        if (persisted == null)
        {
            return false;
        }

        _runtimeStateStore.ClearService(persisted.ServiceKey);
        return true;
    }

    internal void ClearServiceState(ServiceRegistryInfo? service)
    {
        var persisted = _circuitJsonStore.Find(service);
        if (persisted == null)
        {
            return;
        }

        _runtimeStateStore.ClearService(persisted.ServiceKey);
    }

    private static ClientIdentityKey NormalizeClientIdentity(
        string? clientName,
        string? localIp,
        string? operatorIp,
        string? publicIp,
        string? requesterIp)
    {
        var normalizedLocalIp = string.IsNullOrWhiteSpace(localIp) ? requesterIp : localIp;
        return ClientIdentityKey.Create(clientName, normalizedLocalIp, operatorIp, publicIp);
    }

    private static ServiceAccessReportResponse CreateDecision(string decisionCode, string? message)
    {
        return new ServiceAccessReportResponse
        {
            DecisionCode = decisionCode ?? ServiceAccessDecisionCodes.Complete,
            Message = message ?? string.Empty,
        };
    }
}

internal sealed record ServiceCircuitInstanceAdminSnapshot(
    string ServiceId,
    ServiceInstanceKey ServiceKey,
    DateTimeOffset LastSeenAtUtc,
    ServiceCircuitBreakerSettings CircuitBreaker,
    IReadOnlyList<OpenClientCircuitSnapshot> OpenClients);

internal sealed class ServiceAccessResolveOutcome
{
    public bool IsSuccess { get; init; }

    public int StatusCode { get; init; }

    public string Message { get; init; } = string.Empty;

    public string ErrorKey { get; init; } = string.Empty;

    public ServiceAccessResolveResponse? Response { get; init; }

    public static ServiceAccessResolveOutcome Success(ServiceAccessResolveResponse response)
    {
        return new ServiceAccessResolveOutcome
        {
            IsSuccess = true,
            StatusCode = StatusCodes.Status200OK,
            Response = response,
        };
    }

    public static ServiceAccessResolveOutcome Fail(int statusCode, string message, string errorKey)
    {
        return new ServiceAccessResolveOutcome
        {
            IsSuccess = false,
            StatusCode = statusCode,
            Message = message ?? string.Empty,
            ErrorKey = errorKey ?? string.Empty,
        };
    }
}
