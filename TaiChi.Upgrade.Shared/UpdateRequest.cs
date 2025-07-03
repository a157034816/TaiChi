using System;

namespace TaiChi.Upgrade.Shared
{
    /// <summary>
    /// 更新请求
    /// </summary>
    public class UpdateRequest
    {
        // 应用ID
        public string AppId { get; set; } = string.Empty;

        // 当前版本
        public Version CurrentVersion { get; set; } = new Version(1, 0, 0, 0);
        
        // 断点续传：当前下载的文件偏移（字节）
        public long FileOffset { get; set; } = 0;
        
        // 断点续传：请求的文件标识符
        public string? PackageId { get; set; }
    }
}