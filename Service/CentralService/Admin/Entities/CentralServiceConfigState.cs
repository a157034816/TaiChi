using SqlSugar;

namespace CentralService.Admin.Entities;

[SugarTable("Gateway_ConfigState")]
public sealed class CentralServiceConfigState
{
    [SugarColumn(IsPrimaryKey = true)]
    public int Id { get; set; } = 1;

    [SugarColumn(IsNullable = true)]
    public int? CurrentVersionId { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
