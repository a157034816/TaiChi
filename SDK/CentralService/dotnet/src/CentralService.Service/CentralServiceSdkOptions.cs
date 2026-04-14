using System;
using System.Collections.Generic;
using System.Linq;
using CentralService.Shared.Internal;

namespace CentralService.Service
{
    /// <summary>
    /// 定义中心服务服务端 SDK 的连接选项。
    /// </summary>
    public sealed class CentralServiceSdkOptions
    {
        private const int DefaultMaxAttempts = 2;
        private readonly IReadOnlyList<CentralServiceEndpointOptions> _endpoints;

        /// <summary>
        /// 使用单个中心服务根地址初始化客户端选项。
        /// </summary>
        /// <param name="baseUrl">中心服务的根地址。</param>
        public CentralServiceSdkOptions(string baseUrl)
            : this(new[] { new CentralServiceEndpointOptions(baseUrl) })
        {
        }

        /// <summary>
        /// 使用多个中心服务端点初始化客户端选项。
        /// </summary>
        /// <param name="endpoints">中心服务端点列表。</param>
        public CentralServiceSdkOptions(IEnumerable<CentralServiceEndpointOptions> endpoints)
        {
            if (endpoints == null) throw new ArgumentNullException("endpoints");

            var normalized = NormalizeEndpoints(endpoints).ToArray();
            if (normalized.Length == 0)
            {
                throw new ArgumentException("至少需要一个中心服务端点。", "endpoints");
            }

            _endpoints = normalized;
            BaseUrl = _endpoints[0].BaseUrl;
            Timeout = TimeSpan.FromSeconds(5);
            IgnoreSslErrors = false;
        }

        /// <summary>
        /// 获取首个中心服务根地址（与 <see cref="Endpoints"/> 的首项等价）。
        /// </summary>
        public string BaseUrl { get; private set; }

        /// <summary>
        /// 获取按优先级归一化后的中心服务端点列表。
        /// </summary>
        public IReadOnlyList<CentralServiceEndpointOptions> Endpoints => _endpoints;

        /// <summary>
        /// 获取或设置单次请求超时时间。
        /// </summary>
        public TimeSpan Timeout { get; set; }

        /// <summary>
        /// 获取或设置是否忽略 SSL 证书错误。
        /// </summary>
        public bool IgnoreSslErrors { get; set; }

        /// <summary>
        /// 获取或设置自定义的 HTTP 消息处理器，用于在测试环境中与 TestServer 集成。
        /// </summary>
        public System.Net.Http.HttpMessageHandler? HttpMessageHandler { get; set; }

        internal IReadOnlyList<CentralServiceTransportEndpoint> CreateTransportEndpoints()
        {
            return _endpoints
                .Select((endpoint, index) => endpoint.ToTransportEndpoint(index))
                .ToArray();
        }

        private static IEnumerable<CentralServiceEndpointOptions> NormalizeEndpoints(IEnumerable<CentralServiceEndpointOptions> endpoints)
        {
            return endpoints
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.BaseUrl))
                .Select((x, index) => x.Normalize(index))
                .OrderBy(x => x.Priority)
                .ThenBy(x => x.Order)
                .ToArray();
        }

        /// <summary>
        /// 表示单个中心服务端点的连接与熔断配置。
        /// </summary>
        public sealed class CentralServiceEndpointOptions
        {
            /// <summary>
            /// 初始化一个空的端点配置实例，供配置系统反序列化使用。
            /// </summary>
            public CentralServiceEndpointOptions()
            {
                BaseUrl = string.Empty;
            }

            /// <summary>
            /// 使用中心服务根地址创建端点配置。
            /// </summary>
            public CentralServiceEndpointOptions(string baseUrl)
            {
                BaseUrl = baseUrl ?? string.Empty;
            }

            /// <summary>
            /// 获取或设置中心服务根地址。
            /// </summary>
            public string BaseUrl { get; set; }

            /// <summary>
            /// 获取或设置优先级，数值越小越优先。
            /// </summary>
            public int Priority { get; set; }

            /// <summary>
            /// 获取或设置单中心最大尝试次数；为空时使用系统默认值 2。
            /// </summary>
            public int? MaxAttempts { get; set; }

            /// <summary>
            /// 获取或设置该端点的熔断配置；为空时表示禁用熔断。
            /// </summary>
            public CentralServiceCircuitBreakerOptions? CircuitBreaker { get; set; }

            internal int Order { get; private set; }

            internal CentralServiceEndpointOptions Normalize(int order)
            {
                return new CentralServiceEndpointOptions(NormalizeBaseUrl(BaseUrl))
                {
                    Priority = Priority,
                    MaxAttempts = NormalizeMaxAttempts(MaxAttempts),
                    CircuitBreaker = CircuitBreaker?.Normalize(),
                    Order = order,
                };
            }

            internal CentralServiceTransportEndpoint ToTransportEndpoint(int order)
            {
                var maxAttempts = NormalizeMaxAttempts(MaxAttempts);
                var circuitBreakerState = CircuitBreaker == null
                    ? null
                    : new CentralServiceCircuitBreakerState(
                        CircuitBreaker.FailureThreshold,
                        TimeSpan.FromMinutes(CircuitBreaker.BreakDurationMinutes),
                        CircuitBreaker.RecoveryThreshold);

                return new CentralServiceTransportEndpoint(NormalizeBaseUrl(BaseUrl), Priority, maxAttempts, order, circuitBreakerState);
            }

            private static string NormalizeBaseUrl(string baseUrl)
            {
                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    throw new ArgumentNullException("baseUrl");
                }

                return baseUrl.Trim().TrimEnd('/');
            }

            private static int NormalizeMaxAttempts(int? maxAttempts)
            {
                var value = maxAttempts.GetValueOrDefault(DefaultMaxAttempts);
                return value < 1 ? DefaultMaxAttempts : value;
            }
        }

        /// <summary>
        /// 表示单个中心服务端点的熔断配置。
        /// </summary>
        public sealed class CentralServiceCircuitBreakerOptions
        {
            /// <summary>
            /// 获取或设置连续失败阈值。
            /// </summary>
            public int FailureThreshold { get; set; }

            /// <summary>
            /// 获取或设置熔断持续时间（分钟）。
            /// </summary>
            public int BreakDurationMinutes { get; set; }

            /// <summary>
            /// 获取或设置半开状态下的恢复成功阈值。
            /// </summary>
            public int RecoveryThreshold { get; set; }

            internal CentralServiceCircuitBreakerOptions Normalize()
            {
                return new CentralServiceCircuitBreakerOptions
                {
                    FailureThreshold = Math.Max(1, FailureThreshold),
                    BreakDurationMinutes = Math.Max(1, BreakDurationMinutes),
                    RecoveryThreshold = Math.Max(1, RecoveryThreshold),
                };
            }
        }
    }
}
