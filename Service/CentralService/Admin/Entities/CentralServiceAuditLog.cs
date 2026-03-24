using SqlSugar;

namespace CentralService.Admin.Entities;

[SugarTable("Gateway_AuditLog")]
public sealed class CentralServiceAuditLog
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(IsNullable = true)]
    public int? ActorUserId { get; set; }

    [SugarColumn(Length = 64, IsNullable = true)]
    public string? ActorUsername { get; set; }

    [SugarColumn(Length = 128, IsNullable = false)]
    public string Action { get; set; } = string.Empty;

    [SugarColumn(Length = 256, IsNullable = false)]
    public string Resource { get; set; } = string.Empty;

    [SugarColumn(ColumnDataType = "ntext", IsNullable = true)]
    public string? BeforeJson { get; set; }

    [SugarColumn(ColumnDataType = "ntext", IsNullable = true)]
    public string? AfterJson { get; set; }

    [SugarColumn(Length = 64, IsNullable = true)]
    public string? TraceId { get; set; }

    [SugarColumn(Length = 64, IsNullable = true)]
    public string? Ip { get; set; }

    [SugarColumn(Length = 512, IsNullable = true)]
    public string? UserAgent { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
