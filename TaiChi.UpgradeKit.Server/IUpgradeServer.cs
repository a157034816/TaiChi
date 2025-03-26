using System;
using System.Collections.Generic;
using TaiChi.Upgrade.Shared;

namespace TaiChi.UpgradeKit.Server
{
    /// <summary>
    /// 升级服务器接口
    /// </summary>
    public interface IUpgradeServer
    {
        /// <summary>
        /// 启动升级服务
        /// </summary>
        void Start();

        /// <summary>
        /// 注册应用
        /// </summary>
        /// <param name="appInfo">应用信息</param>
        void RegisterApp(AppInfo appInfo);

        /// <summary>
        /// 发布新版本
        /// </summary>
        /// <param name="versionInfo">版本信息</param>
        /// <param name="fullPackagePath">完整包路径</param>
        void PublishVersion(VersionInfo versionInfo, string fullPackagePath);

        /// <summary>
        /// 发布增量补丁
        /// </summary>
        /// <param name="appId">应用ID</param>
        /// <param name="sourceVersionId">源版本ID</param>
        /// <param name="targetVersionId">目标版本ID</param>
        /// <param name="incrementalPackagePath">增量包路径</param>
        void PublishIncrementalPackage(string appId, string sourceVersionId, string targetVersionId, string incrementalPackagePath);

        /// <summary>
        /// 检查更新
        /// </summary>
        /// <param name="request">更新请求</param>
        /// <returns>更新响应</returns>
        UpdateResponse CheckUpdate(UpdateRequest request);

        /// <summary>
        /// 获取所有应用
        /// </summary>
        /// <returns>应用列表</returns>
        List<AppInfo> GetAllApps();

        /// <summary>
        /// 获取应用版本列表
        /// </summary>
        /// <param name="appId">应用ID</param>
        /// <returns>版本列表</returns>
        List<VersionInfo> GetAppVersions(string appId);

        /// <summary>
        /// 获取更新包数据流（支持断点续传）
        /// </summary>
        /// <param name="packageId">包ID</param>
        /// <param name="startPosition">起始位置（字节）</param>
        /// <param name="length">长度（字节，0表示到文件末尾）</param>
        /// <returns>包含数据的内存流及相关信息</returns>
        (System.IO.Stream DataStream, long TotalSize, bool SupportsResume, long StartPosition) GetPackageDataStream(string packageId, long startPosition = 0, long length = 0);
    }
}