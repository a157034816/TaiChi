using CentralService.Service.Models;
using CentralService.Services.ServiceCircuiting;
using ApiResponseFactory = CentralService.Internal.CentralServiceApiResponseFactory;
using Microsoft.AspNetCore.Mvc;

namespace CentralService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class ServiceAccessController : ControllerBase
{
    private readonly ServiceAccessService _serviceAccessService;
    private readonly ILogger<ServiceAccessController> _logger;

    public ServiceAccessController(
        ServiceAccessService serviceAccessService,
        ILogger<ServiceAccessController> logger)
    {
        _serviceAccessService = serviceAccessService ?? throw new ArgumentNullException(nameof(serviceAccessService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost("resolve")]
    [ProducesResponseType(typeof(ApiResponse<ServiceAccessResolveResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ServiceAccessResolveResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ServiceAccessResolveResponse>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Resolve(
        [FromBody] ServiceAccessResolveRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var outcome = await _serviceAccessService.ResolveAsync(
                request,
                GetRequesterIpAddress(),
                cancellationToken);

            if (outcome.IsSuccess)
            {
                return Ok(ApiResponseFactory.Success(outcome.Response!));
            }

            return StatusCode(
                outcome.StatusCode,
                ApiResponseFactory.Error<ServiceAccessResolveResponse>(
                    outcome.Message,
                    outcome.StatusCode,
                    outcome.ErrorKey));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理服务访问 resolve 请求失败");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponseFactory.Error<ServiceAccessResolveResponse>(
                    "服务访问 resolve 失败",
                    StatusCodes.Status500InternalServerError));
        }
    }

    [HttpPost("report")]
    [ProducesResponseType(typeof(ApiResponse<ServiceAccessReportResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Report(
        [FromBody] ServiceAccessReportRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _serviceAccessService.ReportAsync(
                request,
                GetRequesterIpAddress(),
                cancellationToken);

            return Ok(ApiResponseFactory.Success(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理服务访问 report 请求失败");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponseFactory.Error<ServiceAccessReportResponse>(
                    "服务访问 report 失败",
                    StatusCodes.Status500InternalServerError));
        }
    }

    private string GetRequesterIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? string.Empty;
    }
}
