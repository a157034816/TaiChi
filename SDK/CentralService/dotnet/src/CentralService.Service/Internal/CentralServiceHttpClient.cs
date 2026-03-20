using System;
using System.IO;
using System.Net;
using System.Text;

namespace CentralService.Service.Internal
{
    internal sealed class CentralServiceHttpClient : IDisposable
    {
        private readonly string _baseUrl;
        private readonly TimeSpan _timeout;

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

        public void Dispose()
        {
        }
    }
}
