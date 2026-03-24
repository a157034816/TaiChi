namespace CentralService.Admin.Models;

public sealed record ConfigVersionListItem(
    int Id,
    int VersionNo,
    string Status,
    string? Comment,
    int? BasedOnVersionId,
    DateTime CreatedAtUtc,
    string? CreatedByUsername,
    DateTime UpdatedAtUtc,
    string? UpdatedByUsername,
    DateTime? PublishedAtUtc,
    string? PublishedByUsername);

public sealed record ConfigVersionDetail(
    int Id,
    int VersionNo,
    string Status,
    string? Comment,
    int? BasedOnVersionId,
    DateTime CreatedAtUtc,
    string? CreatedByUsername,
    DateTime UpdatedAtUtc,
    string? UpdatedByUsername,
    DateTime? PublishedAtUtc,
    string? PublishedByUsername,
    string ConfigJson);

public sealed record CurrentConfigResponse(
    int? CurrentVersionId,
    int? CurrentVersionNo,
    string ConfigJson);

public sealed record CreateConfigDraftRequest(string? Comment, int? BasedOnVersionId);

public sealed record UpdateConfigDraftRequest(string ConfigJson, string? Comment);

public sealed record PublishConfigRequest(string? Note);

public sealed record RollbackConfigRequest(string? Note);

public sealed record ConfigDiffResponse(
    int BaseVersionId,
    int TargetVersionId,
    string BaseJson,
    string TargetJson);

public sealed record PublishHistoryItem(
    int Id,
    string Action,
    int? FromVersionId,
    int ToVersionId,
    string? Note,
    string? ActorUsername,
    DateTime CreatedAtUtc);

