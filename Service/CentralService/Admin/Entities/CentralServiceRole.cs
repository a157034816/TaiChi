using SqlSugar;

namespace CentralService.Admin.Entities;

[SugarTable("Gateway_Role")]
public sealed class CentralServiceRole
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string Name { get; set; } = string.Empty;

    [SugarColumn(Length = 256, IsNullable = true)]
    public string? Description { get; set; }

    public bool IsSystem { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}

