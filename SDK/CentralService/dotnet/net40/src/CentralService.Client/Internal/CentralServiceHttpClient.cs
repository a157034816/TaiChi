using System;
using System.IO;
using System.Net;
using System.Text;

namespace CentralService.Client.Internal
{
    /// <summary>
    /// 封装面向中心服务的底层 HTTP 请求发送逻辑。
    /// </summary>
    internal sealed class CentralServiceHttpClient : IDisposable
    {
        private readonly string _baseUrl;
        private readonly TimeSpan _timeout;

        /// <summary>
        /// 使用指定连接参数初始化底层 HTTP 客户端。
        /// </summary>
        /// <param name="baseUrl">中心服务根地址。</param>
        /// <param name="timeout">请求超时时间。</param>
        /// <param name="ignoreSslErrors">是否忽略服务器证书错误。</param>
        public CentralServiceHttpClient(string baseUrl, TimeSpan timeout, bool ignoreSslErrors)
        {
            _baseUrl = (baseUrl ?? string.Empty).TrimEnd('/');
            _timeout = timeout;

            if (ignoreSslErrors)
            {
                // 兼容调试或自签证书环境；这里沿用全局回调是为了保持现有行为不变。
                ServicePointManager.ServerCertificateValidationCallback += delegate { return true; };
            }
        }

        /// <summary>
        /// 将相对路径拼接为完整请求地址。
        /// </summary>
        /// <param name="path">相对路径或带前导斜杠的路径。</param>
        /// <returns>完整请求地址。</returns>
        public string BuildUrl(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return _baseUrl;
            }

            // 统一补齐前导斜杠，避免调用方混用相对路径和绝对路径片段时生成双重规则。
            if (!path.StartsWith("/", StringComparison.Ordinal))
            {
                path = "/" + path;
            }

            return _baseUrl + path;
        }

        /// <summary>
        /// 发送 HTTP 请求并返回状态码与响应正文。
        /// </summary>
        /// <param name="method">HTTP 方法。</param>
        /// <param name="url">完整请求地址。</param>
        /// <param name="jsonBody">JSON 请求体；为空时不发送正文。</param>
        /// <returns>状态码与响应正文。</returns>
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

                // 非 2xx 响应会通过 WebException 抛出，但 SDK 仍然需要读取响应正文交给上层统一解析业务错误。
                using (resp)
                using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    var body = reader.ReadToEnd();
                    return new CentralServiceHttpResponse(resp.StatusCode, body);
                }
            }
        }

        /// <summary>
        /// 释放客户端资源。
        /// </summary>
        /// <remarks>当前实现按请求创建 <see cref="HttpWebRequest"/>，因此此处无需额外释放托管资源。</remarks>
        public void Dispose()
        {
        }
    }
}
