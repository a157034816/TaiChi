using SqlSugar;

namespace CentralService.Admin.Entities;

[SugarTable("Gateway_User")]
public sealed class CentralServiceUser
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string Username { get; set; } = string.Empty;

    [SugarColumn(Length = 256, IsNullable = false)]
    public string PasswordHash { get; set; } = string.Empty;

    public bool IsDisabled { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? LastLoginAtUtc { get; set; }
}
