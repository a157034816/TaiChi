using System.Net;

namespace CentralService.Service.Internal
{
    /// <summary>
    /// 表示底层 HTTP 传输层返回的原始响应。
    /// </summary>
    internal sealed class CentralServiceHttpResponse
    {
        /// <summary>
        /// 使用 HTTP 状态码与原始响应体创建响应对象。
        /// </summary>
        /// <param name="statusCode">服务端返回的 HTTP 状态码。</param>
        /// <param name="body">原始响应体文本。</param>
        public CentralServiceHttpResponse(HttpStatusCode statusCode, string body)
        {
            StatusCode = statusCode;
            Body = body ?? string.Empty;
        }

        /// <summary>
        /// 获取服务端返回的 HTTP 状态码。
        /// </summary>
        public HttpStatusCode StatusCode { get; private set; }

        /// <summary>
        /// 获取服务端返回的原始响应体。
        /// </summary>
        public string Body { get; private set; }
    }
}
