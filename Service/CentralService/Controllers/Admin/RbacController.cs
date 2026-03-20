using CentralService.Admin;
using CentralService.Admin.Data;
using CentralService.Admin.Entities;
using CentralService.Admin.Models;
using CentralService.Admin.Security;
using CentralService.Service.Models;
using ApiResponseFactory = CentralService.Internal.CentralServiceApiResponseFactory;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;

namespace CentralService.Controllers.Admin;

[ApiController]
[Route("api/admin/rbac")]
[Produces("application/json")]
[EnableCors("CentralServiceAdmin")]
[Authorize(
    AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme,
    Policy = CentralServicePermissions.Users.Manage)]
public sealed class RbacController : ControllerBase
{
    private readonly CentralServiceAdminDb _db;
    private readonly CentralServiceAuditService _auditService;

    public RbacController(CentralServiceAdminDb db, CentralServiceAuditService auditService)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
    }

    [HttpGet("permissions")]
    public ActionResult<ApiResponse<IReadOnlyList<CentralServicePermissions.PermissionDefinition>>> GetPermissions()
    {
        return Ok(ApiResponseFactory.Success<IReadOnlyList<CentralServicePermissions.PermissionDefinition>>(CentralServicePermissions.Definitions));
    }

    [HttpGet("roles")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RoleDto>>>> GetRoles()
    {
        var roles = await _db.Db.Queryable<CentralServiceRole>()
            .OrderBy(x => x.Id)
            .ToListAsync();

        var result = roles
            .Select(x => new RoleDto(x.Id, x.Name, x.Description, x.IsSystem))
            .ToArray();

        return Ok(ApiResponseFactory.Success<IReadOnlyList<RoleDto>>(result));
    }

    [HttpPost("roles")]
    public async Task<ActionResult<ApiResponse<RoleDto>>> CreateRole([FromBody] CreateRoleRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(ApiResponseFactory.Error<RoleDto>("角色名称不能为空", 400));
        }

        var roleName = request.Name.Trim();
        if (roleName.Length > 64)
        {
            return BadRequest(ApiResponseFactory.Error<RoleDto>("角色名称过长", 400));
        }

        var existing = await _db.Db.Queryable<CentralServiceRole>().Where(x => x.Name == roleName).FirstAsync();
        if (existing != null)
        {
            return Conflict(ApiResponseFactory.Error<RoleDto>("角色已存在", 409));
        }

        var now = DateTime.UtcNow;
        var role = new CentralServiceRole
        {
            Name = roleName,
            Description = request.Description,
            IsSystem = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        role.Id = await _db.Db.Insertable(role).ExecuteReturnIdentityAsync();
        var dto = new RoleDto(role.Id, role.Name, role.Description, role.IsSystem);

        await _auditService.TryWriteAsync(
            action: "rbac.role.create",
            resource: $"role:{role.Id}",
            after: new { role.Id, role.Name, role.Description });

        return Ok(ApiResponseFactory.Success(dto));
    }

    [HttpPut("roles/{roleId:int}/permissions")]
    public async Task<ActionResult<ApiResponse<object>>> SetRolePermissions(int roleId, [FromBody] SetRolePermissionsRequest request)
    {
        if (request == null)
        {
            return BadRequest(ApiResponseFactory.Error<object>("请求不能为空", 400));
        }

        var role = await _db.Db.Queryable<CentralServiceRole>().Where(x => x.Id == roleId).FirstAsync();
        if (role == null)
        {
            return NotFound(ApiResponseFactory.Error<object>("角色不存在", 404));
        }

        if (role.IsSystem)
        {
            return BadRequest(ApiResponseFactory.Error<object>("系统角色不允许修改权限", 400));
        }

        var permissionKeys = (request.PermissionKeys ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var invalidKeys = permissionKeys
            .Where(x => !CentralServicePermissions.AllKeys.Contains(x, StringComparer.Ordinal))
            .ToArray();

        if (invalidKeys.Length > 0)
        {
            return BadRequest(ApiResponseFactory.Error<object>($"存在无效权限Key: {string.Join(", ", invalidKeys)}", 400));
        }

        var beforeKeys = await _db.Db.Queryable<CentralServiceRolePermission>()
            .Where(x => x.RoleId == roleId)
            .Select(x => x.PermissionKey)
            .ToListAsync();

        await _db.Db.Deleteable<CentralServiceRolePermission>().Where(x => x.RoleId == roleId).ExecuteCommandAsync();

        if (permissionKeys.Length > 0)
        {
            var rolePermissions = permissionKeys.Select(key => new CentralServiceRolePermission
            {
                RoleId = roleId,
                PermissionKey = key,
            });

            await _db.Db.Insertable(rolePermissions.ToList()).ExecuteCommandAsync();
        }

        await _auditService.TryWriteAsync(
            action: "rbac.role.permissions.set",
            resource: $"role:{roleId}",
            before: new { permissionKeys = beforeKeys.Distinct(StringComparer.Ordinal).ToArray() },
            after: new { permissionKeys });

        return Ok(ApiResponseFactory.Success<object>(new { }));
    }

    [HttpGet("users")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<UserListItem>>>> GetUsers()
    {
        var users = await _db.Db.Queryable<CentralServiceUser>()
            .OrderBy(x => x.Id)
            .ToListAsync();

        var userRoles = await _db.Db.Queryable<CentralServiceUserRole, CentralServiceRole>((ur, r) =>
                new JoinQueryInfos(JoinType.Inner, ur.RoleId == r.Id))
            .Select((ur, r) => new { ur.UserId, RoleName = r.Name })
            .ToListAsync();

        var rolesByUser = userRoles
            .GroupBy(x => x.UserId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.Select(x => x.RoleName).Distinct(StringComparer.Ordinal).ToArray());

        var result = users
            .Select(u =>
                new UserListItem(
                    u.Id,
                    u.Username,
                    u.IsDisabled,
                    rolesByUser.TryGetValue(u.Id, out var roles) ? roles : Array.Empty<string>()))
            .ToArray();

        return Ok(ApiResponseFactory.Success<IReadOnlyList<UserListItem>>(result));
    }

    [HttpPost("users")]
    public async Task<ActionResult<ApiResponse<object>>> CreateUser([FromBody] CreateUserRequest request)
    {
        if (request == null ||
            string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(ApiResponseFactory.Error<object>("用户名或密码不能为空", 400));
        }

        var username = request.Username.Trim();
        if (username.Length > 64)
        {
            return BadRequest(ApiResponseFactory.Error<object>("用户名过长", 400));
        }

        var existing = await _db.Db.Queryable<CentralServiceUser>().Where(x => x.Username == username).FirstAsync();
        if (existing != null)
        {
            return Conflict(ApiResponseFactory.Error<object>("用户已存在", 409));
        }

        var roleIds = (request.RoleIds ?? Array.Empty<int>()).Distinct().ToArray();
        if (roleIds.Length == 0)
        {
            return BadRequest(ApiResponseFactory.Error<object>("至少需要指定一个角色", 400));
        }

        var roles = await _db.Db.Queryable<CentralServiceRole>()
            .Where(x => SqlFunc.ContainsArray(roleIds, x.Id))
            .ToListAsync();
        if (roles.Count != roleIds.Length)
        {
            return BadRequest(ApiResponseFactory.Error<object>("包含不存在的角色Id", 400));
        }

        var now = DateTime.UtcNow;
        var user = new CentralServiceUser
        {
            Username = username,
            PasswordHash = PasswordHash.Hash(request.Password),
            IsDisabled = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        user.Id = await _db.Db.Insertable(user).ExecuteReturnIdentityAsync();

        var userRoleEntities = roleIds.Select(roleId => new CentralServiceUserRole
        {
            UserId = user.Id,
            RoleId = roleId,
        });
        await _db.Db.Insertable(userRoleEntities.ToList()).ExecuteCommandAsync();

        await _auditService.TryWriteAsync(
            action: "rbac.user.create",
            resource: $"user:{user.Id}",
            after: new { user.Id, user.Username, roleIds });

        return Ok(ApiResponseFactory.Success<object>(new { userId = user.Id }));
    }

    [HttpPut("users/{userId:int}/password")]
    public async Task<ActionResult<ApiResponse<object>>> ResetPassword(int userId, [FromBody] SetUserPasswordRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(ApiResponseFactory.Error<object>("新密码不能为空", 400));
        }

        var user = await _db.Db.Queryable<CentralServiceUser>().Where(x => x.Id == userId).FirstAsync();
        if (user == null)
        {
            return NotFound(ApiResponseFactory.Error<object>("用户不存在", 404));
        }

        user.PasswordHash = PasswordHash.Hash(request.NewPassword);
        user.UpdatedAtUtc = DateTime.UtcNow;
        await _db.Db.Updateable(user).ExecuteCommandAsync();

        await _auditService.TryWriteAsync(
            action: "rbac.user.password.reset",
            resource: $"user:{user.Id}",
            after: new { user.Id, user.Username });

        return Ok(ApiResponseFactory.Success<object>(new { }));
    }

    [HttpPut("users/{userId:int}/roles")]
    public async Task<ActionResult<ApiResponse<object>>> SetUserRoles(int userId, [FromBody] SetUserRolesRequest request)
    {
        if (request == null)
        {
            return BadRequest(ApiResponseFactory.Error<object>("请求不能为空", 400));
        }

        var user = await _db.Db.Queryable<CentralServiceUser>().Where(x => x.Id == userId).FirstAsync();
        if (user == null)
        {
            return NotFound(ApiResponseFactory.Error<object>("用户不存在", 404));
        }

        var roleIds = (request.RoleIds ?? Array.Empty<int>()).Distinct().ToArray();
        if (roleIds.Length == 0)
        {
            return BadRequest(ApiResponseFactory.Error<object>("至少需要指定一个角色", 400));
        }

        var roles = await _db.Db.Queryable<CentralServiceRole>()
            .Where(x => SqlFunc.ContainsArray(roleIds, x.Id))
            .ToListAsync();
        if (roles.Count != roleIds.Length)
        {
            return BadRequest(ApiResponseFactory.Error<object>("包含不存在的角色Id", 400));
        }

        var beforeRoleNames = await _db.Db.Queryable<CentralServiceUserRole, CentralServiceRole>((ur, r) =>
                new JoinQueryInfos(JoinType.Inner, ur.RoleId == r.Id))
            .Where((ur, r) => ur.UserId == userId)
            .Select((ur, r) => r.Name)
            .ToListAsync();

        await _db.Db.Deleteable<CentralServiceUserRole>().Where(x => x.UserId == userId).ExecuteCommandAsync();

        var userRoleEntities = roleIds.Select(roleId => new CentralServiceUserRole
        {
            UserId = userId,
            RoleId = roleId,
        });
        await _db.Db.Insertable(userRoleEntities.ToList()).ExecuteCommandAsync();

        var afterRoleNames = roles.Select(x => x.Name).Distinct(StringComparer.Ordinal).ToArray();
        await _auditService.TryWriteAsync(
            action: "rbac.user.roles.set",
            resource: $"user:{userId}",
            before: new { roles = beforeRoleNames.Distinct(StringComparer.Ordinal).ToArray() },
            after: new { roles = afterRoleNames });

        return Ok(ApiResponseFactory.Success<object>(new { }));
    }

    [HttpPut("users/{userId:int}/disabled")]
    public async Task<ActionResult<ApiResponse<object>>> SetUserDisabled(int userId, [FromBody] SetUserDisabledRequest request)
    {
        if (request == null)
        {
            return BadRequest(ApiResponseFactory.Error<object>("请求不能为空", 400));
        }

        var user = await _db.Db.Queryable<CentralServiceUser>().Where(x => x.Id == userId).FirstAsync();
        if (user == null)
        {
            return NotFound(ApiResponseFactory.Error<object>("用户不存在", 404));
        }

        var beforeDisabled = user.IsDisabled;

        user.IsDisabled = request.IsDisabled;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await _db.Db.Updateable(user).ExecuteCommandAsync();

        await _auditService.TryWriteAsync(
            action: "rbac.user.disabled.set",
            resource: $"user:{user.Id}",
            before: new { user.Id, user.Username, isDisabled = beforeDisabled },
            after: new { user.Id, user.Username, isDisabled = user.IsDisabled });

        return Ok(ApiResponseFactory.Success<object>(new { }));
    }
}
