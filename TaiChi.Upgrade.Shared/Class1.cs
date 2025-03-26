using System;
using System.Collections.Generic;

namespace TaiChi.Upgrade.Shared
{
    // 应用信息
    public class AppInfo
    {
        // 应用标识
        public string AppId { get; set; } = string.Empty;

        // 应用名称
        public string AppName { get; set; } = string.Empty;

        // 描述
        public string Description { get; set; } = string.Empty;
    }

    // 版本信息
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

    // 更新包信息
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

    // 更新包类型
    public enum UpdatePackageType
    {
        // 增量补丁包
        Incremental,

        // 完整内容包
        Full
    }

    // 更新请求
    public class UpdateRequest
    {
        // 应用ID
        public string AppId { get; set; } = string.Empty;

        // 当前版本
        public Version CurrentVersion { get; set; } = new Version(1, 0, 0, 0);
    }

    // 更新响应
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
    }
}