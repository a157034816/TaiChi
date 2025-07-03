using System.Net.Http.Headers;
using System.Net.Http.Json;
using TaiChi.Upgrade.Shared;

namespace TaiChi.UpgradeKit.Client.Examples
{
    /// <summary>
    /// 基于WebAPI的客户端升级控制器
    /// </summary>
    public class WebApiUpgradeClient : UpgradeClient
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="appId">应用ID</param>
        /// <param name="appDirectory">应用目录</param>
        /// <param name="currentVersion">当前版本</param>
        /// <param name="upgradeServerUrl">升级服务器URL</param>
        public WebApiUpgradeClient(string appId, string appDirectory, Version currentVersion, string upgradeServerUrl)
            : base(appId, appDirectory, currentVersion, upgradeServerUrl)
        {
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
                // 实际的WebAPI调用实现
                HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/upgrade/checkUpdate", request);
                response.EnsureSuccessStatusCode();
                
                // 从响应中读取更新信息
                UpdateResponse updateResponse = await response.Content.ReadFromJsonAsync<UpdateResponse>();
                return updateResponse;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WebAPI检查更新失败: {ex.Message}");
                // 返回一个默认的响应表示没有更新
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
                // 添加断点续传头
                using var httpRequest = new HttpRequestMessage(HttpMethod.Get, 
                    $"api/upgrade/packages/{packageInfo.PackageId}/download");
                
                if (existingFileSize > 0)
                {
                    httpRequest.Headers.Range = new RangeHeaderValue(existingFileSize, null);
                }
                
                using var response = await HttpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);
                
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
                using var contentStream = await response.Content.ReadAsStreamAsync();
                byte[] buffer = new byte[8192];
                long totalBytesRead = existingFileSize;
                int bytesRead;
                
                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalBytesRead += bytesRead;
                    progressCallback?.Invoke(totalBytesRead, totalSize);
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
                throw new InvalidOperationException($"通过WebAPI下载更新包失败: {ex.Message}", ex);
            }
        }
    }
} 