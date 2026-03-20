using System;
using System.Collections.Generic;
using System.Net;
using CentralService.Service.Errors;
using CentralService.Service.Internal;
using CentralService.Service.Models;
using CentralService.Shared.Internal;

namespace CentralService.Service
{
    /// <summary>
    /// 提供服务注册、心跳与注销相关的中心服务客户端能力。
    /// </summary>
    public sealed class CentralServiceServiceClient : IDisposable
    {
        private readonly ICentralServiceTransport _transport;

        /// <summary>
        /// 使用指定配置初始化中心服务客户端。
        /// </summary>
        /// <param name="options">SDK 连接配置。</param>
        public CentralServiceServiceClient(CentralServiceSdkOptions options)
            : this(options, null)
        {
        }

        internal CentralServiceServiceClient(CentralServiceSdkOptions options, Func<DateTimeOffset>? utcNowProvider)
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
        }

        /// <summary>
        /// 向中心服务注册当前服务实例。
        /// </summary>
        public ServiceRegistrationResponse Register(ServiceRegistrationRequest request)
        {
            if (request == null) throw new ArgumentNullException("request");

            var transport = Send("POST", "/api/Service/register", CentralServiceJson.Serialize(request));
            if (!IsSuccess(transport.StatusCode))
            {
                throw CreateParsedError("POST", transport, CentralServiceErrorParser.Parse("POST", transport.Url, transport.StatusCode, transport.Body));
            }

            var parsed = CentralServiceJson.Deserialize<ApiResponse<ServiceRegistrationResponse>>(transport.Body);
            if (parsed == null)
            {
                throw CreateParsedError(
                    "POST",
                    transport,
                    new CentralServiceError(
                        transport.StatusCode,
                        "POST",
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
                    "POST",
                    transport,
                    new CentralServiceError(
                        transport.StatusCode,
                        "POST",
                        transport.Url,
                        CentralServiceErrorKind.ApiResponse,
                        parsed.ErrorMessage,
                        parsed.ErrorCode,
                        parsed.ErrorKey,
                        transport.Body));
            }

            return parsed.Data;
        }

        /// <summary>
        /// 向中心服务上报指定服务实例的心跳。
        /// </summary>
        public void Heartbeat(string serviceId)
        {
            if (string.IsNullOrWhiteSpace(serviceId)) throw new ArgumentNullException("serviceId");

            var transport = Send(
                "POST",
                "/api/Service/heartbeat",
                CentralServiceJson.Serialize(new ServiceHeartbeatRequest { Id = serviceId }));

            if (!IsSuccess(transport.StatusCode))
            {
                throw CreateParsedError("POST", transport, CentralServiceErrorParser.Parse("POST", transport.Url, transport.StatusCode, transport.Body));
            }

            var parsed = CentralServiceJson.Deserialize<ApiResponse<object>>(transport.Body);
            if (parsed != null && !parsed.Success)
            {
                throw CreateParsedError(
                    "POST",
                    transport,
                    new CentralServiceError(
                        transport.StatusCode,
                        "POST",
                        transport.Url,
                        CentralServiceErrorKind.ApiResponse,
                        parsed.ErrorMessage,
                        parsed.ErrorCode,
                        parsed.ErrorKey,
                        transport.Body));
            }
        }

        /// <summary>
        /// 从中心服务注销指定服务实例。
        /// </summary>
        public void Deregister(string serviceId)
        {
            if (string.IsNullOrWhiteSpace(serviceId)) throw new ArgumentNullException("serviceId");

            var path = "/api/Service/deregister/" + Uri.EscapeDataString(serviceId);
            var transport = Send("DELETE", path, null);
            if (!IsSuccess(transport.StatusCode))
            {
                throw CreateParsedError("DELETE", transport, CentralServiceErrorParser.Parse("DELETE", transport.Url, transport.StatusCode, transport.Body));
            }

            var parsed = CentralServiceJson.Deserialize<ApiResponse<object>>(transport.Body);
            if (parsed != null && !parsed.Success)
            {
                throw CreateParsedError(
                    "DELETE",
                    transport,
                    new CentralServiceError(
                        transport.StatusCode,
                        "DELETE",
                        transport.Url,
                        CentralServiceErrorKind.ApiResponse,
                        parsed.ErrorMessage,
                        parsed.ErrorCode,
                        parsed.ErrorKey,
                        transport.Body));
            }
        }

        /// <summary>
        /// 释放客户端持有的资源。
        /// </summary>
        public void Dispose()
        {
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
                $"端点={transport.BaseUrl}",
                $"尝试={transport.Attempt}/{transport.MaxAttempts}",
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
