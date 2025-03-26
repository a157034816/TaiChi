using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Encodings.Web;
using TaiChi.Upgrade.Shared;

namespace TaiChi.UpgradeKit.Server
{
    public class UpgradeServer : IUpgradeServer
    {
        private readonly string _upgradeRootPath;
        private readonly Dictionary<string, AppInfo> _appInfos = new Dictionary<string, AppInfo>();
        private readonly Dictionary<string, List<VersionInfo>> _appVersions = new Dictionary<string, List<VersionInfo>>();
        private readonly Dictionary<string, List<UpdatePackageInfo>> _updatePackages = new Dictionary<string, List<UpdatePackageInfo>>();

        // 全局JSON序列化配置
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="upgradeRootPath">升级文件根目录</param>
        public UpgradeServer(string upgradeRootPath)
        {
            _upgradeRootPath = upgradeRootPath ?? throw new ArgumentNullException(nameof(upgradeRootPath));
            if (!Directory.Exists(_upgradeRootPath))
            {
                Directory.CreateDirectory(_upgradeRootPath);
            }
        }

        /// <summary>
        /// 启动升级服务
        /// </summary>
        public void Start()
        {
            Console.WriteLine("UpgradeServer 已启动");

            // 初始化数据
            LoadData();
        }

        /// <summary>
        /// 加载数据
        /// </summary>
        private void LoadData()
        {
            try
            {
                Console.WriteLine("正在从持久化存储加载数据...");

                // 1. 加载应用信息
                string appsFilePath = Path.Combine(_upgradeRootPath, "apps.json");
                if (File.Exists(appsFilePath))
                {
                    string appsJson = File.ReadAllText(appsFilePath);
                    var appsList = JsonSerializer.Deserialize<List<AppInfo>>(appsJson, _jsonOptions);
                    if (appsList != null)
                    {
                        foreach (var app in appsList)
                        {
                            _appInfos[app.AppId] = app;
                        }
                    }
                }

                // 2. 遍历应用目录，加载版本信息和更新包信息
                foreach (var appId in _appInfos.Keys)
                {
                    string appDirectory = Path.Combine(_upgradeRootPath, appId);
                    if (!Directory.Exists(appDirectory))
                    {
                        Directory.CreateDirectory(appDirectory);
                        continue;
                    }

                    // 确保应用有对应的版本列表和更新包列表
                    if (!_appVersions.ContainsKey(appId))
                        _appVersions[appId] = new List<VersionInfo>();

                    if (!_updatePackages.ContainsKey(appId))
                        _updatePackages[appId] = new List<UpdatePackageInfo>();

                    // 加载版本信息
                    string versionsFilePath = Path.Combine(appDirectory, "versions.json");
                    if (File.Exists(versionsFilePath))
                    {
                        string versionsJson = File.ReadAllText(versionsFilePath);
                        var versionsList = JsonSerializer.Deserialize<List<VersionInfo>>(versionsJson, _jsonOptions);
                        if (versionsList != null)
                        {
                            _appVersions[appId].AddRange(versionsList);
                        }
                    }

                    // 加载更新包信息
                    string packagesFilePath = Path.Combine(appDirectory, "packages.json");
                    if (File.Exists(packagesFilePath))
                    {
                        string packagesJson = File.ReadAllText(packagesFilePath);
                        var packagesList = JsonSerializer.Deserialize<List<UpdatePackageInfo>>(packagesJson, _jsonOptions);
                        if (packagesList != null)
                        {
                            _updatePackages[appId].AddRange(packagesList);
                        }
                    }
                }

                Console.WriteLine($"数据加载完成，应用数: {_appInfos.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存数据
        /// </summary>
        private void SaveData()
        {
            try
            {
                Console.WriteLine("正在保存数据到持久化存储...");

                // 1. 保存应用信息
                string appsFilePath = Path.Combine(_upgradeRootPath, "apps.json");
                string appsJson = JsonSerializer.Serialize(_appInfos.Values.ToList(), _jsonOptions);
                File.WriteAllText(appsFilePath, appsJson);

                // 2. 为每个应用保存版本信息和更新包信息
                foreach (var appId in _appInfos.Keys)
                {
                    string appDirectory = Path.Combine(_upgradeRootPath, appId);
                    if (!Directory.Exists(appDirectory))
                    {
                        Directory.CreateDirectory(appDirectory);
                    }

                    // 保存版本信息
                    if (_appVersions.ContainsKey(appId))
                    {
                        string versionsFilePath = Path.Combine(appDirectory, "versions.json");
                        string versionsJson = JsonSerializer.Serialize(_appVersions[appId], _jsonOptions);
                        File.WriteAllText(versionsFilePath, versionsJson);
                    }

                    // 保存更新包信息
                    if (_updatePackages.ContainsKey(appId))
                    {
                        string packagesFilePath = Path.Combine(appDirectory, "packages.json");
                        string packagesJson = JsonSerializer.Serialize(_updatePackages[appId], _jsonOptions);
                        File.WriteAllText(packagesFilePath, packagesJson);
                    }
                }

                Console.WriteLine("数据保存完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 注册应用
        /// </summary>
        /// <param name="appInfo">应用信息</param>
        public void RegisterApp(AppInfo appInfo)
        {
            if (appInfo == null)
                throw new ArgumentNullException(nameof(appInfo));

            if (string.IsNullOrWhiteSpace(appInfo.AppId))
                throw new ArgumentException("应用ID不能为空", nameof(appInfo.AppId));

            // 保存应用信息
            _appInfos[appInfo.AppId] = appInfo;

            // 创建应用版本列表
            if (!_appVersions.ContainsKey(appInfo.AppId))
                _appVersions[appInfo.AppId] = new List<VersionInfo>();

            // 创建应用更新包列表
            if (!_updatePackages.ContainsKey(appInfo.AppId))
                _updatePackages[appInfo.AppId] = new List<UpdatePackageInfo>();

            // 创建应用目录
            string appDirectory = Path.Combine(_upgradeRootPath, appInfo.AppId);
            if (!Directory.Exists(appDirectory))
                Directory.CreateDirectory(appDirectory);

            // 保存数据
            SaveData();
        }

        /// <summary>
        /// 发布新版本
        /// </summary>
        /// <param name="versionInfo">版本信息</param>
        /// <param name="fullPackagePath">完整包路径</param>
        public void PublishVersion(VersionInfo versionInfo, string fullPackagePath)
        {
            if (versionInfo == null)
                throw new ArgumentNullException(nameof(versionInfo));

            if (string.IsNullOrWhiteSpace(versionInfo.AppId))
                throw new ArgumentException("应用ID不能为空", nameof(versionInfo.AppId));

            if (!_appInfos.ContainsKey(versionInfo.AppId))
                throw new ArgumentException($"应用 {versionInfo.AppId} 未注册");

            if (string.IsNullOrWhiteSpace(fullPackagePath) || !File.Exists(fullPackagePath))
                throw new ArgumentException("完整包路径无效", nameof(fullPackagePath));

            // 获取应用版本列表
            var versions = _appVersions[versionInfo.AppId];

            // 生成版本ID
            if (string.IsNullOrWhiteSpace(versionInfo.VersionId))
                versionInfo.VersionId = Guid.NewGuid().ToString();

            // 设置所有现有版本为非最新
            foreach (var version in versions)
            {
                version.IsLatest = false;
            }

            // 设置新版本为最新
            versionInfo.IsLatest = true;

            // 添加新版本
            versions.Add(versionInfo);

            // 创建版本目录
            string versionDirectory = Path.Combine(_upgradeRootPath, versionInfo.AppId, versionInfo.VersionId);
            if (!Directory.Exists(versionDirectory))
                Directory.CreateDirectory(versionDirectory);

            // 复制完整包到版本目录
            string destFullPackagePath = Path.Combine(versionDirectory, Path.GetFileName(fullPackagePath));
            File.Copy(fullPackagePath, destFullPackagePath, true);

            // 创建完整包更新信息
            var fullPackageInfo = new UpdatePackageInfo
            {
                PackageId = Guid.NewGuid().ToString(),
                TargetVersionId = versionInfo.VersionId,
                PackageType = UpdatePackageType.Full,
                PackagePath = destFullPackagePath,
                PackageSize = new FileInfo(destFullPackagePath).Length,
                // 计算校验和
                Checksum = UpgradeServerHelper.CalculateChecksum(destFullPackagePath)
            };

            // 添加完整包更新信息
            _updatePackages[versionInfo.AppId].Add(fullPackageInfo);

            // 保存数据
            SaveData();

            Console.WriteLine($"已发布应用 {versionInfo.AppId} 的 {versionInfo.Version} 版本");
        }

        /// <summary>
        /// 发布增量补丁
        /// </summary>
        /// <param name="sourceVersionId">源版本ID</param>
        /// <param name="targetVersionId">目标版本ID</param>
        /// <param name="incrementalPackagePath">增量包路径</param>
        public void PublishIncrementalPackage(string appId, string sourceVersionId, string targetVersionId, string incrementalPackagePath)
        {
            if (string.IsNullOrWhiteSpace(appId))
                throw new ArgumentException("应用ID不能为空", nameof(appId));

            if (!_appInfos.ContainsKey(appId))
                throw new ArgumentException($"应用 {appId} 未注册");

            if (string.IsNullOrWhiteSpace(sourceVersionId))
                throw new ArgumentException("源版本ID不能为空", nameof(sourceVersionId));

            if (string.IsNullOrWhiteSpace(targetVersionId))
                throw new ArgumentException("目标版本ID不能为空", nameof(targetVersionId));

            if (string.IsNullOrWhiteSpace(incrementalPackagePath) || !File.Exists(incrementalPackagePath))
                throw new ArgumentException("增量包路径无效", nameof(incrementalPackagePath));

            // 检查源版本和目标版本是否存在
            var versions = _appVersions[appId];
            if (!versions.Any(v => v.VersionId == sourceVersionId))
                throw new ArgumentException($"源版本 {sourceVersionId} 不存在");

            if (!versions.Any(v => v.VersionId == targetVersionId))
                throw new ArgumentException($"目标版本 {targetVersionId} 不存在");

            // 创建增量包目录
            string incrementalDirectory = Path.Combine(_upgradeRootPath, appId, $"{sourceVersionId}_{targetVersionId}");
            if (!Directory.Exists(incrementalDirectory))
                Directory.CreateDirectory(incrementalDirectory);

            // 复制增量包到版本目录
            string destIncrementalPackagePath = Path.Combine(incrementalDirectory, Path.GetFileName(incrementalPackagePath));
            File.Copy(incrementalPackagePath, destIncrementalPackagePath, true);

            // 创建增量包更新信息
            var incrementalPackageInfo = new UpdatePackageInfo
            {
                PackageId = Guid.NewGuid().ToString(),
                SourceVersionId = sourceVersionId,
                TargetVersionId = targetVersionId,
                PackageType = UpdatePackageType.Incremental,
                PackagePath = destIncrementalPackagePath,
                PackageSize = new FileInfo(destIncrementalPackagePath).Length,
                // 计算校验和
                Checksum = UpgradeServerHelper.CalculateChecksum(destIncrementalPackagePath)
            };

            // 添加增量包更新信息
            _updatePackages[appId].Add(incrementalPackageInfo);

            // 保存数据
            SaveData();

            Console.WriteLine($"已发布应用 {appId} 的从 {sourceVersionId} 到 {targetVersionId} 的增量补丁包");
        }

        /// <summary>
        /// 检查更新
        /// </summary>
        /// <param name="request">更新请求</param>
        /// <returns>更新响应</returns>
        public UpdateResponse CheckUpdate(UpdateRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrWhiteSpace(request.AppId))
                throw new ArgumentException("应用ID不能为空", nameof(request.AppId));

            if (!_appInfos.ContainsKey(request.AppId))
                throw new ArgumentException($"应用 {request.AppId} 未注册");

            // 获取应用版本列表
            var versions = _appVersions[request.AppId];

            // 获取当前版本信息
            var currentVersion = versions.FirstOrDefault(v => v.Version.Equals(request.CurrentVersion));

            // 获取最新版本信息
            var latestVersion = versions.FirstOrDefault(v => v.IsLatest);

            if (latestVersion == null)
            {
                return new UpdateResponse { HasUpdate = false };
            }

            // 如果当前版本就是最新版本，则不需要更新
            if (latestVersion.Version.Equals(request.CurrentVersion))
            {
                return new UpdateResponse { HasUpdate = false };
            }

            // 构建更新响应
            var response = new UpdateResponse
            {
                HasUpdate = true,
                LatestVersion = latestVersion
            };

            // 获取应用更新包列表
            var packages = _updatePackages[request.AppId];

            // 计算版本差异
            int versionGap = 0;
            if (currentVersion != null)
            {
                // 根据语义版本计算差异
                versionGap = Math.Abs(
                    latestVersion.Version.Major * 10000 +
                    latestVersion.Version.Minor * 100 +
                    latestVersion.Version.Build -
                    (currentVersion.Version.Major * 10000 +
                     currentVersion.Version.Minor * 100 +
                     currentVersion.Version.Build)
                );
            }

            // 如果当前版本存在且版本差异为1，则尝试使用增量更新
            if (currentVersion != null && versionGap <= 100)
            {
                // 查找从当前版本到最新版本的增量包
                var incrementalPackage = packages.FirstOrDefault(p =>
                    p.PackageType == UpdatePackageType.Incremental &&
                    p.SourceVersionId == currentVersion.VersionId &&
                    p.TargetVersionId == latestVersion.VersionId);

                if (incrementalPackage != null)
                {
                    response.SuggestedPackage = incrementalPackage;
                    response.AvailablePackages.Add(incrementalPackage);
                }
            }

            // 如果没有找到增量包，或者版本差异较大，则使用完整更新
            if (response.SuggestedPackage == null)
            {
                // 查找最新版本的完整包
                var fullPackage = packages.FirstOrDefault(p =>
                    p.PackageType == UpdatePackageType.Full &&
                    p.TargetVersionId == latestVersion.VersionId);

                if (fullPackage != null)
                {
                    response.SuggestedPackage = fullPackage;
                    response.AvailablePackages.Add(fullPackage);
                }
            }

            // 添加所有可用的更新包（为了让客户端有更多选择）
            foreach (var package in packages)
            {
                if (package.TargetVersionId == latestVersion.VersionId &&
                    !response.AvailablePackages.Contains(package))
                {
                    response.AvailablePackages.Add(package);
                }
            }

            return response;
        }

        /// <summary>
        /// 获取所有应用
        /// </summary>
        /// <returns>应用列表</returns>
        public List<AppInfo> GetAllApps()
        {
            return _appInfos.Values.ToList();
        }

        /// <summary>
        /// 获取应用版本列表
        /// </summary>
        /// <param name="appId">应用ID</param>
        /// <returns>版本列表</returns>
        public List<VersionInfo> GetAppVersions(string appId)
        {
            if (string.IsNullOrWhiteSpace(appId))
                throw new ArgumentException("应用ID不能为空", nameof(appId));

            if (!_appInfos.ContainsKey(appId))
                throw new ArgumentException($"应用 {appId} 未注册");

            return _appVersions[appId].ToList();
        }
    }
}