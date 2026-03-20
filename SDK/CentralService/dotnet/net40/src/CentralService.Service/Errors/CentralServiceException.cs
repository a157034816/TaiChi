using System;

namespace CentralService.Service.Errors
{
    /// <summary>
    /// 表示中心服务 SDK 调用失败的运行时异常。
    /// </summary>
    /// <remarks>
    /// 调用方通常应优先读取 <see cref="Error"/> 中的结构化错误上下文，
    /// 而不是只依赖异常消息文本。
    /// </remarks>
    public sealed class CentralServiceException : Exception
    {
        /// <summary>
        /// 使用结构化错误详情构造异常。
        /// </summary>
        /// <param name="error">错误详情，不能为空。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="error"/> 为 <c>null</c> 时抛出。</exception>
        public CentralServiceException(CentralServiceError error)
            : base(error != null ? error.Message : null)
        {
            if (error == null) throw new ArgumentNullException("error");
            Error = error;
        }

        /// <summary>
        /// 获取结构化错误详情。
        /// </summary>
        public CentralServiceError Error { get; private set; }
    }
}
