using System;
using System.IO;
using System.Net;
using System.Text;

namespace CentralService.Client.Internal
{
    /// <summary>
    /// 对 <see cref="HttpWebRequest"/> 的最小封装，用于发送中心服务请求。
    /// </summary>
    internal sealed class CentralServiceHttpClient : IDisposable
    {
        private readonly string _baseUrl;
        private readonly TimeSpan _timeout;

        /// <summary>
        /// 使用基础地址、超时和证书校验选项创建 HTTP 客户端。
        /// </summary>
        /// <param name="baseUrl">中心服务根地址。</param>
        /// <param name="timeout">请求超时时间。</param>
        /// <param name="ignoreSslErrors">是否忽略 SSL 证书错误。</param>
        public CentralServiceHttpClient(string baseUrl, TimeSpan timeout, bool ignoreSslErrors)
        {
            _baseUrl = (baseUrl ?? string.Empty).TrimEnd('/');
            _timeout = timeout;

            if (ignoreSslErrors)
            {
                // 旧版 HttpWebRequest 只能通过全局证书回调忽略校验，SDK 保持该行为以兼容现有调用方式。
                ServicePointManager.ServerCertificateValidationCallback += delegate { return true; };
            }
        }

        /// <summary>
        /// 将相对路径拼接为完整请求地址。
        /// </summary>
        /// <param name="path">相对路径或空值。</param>
        /// <returns>拼接后的完整地址。</returns>
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
        /// 发送 HTTP 请求并返回状态码与正文。
        /// </summary>
        /// <param name="method">HTTP 方法。</param>
        /// <param name="url">完整请求地址。</param>
        /// <param name="jsonBody">可选的 JSON 请求体。</param>
        /// <returns>底层 HTTP 响应包装对象。</returns>
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

                using (resp)
                using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    // 服务端即使返回 4xx/5xx，也可能携带结构化 JSON 错误体，因此要继续把正文交给上层解析。
                    var body = reader.ReadToEnd();
                    return new CentralServiceHttpResponse(resp.StatusCode, body);
                }
            }
        }

        /// <summary>
        /// 释放客户端资源。
        /// </summary>
        public void Dispose()
        {
        }
    }
}
