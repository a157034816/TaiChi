using System.Security.Claims;
using CentralService.Admin;
using CentralService.Admin.Data;
using CentralService.Admin.Entities;
using CentralService.Admin.Models;
using CentralService.Admin.Security;
using CentralService.Service.Models;
using ApiResponseFactory = CentralService.Internal.CentralServiceApiResponseFactory;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;

namespace CentralService.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
[EnableCors("CentralServiceAdmin")]
public sealed class AuthController : ControllerBase
{
    private readonly CentralServiceRbacService _rbacService;
    private readonly CentralServiceAdminDb _db;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IConfiguration _configuration;
    private readonly CentralServiceAuditService _auditService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        CentralServiceRbacService rbacService,
        CentralServiceAdminDb db,
        IHostEnvironment hostEnvironment,
        IConfiguration configuration,
        CentralServiceAuditService auditService,
        ILogger<AuthController> logger)
    {
        _rbacService = rbacService ?? throw new ArgumentNullException(nameof(rbacService));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _hostEnvironment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 获取首次初始化可用性状态。
    /// </summary>
    [HttpGet("bootstrap/status")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<BootstrapStatusResponse>>> GetBootstrapStatus()
    {
        var status = await BuildBootstrapStatusAsync();
        return Ok(ApiResponseFactory.Success(status));
    }

    /// <summary>
    /// 初始化管理员账号（仅在没有任何用户时允许，默认仅开发环境启用且限制本机访问）。
    /// </summary>
    [HttpPost("bootstrap")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Bootstrap([FromBody] LoginRequest request)
    {
        var status = await BuildBootstrapStatusAsync();
        if (!status.Enabled)
        {
            return NotFound(ApiResponseFactory.Error<LoginResponse>("Bootstrap 未启用", 404));
        }

        if (!status.IsLoopbackRequest)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponseFactory.Error<LoginResponse>("仅允许本机初始化", 403));
        }

        if (request == null ||
            string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(ApiResponseFactory.Error<LoginResponse>("用户名或密码不能为空", 400));
        }

        if (status.HasAnyUser)
        {
            return Conflict(ApiResponseFactory.Error<LoginResponse>("系统已初始化", 409));
        }

        var now = DateTime.UtcNow;
        var adminRole = await _db.Db.Queryable<CentralServiceRole>()
            .Where(x => x.Name == CentralServiceRoleNames.Administrator)
            .FirstAsync();

        if (adminRole == null)
        {
            adminRole = new CentralServiceRole
            {
                Name = CentralServiceRoleNames.Administrator,
                Description = "系统内置：管理员（拥有全部权限）",
                IsSystem = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
            adminRole.Id = await _db.Db.Insertable(adminRole).ExecuteReturnIdentityAsync();

            var rolePermissions = CentralServicePermissions.AllKeys.Select(key => new CentralServiceRolePermission
            {
                RoleId = adminRole.Id,
                PermissionKey = key,
            }).ToList();

            await _db.Db.Insertable(rolePermissions).ExecuteCommandAsync();
        }

        var username = request.Username.Trim();
        var user = new CentralServiceUser
        {
            Username = username,
            PasswordHash = PasswordHash.Hash(request.Password),
            IsDisabled = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        user.Id = await _db.Db.Insertable(user).ExecuteReturnIdentityAsync();
        await _db.Db.Insertable(new CentralServiceUserRole { UserId = user.Id, RoleId = adminRole.Id }).ExecuteCommandAsync();

        var principal = await _rbacService.BuildPrincipalAsync(user);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        var roles = principal.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray();
        var permissions = principal.FindAll(CentralServiceClaimTypes.Permission).Select(x => x.Value).ToArray();

        await _auditService.TryWriteAsync(
            action: "auth.bootstrap",
            resource: $"user:{user.Id}",
            after: new { user.Id, user.Username, roles });

        _logger.LogWarning("已完成首次管理员初始化：{Username}", user.Username);
        return Ok(ApiResponseFactory.Success(new LoginResponse(user.Id, user.Username, roles, permissions)));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromBody] LoginRequest request)
    {
        if (request == null ||
            string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(ApiResponseFactory.Error<LoginResponse>("用户名或密码不能为空", 400));
        }

        var user = await _rbacService.FindUserByUsernameAsync(request.Username);
        if (user == null || user.IsDisabled)
        {
            return Unauthorized(ApiResponseFactory.Error<LoginResponse>("用户名或密码错误", 401));
        }

        if (!PasswordHash.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(ApiResponseFactory.Error<LoginResponse>("用户名或密码错误", 401));
        }

        user.LastLoginAtUtc = DateTime.UtcNow;
        user.UpdatedAtUtc = user.LastLoginAtUtc.Value;
        await _db.Db.Updateable(user).ExecuteCommandAsync();

        var principal = await _rbacService.BuildPrincipalAsync(user);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        var roles = principal.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray();
        var permissions = principal.FindAll(CentralServiceClaimTypes.Permission).Select(x => x.Value).ToArray();

        await _auditService.TryWriteAsync(
            action: "auth.login",
            resource: $"user:{user.Id}",
            after: new { user.Id, user.Username, roles });

        _logger.LogInformation("用户登录成功：{Username}", user.Username);
        return Ok(ApiResponseFactory.Success(new LoginResponse(user.Id, user.Username, roles, permissions)));
    }

    [HttpPost("logout")]
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
    public async Task<ActionResult<ApiResponse<object>>> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        await _auditService.TryWriteAsync(
            action: "auth.logout",
            resource: "session",
            after: new { });

        return Ok(ApiResponseFactory.Success<object>(new { }));
    }

    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Me()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdValue, out var userId))
        {
            return Unauthorized(ApiResponseFactory.Error<LoginResponse>("未登录", 401));
        }

        var user = await _rbacService.FindUserByIdAsync(userId);
        if (user == null || user.IsDisabled)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Unauthorized(ApiResponseFactory.Error<LoginResponse>("未登录", 401));
        }

        var principal = await _rbacService.BuildPrincipalAsync(user);
        var roles = principal.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray();
        var permissions = principal.FindAll(CentralServiceClaimTypes.Permission).Select(x => x.Value).ToArray();

        return Ok(ApiResponseFactory.Success(new LoginResponse(user.Id, user.Username, roles, permissions)));
    }

    [HttpPost("refresh")]
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Refresh()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdValue, out var userId))
        {
            return Unauthorized(ApiResponseFactory.Error<LoginResponse>("未登录", 401));
        }

        var user = await _rbacService.FindUserByIdAsync(userId);
        if (user == null || user.IsDisabled)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Unauthorized(ApiResponseFactory.Error<LoginResponse>("未登录", 401));
        }

        var principal = await _rbacService.BuildPrincipalAsync(user);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        var roles = principal.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray();
        var permissions = principal.FindAll(CentralServiceClaimTypes.Permission).Select(x => x.Value).ToArray();

        return Ok(ApiResponseFactory.Success(new LoginResponse(user.Id, user.Username, roles, permissions)));
    }

    private async Task<BootstrapStatusResponse> BuildBootstrapStatusAsync()
    {
        var enabled = _configuration.GetValue<bool?>("CentralServiceAuth:Bootstrap:Enabled") ?? _hostEnvironment.IsDevelopment();
        var remoteIp = HttpContext.Connection.RemoteIpAddress;
        var isLoopbackRequest = remoteIp != null && System.Net.IPAddress.IsLoopback(remoteIp);
        var hasAnyUser = await _db.Db.Queryable<CentralServiceUser>().AnyAsync();

        if (!enabled)
        {
            return new BootstrapStatusResponse(
                Enabled: false,
                HasAnyUser: hasAnyUser,
                IsLoopbackRequest: isLoopbackRequest,
                CanBootstrap: false,
                Message: "当前环境未启用首次初始化。请配置种子管理员，或显式设置 CentralServiceAuth:Bootstrap:Enabled=true。");
        }

        if (!isLoopbackRequest)
        {
            return new BootstrapStatusResponse(
                Enabled: true,
                HasAnyUser: hasAnyUser,
                IsLoopbackRequest: false,
                CanBootstrap: false,
                Message: "首次初始化仅允许本机请求。");
        }

        if (hasAnyUser)
        {
            return new BootstrapStatusResponse(
                Enabled: true,
                HasAnyUser: true,
                IsLoopbackRequest: true,
                CanBootstrap: false,
                Message: "系统已初始化，请直接使用现有管理员账号登录。");
        }

        return new BootstrapStatusResponse(
            Enabled: true,
            HasAnyUser: false,
            IsLoopbackRequest: true,
            CanBootstrap: true,
            Message: "当前允许首次初始化。");
    }
}
