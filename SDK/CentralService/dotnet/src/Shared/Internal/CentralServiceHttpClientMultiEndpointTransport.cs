using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CentralService.Shared.Internal
{
    internal sealed class CentralServiceHttpClientMultiEndpointTransport : ICentralServiceTransport
    {
        private readonly IReadOnlyList<CentralServiceTransportEndpoint> _endpoints;
        private readonly TimeSpan _timeout;
        private readonly Func<DateTimeOffset> _utcNowProvider;
        private readonly HttpMessageHandler _handler;

        public CentralServiceHttpClientMultiEndpointTransport(
            IReadOnlyList<CentralServiceTransportEndpoint> endpoints,
            TimeSpan timeout,
            HttpMessageHandler handler,
            Func<DateTimeOffset>? utcNowProvider = null)
        {
            if (endpoints == null) throw new ArgumentNullException("endpoints");
            if (endpoints.Count == 0) throw new ArgumentException("至少需要一个中心服务端点。", "endpoints");
            if (handler == null) throw new ArgumentNullException("handler");

            _endpoints = endpoints
                .OrderBy(x => x.Priority)
                .ThenBy(x => x.Order)
                .ToArray();
            _timeout = timeout;
            _handler = handler;
            _utcNowProvider = utcNowProvider ?? (() => DateTimeOffset.UtcNow);
        }

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
                        var response = SendCore(method, url, jsonBody, _timeout, _handler);
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
            return exception is HttpRequestException
                   || exception is TaskCanceledException
                   || exception is OperationCanceledException;
        }

        private static CentralServiceTransportHttpResponse SendCore(
            string method,
            string url,
            string? jsonBody,
            TimeSpan timeout,
            HttpMessageHandler handler)
        {
            using var client = new HttpClient(handler, disposeHandler: false);
            using var request = new HttpRequestMessage(new HttpMethod(method), url);
            request.Headers.Accept.ParseAdd("application/json");

            if (!string.IsNullOrEmpty(jsonBody))
            {
                request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            }

            using var cts = new CancellationTokenSource(timeout);
            var response = client.SendAsync(request, cts.Token).GetAwaiter().GetResult();
            var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return new CentralServiceTransportHttpResponse(response.StatusCode, body);
        }
    }
}

