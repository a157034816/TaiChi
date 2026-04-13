using System;
using System.Net;
using CentralService.Service.Errors;
using CentralService.Service.Internal;
using CentralService.Service.Models;

namespace CentralService.Service
{
    /// <summary>
    /// 面向服务注册侧的中心服务 SDK 客户端。
    /// </summary>
    /// <remarks>
    /// 该客户端负责调用注册与注销接口，不包含服务发现相关能力。
    /// 调用方需要自行保存服务 ID，并根据自身场景处理重试或续约策略。
    /// </remarks>
    public sealed class CentralServiceServiceClient : IDisposable
    {
        private readonly CentralServiceHttpClient _http;

        /// <summary>
        /// 使用完整配置创建客户端。
        /// </summary>
        /// <param name="options">SDK 配置，不能为空。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="options"/> 为 <c>null</c> 时抛出。</exception>
        public CentralServiceServiceClient(CentralServiceSdkOptions options)
        {
            if (options == null) throw new ArgumentNullException("options");
            _http = new CentralServiceHttpClient(options.BaseUrl, options.Timeout, options.IgnoreSslErrors);
        }

        /// <summary>
        /// 注册当前服务实例。
        /// </summary>
        /// <param name="request">服务注册请求，不能为空。</param>
        /// <returns>注册结果，通常包含最终服务 ID 与注册时间戳。</returns>
        /// <exception cref="ArgumentNullException">当 <paramref name="request"/> 为 <c>null</c> 时抛出。</exception>
        /// <exception cref="CentralServiceException">
        /// 当服务端返回非 2xx，或返回的 <c>ApiResponse.success</c> 为 <c>false</c>，
        /// 或 2xx 响应体无法解析为预期结构时抛出。
        /// </exception>
        public ServiceRegistrationResponse Register(ServiceRegistrationRequest request)
        {
            if (request == null) throw new ArgumentNullException("request");

            var url = _http.BuildUrl("/api/Service/register");
            var body = CentralServiceJson.Serialize(request);
            var resp = _http.Send("POST", url, body);

            if (!IsSuccess(resp.StatusCode))
            {
                throw new CentralServiceException(CentralServiceErrorParser.Parse("POST", url, resp.StatusCode, resp.Body));
            }

            var parsed = CentralServiceJson.Deserialize<ApiResponse<ServiceRegistrationResponse>>(resp.Body);
            if (parsed == null)
            {
                throw new CentralServiceException(new CentralServiceError(resp.StatusCode, "POST", url, CentralServiceErrorKind.Unknown, "无法解析响应", null, resp.Body));
            }

            if (!parsed.Success)
            {
                throw new CentralServiceException(new CentralServiceError(resp.StatusCode, "POST", url, CentralServiceErrorKind.ApiResponse, parsed.ErrorMessage, parsed.ErrorCode, resp.Body));
            }

            return parsed.Data;
        }

        /// <summary>
        /// 注销一个已注册的服务实例。
        /// </summary>
        /// <param name="serviceId">已注册服务实例的唯一标识，不能为空白。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="serviceId"/> 为空或仅包含空白字符时抛出。</exception>
        /// <exception cref="CentralServiceException">
        /// 当服务端返回非 2xx，或返回的 <c>ApiResponse</c> 明确声明失败时抛出。
        /// </exception>
        public void Deregister(string serviceId)
        {
            if (string.IsNullOrWhiteSpace(serviceId)) throw new ArgumentNullException("serviceId");

            var url = _http.BuildUrl("/api/Service/deregister/" + Uri.EscapeDataString(serviceId));
            var resp = _http.Send("DELETE", url, null);

            if (!IsSuccess(resp.StatusCode))
            {
                throw new CentralServiceException(CentralServiceErrorParser.Parse("DELETE", url, resp.StatusCode, resp.Body));
            }

            var parsed = CentralServiceJson.Deserialize<ApiResponse<object>>(resp.Body);
            if (parsed != null && !parsed.Success)
            {
                throw new CentralServiceException(new CentralServiceError(resp.StatusCode, "DELETE", url, CentralServiceErrorKind.ApiResponse, parsed.ErrorMessage, parsed.ErrorCode, resp.Body));
            }
        }


        /// <summary>
        /// 释放底层 HTTP 资源。
        /// </summary>
        public void Dispose()
        {
            _http.Dispose();
        }

        /// <summary>
        /// 判断 HTTP 状态码是否位于成功区间。
        /// </summary>
        /// <param name="code">待检查的 HTTP 状态码。</param>
        /// <returns>仅当状态码位于 <c>[200, 299]</c> 时返回 <c>true</c>。</returns>
        private static bool IsSuccess(HttpStatusCode code)
        {
            var i = (int)code;
            return i >= 200 && i <= 299;
        }
    }
}
