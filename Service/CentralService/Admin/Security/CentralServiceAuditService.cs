using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using CentralService.Admin.Data;
using CentralService.Admin.Entities;

namespace CentralService.Admin.Security;

public sealed class CentralServiceAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    private readonly CentralServiceAdminDb _db;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CentralServiceAuditService> _logger;

    public CentralServiceAuditService(
        CentralServiceAdminDb db,
        IHttpContextAccessor httpContextAccessor,
        ILogger<CentralServiceAuditService> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task TryWriteAsync(string action, string resource, object? before = null, object? after = null)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var actorUserId = TryGetActorUserId(httpContext?.User);
            var actorUsername = httpContext?.User?.Identity?.Name;

            var entry = new CentralServiceAuditLog
            {
                ActorUserId = actorUserId,
                ActorUsername = actorUsername,
                Action = action,
                Resource = resource,
                BeforeJson = before == null ? null : JsonSerializer.Serialize(before, JsonOptions),
                AfterJson = after == null ? null : JsonSerializer.Serialize(after, JsonOptions),
                TraceId = Activity.Current?.TraceId.ToString() ?? httpContext?.TraceIdentifier,
                Ip = httpContext?.Connection.RemoteIpAddress?.ToString(),
                UserAgent = httpContext?.Request.Headers.UserAgent.ToString(),
                CreatedAtUtc = DateTime.UtcNow,
            };

            await _db.Db.Insertable(entry).ExecuteCommandAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "写入审计日志失败: {Action} {Resource}", action, resource);
        }
    }

    private static int? TryGetActorUserId(ClaimsPrincipal? user)
    {
        var value = user?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(value, out var userId))
        {
            return userId;
        }

        return null;
    }
}

