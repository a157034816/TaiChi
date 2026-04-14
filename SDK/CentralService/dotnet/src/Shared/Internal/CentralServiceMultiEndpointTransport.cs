using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace CentralService.Shared.Internal
{
    /// <summary>
    /// 负责在多个中心服务端点之间按优先级切换并处理单端点熔断。
    /// </summary>
    internal sealed class CentralServiceMultiEndpointTransport : ICentralServiceTransport
    {
        private readonly IReadOnlyList<CentralServiceTransportEndpoint> _endpoints;
        private readonly TimeSpan _timeout;
        private readonly Func<DateTimeOffset> _utcNowProvider;

        public CentralServiceMultiEndpointTransport(
            IReadOnlyList<CentralServiceTransportEndpoint> endpoints,
            TimeSpan timeout,
            bool ignoreSslErrors,
            Func<DateTimeOffset>? utcNowProvider = null)
        {
            if (endpoints == null) throw new ArgumentNullException("endpoints");
            if (endpoints.Count == 0) throw new ArgumentException("至少需要一个中心服务端点。", "endpoints");

            _endpoints = endpoints
                .OrderBy(x => x.Priority)
                .ThenBy(x => x.Order)
                .ToArray();
            _timeout = timeout;
            _utcNowProvider = utcNowProvider ?? (() => DateTimeOffset.UtcNow);

            if (ignoreSslErrors)
            {
                // 保持与既有 SDK 行为一致：在需要忽略证书错误时使用全局回调。
                ServicePointManager.ServerCertificateValidationCallback += delegate { return true; };
            }
        }

        /// <summary>
        /// 发送一次带多端点切换能力的 HTTP 请求。
        /// </summary>
        public CentralServiceTransportResult Send(string method, string path, string? jsonBody)
        {
            var skippedEndpoints = new List<string>();
            var failureSummaries = new List<string>();
            Exception? lastException = null;
            string? lastUrl = null;

            foreach (var endpoint in _endpoints)
            {
                var now = _utcNowProvider();
                if (endpoint.CircuitBreaker != null
                    && !endpoint.CircuitBreaker.TryAllowRequest(now, out var skipReason))
                {
                    skippedEndpoints.Add($"{endpoint.BaseUrl}（{skipReason}）");
                    continue;
                }

                for (var attempt = 1; attempt <= endpoint.MaxAttempts; attempt++)
                {
                    var url = endpoint.BuildUrl(path);
                    lastUrl = url;

                    try
                    {
                        var response = SendCore(method, url, jsonBody, _timeout);
                        endpoint.CircuitBreaker?.ReportSuccess();

                        return new CentralServiceTransportResult(
                            endpoint.BaseUrl,
                            url,
                            attempt,
                            endpoint.MaxAttempts,
                            response.StatusCode,
                            response.Body,
                            skippedEndpoints);
                    }
                    catch (Exception ex) when (IsTransportException(ex))
                    {
                        lastException = ex;
                        endpoint.CircuitBreaker?.ReportFailure(_utcNowProvider());
                        failureSummaries.Add(
                            $"{endpoint.BaseUrl} 第 {attempt}/{endpoint.MaxAttempts} 次失败：{ex.GetType().Name}: {ex.Message}");
                    }
                }
            }

            throw CentralServiceTransportExhaustedException.Create(method, path, lastUrl, skippedEndpoints, failureSummaries, lastException);
        }

        private static bool IsTransportException(Exception exception)
        {
            if (exception is WebException webException)
            {
                return webException.Response == null;
            }

            return exception is IOException
                || exception is InvalidOperationException
                || exception is NotSupportedException;
        }

        private static CentralServiceTransportHttpResponse SendCore(string method, string url, string? jsonBody, TimeSpan timeout)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = method;
            request.Accept = "application/json";
            request.Timeout = (int)timeout.TotalMilliseconds;
            request.ReadWriteTimeout = request.Timeout;

            if (!string.IsNullOrEmpty(jsonBody))
            {
                var bytes = Encoding.UTF8.GetBytes(jsonBody);
                request.ContentType = "application/json; charset=utf-8";
                request.ContentLength = bytes.Length;
                using (var stream = request.GetRequestStream())
                {
                    stream.Write(bytes, 0, bytes.Length);
                }
            }

            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    return new CentralServiceTransportHttpResponse(response.StatusCode, reader.ReadToEnd());
                }
            }
            catch (WebException ex)
            {
                var response = ex.Response as HttpWebResponse;
                if (response == null)
                {
                    throw;
                }

                using (response)
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    return new CentralServiceTransportHttpResponse(response.StatusCode, reader.ReadToEnd());
                }
            }
        }
    }

    /// <summary>
    /// 表示一次多端点传输成功后的原始 HTTP 结果。
    /// </summary>
    internal sealed class CentralServiceTransportResult
    {
        public CentralServiceTransportResult(
            string baseUrl,
            string url,
            int attempt,
            int maxAttempts,
            HttpStatusCode statusCode,
            string body,
            IReadOnlyList<string> skippedEndpoints)
        {
            BaseUrl = baseUrl ?? string.Empty;
            Url = url ?? string.Empty;
            Attempt = attempt;
            MaxAttempts = maxAttempts;
            StatusCode = statusCode;
            Body = body ?? string.Empty;
            SkippedEndpoints = skippedEndpoints ?? Array.Empty<string>();
        }

        public string BaseUrl { get; }

        public string Url { get; }

        public int Attempt { get; }

        public int MaxAttempts { get; }

        public HttpStatusCode StatusCode { get; }

        public string Body { get; }

        public IReadOnlyList<string> SkippedEndpoints { get; }
    }

    /// <summary>
    /// 表示所有候选端点均因传输异常或熔断跳过而无法完成请求。
    /// </summary>
    internal sealed class CentralServiceTransportExhaustedException : Exception
    {
        private CentralServiceTransportExhaustedException(
            string method,
            string path,
            string? lastUrl,
            string message,
            string rawDetail,
            Exception? innerException)
            : base(message, innerException)
        {
            Method = method ?? string.Empty;
            Path = path ?? string.Empty;
            LastUrl = lastUrl ?? string.Empty;
            RawDetail = rawDetail ?? string.Empty;
        }

        public string Method { get; }

        public string Path { get; }

        public string LastUrl { get; }

        public string RawDetail { get; }

        public static CentralServiceTransportExhaustedException Create(
            string method,
            string path,
            string? lastUrl,
            IReadOnlyList<string> skippedEndpoints,
            IReadOnlyList<string> failureSummaries,
            Exception? lastException)
        {
            var segments = new List<string>();
            if (skippedEndpoints.Count > 0)
            {
                segments.Add("跳过端点: " + string.Join("; ", skippedEndpoints));
            }

            if (failureSummaries.Count > 0)
            {
                segments.Add("失败详情: " + string.Join("; ", failureSummaries));
            }

            if (segments.Count == 0)
            {
                segments.Add("未找到可用的中心服务端点。");
            }

            var summary = string.Join(" | ", segments);
            var message = "中心服务调用失败，所有可用端点均已耗尽。 " + summary;
            return new CentralServiceTransportExhaustedException(method, path, lastUrl, message, summary, lastException);
        }
    }

    internal sealed class CentralServiceTransportHttpResponse
    {
        public CentralServiceTransportHttpResponse(HttpStatusCode statusCode, string body)
        {
            StatusCode = statusCode;
            Body = body ?? string.Empty;
        }

        public HttpStatusCode StatusCode { get; }

        public string Body { get; }
    }
}
