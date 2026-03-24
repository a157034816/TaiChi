using System;
using System.Net;
using CentralService.Client.Errors;
using CentralService.Client.Models;

namespace CentralService.Client.Internal
{
    /// <summary>
    /// 将中心服务返回的错误正文归一化为客户端异常模型。
    /// </summary>
    internal static class CentralServiceErrorParser
    {
        /// <summary>
        /// 按已知错误结构解析服务端返回内容。
        /// </summary>
        /// <param name="method">请求方法。</param>
        /// <param name="url">请求地址。</param>
        /// <param name="statusCode">HTTP 状态码。</param>
        /// <param name="body">响应正文。</param>
        /// <returns>归一化后的错误对象。</returns>
        public static CentralServiceError Parse(string method, string url, HttpStatusCode statusCode, string body)
        {
            body = body ?? string.Empty;

            if (!CentralServiceJson.LooksLikeJson(body))
            {
                var msg = string.IsNullOrWhiteSpace(body) ? ("HTTP " + (int)statusCode) : body.Trim();
                return new CentralServiceError(statusCode, method, url, CentralServiceErrorKind.PlainText, msg, null, string.Empty, body);
            }

            // 解析顺序按“特征更强的结构优先”处理，避免统一响应和 ProblemDetails 的字段交叉导致误分类。
            if (ContainsIgnoreCase(body, "\"errors\""))
            {
                var vp = TryDeserialize<CentralServiceValidationProblemDetails>(body);
                var msg = (vp != null && !string.IsNullOrWhiteSpace(vp.Title)) ? vp.Title : "Validation error";
                return new CentralServiceError(statusCode, method, url, CentralServiceErrorKind.ValidationProblemDetails, msg, null, string.Empty, body);
            }

            if (ContainsIgnoreCase(body, "\"title\"") && ContainsIgnoreCase(body, "\"status\""))
            {
                var pd = TryDeserialize<CentralServiceProblemDetails>(body);
                var msg = (pd != null && !string.IsNullOrWhiteSpace(pd.Title)) ? pd.Title : "ProblemDetails";
                return new CentralServiceError(statusCode, method, url, CentralServiceErrorKind.ProblemDetails, msg, null, string.Empty, body);
            }

            if (ContainsIgnoreCase(body, "\"success\""))
            {
                var api = TryDeserialize<ApiResponseErrorOnly>(body);
                var msg = (api != null && !string.IsNullOrWhiteSpace(api.ErrorMessage)) ? api.ErrorMessage : "ApiResponse error";
                return new CentralServiceError(
                    statusCode,
                    method,
                    url,
                    CentralServiceErrorKind.ApiResponse,
                    msg,
                    api != null ? api.ErrorCode : null,
                    api != null ? api.ErrorKey : string.Empty,
                    body);
            }

            return new CentralServiceError(statusCode, method, url, CentralServiceErrorKind.Unknown, "Unknown error", null, string.Empty, body);
        }

        private static bool ContainsIgnoreCase(string text, string value)
        {
            return text != null && value != null && text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static T TryDeserialize<T>(string json) where T : class
        {
            try
            {
                return CentralServiceJson.Deserialize<T>(json);
            }
            catch
            {
                // 分类失败时保留原始响应体并回退到更宽泛的类型，避免额外异常掩盖真实服务端错误。
                return null;
            }
        }
    }
}
