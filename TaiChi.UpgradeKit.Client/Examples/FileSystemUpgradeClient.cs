using System.Text.Json;
using TaiChi.Upgrade.Shared;

namespace TaiChi.UpgradeKit.Client.Examples
{
    /// <summary>
    /// 基于文件系统的客户端升级控制器
    /// </summary>
    public class FileSystemUpgradeClient : UpgradeClient
    {
        private readonly string _upgradeConfigPath;
        private readonly string _upgradePackagesPath;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="appId">应用ID</param>
        /// <param name="appDirectory">应用目录</param>
        /// <param name="currentVersion">当前版本</param>
        /// <param name="upgradeServerPath">升级服务器路径（本地文件系统路径）</param>
        public FileSystemUpgradeClient(string appId, string appDirectory, Version currentVersion, string upgradeServerPath)
            : base(appId, appDirectory, currentVersion, upgradeServerPath)
        {
            // 初始化升级配置文件路径和升级包存储路径
            _upgradeConfigPath = Path.Combine(upgradeServerPath, "config");
            _upgradePackagesPath = Path.Combine(upgradeServerPath, "packages");
            
            // 确保目录存在
            Directory.CreateDirectory(_upgradeConfigPath);
            Directory.CreateDirectory(_upgradePackagesPath);
        }

        /// <summary>
        /// 重写发送检查更新请求的方法
        /// </summary>
        /// <param name="request">更新请求</param>
        /// <returns>更新响应</returns>
        protected override async Task<UpdateResponse> SendCheckUpdateRequestAsync(UpdateRequest request)
        {
            try
            {
                // 从文件系统获取升级配置
                string configFilePath = Path.Combine(_upgradeConfigPath, $"{request.AppId}.json");
                
                if (!File.Exists(configFilePath))
                {
                    Console.WriteLine($"找不到应用 {request.AppId} 的更新配置");
                    return new UpdateResponse { HasUpdate = false };
                }

                // 读取并解析更新配置文件
                string json = await File.ReadAllTextAsync(configFilePath);
                var config = JsonSerializer.Deserialize<AppUpgradeConfig>(json);

                if (config == null)
                {
                    Console.WriteLine("更新配置解析失败");
                    return new UpdateResponse { HasUpdate = false };
                }

                // 检查是否有可用的更新
                if (config.LatestVersion == null || config.LatestVersion.Version <= request.CurrentVersion)
                {
                    return new UpdateResponse { HasUpdate = false };
                }

                // 构建更新响应
                var response = new UpdateResponse
                {
                    HasUpdate = true,
                    LatestVersion = config.LatestVersion,
                    SuggestedPackage = null,
                    AvailablePackages = new System.Collections.Generic.List<UpdatePackageInfo>()
                };

                // 获取建议的更新包
                if (config.Packages != null && config.Packages.Count > 0)
                {
                    // 先尝试查找增量更新包
                    var incrementalPackage = config.Packages.Find(p => 
                        p.PackageType == UpdatePackageType.Incremental && 
                        new Version(p.SourceVersionId) == request.CurrentVersion && 
                        new Version(p.TargetVersionId) == config.LatestVersion.Version);

                    if (incrementalPackage != null)
                    {
                        response.SuggestedPackage = incrementalPackage;
                        response.AvailablePackages.Add(incrementalPackage);
                    }
                    else
                    {
                        // 如果没有合适的增量包，使用完整更新包
                        var fullPackage = config.Packages.Find(p => 
                            p.PackageType == UpdatePackageType.Full && 
                            new Version(p.TargetVersionId) == config.LatestVersion.Version);

                        if (fullPackage != null)
                        {
                            response.SuggestedPackage = fullPackage;
                            response.AvailablePackages.Add(fullPackage);
                        }
                    }

                    // 添加所有可用的更新包
                    foreach (var package in config.Packages)
                    {
                        if (!response.AvailablePackages.Contains(package))
                        {
                            response.AvailablePackages.Add(package);
                        }
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"文件系统检查更新失败: {ex.Message}");
                return new UpdateResponse { HasUpdate = false };
            }
        }

        /// <summary>
        /// 重写下载更新包文件的方法
        /// </summary>
        /// <param name="request">更新请求</param>
        /// <param name="packageInfo">更新包信息</param>
        /// <param name="downloadFilePath">下载文件保存路径</param>
        /// <param name="existingFileSize">已存在文件的大小</param>
        /// <param name="progressCallback">进度回调函数</param>
        /// <returns>异步任务</returns>
        protected override async Task DownloadPackageFileAsync(
            UpdateRequest request,
            UpdatePackageInfo packageInfo,
            string downloadFilePath,
            long existingFileSize,
            Action<long, long> progressCallback)
        {
            try
            {
                // 组合包的完整路径
                string sourcePath = Path.Combine(_upgradePackagesPath, Path.GetFileName(packageInfo.PackagePath));
                
                if (!File.Exists(sourcePath))
                {
                    throw new FileNotFoundException($"找不到更新包文件: {sourcePath}");
                }

                // 获取文件信息
                var fileInfo = new FileInfo(sourcePath);
                long totalSize = fileInfo.Length;
                
                // 如果是全新下载，或文件大小不匹配，直接复制整个文件
                if (existingFileSize == 0 || existingFileSize >= totalSize)
                {
                    await Task.Run(() => 
                    {
                        // 直接复制文件
                        File.Copy(sourcePath, downloadFilePath, true);
                        
                        // 报告进度
                        progressCallback?.Invoke(totalSize, totalSize);
                    });
                }
                else
                {
                    // 实现续传 - 从指定位置开始读取文件并追加到目标文件
                    await Task.Run(() =>
                    {
                        using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read);
                        using var targetStream = new FileStream(downloadFilePath, FileMode.Append, FileAccess.Write);
                        
                        // 移动到已下载的位置
                        sourceStream.Seek(existingFileSize, SeekOrigin.Begin);
                        
                        // 复制剩余部分
                        byte[] buffer = new byte[81920]; // 使用较大的缓冲区提高性能
                        int bytesRead;
                        long totalBytesRead = existingFileSize;
                        
                        while ((bytesRead = sourceStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            targetStream.Write(buffer, 0, bytesRead);
                            totalBytesRead += bytesRead;
                            progressCallback?.Invoke(totalBytesRead, totalSize);
                        }
                    });
                }
                
                // 校验文件完整性
                string checksum = UpgradeClientHelper.CalculateChecksum(downloadFilePath);
                if (!string.Equals(checksum, packageInfo.Checksum, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"文件校验失败，校验和不匹配。期望: {packageInfo.Checksum}, 实际: {checksum}");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"从文件系统下载更新包失败: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// 应用升级配置
    /// </summary>
    internal class AppUpgradeConfig
    {
        /// <summary>
        /// 最新版本信息
        /// </summary>
        public VersionInfo LatestVersion { get; set; }

        /// <summary>
        /// 可用的更新包列表
        /// </summary>
        public System.Collections.Generic.List<UpdatePackageInfo> Packages { get; set; } = new System.Collections.Generic.List<UpdatePackageInfo>();
    }
} 