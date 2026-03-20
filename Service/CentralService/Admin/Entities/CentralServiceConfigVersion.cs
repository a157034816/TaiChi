using SqlSugar;

namespace CentralService.Admin.Entities;

[SugarTable("Gateway_ConfigVersion")]
public sealed class CentralServiceConfigVersion
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    public int VersionNo { get; set; }

    [SugarColumn(Length = 32, IsNullable = false)]
    public string Status { get; set; } = "Draft";

    [SugarColumn(Length = 512, IsNullable = true)]
    public string? Comment { get; set; }

    [SugarColumn(ColumnDataType = "ntext", IsNullable = false)]
    public string ConfigJson { get; set; } = "{}";

    [SugarColumn(IsNullable = true)]
    public int? BasedOnVersionId { get; set; }

    [SugarColumn(IsNullable = true)]
    public int? CreatedByUserId { get; set; }

    [SugarColumn(Length = 64, IsNullable = true)]
    public string? CreatedByUsername { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    [SugarColumn(IsNullable = true)]
    public int? UpdatedByUserId { get; set; }

    [SugarColumn(Length = 64, IsNullable = true)]
    public string? UpdatedByUsername { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? PublishedAtUtc { get; set; }

    [SugarColumn(IsNullable = true)]
    public int? PublishedByUserId { get; set; }

    [SugarColumn(Length = 64, IsNullable = true)]
    public string? PublishedByUsername { get; set; }
}
