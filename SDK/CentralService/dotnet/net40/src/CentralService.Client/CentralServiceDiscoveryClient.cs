using System;
using System.Net;
using CentralService.Client.Errors;
using CentralService.Client.Internal;
using CentralService.Client.Models;

namespace CentralService.Client
{
    /// <summary>
    /// 提供服务列表查询、服务发现与网络状态读取相关的中心服务客户端能力。
    /// </summary>
    public sealed class CentralServiceDiscoveryClient : IDisposable
    {
        // 复用底层 HTTP 传输层，统一处理 URL 拼接、请求发送与响应正文读取。
        private readonly CentralServiceHttpClient _http;

        /// <summary>
        /// 使用指定配置初始化中心服务发现客户端。
        /// </summary>
        /// <param name="options">SDK 连接配置。</param>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> 为 <c>null</c>。</exception>
        public CentralServiceDiscoveryClient(CentralServiceSdkOptions options)
        {
            if (options == null) throw new ArgumentNullException("options");
            _http = new CentralServiceHttpClient(options.BaseUrl, options.Timeout, options.IgnoreSslErrors);
        }

        /// <summary>
        /// 查询当前中心服务中登记的服务列表。
        /// </summary>
        /// <param name="name">服务名称过滤条件；为 <c>null</c> 或空白时返回全部服务。</param>
        /// <returns>服务列表结果。</returns>
        /// <exception cref="CentralServiceException">中心服务返回错误响应、业务失败结果，或响应无法解析。</exception>
        public ServiceListResponse List(string name)
        {
            var url = "/api/Service/list";
            if (!string.IsNullOrWhiteSpace(name))
            {
                url += "?name=" + Uri.EscapeDataString(name);
            }

            var fullUrl = _http.BuildUrl(url);
            var resp = _http.Send("GET", fullUrl, null);

            if (!IsSuccess(resp.StatusCode))
            {
                throw new CentralServiceException(CentralServiceErrorParser.Parse("GET", fullUrl, resp.StatusCode, resp.Body));
            }

            // List 接口成功时返回统一 ApiResponse 包裹，需要先判断 success 再提取 data。
            var parsed = CentralServiceJson.Deserialize<ApiResponse<ServiceListResponse>>(resp.Body);
            if (parsed == null)
            {
                throw new CentralServiceException(new CentralServiceError(resp.StatusCode, "GET", fullUrl, CentralServiceErrorKind.Unknown, "无法解析响应", null, resp.Body));
            }

            if (!parsed.Success)
            {
                throw new CentralServiceException(new CentralServiceError(resp.StatusCode, "GET", fullUrl, CentralServiceErrorKind.ApiResponse, parsed.ErrorMessage, parsed.ErrorCode, resp.Body));
            }

            return parsed.Data;
        }

        /// <summary>
        /// 使用轮询策略发现服务实例。
        /// </summary>
        /// <param name="serviceName">服务名称。</param>
        /// <returns>发现到的服务实例；当响应正文为空时返回 <c>null</c>。</returns>
        /// <exception cref="CentralServiceException">中心服务返回错误响应。</exception>
        public ServiceInfo DiscoverRoundRobin(string serviceName)
        {
            return GetServiceInfo("/api/ServiceDiscovery/discover/roundrobin/" + Uri.EscapeDataString(serviceName));
        }

        /// <summary>
        /// 使用权重策略发现服务实例。
        /// </summary>
        /// <param name="serviceName">服务名称。</param>
        /// <returns>发现到的服务实例；当响应正文为空时返回 <c>null</c>。</returns>
        /// <exception cref="CentralServiceException">中心服务返回错误响应。</exception>
        public ServiceInfo DiscoverWeighted(string serviceName)
        {
            return GetServiceInfo("/api/ServiceDiscovery/discover/weighted/" + Uri.EscapeDataString(serviceName));
        }

        /// <summary>
        /// 使用综合最优策略发现服务实例。
        /// </summary>
        /// <param name="serviceName">服务名称。</param>
        /// <returns>发现到的服务实例；当响应正文为空时返回 <c>null</c>。</returns>
        /// <exception cref="CentralServiceException">中心服务返回错误响应。</exception>
        public ServiceInfo DiscoverBest(string serviceName)
        {
            return GetServiceInfo("/api/ServiceDiscovery/discover/best/" + Uri.EscapeDataString(serviceName));
        }

        /// <summary>
        /// 获取全部服务实例的网络状态。
        /// </summary>
        /// <returns>网络状态数组；当响应正文为空时返回 <c>null</c>。</returns>
        /// <exception cref="CentralServiceException">中心服务返回错误响应。</exception>
        public ServiceNetworkStatus[] GetNetworkAll()
        {
            var url = _http.BuildUrl("/api/ServiceDiscovery/network/all");
            var resp = _http.Send("GET", url, null);
            if (!IsSuccess(resp.StatusCode))
            {
                throw new CentralServiceException(CentralServiceErrorParser.Parse("GET", url, resp.StatusCode, resp.Body));
            }

            return CentralServiceJson.Deserialize<ServiceNetworkStatus[]>(resp.Body);
        }

        /// <summary>
        /// 获取指定服务实例的网络状态。
        /// </summary>
        /// <param name="serviceId">服务实例标识。</param>
        /// <returns>网络状态；当响应正文为空时返回 <c>null</c>。</returns>
        /// <exception cref="CentralServiceException">中心服务返回错误响应。</exception>
        public ServiceNetworkStatus GetNetwork(string serviceId)
        {
            var url = _http.BuildUrl("/api/ServiceDiscovery/network/" + Uri.EscapeDataString(serviceId));
            var resp = _http.Send("GET", url, null);
            if (!IsSuccess(resp.StatusCode))
            {
                throw new CentralServiceException(CentralServiceErrorParser.Parse("GET", url, resp.StatusCode, resp.Body));
            }

            return CentralServiceJson.Deserialize<ServiceNetworkStatus>(resp.Body);
        }

        /// <summary>
        /// 触发对指定服务实例的网络重新评估。
        /// </summary>
        /// <param name="serviceId">服务实例标识。</param>
        /// <returns>最新网络状态；当响应正文为空时返回 <c>null</c>。</returns>
        /// <exception cref="CentralServiceException">中心服务返回错误响应。</exception>
        public ServiceNetworkStatus EvaluateNetwork(string serviceId)
        {
            var url = _http.BuildUrl("/api/ServiceDiscovery/network/evaluate/" + Uri.EscapeDataString(serviceId));
            var resp = _http.Send("POST", url, string.Empty);
            if (!IsSuccess(resp.StatusCode))
            {
                throw new CentralServiceException(CentralServiceErrorParser.Parse("POST", url, resp.StatusCode, resp.Body));
            }

            return CentralServiceJson.Deserialize<ServiceNetworkStatus>(resp.Body);
        }

        /// <summary>
        /// 获取单个服务发现接口的结果。
        /// </summary>
        /// <param name="path">已编码好的相对路径。</param>
        /// <returns>服务信息；当响应正文为空时返回 <c>null</c>。</returns>
        private ServiceInfo GetServiceInfo(string path)
        {
            var url = _http.BuildUrl(path);
            var resp = _http.Send("GET", url, null);
            if (!IsSuccess(resp.StatusCode))
            {
                throw new CentralServiceException(CentralServiceErrorParser.Parse("GET", url, resp.StatusCode, resp.Body));
            }

            return CentralServiceJson.Deserialize<ServiceInfo>(resp.Body);
        }

        /// <summary>
        /// 释放客户端持有的 HTTP 资源。
        /// </summary>
        public void Dispose()
        {
            _http.Dispose();
        }

        /// <summary>
        /// 判断 HTTP 状态码是否位于成功区间。
        /// </summary>
        /// <param name="code">HTTP 状态码。</param>
        /// <returns>仅当状态码位于 <c>[200, 299]</c> 时返回 <c>true</c>。</returns>
        private static bool IsSuccess(HttpStatusCode code)
        {
            var i = (int)code;
            return i >= 200 && i <= 299;
        }
    }
}
