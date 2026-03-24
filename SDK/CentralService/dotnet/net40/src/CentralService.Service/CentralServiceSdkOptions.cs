using System;

namespace CentralService.Service
{
    /// <summary>
    /// 定义 <see cref="CentralServiceServiceClient"/> 的构造选项。
    /// </summary>
    /// <remarks>
    /// 该配置仅覆盖服务注册侧 SDK 的基础地址、请求超时与证书校验行为，
    /// 不负责缓存服务 ID、重试策略或其他运行时状态。
    /// </remarks>
    public sealed class CentralServiceSdkOptions
    {
        /// <summary>
        /// 使用中心服务根地址创建 SDK 配置。
        /// </summary>
        /// <param name="baseUrl">中心服务的 HTTP 根地址，例如 <c>http://127.0.0.1:5000</c>。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="baseUrl"/> 为空或仅包含空白字符时抛出。</exception>
        public CentralServiceSdkOptions(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl)) throw new ArgumentNullException("baseUrl");
            BaseUrl = baseUrl.Trim().TrimEnd('/');
            Timeout = TimeSpan.FromSeconds(5);
            IgnoreSslErrors = false;
        }

        /// <summary>
        /// 获取规范化后的中心服务根地址。
        /// </summary>
        /// <remarks>
        /// 构造函数会移除首尾空白和末尾斜杠，便于后续直接拼接 SDK 约定路径。
        /// </remarks>
        public string BaseUrl { get; private set; }

        /// <summary>
        /// 获取或设置单次请求超时时间。
        /// </summary>
        /// <value>默认值为 5 秒。</value>
        public TimeSpan Timeout { get; set; }

        /// <summary>
        /// 获取或设置是否忽略 HTTPS 证书错误。
        /// </summary>
        /// <remarks>
        /// 该开关通常仅适用于本地调试或受控测试环境。
        /// </remarks>
        public bool IgnoreSslErrors { get; set; }
    }
}
