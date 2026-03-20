package erp.centralservice.service.errors;

/**
 * 描述一次中心服务调用失败时的结构化错误信息。
 *
 * <p>该对象会尽量保留 HTTP 元数据、解析后的错误类别以及原始响应体，便于调用方做日志记录、
 * 监控上报或重试决策。</p>
 */
public final class CentralServiceError {
    /** 服务端返回的 HTTP 状态码。 */
    public final int httpStatus;
    /** 发起请求时使用的 HTTP 方法。 */
    public final String method;
    /** 发生错误的完整请求 URL。 */
    public final String url;
    /** SDK 根据响应体结构归类后的错误类型。 */
    public final CentralServiceErrorKind kind;
    /** 面向调用方的错误消息。 */
    public final String message;
    /** 业务错误码；当响应体未提供时为 {@code null}。 */
    public final Integer errorCode;
    /** 未经修改的原始响应体，便于排查协议差异。 */
    public final String rawBody;

    /**
     * 创建结构化错误对象。
     *
     * @param httpStatus HTTP 状态码
     * @param method 请求方法
     * @param url 请求 URL
     * @param kind 错误分类
     * @param message 错误消息
     * @param errorCode 业务错误码，可为 {@code null}
     * @param rawBody 原始响应体，可为 {@code null}
     */
    public CentralServiceError(int httpStatus, String method, String url, CentralServiceErrorKind kind, String message, Integer errorCode, String rawBody) {
        this.httpStatus = httpStatus;
        this.method = method;
        this.url = url;
        this.kind = kind;
        this.message = message;
        this.errorCode = errorCode;
        this.rawBody = rawBody;
    }

    /**
     * 输出紧凑的错误摘要，包含分类、HTTP 元数据与主要消息。
     *
     * @return 便于日志打印的单行文本
     */
    @Override
    public String toString() {
        return String.valueOf(kind) + " HTTP " + httpStatus + " " + method + " " + url + ": " + message;
    }
}


