using System.Net.WebSockets;
using System.Text;
using CentralService.Service;
using Microsoft.AspNetCore.TestHost;

namespace CentralService.Tests;

/// <summary>
/// 用于集成测试的“假服务”心跳 WebSocket 客户端：
/// 连接中心服务的心跳通道，并在收到心跳请求时自动回复。
/// </summary>
internal sealed class ServiceHeartbeatWebSocketTestClient : IAsyncDisposable
{
    private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly WebSocket _webSocket;
    private readonly CancellationTokenSource _cts;
    private readonly Task _runTask;
    private readonly TaskCompletionSource<DateTimeOffset> _firstHeartbeatHandled =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private ServiceHeartbeatWebSocketTestClient(WebSocket webSocket, CancellationToken externalCancellationToken)
    {
        _webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
        _cts = CancellationTokenSource.CreateLinkedTokenSource(externalCancellationToken);
        _runTask = Task.Run(() => RunAsync(_cts.Token));
    }

    /// <summary>
    /// 等待首次心跳请求完成处理（收到请求并已发送响应）。
    /// </summary>
    public Task<DateTimeOffset> FirstHeartbeatHandled => _firstHeartbeatHandled.Task;

    public static async Task<ServiceHeartbeatWebSocketTestClient> StartAsync(
        CentralServiceWebApplicationFactory factory,
        string serviceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceId);

        var wsClient = factory.Server.CreateWebSocketClient();
        var baseUri = factory.Server.BaseAddress;
        var scheme = string.Equals(baseUri.Scheme, "https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws";

        var wsUri = new UriBuilder(baseUri)
        {
            Scheme = scheme,
            Path = CentralServiceHeartbeatWebSocketProtocol.HeartbeatWebSocketPath,
            Query = CentralServiceHeartbeatWebSocketProtocol.ServiceIdQueryKey + "=" + Uri.EscapeDataString(serviceId),
        }.Uri;

        var socket = await wsClient.ConnectAsync(wsUri, cancellationToken);
        return new ServiceHeartbeatWebSocketTestClient(socket, cancellationToken);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4 * 1024];

        try
        {
            while (!cancellationToken.IsCancellationRequested && _webSocket.State == WebSocketState.Open)
            {
                var message = await ReceiveTextMessageAsync(_webSocket, buffer, cancellationToken);
                if (message == null)
                {
                    return;
                }

                if (!string.Equals(message, CentralServiceHeartbeatWebSocketProtocol.HeartbeatRequestMessage, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                await SendTextMessageAsync(
                    _webSocket,
                    CentralServiceHeartbeatWebSocketProtocol.HeartbeatResponseMessage,
                    cancellationToken);

                _firstHeartbeatHandled.TrySetResult(DateTimeOffset.UtcNow);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch
        {
            // 测试辅助类不向外抛出通信异常，避免影响断言逻辑；调用方可通过中心服务侧行为判断。
        }
        finally
        {
            _firstHeartbeatHandled.TrySetCanceled(cancellationToken);
        }
    }

    private static async Task<string?> ReceiveTextMessageAsync(WebSocket webSocket, byte[] buffer, CancellationToken cancellationToken)
    {
        WebSocketReceiveResult result;
        var builder = new StringBuilder();

        do
        {
            result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            if (result.MessageType != WebSocketMessageType.Text)
            {
                return null;
            }

            if (result.Count > 0)
            {
                builder.Append(Utf8NoBom.GetString(buffer, 0, result.Count));
            }
        } while (!result.EndOfMessage);

        return builder.ToString();
    }

    private static Task SendTextMessageAsync(WebSocket webSocket, string message, CancellationToken cancellationToken)
    {
        var payload = Utf8NoBom.GetBytes(message ?? string.Empty);
        return webSocket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _cts.Cancel();
        }
        catch
        {
        }

        try
        {
            if (_webSocket.State == WebSocketState.Open)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "test shutdown", CancellationToken.None);
            }
        }
        catch
        {
        }

        try
        {
            await _runTask;
        }
        catch
        {
        }

        _cts.Dispose();
        _webSocket.Dispose();
    }
}

