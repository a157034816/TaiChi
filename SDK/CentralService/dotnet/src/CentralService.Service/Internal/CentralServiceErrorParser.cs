using System;
using System.Net;
using CentralService.Service.Errors;
using CentralService.Service.Models;

namespace CentralService.Service.Internal
{
    internal static class CentralServiceErrorParser
    {
        public static CentralServiceError Parse(string method, string url, HttpStatusCode statusCode, string body)
        {
            body = body ?? string.Empty;

            // 先用轻量级启发式判断是否值得进入 JSON 解析，
            // 避免在纯文本、HTML 网关错误页等场景里因为反序列化异常掩盖原始错误。
            if (!CentralServiceJson.LooksLikeJson(body))
            {
                var msg = string.IsNullOrWhiteSpace(body) ? ("HTTP " + (int)statusCode) : body.Trim();
                return new CentralServiceError(statusCode, method, url, CentralServiceErrorKind.PlainText, msg, null, string.Empty, body);
            }

            // 按最具体到最宽泛的结构依次识别，保证带 errors 的验证错误不会被更泛化的 ProblemDetails 或 ApiResponse 分支提前吞掉。
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
                // 错误归类阶段以“尽量返回可消费的错误对象”为目标，
                // 这里吞掉反序列化异常，交由调用方继续走后续兜底分类。
                return null;
            }
        }
    }
}
