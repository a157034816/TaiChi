using System;

namespace CentralService.Client
{
    /// <summary>
    /// 定义 Central Service 发现侧 SDK 的基础连接选项。
    /// </summary>
    public sealed class CentralServiceSdkOptions
    {
        /// <summary>
        /// 使用指定的服务根地址创建 SDK 配置。
        /// </summary>
        /// <param name="baseUrl">中心服务的根地址。</param>
        /// <exception cref="ArgumentNullException"><paramref name="baseUrl"/> 为空、空白或仅包含空白字符。</exception>
        public CentralServiceSdkOptions(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl)) throw new ArgumentNullException("baseUrl");
            BaseUrl = baseUrl.Trim().TrimEnd('/');
            Timeout = TimeSpan.FromSeconds(5);
            IgnoreSslErrors = false;
        }

        /// <summary>
        /// 获取中心服务的根地址。
        /// </summary>
        public string BaseUrl { get; private set; }

        /// <summary>
        /// 获取或设置单次 HTTP 请求的超时时间。
        /// </summary>
        public TimeSpan Timeout { get; set; }

        /// <summary>
        /// 获取或设置是否忽略服务器证书错误。
        /// </summary>
        public bool IgnoreSslErrors { get; set; }
    }
}
