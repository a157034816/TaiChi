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
        /// <param name="progressCallback">进度回调函数</param>
        /// <returns>下载的文件路径</returns>
        public async Task<string> DownloadPackageAsync(UpdatePackageInfo packageInfo, Action<long, long> progressCallback = null)
        {
            if (packageInfo == null)
                throw new ArgumentNullException(nameof(packageInfo));

            try
            {
                // 确定下载文件路径
                string packageFileName = $"{packageInfo.PackageId}_{Path.GetFileName(packageInfo.PackagePath)}";
                string downloadFilePath = Path.Combine(_downloadDirectory, packageFileName);
                
                // 检查文件是否已经存在，如果存在则确定已下载的大小
                long existingFileSize = 0;
                FileMode fileMode = FileMode.Create;
                
                if (File.Exists(downloadFilePath))
                {
                    var fileInfo = new FileInfo(downloadFilePath);
                    existingFileSize = fileInfo.Length;
                    fileMode = FileMode.OpenOrCreate;
                    Console.WriteLine($"发现已存在的文件，大小: {existingFileSize} 字节");
                }

                Console.WriteLine($"开始下载更新包: {packageInfo.PackagePath}，断点续传位置: {existingFileSize} 字节");

                // TODO: 实际项目中应该使用HTTP请求下载文件
                // 这里示例使用直接调用的方式，增加了断点续传支持
                
                // 构建断点续传请求
                var request = new UpdateRequest
                {
                    AppId = _appId,
                    CurrentVersion = _currentVersion,
                    FileOffset = existingFileSize,
                    PackageId = packageInfo.PackageId
                };
                
                /* 实际HTTP实现示例:
                // 添加断点续传头
                using var httpRequest = new HttpRequestMessage(HttpMethod.Get, 
                    $"api/upgrade/downloadPackage/{packageInfo.PackageId}");
                
                if (existingFileSize > 0)
                {
                    httpRequest.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existingFileSize, null);
                }
                
                using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);
                
                // 检查是否支持断点续传
                bool supportsResume = response.StatusCode == System.Net.HttpStatusCode.PartialContent;
                
                // 获取文件总大小
                long totalSize = packageInfo.PackageSize;
                if (response.Content.Headers.ContentRange != null)
                {
                    totalSize = response.Content.Headers.ContentRange.Length ?? packageInfo.PackageSize;
                }
                
                response.EnsureSuccessStatusCode();
                
                // 打开文件流，如果是断点续传则追加写入
                using var fileStream = new FileStream(
                    downloadFilePath, 
                    fileMode, 
                    FileAccess.Write, 
                    FileShare.None, 
                    4096, 
                    true);
                
                if (existingFileSize > 0 && supportsResume)
                {
                    fileStream.Seek(existingFileSize, SeekOrigin.Begin);
                }
                else
                {
                    // 如果服务器不支持断点续传，则从头开始下载
                    fileStream.SetLength(0);
                }
                
                // 下载数据
                await using var contentStream = await response.Content.ReadAsStreamAsync();
                byte[] buffer = new byte[8192];
                long totalBytesRead = existingFileSize;
                int bytesRead;
                
                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalBytesRead += bytesRead;
                    progressCallback?.Invoke(totalBytesRead, totalSize);
                }
                */

                // 校验文件完整性
                /* 实际实现:
                string checksum = UpgradeClientHelper.CalculateChecksum(downloadFilePath);
                if (!string.Equals(checksum, packageInfo.Checksum, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"文件校验失败，校验和不匹配。期望: {packageInfo.Checksum}, 实际: {checksum}");
                }
                */

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
        /// <param name="progressCallback">进度回调函数</param>
        /// <returns>是否成功启动更新流程</returns>
        public async Task<bool> DownloadAndExecuteSelfUpdateAsync(UpdatePackageInfo packageInfo, Action<long, long> progressCallback = null)
        {
            if (packageInfo == null)
                throw new ArgumentNullException(nameof(packageInfo));

            try
            {
                // 下载更新包
                string packagePath = await DownloadPackageAsync(packageInfo, progressCallback);

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