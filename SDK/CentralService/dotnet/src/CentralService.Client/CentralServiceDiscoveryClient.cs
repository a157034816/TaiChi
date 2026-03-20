using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CentralService.Client.Errors;
using CentralService.Client.Internal;
using CentralService.Client.Models;
using CentralService.Shared.Internal;

namespace CentralService.Client
{
    /// <summary>
    /// 提供中心服务的服务发现、网络状态查询与闭环访问能力。
    /// </summary>
    public sealed class CentralServiceDiscoveryClient : IDisposable
    {
        private readonly ICentralServiceTransport _transport;
        private readonly CentralServiceClientIdentity _clientIdentity;

        /// <summary>
        /// 使用给定的 SDK 配置创建服务发现客户端。
        /// </summary>
        /// <param name="options">中心服务地址、超时和证书校验等配置。</param>
        public CentralServiceDiscoveryClient(CentralServiceSdkOptions options)
            : this(options, null)
        {
        }

        internal CentralServiceDiscoveryClient(CentralServiceSdkOptions options, Func<DateTimeOffset>? utcNowProvider)
        {
            if (options == null) throw new ArgumentNullException("options");

            var endpoints = options.CreateTransportEndpoints();
            if (options.HttpMessageHandler != null)
            {
                _transport = new CentralServiceHttpClientMultiEndpointTransport(
                    endpoints,
                    options.Timeout,
                    options.HttpMessageHandler,
                    utcNowProvider);
            }
            else
            {
                _transport = new CentralServiceMultiEndpointTransport(
                    endpoints,
                    options.Timeout,
                    options.IgnoreSslErrors,
                    utcNowProvider);
            }
            _clientIdentity = options.ClientIdentity ?? new CentralServiceClientIdentity();
        }

        /// <summary>
        /// 查询已注册服务列表。
        /// </summary>
        /// <param name="name">可选的服务名称过滤条件。</param>
        /// <returns>匹配条件的服务列表响应。</returns>
        public ServiceListResponse List(string name)
        {
            var path = "/api/Service/list";
            if (!string.IsNullOrWhiteSpace(name))
            {
                path += "?name=" + Uri.EscapeDataString(name);
            }

            var transport = Send("GET", path, null);
            return ParseApiResponse<ServiceListResponse>("GET", transport).Data;
        }

        /// <summary>
        /// 使用轮询策略选择指定服务的一个可用实例。
        /// </summary>
        public ServiceInfo DiscoverRoundRobin(string serviceName)
        {
            return GetServiceInfo("/api/ServiceDiscovery/discover/roundrobin/" + Uri.EscapeDataString(serviceName));
        }

        /// <summary>
        /// 使用权重策略选择指定服务的一个可用实例。
        /// </summary>
        public ServiceInfo DiscoverWeighted(string serviceName)
        {
            return GetServiceInfo("/api/ServiceDiscovery/discover/weighted/" + Uri.EscapeDataString(serviceName));
        }

        /// <summary>
        /// 使用综合最优策略选择指定服务的一个可用实例。
        /// </summary>
        public ServiceInfo DiscoverBest(string serviceName)
        {
            return GetServiceInfo("/api/ServiceDiscovery/discover/best/" + Uri.EscapeDataString(serviceName));
        }

        /// <summary>
        /// 获取所有已记录服务实例的网络状态。
        /// </summary>
        public ServiceNetworkStatus[] GetNetworkAll()
        {
            var transport = Send("GET", "/api/ServiceDiscovery/network/all", null);
            if (!IsSuccess(transport.StatusCode))
            {
                throw CreateParsedError("GET", transport, CentralServiceErrorParser.Parse("GET", transport.Url, transport.StatusCode, transport.Body));
            }

            return CentralServiceJson.Deserialize<ServiceNetworkStatus[]>(transport.Body);
        }

        /// <summary>
        /// 获取指定服务实例的网络状态。
        /// </summary>
        public ServiceNetworkStatus GetNetwork(string serviceId)
        {
            var path = "/api/ServiceDiscovery/network/" + Uri.EscapeDataString(serviceId);
            var transport = Send("GET", path, null);
            if (!IsSuccess(transport.StatusCode))
            {
                throw CreateParsedError("GET", transport, CentralServiceErrorParser.Parse("GET", transport.Url, transport.StatusCode, transport.Body));
            }

            return CentralServiceJson.Deserialize<ServiceNetworkStatus>(transport.Body);
        }

        /// <summary>
        /// 触发指定服务实例的网络状态即时评估。
        /// </summary>
        public ServiceNetworkStatus EvaluateNetwork(string serviceId)
        {
            var path = "/api/ServiceDiscovery/network/evaluate/" + Uri.EscapeDataString(serviceId);
            var transport = Send("POST", path, string.Empty);
            if (!IsSuccess(transport.StatusCode))
            {
                throw CreateParsedError("POST", transport, CentralServiceErrorParser.Parse("POST", transport.Url, transport.StatusCode, transport.Body));
            }

            return CentralServiceJson.Deserialize<ServiceNetworkStatus>(transport.Body);
        }

        /// <summary>
        /// 通过中心服务执行一次带结果上报的服务访问。
        /// </summary>
        public T Access<T>(string serviceName, Func<ServiceAccessContext, ServiceAccessCallbackResult<T>> callback)
        {
            if (callback == null) throw new ArgumentNullException("callback");

            return AccessAsync(
                    serviceName,
                    context => Task.FromResult(callback(context)),
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        }

        /// <summary>
        /// 通过中心服务执行一次异步服务访问。
        /// </summary>
        public async Task<T> AccessAsync<T>(
            string serviceName,
            Func<ServiceAccessContext, Task<ServiceAccessCallbackResult<T>>> callback,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(serviceName)) throw new ArgumentNullException("serviceName");
            if (callback == null) throw new ArgumentNullException("callback");

            var triedServiceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var callbackExecutions = 0;
            int? maxAttemptsBudget = null;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ServiceAccessResolveResponse resolved;
                try
                {
                    resolved = ResolveAccess(serviceName, triedServiceIds);
                }
                catch (CentralServiceException ex)
                {
                    throw new CentralServiceAccessException(
                        serviceName,
                        ex.Error.Message,
                        ServiceAccessFailureKind.Transport,
                        null);
                }
                if (resolved == null || resolved.Service == null)
                {
                    throw new CentralServiceAccessException(serviceName, "中心服务未返回可访问的服务实例。", null, null);
                }

                if (!maxAttemptsBudget.HasValue)
                {
                    maxAttemptsBudget = resolved.MaxAttempts < 1 ? 1 : resolved.MaxAttempts;
                }

                if (callbackExecutions >= maxAttemptsBudget.Value)
                {
                    throw new CentralServiceAccessException(
                        serviceName,
                        "已达到本次访问允许的最大尝试次数。",
                        ServiceAccessFailureKind.Unknown,
                        resolved.Service);
                }

                ServiceAccessCallbackResult<T> callbackResult;
                try
                {
                    callbackResult = await callback(new ServiceAccessContext { Service = resolved.Service }).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    callbackResult = ServiceAccessCallbackResult<T>.FromFailure(ServiceAccessFailureKind.Unknown, ex.Message);
                }

                callbackResult = callbackResult ?? ServiceAccessCallbackResult<T>.FromFailure(
                    ServiceAccessFailureKind.Unknown,
                    "回调未返回连接结果。");

                callbackExecutions++;

                var report = ReportAccess(resolved.AccessTicket, callbackResult);
                if (callbackResult.Success)
                {
                    return callbackResult.Value;
                }

                if (string.Equals(report.DecisionCode, ServiceAccessCodes.Complete, StringComparison.OrdinalIgnoreCase)
                    || callbackExecutions >= maxAttemptsBudget.Value)
                {
                    throw new CentralServiceAccessException(
                        serviceName,
                        string.IsNullOrWhiteSpace(callbackResult.FailureMessage)
                            ? "服务访问失败。"
                            : callbackResult.FailureMessage,
                        callbackResult.FailureKind,
                        resolved.Service);
                }

                if (string.Equals(report.DecisionCode, ServiceAccessCodes.TryNextInstance, StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(resolved.Service.Id))
                    {
                        triedServiceIds.Add(resolved.Service.Id);
                    }

                    continue;
                }

                if (string.Equals(report.DecisionCode, ServiceAccessCodes.RetryResolve, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                throw new CentralServiceAccessException(
                    serviceName,
                    string.IsNullOrWhiteSpace(callbackResult.FailureMessage)
                        ? "服务访问失败。"
                        : callbackResult.FailureMessage,
                    callbackResult.FailureKind,
                    resolved.Service);
            }
        }

        /// <summary>
        /// 释放底层资源。
        /// </summary>
        public void Dispose()
        {
        }

        private ServiceInfo GetServiceInfo(string path)
        {
            var transport = Send("GET", path, null);
            if (!IsSuccess(transport.StatusCode))
            {
                throw CreateParsedError("GET", transport, CentralServiceErrorParser.Parse("GET", transport.Url, transport.StatusCode, transport.Body));
            }

            return CentralServiceJson.Deserialize<ServiceInfo>(transport.Body);
        }

        private ServiceAccessResolveResponse ResolveAccess(string serviceName, HashSet<string> excludedServiceIds)
        {
            var request = new ServiceAccessResolveRequest
            {
                ServiceName = serviceName ?? string.Empty,
                ClientName = _clientIdentity.ClientName ?? string.Empty,
                ClientLocalIp = _clientIdentity.LocalIp ?? string.Empty,
                ClientOperatorIp = _clientIdentity.OperatorIp ?? string.Empty,
                ClientPublicIp = _clientIdentity.PublicIp ?? string.Empty,
                ExcludedServiceIds = excludedServiceIds == null
                    ? Array.Empty<string>()
                    : new List<string>(excludedServiceIds).ToArray(),
            };

            var transport = Send("POST", "/api/ServiceAccess/resolve", CentralServiceJson.Serialize(request));
            return ParseApiResponse<ServiceAccessResolveResponse>("POST", transport).Data;
        }

        private static bool IsServiceAccessResolveError(CentralServiceException exception)
        {
            if (exception == null)
            {
                return false;
            }

            var errorKey = exception.Error == null ? string.Empty : (exception.Error.ErrorKey ?? string.Empty);
            return string.Equals(errorKey, ServiceAccessCodes.NoAvailableInstance, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(errorKey, ServiceAccessCodes.CircuitOpen, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(errorKey, ServiceAccessCodes.RetryResolve, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(errorKey, ServiceAccessCodes.TryNextInstance, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(errorKey, ServiceAccessCodes.InvalidClientIdentity, StringComparison.OrdinalIgnoreCase);
        }

        private ServiceAccessReportResponse ReportAccess<T>(string accessTicket, ServiceAccessCallbackResult<T> callbackResult)
        {
            var request = new ServiceAccessReportRequest
            {
                AccessTicket = accessTicket ?? string.Empty,
                ClientName = _clientIdentity.ClientName ?? string.Empty,
                ClientLocalIp = _clientIdentity.LocalIp ?? string.Empty,
                ClientOperatorIp = _clientIdentity.OperatorIp ?? string.Empty,
                ClientPublicIp = _clientIdentity.PublicIp ?? string.Empty,
                Success = callbackResult.Success,
                FailureKind = callbackResult.FailureKind.HasValue ? callbackResult.FailureKind.Value.ToString() : string.Empty,
                FailureMessage = callbackResult.FailureMessage ?? string.Empty,
            };

            var transport = Send("POST", "/api/ServiceAccess/report", CentralServiceJson.Serialize(request));
            return ParseApiResponse<ServiceAccessReportResponse>("POST", transport).Data;
        }

        private CentralServiceTransportResult Send(string method, string path, string? jsonBody)
        {
            try
            {
                return _transport.Send(method, path, jsonBody);
            }
            catch (CentralServiceTransportExhaustedException ex)
            {
                throw new CentralServiceException(
                    new CentralServiceError(
                        HttpStatusCode.ServiceUnavailable,
                        method,
                        string.IsNullOrWhiteSpace(ex.LastUrl) ? path : ex.LastUrl,
                        CentralServiceErrorKind.Transport,
                        ex.Message,
                        null,
                        string.Empty,
                        ex.RawDetail));
            }
        }

        private static ApiResponse<T> ParseApiResponse<T>(string method, CentralServiceTransportResult transport)
        {
            ApiResponse<T> parsed;
            try
            {
                parsed = CentralServiceJson.Deserialize<ApiResponse<T>>(transport.Body);
            }
            catch
            {
                throw CreateParsedError(
                    method,
                    transport,
                    new CentralServiceError(
                        transport.StatusCode,
                        method,
                        transport.Url,
                        CentralServiceErrorKind.PlainText,
                        "无法解析响应",
                        null,
                        string.Empty,
                        transport.Body));
            }
            if (parsed == null)
            {
                throw CreateParsedError(
                    method,
                    transport,
                    new CentralServiceError(
                        transport.StatusCode,
                        method,
                        transport.Url,
                        CentralServiceErrorKind.Unknown,
                        "无法解析响应",
                        null,
                        string.Empty,
                        transport.Body));
            }

            if (!parsed.Success)
            {
                throw CreateParsedError(
                    method,
                    transport,
                    new CentralServiceError(
                        transport.StatusCode,
                        method,
                        transport.Url,
                        CentralServiceErrorKind.ApiResponse,
                        parsed.ErrorMessage,
                        parsed.ErrorCode,
                        parsed.ErrorKey,
                        transport.Body));
            }

            return parsed;
        }

        private static CentralServiceException CreateParsedError(string method, CentralServiceTransportResult transport, CentralServiceError error)
        {
            return new CentralServiceException(
                new CentralServiceError(
                    error.HttpStatus,
                    method,
                    transport.Url,
                    error.Kind,
                    AppendTransportContext(error.Message, transport),
                    error.ErrorCode,
                    error.ErrorKey,
                    error.RawBody));
        }

        private static string AppendTransportContext(string message, CentralServiceTransportResult transport)
        {
            var segments = new List<string>
            {
                string.Format("端点={0}", transport.BaseUrl),
                string.Format("尝试={0}/{1}", transport.Attempt, transport.MaxAttempts),
            };

            if (transport.SkippedEndpoints.Count > 0)
            {
                segments.Add("已跳过=" + string.Join("、", transport.SkippedEndpoints));
            }

            return string.Format("{0} ({1})", message ?? string.Empty, string.Join("; ", segments));
        }

        private static bool IsSuccess(HttpStatusCode code)
        {
            var value = (int)code;
            return value >= 200 && value <= 299;
        }
    }
}
