namespace TaiChi.Upgrade.Shared
{
    /// <summary>
    /// 更新包信息
    /// </summary>
    public class UpdatePackageInfo
    {
        // 包标识
        public string PackageId { get; set; } = string.Empty;

        // 源版本ID
        public string SourceVersionId { get; set; } = string.Empty;

        // 目标版本ID
        public string TargetVersionId { get; set; } = string.Empty;

        // 包类型（增量/完整）
        public UpdatePackageType PackageType { get; set; }

        // 包路径
        public string PackagePath { get; set; } = string.Empty;

        // 包大小（字节）
        public long PackageSize { get; set; }

        // 校验和
        public string Checksum { get; set; } = string.Empty;
    }
}