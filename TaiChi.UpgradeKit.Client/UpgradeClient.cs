using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using TaiChi.Upgrade.Shared;

namespace TaiChi.UpgradeKit.Client
{
    /// <summary>
    /// 客户端升级控制器
    /// </summary>
    public class UpgradeClient
    {
        private readonly string _appId;
        private readonly string _appDirectory;
        private readonly string _upgradeServerUrl;
        private readonly HttpClient _httpClient;
        private readonly Version _currentVersion;
        private readonly string _downloadDirectory;
        private readonly string _backupDirectory;

        /// <summary>
        /// 应用可执行文件路径
        /// </summary>
        public string ExecutablePath { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="appId">应用ID</param>
        /// <param name="appDirectory">应用目录</param>
        /// <param name="currentVersion">当前版本</param>
        /// <param name="upgradeServerUrl">升级服务器URL</param>
        public UpgradeClient(string appId, string appDirectory, Version currentVersion, string upgradeServerUrl)
        {
            _appId = appId ?? throw new ArgumentNullException(nameof(appId));
            _appDirectory = appDirectory ?? throw new ArgumentNullException(nameof(appDirectory));
            _currentVersion = currentVersion ?? throw new ArgumentNullException(nameof(currentVersion));
            _upgradeServerUrl = upgradeServerUrl ?? throw new ArgumentNullException(nameof(upgradeServerUrl));

            // 创建HttpClient
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_upgradeServerUrl)
            };

            // 初始化下载和备份目录
            _downloadDirectory = Path.Combine(_appDirectory, "Downloads");
            _backupDirectory = Path.Combine(_appDirectory, "Backups");

            // 确保目录存在
            Directory.CreateDirectory(_downloadDirectory);
            Directory.CreateDirectory(_backupDirectory);
        }

        /// <summary>
        /// 启动升级客户端
        /// </summary>
        public void Start()
        {
            Console.WriteLine($"UpgradeClient 已启动，应用ID: {_appId}，当前版本: {_currentVersion}");
        }

        /// <summary>
        /// 检查更新
        /// </summary>
        /// <returns>更新响应</returns>
        public async Task<UpdateResponse> CheckUpdateAsync()
        {
            try
            {
                var request = new UpdateRequest
                {
                    AppId = _appId,
                    CurrentVersion = _currentVersion
                };

                Console.WriteLine($"检查应用 {_appId} 的更新，当前版本: {_currentVersion}");

                // TODO: 实际项目中应该使用HTTP请求调用服务端API
                // 这里示例使用直接调用的方式

                // 模拟HTTP请求
                // HttpResponseMessage response = await _httpClient.PostAsJsonAsync("api/upgrade/checkUpdate", request);
                // response.EnsureSuccessStatusCode();
                // UpdateResponse updateResponse = await response.Content.ReadFromJsonAsync<UpdateResponse>();

                // 临时返回空响应
                return new UpdateResponse { HasUpdate = false };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"检查更新失败: {ex.Message}");
                return new UpdateResponse { HasUpdate = false };
            }
        }

        /// <summary>
        /// 下载更新包
        /// </summary>
        /// <param name="packageInfo">更新包信息</param>
        /// <returns>下载的文件路径</returns>
        public async Task<string> DownloadPackageAsync(UpdatePackageInfo packageInfo)
        {
            if (packageInfo == null)
                throw new ArgumentNullException(nameof(packageInfo));

            try
            {
                // 确定下载文件路径
                string packageFileName = $"{packageInfo.PackageId}_{Path.GetFileName(packageInfo.PackagePath)}";
                string downloadFilePath = Path.Combine(_downloadDirectory, packageFileName);

                Console.WriteLine($"开始下载更新包: {packageInfo.PackagePath}");

                // TODO: 实际项目中应该使用HTTP请求下载文件
                // 这里示例使用直接调用的方式

                // 模拟HTTP下载
                // Uri fileUri = new Uri(new Uri(_upgradeServerUrl), $"api/upgrade/downloadPackage/{packageInfo.PackageId}");
                // using var response = await _httpClient.GetAsync(fileUri, HttpCompletionOption.ResponseHeadersRead);
                // response.EnsureSuccessStatusCode();
                // 
                // using var fileStream = new FileStream(downloadFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
                // await using var contentStream = await response.Content.ReadAsStreamAsync();
                // await contentStream.CopyToAsync(fileStream);

                // 校验文件完整性
                // string checksum = UpgradeClientHelper.CalculateChecksum(downloadFilePath);
                // if (!string.Equals(checksum, packageInfo.Checksum, StringComparison.OrdinalIgnoreCase))
                // {
                //     throw new InvalidOperationException($"文件校验失败，校验和不匹配。期望: {packageInfo.Checksum}, 实际: {checksum}");
                // }

                Console.WriteLine($"更新包下载完成: {downloadFilePath}");
                return downloadFilePath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"下载更新包失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 应用更新
        /// </summary>
        /// <param name="packageInfo">更新包信息</param>
        /// <param name="packagePath">更新包路径</param>
        /// <returns>是否更新成功</returns>
        public async Task<bool> ApplyUpdateAsync(UpdatePackageInfo packageInfo, string packagePath)
        {
            if (packageInfo == null)
                throw new ArgumentNullException(nameof(packageInfo));

            if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
                throw new ArgumentException("更新包路径无效", nameof(packagePath));

            try
            {
                Console.WriteLine($"开始应用更新，类型: {packageInfo.PackageType}");

                // 创建备份目录
                string backupDirName = $"Backup_{DateTime.Now:yyyyMMdd_HHmmss}";
                string backupDir = Path.Combine(_backupDirectory, backupDirName);
                Directory.CreateDirectory(backupDir);

                // 备份当前应用
                await Task.Run(() => UpgradeClientHelper.BackupApplication(_appDirectory, backupDir));

                // 根据包类型应用更新
                if (packageInfo.PackageType == UpdatePackageType.Full)
                {
                    // 应用完整更新
                    await Task.Run(() => UpgradeClientHelper.ApplyFullUpdate(packagePath, _appDirectory));
                }
                else if (packageInfo.PackageType == UpdatePackageType.Incremental)
                {
                    // 应用增量更新
                    await Task.Run(() => UpgradeClientHelper.ApplyIncrementalUpdate(packagePath, _appDirectory));
                }
                else
                {
                    throw new InvalidOperationException($"不支持的更新包类型: {packageInfo.PackageType}");
                }

                Console.WriteLine("更新应用成功");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"应用更新失败: {ex.Message}");

                // TODO: 实现回滚机制

                return false;
            }
        }

        /// <summary>
        /// 执行自我更新
        /// </summary>
        /// <param name="packageInfo">更新包信息</param>
        /// <param name="packagePath">更新包路径</param>
        /// <returns>是否成功启动更新流程</returns>
        public async Task<bool> ExecuteSelfUpdateAsync(UpdatePackageInfo packageInfo, string packagePath)
        {
            if (packageInfo == null)
                throw new ArgumentNullException(nameof(packageInfo));

            if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
                throw new ArgumentException("更新包路径无效", nameof(packagePath));

            if (string.IsNullOrWhiteSpace(ExecutablePath))
                throw new InvalidOperationException("未设置应用程序可执行文件路径。请在调用此方法前设置 ExecutablePath 属性。");

            if (!File.Exists(ExecutablePath))
                throw new FileNotFoundException("可执行文件不存在", ExecutablePath);

            try
            {
                Console.WriteLine($"开始执行自我更新，类型: {packageInfo.PackageType}");

                // 检查是否为增量更新
                bool isIncremental = packageInfo.PackageType == UpdatePackageType.Incremental;

                // 启动自我更新流程
                bool result = UpgradeClientHelper.StartSelfUpdate(
                    packagePath,
                    _appDirectory,
                    ExecutablePath,
                    isIncremental
                );

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"执行自我更新失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 下载并执行自我更新
        /// </summary>
        /// <param name="packageInfo">更新包信息</param>
        /// <returns>是否成功启动更新流程</returns>
        public async Task<bool> DownloadAndExecuteSelfUpdateAsync(UpdatePackageInfo packageInfo)
        {
            if (packageInfo == null)
                throw new ArgumentNullException(nameof(packageInfo));

            try
            {
                // 下载更新包
                string packagePath = await DownloadPackageAsync(packageInfo);

                // 执行自我更新
                return await ExecuteSelfUpdateAsync(packageInfo, packagePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"下载并执行自我更新失败: {ex.Message}");
                return false;
            }
        }
    }
}