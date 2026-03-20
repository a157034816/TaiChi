using System;

namespace CentralService.Client.Errors
{
    /// <summary>
    /// 表示中心服务调用失败时抛出的异常。
    /// </summary>
    public sealed class CentralServiceException : Exception
    {
        /// <summary>
        /// 使用解析后的错误信息初始化异常。
        /// </summary>
        /// <param name="error">中心服务错误详情。</param>
        /// <exception cref="ArgumentNullException"><paramref name="error"/> 为 <c>null</c>。</exception>
        public CentralServiceException(CentralServiceError error)
            : base(error != null ? error.Message : null)
        {
            if (error == null) throw new ArgumentNullException("error");
            Error = error;
        }

        /// <summary>
        /// 获取中心服务返回的结构化错误详情。
        /// </summary>
        public CentralServiceError Error { get; private set; }
    }
}
