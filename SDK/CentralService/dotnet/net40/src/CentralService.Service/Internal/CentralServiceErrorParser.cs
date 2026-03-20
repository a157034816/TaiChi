using System;
using System.Net;
using CentralService.Service.Errors;
using CentralService.Service.Models;

namespace CentralService.Service.Internal
{
    /// <summary>
    /// 将中心服务返回的错误响应归一化为 <see cref="CentralServiceError"/>。
    /// </summary>
    internal static class CentralServiceErrorParser
    {
        /// <summary>
        /// 解析服务端错误响应并推断其错误类型。
        /// </summary>
        /// <param name="method">失败请求使用的 HTTP 方法。</param>
        /// <param name="url">失败请求的绝对地址。</param>
        /// <param name="statusCode">服务端返回的 HTTP 状态码。</param>
        /// <param name="body">原始响应体。</param>
        /// <returns>归一化后的结构化错误对象。</returns>
        public static CentralServiceError Parse(string method, string url, HttpStatusCode statusCode, string body)
        {
            body = body ?? string.Empty;

            if (!CentralServiceJson.LooksLikeJson(body))
            {
                var msg = string.IsNullOrWhiteSpace(body) ? ("HTTP " + (int)statusCode) : body.Trim();
                return new CentralServiceError(statusCode, method, url, CentralServiceErrorKind.PlainText, msg, null, body);
            }

            // 优先匹配 ValidationProblemDetails，因为它是 ProblemDetails 的扩展形态。
            if (ContainsIgnoreCase(body, "\"errors\""))
            {
                var vp = TryDeserialize<CentralServiceValidationProblemDetails>(body);
                var msg = (vp != null && !string.IsNullOrWhiteSpace(vp.Title)) ? vp.Title : "Validation error";
                return new CentralServiceError(statusCode, method, url, CentralServiceErrorKind.ValidationProblemDetails, msg, null, body);
            }

            // 其次匹配标准 ProblemDetails。
            if (ContainsIgnoreCase(body, "\"title\"") && ContainsIgnoreCase(body, "\"status\""))
            {
                var pd = TryDeserialize<CentralServiceProblemDetails>(body);
                var msg = (pd != null && !string.IsNullOrWhiteSpace(pd.Title)) ? pd.Title : "ProblemDetails";
                return new CentralServiceError(statusCode, method, url, CentralServiceErrorKind.ProblemDetails, msg, null, body);
            }

            // 最后再识别业务统一包裹 ApiResponse。
            if (ContainsIgnoreCase(body, "\"success\""))
            {
                var api = TryDeserialize<ApiResponseErrorOnly>(body);
                var msg = (api != null && !string.IsNullOrWhiteSpace(api.ErrorMessage)) ? api.ErrorMessage : "ApiResponse error";
                return new CentralServiceError(statusCode, method, url, CentralServiceErrorKind.ApiResponse, msg, api != null ? api.ErrorCode : null, body);
            }

            return new CentralServiceError(statusCode, method, url, CentralServiceErrorKind.Unknown, "Unknown error", null, body);
        }

        /// <summary>
        /// 使用忽略大小写的方式判断文本是否包含指定片段。
        /// </summary>
        private static bool ContainsIgnoreCase(string text, string value)
        {
            return text != null && value != null && text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// 尝试将 JSON 文本反序列化为指定模型；失败时返回 <c>null</c>。
        /// </summary>
        /// <typeparam name="T">目标引用类型。</typeparam>
        /// <param name="json">JSON 文本。</param>
        /// <returns>反序列化结果；失败时返回 <c>null</c>。</returns>
        private static T TryDeserialize<T>(string json) where T : class
        {
            try
            {
                return CentralServiceJson.Deserialize<T>(json);
            }
            catch
            {
                return null;
            }
        }
    }
}
