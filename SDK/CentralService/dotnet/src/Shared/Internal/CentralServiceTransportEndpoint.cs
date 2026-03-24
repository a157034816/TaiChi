using System;

namespace CentralService.Shared.Internal
{
    /// <summary>
    /// 表示 SDK 在运行时归一化后的单个中心服务端点配置。
    /// </summary>
    internal sealed class CentralServiceTransportEndpoint
    {
        public CentralServiceTransportEndpoint(
            string baseUrl,
            int priority,
            int maxAttempts,
            int order,
            CentralServiceCircuitBreakerState? circuitBreaker)
        {
            BaseUrl = (baseUrl ?? string.Empty).TrimEnd('/');
            Priority = priority;
            MaxAttempts = Math.Max(1, maxAttempts);
            Order = order;
            CircuitBreaker = circuitBreaker;
        }

        public string BaseUrl { get; }

        public int Priority { get; }

        public int MaxAttempts { get; }

        public int Order { get; }

        public CentralServiceCircuitBreakerState? CircuitBreaker { get; }

        public string BuildUrl(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return BaseUrl;
            }

            if (!path.StartsWith("/", StringComparison.Ordinal))
            {
                path = "/" + path;
            }

            return BaseUrl + path;
        }
    }
}
