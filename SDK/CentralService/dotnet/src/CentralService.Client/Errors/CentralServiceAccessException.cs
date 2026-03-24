using System;
using CentralService.Client.Models;

namespace CentralService.Client.Errors
{
    /// <summary>
    /// 表示通过中心服务访问目标实例时，最终未能成功完成回调。
    /// </summary>
    public sealed class CentralServiceAccessException : Exception
    {
        public CentralServiceAccessException(
            string serviceName,
            string message,
            ServiceAccessFailureKind? failureKind,
            ServiceInfo? service)
            : base(message ?? string.Empty)
        {
            ServiceName = serviceName ?? string.Empty;
            FailureKind = failureKind;
            Service = service;
        }

        public string ServiceName { get; private set; }

        public ServiceAccessFailureKind? FailureKind { get; private set; }

        public ServiceInfo? Service { get; private set; }
    }
}
