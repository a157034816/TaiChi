using System.Net;

namespace CentralService.Client.Internal
{
    /// <summary>
    /// 表示一次 HTTP 调用返回的状态码与正文。
    /// </summary>
    internal sealed class CentralServiceHttpResponse
    {
        /// <summary>
        /// 使用状态码与响应正文初始化响应对象。
        /// </summary>
        /// <param name="statusCode">HTTP 状态码。</param>
        /// <param name="body">响应正文。</param>
        public CentralServiceHttpResponse(HttpStatusCode statusCode, string body)
        {
            StatusCode = statusCode;
            Body = body ?? string.Empty;
        }

        /// <summary>
        /// 获取 HTTP 状态码。
        /// </summary>
        public HttpStatusCode StatusCode { get; private set; }

        /// <summary>
        /// 获取响应正文。
        /// </summary>
        public string Body { get; private set; }
    }
}
