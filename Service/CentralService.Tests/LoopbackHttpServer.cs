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

    public LoopbackHttpServer(Func<LoopbackHttpRequest, int, LoopbackHttpResponse> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handler = handler;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _acceptLoopTask = Task.Run(AcceptLoopAsync);
    }

    public int Port { get; }

    public string BaseUrl => $"http://127.0.0.1:{Port}";

    public int RequestCount => Volatile.Read(ref _requestCount);

    public static int GetUnusedPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

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

internal sealed class LoopbackHttpRequest
{
    public LoopbackHttpRequest(string method, string pathAndQuery, string body)
    {
        Method = method ?? string.Empty;
        PathAndQuery = pathAndQuery ?? string.Empty;
        Body = body ?? string.Empty;
    }

    public string Method { get; }

    public string PathAndQuery { get; }

    public string Body { get; }
}

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

    public HttpStatusCode StatusCode { get; }

    public string ContentType { get; }

    public string Body { get; }

    public bool CloseConnectionWithoutResponse { get; }

    public int DelayBeforeCloseMilliseconds { get; private set; }

    public static LoopbackHttpResponse Json(HttpStatusCode statusCode, string body)
    {
        return new LoopbackHttpResponse(statusCode, "application/json; charset=utf-8", body, false);
    }

    public static LoopbackHttpResponse PlainText(HttpStatusCode statusCode, string body)
    {
        return new LoopbackHttpResponse(statusCode, "text/plain; charset=utf-8", body, false);
    }

    public static LoopbackHttpResponse CloseConnection()
    {
        return new LoopbackHttpResponse(HttpStatusCode.ServiceUnavailable, "text/plain", string.Empty, true);
    }

    public static LoopbackHttpResponse HangConnection(int delayBeforeCloseMilliseconds)
    {
        return new LoopbackHttpResponse(HttpStatusCode.ServiceUnavailable, "text/plain", string.Empty, true)
        {
            DelayBeforeCloseMilliseconds = Math.Max(1, delayBeforeCloseMilliseconds),
        };
    }

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
