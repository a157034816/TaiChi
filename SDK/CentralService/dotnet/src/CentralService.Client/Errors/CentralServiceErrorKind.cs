namespace CentralService.Client.Errors
{
    /// <summary>
    /// 表示中心服务错误响应的解析类型。
    /// </summary>
    public enum CentralServiceErrorKind
    {
        /// <summary>
        /// 服务端返回了统一的 API 响应错误结构。
        /// </summary>
        ApiResponse = 1,

        /// <summary>
        /// 服务端返回了 ProblemDetails 结构。
        /// </summary>
        ProblemDetails = 2,

        /// <summary>
        /// 服务端返回了包含字段校验信息的 ValidationProblemDetails 结构。
        /// </summary>
        ValidationProblemDetails = 3,

        /// <summary>
        /// 服务端返回了纯文本错误内容。
        /// </summary>
        PlainText = 4,

        /// <summary>
        /// 错误响应无法归类到已知结构。
        /// </summary>
        Unknown = 5,

        /// <summary>
        /// 所有候选端点都因传输异常或熔断跳过而无法完成请求。
        /// </summary>
        Transport = 6
    }
}
