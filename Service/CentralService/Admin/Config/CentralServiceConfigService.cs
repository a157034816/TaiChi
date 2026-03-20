using System.Security.Claims;
using CentralService.Admin.Data;
using CentralService.Admin.Entities;
using CentralService.Admin.Security;
using SqlSugar;

namespace CentralService.Admin.Config;

public sealed class CentralServiceConfigService
{
    private readonly CentralServiceAdminDb _db;
    private readonly CentralServiceRuntimeConfigProvider _runtimeConfigProvider;
    private readonly CentralServiceAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CentralServiceConfigService> _logger;

    public CentralServiceConfigService(
        CentralServiceAdminDb db,
        CentralServiceRuntimeConfigProvider runtimeConfigProvider,
        CentralServiceAuditService auditService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<CentralServiceConfigService> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _runtimeConfigProvider = runtimeConfigProvider ?? throw new ArgumentNullException(nameof(runtimeConfigProvider));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CentralServiceConfigVersion?> GetCurrentVersionAsync()
    {
        _db.EnsureCreated();

        var state = await EnsureStateAsync();
        if (state.CurrentVersionId == null)
        {
            return null;
        }

        return await _db.Db.Queryable<CentralServiceConfigVersion>()
            .Where(x => x.Id == state.CurrentVersionId.Value)
            .FirstAsync();
    }

    public async Task<CentralServiceConfigState> EnsureStateAsync()
    {
        var state = await _db.Db.Queryable<CentralServiceConfigState>()
            .Where(x => x.Id == 1)
            .FirstAsync();

        if (state != null)
        {
            return state;
        }

        state = new CentralServiceConfigState
        {
            Id = 1,
            CurrentVersionId = null,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        await _db.Db.Insertable(state).ExecuteCommandAsync();
        return state;
    }

    public async Task<IReadOnlyList<CentralServiceConfigVersion>> ListVersionsAsync()
    {
        _db.EnsureCreated();

        return await _db.Db.Queryable<CentralServiceConfigVersion>()
            .OrderBy(x => x.VersionNo, OrderByType.Desc)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<CentralServiceConfigPublish>> ListPublishHistoryAsync(int take = 200)
    {
        _db.EnsureCreated();

        take = take switch
        {
            <= 0 => 50,
            > 500 => 500,
            _ => take
        };

        return await _db.Db.Queryable<CentralServiceConfigPublish>()
            .OrderBy(x => x.Id, OrderByType.Desc)
            .Take(take)
            .ToListAsync();
    }

    public async Task<CentralServiceConfigVersion?> GetVersionAsync(int id)
    {
        _db.EnsureCreated();

        return await _db.Db.Queryable<CentralServiceConfigVersion>()
            .Where(x => x.Id == id)
            .FirstAsync();
    }

    public async Task<CentralServiceConfigVersion> CreateDraftAsync(string? comment = null, int? basedOnVersionId = null)
    {
        _db.EnsureCreated();

        var now = DateTime.UtcNow;
        var actor = GetActor();

        var baseVersion = basedOnVersionId != null
            ? await GetVersionAsync(basedOnVersionId.Value)
            : await GetCurrentVersionAsync();

        var configJson = baseVersion?.ConfigJson ?? CentralServiceRuntimeConfigJson.DefaultJson;

        var maxVersionNo = await _db.Db.Queryable<CentralServiceConfigVersion>()
            .MaxAsync(x => x.VersionNo);
        var nextVersionNo = Math.Max(0, maxVersionNo) + 1;

        var version = new CentralServiceConfigVersion
        {
            VersionNo = nextVersionNo,
            Status = "Draft",
            Comment = comment,
            ConfigJson = configJson,
            BasedOnVersionId = baseVersion?.Id,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CreatedByUserId = actor.UserId,
            CreatedByUsername = actor.Username,
            UpdatedByUserId = actor.UserId,
            UpdatedByUsername = actor.Username,
        };

        version.Id = await _db.Db.Insertable(version).ExecuteReturnIdentityAsync();

        await _auditService.TryWriteAsync(
            action: "config.version.create",
            resource: $"configVersion:{version.Id}",
            after: new { version.Id, version.VersionNo, version.Status, version.BasedOnVersionId, version.Comment });

        return version;
    }

    public async Task<CentralServiceConfigVersion> UpdateDraftAsync(int id, string configJson, string? comment)
    {
        _db.EnsureCreated();

        var version = await GetVersionAsync(id);
        if (version == null)
        {
            throw new InvalidOperationException("配置版本不存在");
        }

        if (!string.Equals(version.Status, "Draft", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("仅草稿版本允许修改");
        }

        var errors = CentralServiceRuntimeConfigValidator.ValidateJson(configJson);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join("; ", errors));
        }

        var actor = GetActor();
        var now = DateTime.UtcNow;

        var before = new { version.Id, version.VersionNo, version.Comment };

        version.ConfigJson = configJson;
        version.Comment = comment;
        version.UpdatedAtUtc = now;
        version.UpdatedByUserId = actor.UserId;
        version.UpdatedByUsername = actor.Username;

        await _db.Db.Updateable(version).ExecuteCommandAsync();

        await _auditService.TryWriteAsync(
            action: "config.version.update",
            resource: $"configVersion:{version.Id}",
            before: before,
            after: new { version.Id, version.VersionNo, version.Comment });

        return version;
    }

    public async Task PublishAsync(int id, string? note)
    {
        _db.EnsureCreated();

        var version = await GetVersionAsync(id);
        if (version == null)
        {
            throw new InvalidOperationException("配置版本不存在");
        }

        if (!string.Equals(version.Status, "Draft", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("仅草稿版本允许发布");
        }

        var errors = CentralServiceRuntimeConfigValidator.ValidateJson(version.ConfigJson);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join("; ", errors));
        }

        var actor = GetActor();
        var now = DateTime.UtcNow;

        var state = await EnsureStateAsync();
        var fromVersionId = state.CurrentVersionId;

        var result = await _db.Db.Ado.UseTranAsync(async () =>
        {
            state.CurrentVersionId = version.Id;
            state.UpdatedAtUtc = now;
            await _db.Db.Updateable(state).ExecuteCommandAsync();

            version.Status = "Published";
            version.PublishedAtUtc = now;
            version.PublishedByUserId = actor.UserId;
            version.PublishedByUsername = actor.Username;
            version.UpdatedAtUtc = now;
            version.UpdatedByUserId = actor.UserId;
            version.UpdatedByUsername = actor.Username;
            await _db.Db.Updateable(version).ExecuteCommandAsync();

            var publish = new CentralServiceConfigPublish
            {
                Action = "Publish",
                FromVersionId = fromVersionId,
                ToVersionId = version.Id,
                Note = note,
                ActorUserId = actor.UserId,
                ActorUsername = actor.Username,
                CreatedAtUtc = now,
            };
            await _db.Db.Insertable(publish).ExecuteCommandAsync();
        });

        if (!result.IsSuccess)
        {
            throw result.ErrorException ?? new InvalidOperationException("发布失败");
        }

        _runtimeConfigProvider.SetCurrent(version.Id, version.ConfigJson);

        await _auditService.TryWriteAsync(
            action: "config.publish",
            resource: $"configVersion:{version.Id}",
            before: new { fromVersionId },
            after: new { toVersionId = version.Id, note });

        _logger.LogInformation("已发布配置版本: {VersionId}", version.Id);
    }

    public async Task RollbackAsync(int targetVersionId, string? note)
    {
        _db.EnsureCreated();

        var target = await GetVersionAsync(targetVersionId);
        if (target == null)
        {
            throw new InvalidOperationException("配置版本不存在");
        }

        if (!string.Equals(target.Status, "Published", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("仅已发布版本允许回滚");
        }

        var errors = CentralServiceRuntimeConfigValidator.ValidateJson(target.ConfigJson);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join("; ", errors));
        }

        var actor = GetActor();
        var now = DateTime.UtcNow;

        var state = await EnsureStateAsync();
        var fromVersionId = state.CurrentVersionId;
        if (fromVersionId == target.Id)
        {
            throw new InvalidOperationException("目标版本已是当前生效版本");
        }

        var result = await _db.Db.Ado.UseTranAsync(async () =>
        {
            state.CurrentVersionId = target.Id;
            state.UpdatedAtUtc = now;
            await _db.Db.Updateable(state).ExecuteCommandAsync();

            var publish = new CentralServiceConfigPublish
            {
                Action = "Rollback",
                FromVersionId = fromVersionId,
                ToVersionId = target.Id,
                Note = note,
                ActorUserId = actor.UserId,
                ActorUsername = actor.Username,
                CreatedAtUtc = now,
            };
            await _db.Db.Insertable(publish).ExecuteCommandAsync();
        });

        if (!result.IsSuccess)
        {
            throw result.ErrorException ?? new InvalidOperationException("回滚失败");
        }

        _runtimeConfigProvider.SetCurrent(target.Id, target.ConfigJson);

        await _auditService.TryWriteAsync(
            action: "config.rollback",
            resource: $"configVersion:{target.Id}",
            before: new { fromVersionId },
            after: new { toVersionId = target.Id, note });

        _logger.LogInformation("已回滚到配置版本: {VersionId}", target.Id);
    }

    private Actor GetActor()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var userIdValue = user?.FindFirstValue(ClaimTypes.NameIdentifier);
        var username = user?.Identity?.Name;

        if (int.TryParse(userIdValue, out var userId))
        {
            return new Actor(userId, username);
        }

        return new Actor(null, username);
    }

    private sealed record Actor(int? UserId, string? Username);
}
