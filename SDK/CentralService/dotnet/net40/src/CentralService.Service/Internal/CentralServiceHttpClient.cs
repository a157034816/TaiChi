using System;
using System.IO;
using System.Net;
using System.Text;

namespace CentralService.Service.Internal
{
    /// <summary>
    /// 为服务注册侧 SDK 提供最小化的 JSON over HTTP 传输能力。
    /// </summary>
    /// <remarks>
    /// 该类型只负责发送请求并返回原始响应文本，响应体的业务解析由上层调用者决定。
    /// </remarks>
    internal sealed class CentralServiceHttpClient : IDisposable
    {
        private readonly string _baseUrl;
        private readonly TimeSpan _timeout;

        /// <summary>
        /// 创建底层 HTTP 传输层。
        /// </summary>
        /// <param name="baseUrl">中心服务根地址。</param>
        /// <param name="timeout">单次请求超时时间。</param>
        /// <param name="ignoreSslErrors">是否忽略 HTTPS 证书错误。</param>
        public CentralServiceHttpClient(string baseUrl, TimeSpan timeout, bool ignoreSslErrors)
        {
            _baseUrl = (baseUrl ?? string.Empty).TrimEnd('/');
            _timeout = timeout;

            if (ignoreSslErrors)
            {
                // net40 只能通过全局回调放宽证书校验，因此该设置会影响当前 AppDomain 中的后续 HTTPS 请求。
                ServicePointManager.ServerCertificateValidationCallback += delegate { return true; };
            }
        }

        /// <summary>
        /// 将 SDK 约定路径拼接为完整请求地址。
        /// </summary>
        /// <param name="path">相对或绝对风格的 API 路径。</param>
        /// <returns>完整请求地址。</returns>
        public string BuildUrl(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return _baseUrl;
            }

            if (!path.StartsWith("/", StringComparison.Ordinal))
            {
                path = "/" + path;
            }

            return _baseUrl + path;
        }

        /// <summary>
        /// 发送 JSON HTTP 请求并返回原始响应。
        /// </summary>
        /// <param name="method">HTTP 方法。</param>
        /// <param name="url">绝对请求地址。</param>
        /// <param name="jsonBody">JSON 请求体；无请求体时传入 <c>null</c>。</param>
        /// <returns>HTTP 状态码与原始响应体。</returns>
        public CentralServiceHttpResponse Send(string method, string url, string jsonBody)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = method;
            request.Accept = "application/json";
            request.Timeout = (int)_timeout.TotalMilliseconds;
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
                    var body = reader.ReadToEnd();
                    return new CentralServiceHttpResponse(response.StatusCode, body);
                }
            }
            catch (WebException ex)
            {
                var resp = ex.Response as HttpWebResponse;
                if (resp == null)
                {
                    throw;
                }

                // 对于带有 HTTP 状态码的失败响应，仍然把响应体交给上层统一转换为 SDK 错误对象。
                using (resp)
                using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    var body = reader.ReadToEnd();
                    return new CentralServiceHttpResponse(resp.StatusCode, body);
                }
            }
        }

        /// <summary>
        /// 释放传输层资源。
        /// </summary>
        /// <remarks>
        /// 当前实现没有持有额外的非托管资源，保留该方法以对齐 SDK 客户端的释放语义。
        /// </remarks>
        public void Dispose()
        {
        }
    }
}
