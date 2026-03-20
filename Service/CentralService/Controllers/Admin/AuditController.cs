using CentralService.Admin;
using CentralService.Admin.Data;
using CentralService.Admin.Entities;
using CentralService.Admin.Models;
using CentralService.Service.Models;
using ApiResponseFactory = CentralService.Internal.CentralServiceApiResponseFactory;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;

namespace CentralService.Controllers.Admin;

[ApiController]
[Route("api/admin/audit")]
[Produces("application/json")]
[EnableCors("CentralServiceAdmin")]
[Authorize(
    AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme,
    Policy = CentralServicePermissions.Audit.Read)]
public sealed class AuditController : ControllerBase
{
    private readonly CentralServiceAdminDb _db;

    public AuditController(CentralServiceAdminDb db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<AuditLogListItem>>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? keyword = null,
        [FromQuery] string? action = null,
        [FromQuery] string? actorUsername = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize switch
        {
            <= 0 => 50,
            > 200 => 200,
            _ => pageSize
        };

        var query = _db.Db.Queryable<CentralServiceAuditLog>();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(x =>
                x.Action.Contains(k) ||
                x.Resource.Contains(k) ||
                (x.ActorUsername != null && x.ActorUsername.Contains(k)));
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            var a = action.Trim();
            query = query.Where(x => x.Action == a);
        }

        if (!string.IsNullOrWhiteSpace(actorUsername))
        {
            var u = actorUsername.Trim();
            query = query.Where(x => x.ActorUsername == u);
        }

        if (fromUtc != null)
        {
            query = query.Where(x => x.CreatedAtUtc >= fromUtc.Value);
        }

        if (toUtc != null)
        {
            query = query.Where(x => x.CreatedAtUtc <= toUtc.Value);
        }

        var total = await query.CountAsync();

        var entities = await query
            .OrderBy(x => x.Id, OrderByType.Desc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = entities
            .Select(x => new AuditLogListItem(
                x.Id,
                x.CreatedAtUtc,
                x.ActorUserId,
                x.ActorUsername,
                x.Action,
                x.Resource,
                x.TraceId,
                x.Ip,
                x.UserAgent))
            .ToArray();

        return Ok(ApiResponseFactory.Success(
            new PagedResult<AuditLogListItem>(page, pageSize, total, items)));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<AuditLogDetail>>> GetById(int id)
    {
        var log = await _db.Db.Queryable<CentralServiceAuditLog>()
            .Where(x => x.Id == id)
            .FirstAsync();

        if (log == null)
        {
            return NotFound(ApiResponseFactory.Error<AuditLogDetail>("审计记录不存在", 404));
        }

        var detail = new AuditLogDetail(
            log.Id,
            log.CreatedAtUtc,
            log.ActorUserId,
            log.ActorUsername,
            log.Action,
            log.Resource,
            log.TraceId,
            log.Ip,
            log.UserAgent,
            log.BeforeJson,
            log.AfterJson);

        return Ok(ApiResponseFactory.Success(detail));
    }
}
