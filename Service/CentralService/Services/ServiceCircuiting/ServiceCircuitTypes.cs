using CentralService.Models;

namespace CentralService.Services.ServiceCircuiting;

public sealed class ServiceCircuitTomlOptions
{
    public string FilePath { get; set; } = string.Empty;

    public int CleanupIntervalMinutes { get; set; } = 60;
}

public sealed class ServiceCircuitJsonOptions
{
    public string FilePath { get; set; } = string.Empty;
}

public sealed class ServiceCircuitBreakerSettings
{
    public int MaxAttempts { get; set; } = 2;

    public int FailureThreshold { get; set; } = 3;

    public int BreakDurationMinutes { get; set; } = 1;

    public int RecoveryThreshold { get; set; } = 2;

    public ServiceCircuitBreakerSettings Clone()
    {
        return new ServiceCircuitBreakerSettings
        {
            MaxAttempts = MaxAttempts,
            FailureThreshold = FailureThreshold,
            BreakDurationMinutes = BreakDurationMinutes,
            RecoveryThreshold = RecoveryThreshold,
        };
    }

    public ServiceCircuitBreakerSettings Normalize(ServiceCircuitBreakerSettings? fallback = null)
    {
        fallback ??= Defaults();
        return new ServiceCircuitBreakerSettings
        {
            MaxAttempts = MaxAttempts < 1 ? fallback.MaxAttempts : MaxAttempts,
            FailureThreshold = FailureThreshold < 1 ? fallback.FailureThreshold : FailureThreshold,
            BreakDurationMinutes = BreakDurationMinutes < 1 ? fallback.BreakDurationMinutes : BreakDurationMinutes,
            RecoveryThreshold = RecoveryThreshold < 1 ? fallback.RecoveryThreshold : RecoveryThreshold,
        };
    }

    public static ServiceCircuitBreakerSettings Defaults()
    {
        return new ServiceCircuitBreakerSettings
        {
            MaxAttempts = 2,
            FailureThreshold = 3,
            BreakDurationMinutes = 1,
            RecoveryThreshold = 2,
        };
    }
}

internal sealed class PersistedServiceCircuitEntry
{
    public ServiceInstanceKey ServiceKey { get; init; } = ServiceInstanceKey.Empty;

    public DateTimeOffset LastSeenAtUtc { get; init; }

    public ServiceCircuitBreakerSettings CircuitBreaker { get; init; } = ServiceCircuitBreakerSettings.Defaults();
}

internal sealed class ServiceCircuitConfigSnapshot
{
    public ServiceCircuitConfigSnapshot(
        int staleDays,
        ServiceCircuitBreakerSettings defaults,
        IReadOnlyDictionary<string, PersistedServiceCircuitEntry> servicesByKey)
    {
        StaleDays = staleDays < 1 ? 30 : staleDays;
        Defaults = defaults?.Normalize() ?? ServiceCircuitBreakerSettings.Defaults();
        ServicesByKey = servicesByKey ?? new Dictionary<string, PersistedServiceCircuitEntry>(StringComparer.OrdinalIgnoreCase);
    }

    public int StaleDays { get; }

    public ServiceCircuitBreakerSettings Defaults { get; }

    public IReadOnlyDictionary<string, PersistedServiceCircuitEntry> ServicesByKey { get; }

    public PersistedServiceCircuitEntry? Find(ServiceInstanceKey key)
    {
        if (key.IsEmpty)
        {
            return null;
        }

        ServicesByKey.TryGetValue(key.PersistentKey, out var entry);
        return entry;
    }

    public static ServiceCircuitConfigSnapshot CreateDefault()
    {
        return new ServiceCircuitConfigSnapshot(
            30,
            ServiceCircuitBreakerSettings.Defaults(),
            new Dictionary<string, PersistedServiceCircuitEntry>(StringComparer.OrdinalIgnoreCase));
    }
}

internal sealed class AccessTicketRecord
{
    public string Ticket { get; init; } = string.Empty;

    public ServiceInstanceKey ServiceKey { get; init; } = ServiceInstanceKey.Empty;

    public ClientIdentityKey ClientKey { get; init; } = ClientIdentityKey.Empty;

    public string ServiceId { get; init; } = string.Empty;

    public string ServiceName { get; init; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; init; }
}

internal sealed class OpenClientCircuitSnapshot
{
    public string ClientName { get; init; } = string.Empty;

    public string LocalIp { get; init; } = string.Empty;

    public string OperatorIp { get; init; } = string.Empty;

    public string PublicIp { get; init; } = string.Empty;

    public DateTimeOffset OpenedAtUtc { get; init; }

    public DateTimeOffset OpenUntilUtc { get; init; }

    public int ConsecutiveFailures { get; init; }
}

internal readonly record struct ServiceInstanceKey(
    string ServiceName,
    string LocalIp,
    string OperatorIp,
    string PublicIp,
    int Port)
{
    public static readonly ServiceInstanceKey Empty = new(string.Empty, string.Empty, string.Empty, string.Empty, 0);

    public bool IsEmpty => string.IsNullOrWhiteSpace(ServiceName) || string.IsNullOrWhiteSpace(LocalIp) || Port <= 0;

    public string PersistentKey =>
        $"{ServiceName}|{LocalIp}|{OperatorIp}|{PublicIp}|{Port}";

    public static ServiceInstanceKey FromService(ServiceInfo? service)
    {
        if (service == null)
        {
            return Empty;
        }

        return new ServiceInstanceKey(
            Normalize(service.Name),
            Normalize(service.LocalIp),
            Normalize(service.OperatorIp),
            Normalize(service.PublicIp),
            service.Port);
    }

    private static string Normalize(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }
}

internal readonly record struct ClientIdentityKey(
    string ClientName,
    string LocalIp,
    string OperatorIp,
    string PublicIp)
{
    public static readonly ClientIdentityKey Empty = new(string.Empty, string.Empty, string.Empty, string.Empty);

    public bool IsEmpty => string.IsNullOrWhiteSpace(ClientName) || string.IsNullOrWhiteSpace(LocalIp);

    public string PersistentKey =>
        $"{ClientName}|{LocalIp}|{OperatorIp}|{PublicIp}";

    public static ClientIdentityKey Create(
        string? clientName,
        string? localIp,
        string? operatorIp,
        string? publicIp)
    {
        return new ClientIdentityKey(
            Normalize(clientName),
            Normalize(localIp),
            Normalize(operatorIp),
            Normalize(publicIp));
    }

    private static string Normalize(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }
}

public static class ServiceAccessErrorKeys
{
    public const string NoAvailableInstance = "ACCESS_NO_AVAILABLE_INSTANCE";
    public const string CircuitOpen = "ACCESS_CIRCUIT_OPEN";
    public const string TryNextInstance = "ACCESS_TRY_NEXT_INSTANCE";
    public const string RetryResolve = "ACCESS_RETRY_RESOLVE";
    public const string InvalidClientIdentity = "ACCESS_INVALID_CLIENT_IDENTITY";
}

public static class ServiceAccessDecisionCodes
{
    public const string Complete = "ACCESS_COMPLETE";
    public const string TryNextInstance = ServiceAccessErrorKeys.TryNextInstance;
    public const string RetryResolve = ServiceAccessErrorKeys.RetryResolve;
}
