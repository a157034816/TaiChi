namespace CentralService.Client.Errors
{
    /// <summary>
    /// 标识中心服务响应被归类后的错误类型。
    /// </summary>
    public enum CentralServiceErrorKind
    {
        /// <summary>
        /// 标准 API 响应体返回了业务失败结果。
        /// </summary>
        ApiResponse = 1,

        /// <summary>
        /// 响应符合 ProblemDetails 结构。
        /// </summary>
        ProblemDetails = 2,

        /// <summary>
        /// 响应符合验证错误 ProblemDetails 结构。
        /// </summary>
        ValidationProblemDetails = 3,

        /// <summary>
        /// 响应为纯文本或非 JSON 内容。
        /// </summary>
        PlainText = 4,

        /// <summary>
        /// 响应内容无法识别或归类。
        /// </summary>
        Unknown = 5
    }
}
