using SqlSugar;

namespace CentralService.Admin.Entities;

[SugarTable("Gateway_ConfigPublish")]
public sealed class CentralServiceConfigPublish
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(Length = 32, IsNullable = false)]
    public string Action { get; set; } = "Publish";

    [SugarColumn(IsNullable = true)]
    public int? FromVersionId { get; set; }

    public int ToVersionId { get; set; }

    [SugarColumn(Length = 512, IsNullable = true)]
    public string? Note { get; set; }

    [SugarColumn(IsNullable = true)]
    public int? ActorUserId { get; set; }

    [SugarColumn(Length = 64, IsNullable = true)]
    public string? ActorUsername { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
