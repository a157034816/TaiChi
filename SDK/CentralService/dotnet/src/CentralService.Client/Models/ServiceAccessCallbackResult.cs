namespace CentralService.Client.Models
{
    /// <summary>
    /// 描述一次服务访问回调的执行结果。
    /// </summary>
    public sealed class ServiceAccessCallbackResult<T>
    {
        /// <summary>
        /// 获取或设置本次回调是否成功。
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 获取或设置成功时返回的业务值。
        /// </summary>
        public T Value { get; set; }

        /// <summary>
        /// 获取或设置失败类型。
        /// </summary>
        public ServiceAccessFailureKind? FailureKind { get; set; }

        /// <summary>
        /// 获取或设置失败消息。
        /// </summary>
        public string FailureMessage { get; set; }

        /// <summary>
        /// 创建成功结果。
        /// </summary>
        public static ServiceAccessCallbackResult<T> FromSuccess(T value)
        {
            return new ServiceAccessCallbackResult<T>
            {
                Success = true,
                Value = value
            };
        }

        /// <summary>
        /// 创建失败结果。
        /// </summary>
        public static ServiceAccessCallbackResult<T> FromFailure(
            ServiceAccessFailureKind failureKind,
            string failureMessage)
        {
            return new ServiceAccessCallbackResult<T>
            {
                Success = false,
                FailureKind = failureKind,
                FailureMessage = failureMessage ?? string.Empty
            };
        }
    }
}
