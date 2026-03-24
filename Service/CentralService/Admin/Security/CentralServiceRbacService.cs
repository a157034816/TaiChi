using System.Security.Claims;
using CentralService.Admin.Data;
using CentralService.Admin.Entities;
using Microsoft.AspNetCore.Authentication.Cookies;
using SqlSugar;

namespace CentralService.Admin.Security;

public sealed class CentralServiceRbacService
{
    private readonly CentralServiceAdminDb _db;

    public CentralServiceRbacService(CentralServiceAdminDb db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<CentralServiceUser?> FindUserByUsernameAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        return await _db.Db.Queryable<CentralServiceUser>()
            .Where(x => x.Username == username)
            .FirstAsync();
    }

    public async Task<CentralServiceUser?> FindUserByIdAsync(int userId)
    {
        return await _db.Db.Queryable<CentralServiceUser>()
            .Where(x => x.Id == userId)
            .FirstAsync();
    }

    public async Task<IReadOnlyList<string>> GetRoleNamesForUserAsync(int userId)
    {
        var roleNames = await _db.Db.Queryable<CentralServiceUserRole, CentralServiceRole>((ur, r) =>
                new JoinQueryInfos(JoinType.Inner, ur.RoleId == r.Id))
            .Where((ur, r) => ur.UserId == userId)
            .Select((ur, r) => r.Name)
            .ToListAsync();

        return roleNames.Distinct(StringComparer.Ordinal).ToArray();
    }

    public async Task<IReadOnlyList<string>> GetPermissionKeysForUserAsync(int userId)
    {
        var permissionKeys = await _db.Db.Queryable<CentralServiceUserRole, CentralServiceRolePermission>((ur, rp) =>
                new JoinQueryInfos(JoinType.Inner, ur.RoleId == rp.RoleId))
            .Where((ur, rp) => ur.UserId == userId)
            .Select((ur, rp) => rp.PermissionKey)
            .ToListAsync();

        return permissionKeys.Distinct(StringComparer.Ordinal).ToArray();
    }

    public async Task<ClaimsPrincipal> BuildPrincipalAsync(CentralServiceUser user)
    {
        var roleNames = await GetRoleNamesForUserAsync(user.Id);
        var permissionKeys = await GetPermissionKeysForUserAsync(user.Id);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
        };

        claims.AddRange(roleNames.Select(roleName => new Claim(ClaimTypes.Role, roleName)));
        claims.AddRange(permissionKeys.Select(permissionKey => new Claim(CentralServiceClaimTypes.Permission, permissionKey)));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }
}

