using CentralService.Admin;
using CentralService.Admin.Models;
using CentralService.Service.Models;
using CentralService.Services.ServiceCircuiting;
using ApiResponseFactory = CentralService.Internal.CentralServiceApiResponseFactory;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace CentralService.Controllers.Admin;

[ApiController]
[Route("api/admin/service-circuits")]
[Produces("application/json")]
[EnableCors("CentralServiceAdmin")]
[Authorize(
    AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme,
    Policy = CentralServicePermissions.Services.Read)]
public sealed class ServiceCircuitController : ControllerBase
{
    private readonly ServiceAccessService _serviceAccessService;

    public ServiceCircuitController(ServiceAccessService serviceAccessService)
    {
        _serviceAccessService = serviceAccessService ?? throw new ArgumentNullException(nameof(serviceAccessService));
    }

    [HttpGet("services/{serviceName}")]
    public ActionResult<ApiResponse<ServiceCircuitServiceDetailResponse>> GetServiceDetail(string serviceName)
    {
        var snapshots = _serviceAccessService.GetAdminSnapshots(serviceName);
        var response = new ServiceCircuitServiceDetailResponse(
            serviceName,
            snapshots.Select(x =>
                    new ServiceCircuitInstanceDto(
                        x.ServiceId,
                        x.LastSeenAtUtc,
                        x.CircuitBreaker.MaxAttempts,
                        x.CircuitBreaker.FailureThreshold,
                        x.CircuitBreaker.BreakDurationMinutes,
                        x.CircuitBreaker.RecoveryThreshold,
                        x.OpenClients.Select(client =>
                                new ServiceCircuitClientDto(
                                    client.ClientName,
                                    client.LocalIp,
                                    client.OperatorIp,
                                    client.PublicIp,
                                    client.OpenedAtUtc,
                                    client.OpenUntilUtc,
                                    client.ConsecutiveFailures))
                            .ToArray()))
                .ToArray());

        return Ok(ApiResponseFactory.Success(response));
    }

    [HttpPut("instances/{serviceId}/config")]
    [Authorize(Policy = CentralServicePermissions.Services.Manage)]
    public async Task<ActionResult<ApiResponse<ServiceCircuitInstanceDto>>> UpdateConfig(
        string serviceId,
        [FromBody] UpdateServiceCircuitConfigRequest request,
        CancellationToken cancellationToken)
    {
        var settings = new ServiceCircuitBreakerSettings
        {
            MaxAttempts = request?.MaxAttempts ?? 0,
            FailureThreshold = request?.FailureThreshold ?? 0,
            BreakDurationMinutes = request?.BreakDurationMinutes ?? 0,
            RecoveryThreshold = request?.RecoveryThreshold ?? 0,
        };

        var updated = await _serviceAccessService.UpdateCircuitBreakerAsync(serviceId, settings, cancellationToken);
        if (updated == null)
        {
            return NotFound(ApiResponseFactory.Error<ServiceCircuitInstanceDto>("服务实例不存在", 404));
        }

        return Ok(ApiResponseFactory.Success(
            new ServiceCircuitInstanceDto(
                serviceId,
                DateTimeOffset.UtcNow,
                updated.MaxAttempts,
                updated.FailureThreshold,
                updated.BreakDurationMinutes,
                updated.RecoveryThreshold,
                Array.Empty<ServiceCircuitClientDto>())));
    }

    [HttpPost("instances/{serviceId}/clear")]
    [Authorize(Policy = CentralServicePermissions.Services.Manage)]
    public ActionResult<ApiResponse<object>> ClearState(string serviceId)
    {
        if (!_serviceAccessService.ClearServiceState(serviceId))
        {
            return NotFound(ApiResponseFactory.Error<object>("服务实例不存在", 404));
        }

        return Ok(ApiResponseFactory.Success<object>(new { }));
    }
}
