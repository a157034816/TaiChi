using System.Net;

namespace CentralService.Service.Errors
{
    /// <summary>
    /// 表示一次中心服务 SDK 调用失败时的结构化错误详情。
    /// </summary>
    /// <remarks>
    /// 该类型统一承载 HTTP 状态码、请求上下文、错误分类和原始响应体，
    /// 便于调用方记录日志或将错误上抛到自己的诊断体系中。
    /// </remarks>
    public sealed class CentralServiceError
    {
        /// <summary>
        /// 使用结构化错误信息创建错误对象。
        /// </summary>
        /// <param name="httpStatus">服务端返回的 HTTP 状态码。</param>
        /// <param name="method">失败请求使用的 HTTP 方法。</param>
        /// <param name="url">失败请求的绝对地址。</param>
        /// <param name="kind">SDK 识别出的错误形态。</param>
        /// <param name="message">面向日志与诊断的简要错误描述。</param>
        /// <param name="errorCode">业务层返回的可选错误码。</param>
        /// <param name="rawBody">原始响应体文本。</param>
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
        /// 获取服务端返回的 HTTP 状态码。
        /// </summary>
        public HttpStatusCode HttpStatus { get; private set; }

        /// <summary>
        /// 获取失败请求使用的 HTTP 方法。
        /// </summary>
        public string Method { get; private set; }

        /// <summary>
        /// 获取失败请求的绝对地址。
        /// </summary>
        public string Url { get; private set; }

        /// <summary>
        /// 获取 SDK 识别出的错误类型。
        /// </summary>
        public CentralServiceErrorKind Kind { get; private set; }

        /// <summary>
        /// 获取人类可读的错误摘要。
        /// </summary>
        public string Message { get; private set; }

        /// <summary>
        /// 获取业务层返回的可选错误码。
        /// </summary>
        public int? ErrorCode { get; private set; }

        /// <summary>
        /// 获取服务端返回的原始响应体。
        /// </summary>
        public string RawBody { get; private set; }
    }
}
