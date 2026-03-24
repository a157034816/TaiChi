using SqlSugar;

namespace CentralService.Admin.Entities;

[SugarTable("Gateway_RolePermission")]
public sealed class CentralServiceRolePermission
{
    [SugarColumn(IsPrimaryKey = true)]
    public int RoleId { get; set; }

    [SugarColumn(IsPrimaryKey = true, Length = 128, IsNullable = false)]
    public string PermissionKey { get; set; } = string.Empty;
}

