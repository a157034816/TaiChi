using System;

namespace CentralService.Client.Errors
{
    /// <summary>
    /// 表示中心服务客户端调用失败时抛出的异常。
    /// </summary>
    public sealed class CentralServiceException : Exception
    {
        /// <summary>
        /// 使用解析后的中心服务错误创建异常实例。
        /// </summary>
        /// <param name="error">中心服务错误详情。</param>
        public CentralServiceException(CentralServiceError error)
            : base(error != null ? error.Message : null)
        {
            if (error == null) throw new ArgumentNullException("error");
            Error = error;
        }

        /// <summary>
        /// 获取本次异常对应的中心服务错误详情。
        /// </summary>
        public CentralServiceError Error { get; private set; }
    }
}
