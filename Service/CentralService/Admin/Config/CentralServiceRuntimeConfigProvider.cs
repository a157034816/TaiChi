using System.Collections.ObjectModel;
using System.Text.Json;
using CentralService.Admin.Data;
using CentralService.Admin.Entities;

namespace CentralService.Admin.Config;

public sealed class CentralServiceRuntimeConfigProvider : IHostedService
{
    private readonly CentralServiceAdminDb _db;
    private readonly ILogger<CentralServiceRuntimeConfigProvider> _logger;

    private volatile CentralServiceRuntimeConfigSnapshot _snapshot = CentralServiceRuntimeConfigSnapshot.Empty;
    private volatile int _currentVersionId;

    public CentralServiceRuntimeConfigProvider(CentralServiceAdminDb db, ILogger<CentralServiceRuntimeConfigProvider> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public CentralServiceRuntimeConfigSnapshot Snapshot => _snapshot;

    public int? CurrentVersionId => _currentVersionId == 0 ? null : _currentVersionId;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await ReloadAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public async Task ReloadAsync()
    {
        try
        {
            _db.EnsureCreated();

            var state = await _db.Db.Queryable<CentralServiceConfigState>()
                .Where(x => x.Id == 1)
                .FirstAsync();

            if (state == null)
            {
                state = new CentralServiceConfigState
                {
                    Id = 1,
                    CurrentVersionId = null,
                    UpdatedAtUtc = DateTime.UtcNow,
                };
                await _db.Db.Insertable(state).ExecuteCommandAsync();
            }

            if (state.CurrentVersionId == null)
            {
                _currentVersionId = 0;
                _snapshot = CentralServiceRuntimeConfigSnapshot.Empty;
                return;
            }

            var version = await _db.Db.Queryable<CentralServiceConfigVersion>()
                .Where(x => x.Id == state.CurrentVersionId.Value)
                .FirstAsync();

            if (version == null)
            {
                _logger.LogWarning("当前配置版本不存在: {VersionId}", state.CurrentVersionId);
                _currentVersionId = 0;
                _snapshot = CentralServiceRuntimeConfigSnapshot.Empty;
                return;
            }

            SetCurrent(version.Id, version.ConfigJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载当前配置失败");
        }
    }

    public void SetCurrent(int? versionId, string configJson)
    {
        CentralServiceRuntimeConfig config;
        try
        {
            config = JsonSerializer.Deserialize<CentralServiceRuntimeConfig>(configJson, CentralServiceRuntimeConfigJson.Options)
                     ?? new CentralServiceRuntimeConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析配置 JSON 失败，忽略本次更新");
            return;
        }

        _snapshot = CentralServiceRuntimeConfigSnapshot.From(config);
        _currentVersionId = versionId ?? 0;
        _logger.LogInformation("已应用配置版本: {VersionId}", versionId);
    }
}

public sealed class CentralServiceRuntimeConfigSnapshot
{
    public static readonly CentralServiceRuntimeConfigSnapshot Empty = From(new CentralServiceRuntimeConfig());

    private CentralServiceRuntimeConfigSnapshot(
        CentralServiceRuntimeConfig config,
        IReadOnlyDictionary<string, CentralServiceServicePolicyConfig> policiesByName,
        IReadOnlyDictionary<string, CentralServiceServiceInstanceOverrideConfig> overridesById)
    {
        Config = config;
        PoliciesByName = policiesByName;
        OverridesById = overridesById;
    }

    public CentralServiceRuntimeConfig Config { get; }

    public IReadOnlyDictionary<string, CentralServiceServicePolicyConfig> PoliciesByName { get; }

    public IReadOnlyDictionary<string, CentralServiceServiceInstanceOverrideConfig> OverridesById { get; }

    public static CentralServiceRuntimeConfigSnapshot From(CentralServiceRuntimeConfig config)
    {
        var policies = (config.Services ?? new List<CentralServiceServicePolicyConfig>())
            .Where(x => x != null && !string.IsNullOrWhiteSpace(x.ServiceName))
            .GroupBy(x => x.ServiceName.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.First(),
                StringComparer.OrdinalIgnoreCase);

        var overrides = (config.Instances ?? new List<CentralServiceServiceInstanceOverrideConfig>())
            .Where(x => x != null && !string.IsNullOrWhiteSpace(x.ServiceId))
            .GroupBy(x => x.ServiceId.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.First(),
                StringComparer.OrdinalIgnoreCase);

        return new CentralServiceRuntimeConfigSnapshot(
            config,
            new ReadOnlyDictionary<string, CentralServiceServicePolicyConfig>(policies),
            new ReadOnlyDictionary<string, CentralServiceServiceInstanceOverrideConfig>(overrides));
    }
}
