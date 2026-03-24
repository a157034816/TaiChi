package erp.centralservice.client.internal;

import erp.centralservice.client.errors.CentralServiceError;
import erp.centralservice.client.errors.CentralServiceErrorKind;

import java.util.Map;

/**
 * 将中心服务的失败响应转换为统一的结构化错误对象。
 *
 * <p>解析顺序遵循“越具体越优先”的原则：先识别验证错误，再识别标准 ProblemDetails，
 * 随后识别 SDK 自有的 {@code ApiResponse} 错误结构，最后再退回未知错误。</p>
 */
public final class CentralServiceErrorParser {
    private CentralServiceErrorParser() {
    }

    /**
     * 解析失败响应。
     *
     * <p>输入约束：{@code method} 与 {@code url} 应来自真实请求上下文，{@code bodyText} 可以为
     * {@code null}。失败语义：当响应体不是 JSON、JSON 语法非法或顶层不是对象时，方法会退回
     * {@link CentralServiceErrorKind#PlainText}；不会因为错误响应本身无法解析而再次抛出异常。</p>
     *
     * @param method HTTP 方法
     * @param url 请求 URL
     * @param statusCode HTTP 状态码
     * @param bodyText 原始响应体，可为 {@code null}
     * @return 结构化错误对象
     */
    public static CentralServiceError parse(String method, String url, int statusCode, String bodyText) {
        String raw = bodyText == null ? "" : bodyText;
        String trimmed = raw.trim();

        if (!CentralServiceJson.looksLikeJson(trimmed)) {
            String msg = trimmed.isEmpty() ? ("HTTP " + statusCode) : trimmed;
            return new CentralServiceError(statusCode, method, url, CentralServiceErrorKind.PlainText, msg, null, raw);
        }

        Object root;
        try {
            root = CentralServiceJson.parse(trimmed);
        } catch (RuntimeException e) {
            String msg = trimmed.isEmpty() ? ("HTTP " + statusCode) : trimmed;
            return new CentralServiceError(statusCode, method, url, CentralServiceErrorKind.PlainText, msg, null, raw);
        }

        Map<String, Object> obj = CentralServiceJson.asObject(root);
        if (obj == null) {
            String msg = trimmed.isEmpty() ? ("HTTP " + statusCode) : trimmed;
            return new CentralServiceError(statusCode, method, url, CentralServiceErrorKind.PlainText, msg, null, raw);
        }

        // 按服务端最常见的返回格式依次尝试，命中后立即停止，避免同一响应被重复归类。
        if (obj.containsKey("errors")) {
            String title = CentralServiceJson.asString(obj.get("title"));
            if (title == null || title.isEmpty()) title = "Validation error";
            return new CentralServiceError(statusCode, method, url, CentralServiceErrorKind.ValidationProblemDetails, title, null, raw);
        }

        if (obj.get("title") != null && obj.get("status") != null) {
            String title = CentralServiceJson.asString(obj.get("title"));
            if (title == null || title.isEmpty()) title = "ProblemDetails";
            return new CentralServiceError(statusCode, method, url, CentralServiceErrorKind.ProblemDetails, title, null, raw);
        }

        if (obj.containsKey("success")) {
            String msg = CentralServiceJson.asString(obj.get("errorMessage"));
            Integer code = CentralServiceJson.asIntNullable(obj.get("errorCode"));
            if (msg == null || msg.isEmpty()) msg = "ApiResponse error";
            return new CentralServiceError(statusCode, method, url, CentralServiceErrorKind.ApiResponse, msg, code, raw);
        }

        return new CentralServiceError(statusCode, method, url, CentralServiceErrorKind.Unknown, "Unknown error", null, raw);
    }

    /**
     * 创建无法进一步分类时使用的兜底错误。
     *
     * @param method HTTP 方法
     * @param url 请求 URL
     * @param statusCode HTTP 状态码
     * @param rawBody 原始响应体，可为 {@code null}
     * @param message 自定义错误消息；为空白时退回默认文案
     * @return 兜底错误对象
     */
    public static CentralServiceError unknown(String method, String url, int statusCode, String rawBody, String message) {
        String msg = message == null || message.trim().isEmpty() ? "Unknown error" : message.trim();
        return new CentralServiceError(statusCode, method, url, CentralServiceErrorKind.Unknown, msg, null, rawBody);
    }
}
