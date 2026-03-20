namespace CentralService.Admin.Models;

public sealed record MonitoringSummaryResponse(
    int TotalServiceInstances,
    int OnlineServiceInstances,
    int FaultServiceInstances,
    int OfflineServiceInstances,
    NetworkScoreSummary Network,
    IReadOnlyList<BackgroundTaskStatusDto> BackgroundTasks);

public sealed record NetworkScoreSummary(
    int KnownStatusCount,
    int AvailableCount,
    int UnavailableCount,
    double AverageScore,
    int? MinScore,
    int? MaxScore,
    DateTime? LastEvaluatedAtUtc);

public sealed record BackgroundTaskStatusDto(
    string TaskName,
    bool IsHealthy,
    DateTime? LastRunAtUtc,
    DateTime? LastSuccessAtUtc,
    DateTime? LastErrorAtUtc,
    string? LastError);

public sealed record MonitoringHealthResponse(
    string Status,
    DateTime CheckedAtUtc,
    IReadOnlyList<HealthCheckItem> Checks);

public sealed record HealthCheckItem(
    string Name,
    string Status,
    string? Description,
    long DurationMs,
    string? Error);

