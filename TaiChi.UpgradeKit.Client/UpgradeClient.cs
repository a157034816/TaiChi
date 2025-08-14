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
        protected readonly string _appId;
        protected readonly string _appDirectory;
        protected readonly string _upgradeServerUrl;
        protected readonly HttpClient _httpClient;
        protected readonly Version _currentVersion;
        protected readonly string _downloadDirectory;
        protected readonly string _backupDirectory;

        /// <summary>
        /// 是否在更新前备份应用
        /// </summary>
        public bool EnableBackup { get; set; } = true;

        /// <summary>
        /// 是否保留下载的更新包
        /// </summary>
        public bool KeepDownloadedPackage { get; set; } = true;

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
        public virtual void Start()
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

                return await SendCheckUpdateRequestAsync(request);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"检查更新失败: {ex.Message}");
                return new UpdateResponse { HasUpdate = false };
            }
        }

        /// <summary>
        /// 发送检查更新请求
        /// </summary>
        /// <param name="request">更新请求</param>
        /// <returns>更新响应</returns>
        protected virtual async Task<UpdateResponse> SendCheckUpdateRequestAsync(UpdateRequest request)
        {
            // 默认实现，子类可以重写以提供自定义的API调用
            // 示例实现（实际项目中应根据需求修改）
            // HttpResponseMessage response = await _httpClient.PostAsJsonAsync("api/upgrade/checkUpdate", request);
            // response.EnsureSuccessStatusCode();
            // UpdateResponse updateResponse = await response.Content.ReadFromJsonAsync<UpdateResponse>();
            
            // 临时返回空响应，子类需要重写此方法提供实际实现
            throw new NotImplementedException("警告：使用了默认的SendCheckUpdateRequestAsync实现，应在子类中重写此方法提供实际实现");
            // return new UpdateResponse { HasUpdate = false };
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

                // 构建断点续传请求
                var request = new UpdateRequest
                {
                    AppId = _appId,
                    CurrentVersion = _currentVersion,
                    FileOffset = existingFileSize,
                    PackageId = packageInfo.PackageId
                };
                
                // 调用实际的下载方法，子类可重写
                await DownloadPackageFileAsync(request, packageInfo, downloadFilePath, existingFileSize, progressCallback);
                
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
        /// 下载更新包文件的具体实现
        /// </summary>
        /// <param name="request">更新请求</param>
        /// <param name="packageInfo">更新包信息</param>
        /// <param name="downloadFilePath">下载文件保存路径</param>
        /// <param name="existingFileSize">已存在文件的大小</param>
        /// <param name="progressCallback">进度回调函数</param>
        /// <returns>异步任务</returns>
        protected virtual async Task DownloadPackageFileAsync(
            UpdateRequest request, 
            UpdatePackageInfo packageInfo, 
            string downloadFilePath, 
            long existingFileSize,
            Action<long, long> progressCallback)
        {
            // 默认实现，子类可以重写以提供自定义的下载逻辑
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
                existingFileSize > 0 && supportsResume ? FileMode.Append : FileMode.Create, 
                FileAccess.Write, 
                FileShare.None, 
                4096, 
                true);
            
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
            
            throw new NotImplementedException("警告：使用了默认的DownloadPackageFileAsync实现，应在子类中重写此方法提供实际实现");
            
            // 校验文件完整性
            /* 实际实现:
            string checksum = UpgradeClientHelper.CalculateChecksum(downloadFilePath);
            if (!string.Equals(checksum, packageInfo.Checksum, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"文件校验失败，校验和不匹配。期望: {packageInfo.Checksum}, 实际: {checksum}");
            }
            */
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

                // 在更新前检查应用目录的权限
                var (hasPermission, errorMessage) = UpgradeClientHelper.CheckDirectoryPermission(_appDirectory);
                if (!hasPermission)
                {
                    Console.WriteLine($"更新失败: {errorMessage}");
                    Console.WriteLine("请确保应用程序有足够的权限访问应用目录，或尝试以管理员身份运行。");
                    return false;
                }

                // 检查是否有被锁定的文件
                var inaccessibleFiles = UpgradeClientHelper.GetInaccessibleFiles(_appDirectory);
                if (inaccessibleFiles.Count > 0)
                {
                    Console.WriteLine($"更新失败: 发现{inaccessibleFiles.Count}个正在使用的文件，无法更新。");
                    Console.WriteLine("请关闭所有可能使用这些文件的程序后重试。");
                    
                    // 如果锁定的文件不多，列出它们
                    if (inaccessibleFiles.Count <= 5)
                    {
                        Console.WriteLine("被锁定的文件:");
                        foreach (var file in inaccessibleFiles)
                        {
                            Console.WriteLine($"- {file}");
                        }
                    }
                    
                    return false;
                }

                string backupDir = string.Empty;
                
                // 仅当启用备份时才执行备份操作
                if (EnableBackup)
                {
                    // 创建备份目录
                    string backupDirName = $"Backup_{DateTime.Now:yyyyMMdd_HHmmss}";
                    backupDir = Path.Combine(_backupDirectory, backupDirName);
                    
                    try
                    {
                        Directory.CreateDirectory(backupDir);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"创建备份目录失败: {ex.Message}");
                        Console.WriteLine("请确保应用程序有足够的权限创建备份目录，或清理备份目录腾出空间。");
                        return false;
                    }

                    // 备份当前应用
                    try
                    {
                        await Task.Run(() => UpgradeClientHelper.BackupApplication(_appDirectory, backupDir));
                        Console.WriteLine($"已成功备份应用到: {backupDir}");
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        Console.WriteLine($"备份应用失败，权限不足: {ex.Message}");
                        Console.WriteLine("请确保应用程序有足够的权限访问和写入备份目录，或尝试以管理员身份运行。");
                        return false;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"备份应用失败: {ex.Message}");
                        // 备份失败不一定要终止更新，但应告知用户风险
                        Console.WriteLine("警告: 备份失败但将继续更新。如果更新出现问题，可能无法恢复到之前版本。");
                    }
                }
                else
                {
                    Console.WriteLine("已禁用备份，将直接应用更新");
                }

                // 根据包类型应用更新
                try
                {
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

                    // 如果不保留下载包，则删除
                    if (!KeepDownloadedPackage && File.Exists(packagePath))
                    {
                        try
                        {
                            File.Delete(packagePath);
                            Console.WriteLine("已删除更新包");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"删除更新包失败: {ex.Message}");
                        }
                    }

                    Console.WriteLine("更新应用成功");
                    return true;
                }
                catch (UnauthorizedAccessException ex)
                {
                    Console.WriteLine($"应用更新失败，权限不足: {ex.Message}");
                    Console.WriteLine("请确保应用程序有足够的权限访问和修改应用目录，或尝试以管理员身份运行。");
                    
                    // 尝试从备份恢复
                    if (EnableBackup && !string.IsNullOrEmpty(backupDir))
                    {
                        Console.WriteLine("正在尝试从备份恢复...");
                        await TryRestoreFromBackupAsync(backupDir);
                    }
                    
                    return false;
                }
                catch (IOException ex)
                {
                    Console.WriteLine($"应用更新失败，IO错误: {ex.Message}");
                    Console.WriteLine("可能是因为某些文件被占用或磁盘空间不足。请关闭可能使用这些文件的程序后重试。");
                    
                    // 尝试从备份恢复
                    if (EnableBackup && !string.IsNullOrEmpty(backupDir))
                    {
                        Console.WriteLine("正在尝试从备份恢复...");
                        await TryRestoreFromBackupAsync(backupDir);
                    }
                    
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"应用更新失败: {ex.Message}");

                // 如果有更详细的嵌套异常，显示它
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"详细错误: {ex.InnerException.Message}");
                }
                
                return false;
            }
        }
        
        /// <summary>
        /// 尝试从备份恢复
        /// </summary>
        /// <param name="backupDir">备份目录</param>
        /// <returns>是否恢复成功</returns>
        private async Task<bool> TryRestoreFromBackupAsync(string backupDir)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(backupDir) || !Directory.Exists(backupDir))
                {
                    Console.WriteLine("恢复失败: 备份目录不存在");
                    return false;
                }
                
                // 检查权限
                var (hasPermission, errorMessage) = UpgradeClientHelper.CheckDirectoryPermission(_appDirectory);
                if (!hasPermission)
                {
                    Console.WriteLine($"恢复失败: {errorMessage}");
                    return false;
                }
                
                await Task.Run(() => 
                {
                    // 排除的目录
                    var excludeDirs = new[] { "Backups", "Downloads" };
                    
                    // 清理目标目录（保留备份和下载目录）
                    foreach (var dirPath in Directory.GetDirectories(_appDirectory))
                    {
                        var dirName = new DirectoryInfo(dirPath).Name;
                        if (Array.IndexOf(excludeDirs, dirName) >= 0)
                            continue;
                            
                        try
                        {
                            Directory.Delete(dirPath, true);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"警告: 无法删除目录 {dirPath}: {ex.Message}");
                        }
                    }
                    
                    foreach (var filePath in Directory.GetFiles(_appDirectory))
                    {
                        try
                        {
                            File.Delete(filePath);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"警告: 无法删除文件 {filePath}: {ex.Message}");
                        }
                    }
                    
                    // 从备份恢复
                    foreach (var dirPath in Directory.GetDirectories(backupDir, "*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            Directory.CreateDirectory(dirPath.Replace(backupDir, _appDirectory));
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"警告: 无法创建目录 {dirPath}: {ex.Message}");
                        }
                    }
                    
                    foreach (var filePath in Directory.GetFiles(backupDir, "*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            File.Copy(filePath, filePath.Replace(backupDir, _appDirectory), true);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"警告: 无法复制文件 {filePath}: {ex.Message}");
                        }
                    }
                });
                
                Console.WriteLine("已从备份恢复应用");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"恢复失败: {ex.Message}");
                Console.WriteLine("请手动从备份目录恢复应用。");
                return false;
            }
        }

        /// <summary>
        /// 执行自我更新
        /// </summary>
        /// <param name="packageInfo">更新包信息</param>
        /// <param name="packagePath">更新包路径</param>
        /// <returns>是否成功启动更新流程</returns>
        public virtual async Task<bool> ExecuteSelfUpdateAsync(UpdatePackageInfo packageInfo, string packagePath)
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
                    isIncremental,
                    3,
                    EnableBackup,
                    KeepDownloadedPackage
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

        /// <summary>
        /// 获取应用ID
        /// </summary>
        protected string AppId => _appId;

        /// <summary>
        /// 获取应用目录
        /// </summary>
        protected string AppDirectory => _appDirectory;

        /// <summary>
        /// 获取升级服务器URL
        /// </summary>
        protected string UpgradeServerUrl => _upgradeServerUrl;

        /// <summary>
        /// 获取HttpClient实例
        /// </summary>
        protected HttpClient HttpClient => _httpClient;

        /// <summary>
        /// 获取当前版本
        /// </summary>
        protected Version CurrentVersion => _currentVersion;

        /// <summary>
        /// 获取下载目录
        /// </summary>
        protected string DownloadDirectory => _downloadDirectory;

        /// <summary>
        /// 获取备份目录
        /// </summary>
        protected string BackupDirectory => _backupDirectory;

        /// <summary>
        /// 设置更新选项
        /// </summary>
        /// <param name="enableBackup">是否在更新前备份应用</param>
        /// <param name="keepDownloadedPackage">是否保留下载的更新包</param>
        public void SetUpdateOptions(bool enableBackup, bool keepDownloadedPackage)
        {
            EnableBackup = enableBackup;
            KeepDownloadedPackage = keepDownloadedPackage;
        }
    }
}