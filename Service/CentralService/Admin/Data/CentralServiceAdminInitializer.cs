using CentralService.Admin;
using CentralService.Admin.Entities;
using CentralService.Admin.Security;
using Microsoft.Extensions.Options;

namespace CentralService.Admin.Data;

/// <summary>
/// 后台管理数据库初始化器：建表、补齐系统角色权限、可选创建种子管理员。
/// </summary>
public sealed class CentralServiceAdminInitializer : IHostedService
{
    private readonly CentralServiceAdminDb _db;
    private readonly CentralServiceAdminSeedOptions _seedOptions;
    private readonly ILogger<CentralServiceAdminInitializer> _logger;

    public CentralServiceAdminInitializer(
        CentralServiceAdminDb db,
        IOptions<CentralServiceAdminSeedOptions> seedOptions,
        ILogger<CentralServiceAdminInitializer> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _seedOptions = seedOptions?.Value ?? throw new ArgumentNullException(nameof(seedOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _db.EnsureCreated();

            var adminRole = await EnsureAdministratorRoleAsync();
            await TrySeedAdministratorAsync(adminRole);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化中心服务后台管理数据库失败");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task<CentralServiceRole> EnsureAdministratorRoleAsync()
    {
        var now = DateTime.UtcNow;

        var role = await _db.Db.Queryable<CentralServiceRole>()
            .Where(x => x.Name == CentralServiceRoleNames.Administrator)
            .FirstAsync();

        if (role == null)
        {
            role = new CentralServiceRole
            {
                Name = CentralServiceRoleNames.Administrator,
                Description = "系统内置：管理员（拥有全部权限）",
                IsSystem = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
            role.Id = await _db.Db.Insertable(role).ExecuteReturnIdentityAsync();

            var permissions = CentralServicePermissions.AllKeys
                .Select(key => new CentralServiceRolePermission
                {
                    RoleId = role.Id,
                    PermissionKey = key,
                })
                .ToList();

            if (permissions.Count > 0)
            {
                await _db.Db.Insertable(permissions).ExecuteCommandAsync();
            }

            _logger.LogInformation("已创建系统角色：{RoleName}", role.Name);
            return role;
        }

        var existingKeys = await _db.Db.Queryable<CentralServiceRolePermission>()
            .Where(x => x.RoleId == role.Id)
            .Select(x => x.PermissionKey)
            .ToListAsync();

        var missingKeys = CentralServicePermissions.AllKeys
            .Except(existingKeys ?? new List<string>(), StringComparer.Ordinal)
            .ToArray();

        if (missingKeys.Length > 0)
        {
            var entities = missingKeys.Select(key => new CentralServiceRolePermission
            {
                RoleId = role.Id,
                PermissionKey = key,
            }).ToList();

            await _db.Db.Insertable(entities).ExecuteCommandAsync();
            _logger.LogInformation("已为管理员角色补齐权限：{Count}", entities.Count);
        }

        return role;
    }

    private async Task TrySeedAdministratorAsync(CentralServiceRole adminRole)
    {
        if (!_seedOptions.Enabled)
        {
            return;
        }

        var username = _seedOptions.AdminUsername?.Trim();
        var password = _seedOptions.AdminPassword;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("已启用 CentralServiceAdminSeed，但未提供用户名或密码，跳过种子管理员初始化。");
            return;
        }

        var hasAnyUser = await _db.Db.Queryable<CentralServiceUser>().AnyAsync();
        if (hasAnyUser)
        {
            _logger.LogInformation("检测到已有用户，跳过种子管理员初始化。");
            return;
        }

        var now = DateTime.UtcNow;

        var user = new CentralServiceUser
        {
            Username = username,
            PasswordHash = PasswordHash.Hash(password),
            IsDisabled = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        user.Id = await _db.Db.Insertable(user).ExecuteReturnIdentityAsync();

        await _db.Db.Insertable(new CentralServiceUserRole
        {
            UserId = user.Id,
            RoleId = adminRole.Id,
        }).ExecuteCommandAsync();

        _logger.LogWarning("已创建种子管理员账号：{Username}", username);
    }
}
