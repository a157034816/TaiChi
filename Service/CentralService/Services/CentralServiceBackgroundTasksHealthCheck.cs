using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CentralService.Services;

public sealed class CentralServiceBackgroundTasksHealthCheck : IHealthCheck
{
    private readonly CentralServiceBackgroundTaskMonitor _taskMonitor;
    private readonly TimeSpan _staleThreshold;

    public CentralServiceBackgroundTasksHealthCheck(CentralServiceBackgroundTaskMonitor taskMonitor, IConfiguration configuration)
    {
        _taskMonitor = taskMonitor ?? throw new ArgumentNullException(nameof(taskMonitor));

        var seconds = configuration.GetValue<int?>("CentralServiceMonitoring:BackgroundTaskStaleSeconds") ?? 300;
        seconds = Math.Clamp(seconds, 30, 24 * 60 * 60);
        _staleThreshold = TimeSpan.FromSeconds(seconds);
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var tasks = _taskMonitor.GetAll();
        if (tasks.Count == 0)
        {
            return Task.FromResult(HealthCheckResult.Degraded("后台任务尚未上报状态"));
        }

        var unhealthy = tasks.Where(x => !x.IsHealthy).Select(x => x.TaskName).ToArray();
        if (unhealthy.Length > 0)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"后台任务异常: {string.Join(", ", unhealthy)}"));
        }

        var now = DateTime.UtcNow;

        var neverRun = tasks.Where(x => x.LastRunAtUtc == null).Select(x => x.TaskName).ToArray();
        if (neverRun.Length > 0)
        {
            return Task.FromResult(HealthCheckResult.Degraded($"后台任务未运行: {string.Join(", ", neverRun)}"));
        }

        var stale = tasks
            .Where(x => x.LastRunAtUtc != null && (now - x.LastRunAtUtc.Value) > _staleThreshold)
            .Select(x => x.TaskName)
            .ToArray();

        if (stale.Length > 0)
        {
            return Task.FromResult(HealthCheckResult.Degraded($"后台任务超时未更新: {string.Join(", ", stale)}"));
        }

        return Task.FromResult(HealthCheckResult.Healthy("后台任务 OK"));
    }
}

