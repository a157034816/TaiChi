using System.Collections.Generic;

namespace TaiChi.Upgrade.Shared
{
    /// <summary>
    /// 更新响应
    /// </summary>
    public class UpdateResponse
    {
        // 是否有更新
        public bool HasUpdate { get; set; }

        // 最新版本信息
        public VersionInfo? LatestVersion { get; set; }

        // 建议的更新包
        public UpdatePackageInfo? SuggestedPackage { get; set; }

        // 可用的更新包列表
        public List<UpdatePackageInfo> AvailablePackages { get; set; } = new List<UpdatePackageInfo>();
        
        // 断点续传：是否支持断点续传
        public bool SupportResume { get; set; } = true;
        
        // 断点续传：确认的文件偏移（字节）
        public long ConfirmedOffset { get; set; } = 0;
        
        // 断点续传：服务器端文件总大小（字节）
        public long TotalSize { get; set; } = 0;
    }
}