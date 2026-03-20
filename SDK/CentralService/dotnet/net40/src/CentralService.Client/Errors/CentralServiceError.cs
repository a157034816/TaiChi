using System.Net;

namespace CentralService.Client.Errors
{
    /// <summary>
    /// 描述中心服务调用过程中解析出的错误信息。
    /// </summary>
    public sealed class CentralServiceError
    {
        /// <summary>
        /// 使用指定的 HTTP 上下文与错误详情初始化错误对象。
        /// </summary>
        /// <param name="httpStatus">HTTP 状态码。</param>
        /// <param name="method">请求方法。</param>
        /// <param name="url">请求地址。</param>
        /// <param name="kind">错误类别。</param>
        /// <param name="message">错误消息。</param>
        /// <param name="errorCode">业务错误码。</param>
        /// <param name="rawBody">原始响应正文。</param>
        public CentralServiceError(
            HttpStatusCode httpStatus,
            string method,
            string url,
            CentralServiceErrorKind kind,
            string message,
            int? errorCode,
            string rawBody)
        {
            HttpStatus = httpStatus;
            Method = method ?? string.Empty;
            Url = url ?? string.Empty;
            Kind = kind;
            Message = message ?? string.Empty;
            ErrorCode = errorCode;
            RawBody = rawBody ?? string.Empty;
        }

        /// <summary>
        /// 获取响应的 HTTP 状态码。
        /// </summary>
        public HttpStatusCode HttpStatus { get; private set; }

        /// <summary>
        /// 获取发起请求时使用的 HTTP 方法。
        /// </summary>
        public string Method { get; private set; }

        /// <summary>
        /// 获取请求地址。
        /// </summary>
        public string Url { get; private set; }

        /// <summary>
        /// 获取错误类别。
        /// </summary>
        public CentralServiceErrorKind Kind { get; private set; }

        /// <summary>
        /// 获取面向调用方的错误消息。
        /// </summary>
        public string Message { get; private set; }

        /// <summary>
        /// 获取业务错误码。
        /// </summary>
        public int? ErrorCode { get; private set; }

        /// <summary>
        /// 获取原始响应正文。
        /// </summary>
        public string RawBody { get; private set; }
    }
}
