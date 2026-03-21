using System.Net;
using System.Net.Sockets;
using System.Text;

namespace CentralService.Tests;

/// <summary>
/// 提供一个极简的回环 HTTP 服务器，用于 SDK 级别的真实网络行为测试。
/// </summary>
internal sealed class LoopbackHttpServer : IDisposable
{
    private readonly Func<LoopbackHttpRequest, int, LoopbackHttpResponse> _handler;
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _acceptLoopTask;
    private int _requestCount;

    /// <summary>
    /// 创建回环服务器并立即开始监听。
    /// </summary>
    /// <param name="handler">
    /// 请求处理器：入参为解析后的请求对象与递增的请求序号，返回要发送的响应。
    /// </param>
    public LoopbackHttpServer(Func<LoopbackHttpRequest, int, LoopbackHttpResponse> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handler = handler;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _acceptLoopTask = Task.Run(AcceptLoopAsync);
    }

    /// <summary>
    /// 实际监听端口（系统分配）。
    /// </summary>
    public int Port { get; }

    /// <summary>
    /// 服务器 BaseUrl（固定使用 127.0.0.1）。
    /// </summary>
    public string BaseUrl => $"http://127.0.0.1:{Port}";

    /// <summary>
    /// 已处理的请求数量（用于断言重试/故障转移次数）。
    /// </summary>
    public int RequestCount => Volatile.Read(ref _requestCount);

    /// <summary>
    /// 获取一个当前未被占用的本地端口。
    /// </summary>
    /// <returns>可用端口号。</returns>
    public static int GetUnusedPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    /// <summary>
    /// 停止监听并释放资源。
    /// </summary>
    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
        try
        {
            _acceptLoopTask.GetAwaiter().GetResult();
        }
        catch
        {
        }

        _cts.Dispose();
    }

    private async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            TcpClient? client = null;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_cts.Token);
                _ = Task.Run(() => HandleClientAsync(client), _cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch
            {
                client?.Dispose();
                if (_cts.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        using (var stream = client.GetStream())
        {
            var request = await ReadRequestAsync(stream, _cts.Token);
            if (request == null)
            {
                return;
            }

            var index = Interlocked.Increment(ref _requestCount);
            var response = _handler(request, index);
            if (response.DelayBeforeCloseMilliseconds > 0)
            {
                await Task.Delay(response.DelayBeforeCloseMilliseconds, _cts.Token);
            }

            if (response.CloseConnectionWithoutResponse)
            {
                return;
            }

            var payload = response.ToPayload();
            await stream.WriteAsync(payload, 0, payload.Length, _cts.Token);
            await stream.FlushAsync(_cts.Token);
        }
    }

    private static async Task<LoopbackHttpRequest?> ReadRequestAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var buffer = new List<byte>();
        var temp = new byte[1024];
        var headerEnd = -1;

        while (headerEnd < 0)
        {
            var read = await stream.ReadAsync(temp.AsMemory(0, temp.Length), cancellationToken);
            if (read == 0)
            {
                return null;
            }

            buffer.AddRange(temp.Take(read));
            headerEnd = FindHeaderEnd(buffer);
        }

        var contentLength = GetContentLength(buffer, headerEnd);
        while (buffer.Count - headerEnd - 4 < contentLength)
        {
            var read = await stream.ReadAsync(temp.AsMemory(0, temp.Length), cancellationToken);
            if (read == 0)
            {
                break;
            }

            buffer.AddRange(temp.Take(read));
        }

        var data = buffer.ToArray();
        var headerText = Encoding.UTF8.GetString(data, 0, headerEnd);
        var body = contentLength > 0
            ? Encoding.UTF8.GetString(data, headerEnd + 4, Math.Min(contentLength, data.Length - headerEnd - 4))
            : string.Empty;
        var requestLine = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None)[0];
        var segments = requestLine.Split(' ');
        return new LoopbackHttpRequest(
            segments.Length > 0 ? segments[0] : string.Empty,
            segments.Length > 1 ? segments[1] : "/",
            body);
    }

    private static int FindHeaderEnd(List<byte> buffer)
    {
        for (var i = 0; i <= buffer.Count - 4; i++)
        {
            if (buffer[i] == '\r'
                && buffer[i + 1] == '\n'
                && buffer[i + 2] == '\r'
                && buffer[i + 3] == '\n')
            {
                return i;
            }
        }

        return -1;
    }

    private static int GetContentLength(List<byte> buffer, int headerEnd)
    {
        var headerText = Encoding.UTF8.GetString(buffer.Take(headerEnd).ToArray());
        var lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(line.Substring("Content-Length:".Length).Trim(), out var contentLength))
            {
                return Math.Max(0, contentLength);
            }
        }

        return 0;
    }
}

/// <summary>
/// 回环服务器解析得到的请求模型（仅包含测试所需的最小字段）。
/// </summary>
internal sealed class LoopbackHttpRequest
{
    /// <summary>
    /// 创建请求模型。
    /// </summary>
    /// <param name="method">HTTP 方法。</param>
    /// <param name="pathAndQuery">路径与查询字符串。</param>
    /// <param name="body">请求体（UTF-8 文本）。</param>
    public LoopbackHttpRequest(string method, string pathAndQuery, string body)
    {
        Method = method ?? string.Empty;
        PathAndQuery = pathAndQuery ?? string.Empty;
        Body = body ?? string.Empty;
    }

    /// <summary>
    /// HTTP 方法。
    /// </summary>
    public string Method { get; }

    /// <summary>
    /// 路径与查询字符串（例如 <c>/api/Service/list?name=foo</c>）。
    /// </summary>
    public string PathAndQuery { get; }

    /// <summary>
    /// 请求体（UTF-8 文本）。
    /// </summary>
    public string Body { get; }
}

/// <summary>
/// 回环服务器响应模型（仅包含测试所需的最小字段）。
/// </summary>
internal sealed class LoopbackHttpResponse
{
    private LoopbackHttpResponse(HttpStatusCode statusCode, string contentType, string body, bool closeConnectionWithoutResponse)
    {
        StatusCode = statusCode;
        ContentType = contentType;
        Body = body ?? string.Empty;
        CloseConnectionWithoutResponse = closeConnectionWithoutResponse;
        DelayBeforeCloseMilliseconds = 0;
    }

    /// <summary>
    /// HTTP 状态码。
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// Content-Type 响应头值。
    /// </summary>
    public string ContentType { get; }

    /// <summary>
    /// 响应体（UTF-8 文本）。
    /// </summary>
    public string Body { get; }

    /// <summary>
    /// 是否直接关闭连接而不返回任何响应（用于模拟传输层异常）。
    /// </summary>
    public bool CloseConnectionWithoutResponse { get; }

    /// <summary>
    /// 关闭连接前的延迟（毫秒）。用于模拟“长时间无响应后断开”。
    /// </summary>
    public int DelayBeforeCloseMilliseconds { get; private set; }

    /// <summary>
    /// 构造 JSON 响应。
    /// </summary>
    /// <param name="statusCode">状态码。</param>
    /// <param name="body">响应体。</param>
    /// <returns>响应模型。</returns>
    public static LoopbackHttpResponse Json(HttpStatusCode statusCode, string body)
    {
        return new LoopbackHttpResponse(statusCode, "application/json; charset=utf-8", body, false);
    }

    /// <summary>
    /// 构造纯文本响应。
    /// </summary>
    /// <param name="statusCode">状态码。</param>
    /// <param name="body">响应体。</param>
    /// <returns>响应模型。</returns>
    public static LoopbackHttpResponse PlainText(HttpStatusCode statusCode, string body)
    {
        return new LoopbackHttpResponse(statusCode, "text/plain; charset=utf-8", body, false);
    }

    /// <summary>
    /// 直接关闭连接（不返回响应），用于模拟连接被对端断开。
    /// </summary>
    /// <returns>响应模型。</returns>
    public static LoopbackHttpResponse CloseConnection()
    {
        return new LoopbackHttpResponse(HttpStatusCode.ServiceUnavailable, "text/plain", string.Empty, true);
    }

    /// <summary>
    /// 延迟一段时间后关闭连接（不返回响应），用于模拟请求超时/卡死后断开。
    /// </summary>
    /// <param name="delayBeforeCloseMilliseconds">延迟毫秒数（至少 1ms）。</param>
    /// <returns>响应模型。</returns>
    public static LoopbackHttpResponse HangConnection(int delayBeforeCloseMilliseconds)
    {
        return new LoopbackHttpResponse(HttpStatusCode.ServiceUnavailable, "text/plain", string.Empty, true)
        {
            DelayBeforeCloseMilliseconds = Math.Max(1, delayBeforeCloseMilliseconds),
        };
    }

    /// <summary>
    /// 把响应模型编码为 HTTP/1.1 字节流（UTF-8 body，ASCII header）。
    /// </summary>
    /// <returns>可直接写入 Socket 的字节数组。</returns>
    public byte[] ToPayload()
    {
        var bodyBytes = Encoding.UTF8.GetBytes(Body);
        var header =
            $"HTTP/1.1 {(int)StatusCode} {StatusCode}\r\n" +
            $"Content-Type: {ContentType}\r\n" +
            $"Content-Length: {bodyBytes.Length}\r\n" +
            "Connection: close\r\n\r\n";
        var headerBytes = Encoding.ASCII.GetBytes(header);
        var payload = new byte[headerBytes.Length + bodyBytes.Length];
        Buffer.BlockCopy(headerBytes, 0, payload, 0, headerBytes.Length);
        Buffer.BlockCopy(bodyBytes, 0, payload, headerBytes.Length, bodyBytes.Length);
        return payload;
    }
}
