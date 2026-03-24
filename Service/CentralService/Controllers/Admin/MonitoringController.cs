using CentralService.Admin;
using CentralService.Admin.Models;
using CentralService.Services;
using CentralService.Service.Models;
using ApiResponseFactory = CentralService.Internal.CentralServiceApiResponseFactory;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CentralService.Controllers.Admin;

[ApiController]
[Route("api/admin/monitoring")]
[Produces("application/json")]
[EnableCors("CentralServiceAdmin")]
[Authorize(
    AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme,
    Policy = CentralServicePermissions.Monitoring.Read)]
public sealed class MonitoringController : ControllerBase
{
    private readonly ServiceRegistry _serviceRegistry;
    private readonly ServiceNetworkEvaluator _networkEvaluator;
    private readonly CentralServiceBackgroundTaskMonitor _taskMonitor;
    private readonly HealthCheckService _healthCheckService;

    public MonitoringController(
        ServiceRegistry serviceRegistry,
        ServiceNetworkEvaluator networkEvaluator,
        CentralServiceBackgroundTaskMonitor taskMonitor,
        HealthCheckService healthCheckService)
    {
        _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
        _networkEvaluator = networkEvaluator ?? throw new ArgumentNullException(nameof(networkEvaluator));
        _taskMonitor = taskMonitor ?? throw new ArgumentNullException(nameof(taskMonitor));
        _healthCheckService = healthCheckService ?? throw new ArgumentNullException(nameof(healthCheckService));
    }

    [HttpGet("summary")]
    public ActionResult<ApiResponse<MonitoringSummaryResponse>> Summary()
    {
        var services = _serviceRegistry.GetAllServices();
        var total = services.Count;
        var online = services.Count(x => x.Status == 1);
        var fault = services.Count(x => x.Status == 2);
        var offline = services.Count(x => x.Status == 0);

        var networkStatuses = _networkEvaluator.GetAllNetworkStatuses();
        var knownCount = networkStatuses.Count;
        var availableScores = networkStatuses
            .Where(x => x.IsAvailable)
            .Select(x => x.CalculateScore())
            .ToArray();

        var availableCount = availableScores.Length;
        var unavailableCount = Math.Max(0, knownCount - availableCount);

        var averageScore = availableCount > 0 ? availableScores.Average() : 0;
        int? minScore = availableCount > 0 ? availableScores.Min() : null;
        int? maxScore = availableCount > 0 ? availableScores.Max() : null;
        DateTime? lastEvaluatedAtUtc = knownCount > 0 ? networkStatuses.Max(x => x.LastCheckTime).ToUniversalTime() : null;

        var backgroundTasks = _taskMonitor.GetAll()
            .Select(x => new BackgroundTaskStatusDto(
                x.TaskName,
                x.IsHealthy,
                x.LastRunAtUtc,
                x.LastSuccessAtUtc,
                x.LastErrorAtUtc,
                x.LastError))
            .ToArray();

        var response = new MonitoringSummaryResponse(
            total,
            online,
            fault,
            offline,
            new NetworkScoreSummary(
                knownCount,
                availableCount,
                unavailableCount,
                averageScore,
                minScore,
                maxScore,
                lastEvaluatedAtUtc),
            backgroundTasks);

        return Ok(ApiResponseFactory.Success(response));
    }

    [HttpGet("health")]
    public async Task<ActionResult<ApiResponse<MonitoringHealthResponse>>> Health()
    {
        var report = await _healthCheckService.CheckHealthAsync();
        var checks = report.Entries
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(x => new HealthCheckItem(
                x.Key,
                x.Value.Status.ToString(),
                x.Value.Description,
                (long)x.Value.Duration.TotalMilliseconds,
                x.Value.Exception?.Message))
            .ToArray();

        return Ok(ApiResponseFactory.Success(
            new MonitoringHealthResponse(
                report.Status.ToString(),
                DateTime.UtcNow,
                checks)));
    }
}
