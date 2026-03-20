using Microsoft.Extensions.Options;

namespace CentralService.Services.ServiceCircuiting;

public sealed class ServiceCircuitCleanupBackgroundService : BackgroundService
{
    private readonly ServiceCircuitTomlStore _store;
    private readonly ServiceCircuitJsonStore _jsonStore;
    private readonly ServiceCircuitRuntimeStateStore _runtimeStateStore;
    private readonly ServiceCircuitTomlOptions _options;
    private readonly ILogger<ServiceCircuitCleanupBackgroundService> _logger;

    public ServiceCircuitCleanupBackgroundService(
        ServiceCircuitTomlStore store,
        ServiceCircuitJsonStore jsonStore,
        ServiceCircuitRuntimeStateStore runtimeStateStore,
        IOptions<ServiceCircuitTomlOptions> options,
        ILogger<ServiceCircuitCleanupBackgroundService> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _jsonStore = jsonStore ?? throw new ArgumentNullException(nameof(jsonStore));
        _runtimeStateStore = runtimeStateStore ?? throw new ArgumentNullException(nameof(runtimeStateStore));
        _options = options?.Value ?? new ServiceCircuitTomlOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = _options.CleanupIntervalMinutes < 1 ? 60 : _options.CleanupIntervalMinutes;
        var interval = TimeSpan.FromMinutes(intervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var removed = await _jsonStore.CleanupStaleAsync(DateTimeOffset.UtcNow, _store.StaleDays, stoppingToken);
                if (removed.Count > 0)
                {
                    _runtimeStateStore.ClearServices(removed);
                    _logger.LogInformation("已清理 {Count} 条过期的服务熔断配置", removed.Count);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理过期服务熔断配置失败");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
