package erp.centralservice.client.errors;

/**
 * 中心服务错误响应的已知分类。
 */
public enum CentralServiceErrorKind {
    /** 所有候选端点都因传输层失败或熔断跳过而不可用。 */
    Transport,
    /** 命中了 SDK 约定的统一 {@code ApiResponse} 错误结构。 */
    ApiResponse,
    /** 命中了 ASP.NET Core {@code ProblemDetails} 结构。 */
    ProblemDetails,
    /** 命中了带有 {@code errors} 节点的验证错误结构。 */
    ValidationProblemDetails,
    /** 响应体不是 JSON，或 JSON 解析失败后退回纯文本解释。 */
    PlainText,
    /** 无法识别响应结构时使用的兜底分类。 */
    Unknown
}

