namespace CentralService.Admin.Models;

public sealed record ServiceCircuitClientDto(
    string ClientName,
    string LocalIp,
    string OperatorIp,
    string PublicIp,
    DateTimeOffset OpenedAtUtc,
    DateTimeOffset OpenUntilUtc,
    int ConsecutiveFailures);

public sealed record ServiceCircuitInstanceDto(
    string ServiceId,
    DateTimeOffset LastSeenAtUtc,
    int MaxAttempts,
    int FailureThreshold,
    int BreakDurationMinutes,
    int RecoveryThreshold,
    IReadOnlyList<ServiceCircuitClientDto> OpenClients);

public sealed record ServiceCircuitServiceDetailResponse(
    string ServiceName,
    IReadOnlyList<ServiceCircuitInstanceDto> Instances);

public sealed record UpdateServiceCircuitConfigRequest(
    int MaxAttempts,
    int FailureThreshold,
    int BreakDurationMinutes,
    int RecoveryThreshold);
