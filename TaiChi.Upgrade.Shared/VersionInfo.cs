using System;

namespace TaiChi.Upgrade.Shared
{
    /// <summary>
    /// 版本信息
    /// </summary>
    public class VersionInfo
    {
        // 版本标识
        public string VersionId { get; set; } = string.Empty;

        // 版本号
        public Version Version { get; set; } = new Version(1, 0, 0, 0);

        // 发布日期
        public DateTime ReleaseDate { get; set; } = DateTime.Now;

        // 版本说明
        public string Description { get; set; } = string.Empty;

        // 所属应用ID
        public string AppId { get; set; } = string.Empty;

        // 是否是最新版本
        public bool IsLatest { get; set; }
    }
}