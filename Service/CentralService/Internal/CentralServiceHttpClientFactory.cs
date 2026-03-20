using System.Net;

namespace CentralService.Internal;

/// <summary>
/// 为中心服务内部后台任务创建带统一策略的 <see cref="HttpClient"/>。
/// </summary>
internal static class CentralServiceHttpClientFactory
{
    /// <summary>
    /// 创建 HTTP 客户端。
    /// </summary>
    public static HttpClient Create(bool ignoreSslErrors = false, TimeSpan? timeout = null)
    {
        var handler = new HttpClientHandler();

        if (ignoreSslErrors)
        {
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls12 |
                SecurityProtocolType.Tls11 |
                SecurityProtocolType.Tls;
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
        }

        var client = new HttpClient(handler)
        {
            Timeout = timeout ?? TimeSpan.FromSeconds(3000)
        };

        return client;
    }
}
