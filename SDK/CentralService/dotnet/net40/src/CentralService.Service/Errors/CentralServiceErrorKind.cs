namespace CentralService.Service.Errors
{
    /// <summary>
    /// 表示中心服务 SDK 可识别的错误响应形态。
    /// </summary>
    public enum CentralServiceErrorKind
    {
        /// <summary>
        /// 服务端返回了业务统一包裹 <c>ApiResponse</c>，但其中声明失败。
        /// </summary>
        ApiResponse = 1,

        /// <summary>
        /// 服务端返回了标准 <c>ProblemDetails</c> 错误结构。
        /// </summary>
        ProblemDetails = 2,

        /// <summary>
        /// 服务端返回了标准 <c>ValidationProblemDetails</c> 错误结构。
        /// </summary>
        ValidationProblemDetails = 3,

        /// <summary>
        /// 服务端返回了纯文本或非 JSON 错误内容。
        /// </summary>
        PlainText = 4,

        /// <summary>
        /// 响应体存在但无法归类到已知错误结构。
        /// </summary>
        Unknown = 5
    }
}
