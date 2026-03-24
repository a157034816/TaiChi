namespace CentralService.Admin.Models;

public sealed record AuditLogListItem(
    int Id,
    DateTime CreatedAtUtc,
    int? ActorUserId,
    string? ActorUsername,
    string Action,
    string Resource,
    string? TraceId,
    string? Ip,
    string? UserAgent);

public sealed record AuditLogDetail(
    int Id,
    DateTime CreatedAtUtc,
    int? ActorUserId,
    string? ActorUsername,
    string Action,
    string Resource,
    string? TraceId,
    string? Ip,
    string? UserAgent,
    string? BeforeJson,
    string? AfterJson);

