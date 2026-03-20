using SqlSugar;

namespace CentralService.Admin.Entities;

[SugarTable("Gateway_UserRole")]
public sealed class CentralServiceUserRole
{
    [SugarColumn(IsPrimaryKey = true)]
    public int UserId { get; set; }

    [SugarColumn(IsPrimaryKey = true)]
    public int RoleId { get; set; }
}

