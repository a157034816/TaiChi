using System.Net;

namespace CentralService.Client.Errors
{
    /// <summary>
    /// 描述一次中心服务调用失败时的上下文信息。
    /// </summary>
    public sealed class CentralServiceError
    {
        /// <summary>
        /// 使用指定的错误信息初始化实例。
        /// </summary>
        /// <param name="httpStatus">服务端返回的 HTTP 状态码。</param>
        /// <param name="method">调用使用的 HTTP 方法。</param>
        /// <param name="url">请求地址。</param>
        /// <param name="kind">错误分类。</param>
        /// <param name="message">面向调用方的错误消息。</param>
        /// <param name="errorCode">服务端业务错误码。</param>
        /// <param name="errorKey">服务端业务错误键。</param>
        /// <param name="rawBody">原始响应体。</param>
        public CentralServiceError(
            HttpStatusCode httpStatus,
            string method,
            string url,
            CentralServiceErrorKind kind,
            string message,
            int? errorCode,
            string errorKey,
            string rawBody)
        {
            HttpStatus = httpStatus;
            Method = method ?? string.Empty;
            Url = url ?? string.Empty;
            Kind = kind;
            Message = message ?? string.Empty;
            ErrorCode = errorCode;
            ErrorKey = errorKey ?? string.Empty;
            RawBody = rawBody ?? string.Empty;
        }

        /// <summary>
        /// 获取服务端返回的 HTTP 状态码。
        /// </summary>
        public HttpStatusCode HttpStatus { get; private set; }

        /// <summary>
        /// 获取触发错误的 HTTP 方法。
        /// </summary>
        public string Method { get; private set; }

        /// <summary>
        /// 获取触发错误的请求地址。
        /// </summary>
        public string Url { get; private set; }

        /// <summary>
        /// 获取错误分类。
        /// </summary>
        public CentralServiceErrorKind Kind { get; private set; }

        /// <summary>
        /// 获取错误消息。
        /// </summary>
        public string Message { get; private set; }

        /// <summary>
        /// 获取服务端返回的业务错误码。
        /// </summary>
        public int? ErrorCode { get; private set; }

        /// <summary>
        /// 获取服务端返回的业务错误键。
        /// </summary>
        public string ErrorKey { get; private set; }

        /// <summary>
        /// 获取服务端原始响应体。
        /// </summary>
        public string RawBody { get; private set; }
    }
}
