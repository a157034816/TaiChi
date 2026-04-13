using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CentralService.Service
{
    /// <summary>
    /// 周边服务侧的 WebSocket 心跳客户端：
    /// 注册成功后主动连接中心服务，并在收到心跳请求时进行响应。
    /// </summary>
    public sealed class CentralServiceHeartbeatWebSocketClient
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private readonly Uri _heartbeatUri;
        private readonly bool _ignoreSslErrors;
        private readonly TimeSpan _reconnectDelay;

        /// <summary>
        /// 获取最近一次从中心服务收到消息的时间（UTC）。
        /// </summary>
        public DateTimeOffset LastReceivedUtc { get; private set; } = DateTimeOffset.MinValue;

        /// <summary>
        /// 获取当前是否处于已连接状态（仅表示连接建立成功，不代表业务心跳一定正常）。
        /// </summary>
        public bool IsConnected { get; private set; }

        /// <summary>
        /// 当收到中心服务心跳请求消息时触发（已在内部完成心跳响应发送）。
        /// </summary>
        public event Action<DateTimeOffset>? HeartbeatRequested;

        /// <summary>
        /// 初始化 WebSocket 心跳客户端。
        /// </summary>
        /// <param name="centralServiceBaseUrl">中心服务根地址（http/https 或 ws/wss）。</param>
        /// <param name="serviceId">服务实例标识（注册接口返回的 id）。</param>
        /// <param name="ignoreSslErrors">是否忽略 SSL 证书错误（仅对 wss 生效，且需要运行时支持）。</param>
        /// <param name="reconnectDelay">断线后重连延迟；为空时默认 3 秒。</param>
        public CentralServiceHeartbeatWebSocketClient(
            string centralServiceBaseUrl,
            string serviceId,
            bool ignoreSslErrors,
            TimeSpan? reconnectDelay = null)
        {
            if (string.IsNullOrWhiteSpace(centralServiceBaseUrl)) throw new ArgumentNullException(nameof(centralServiceBaseUrl));
            if (string.IsNullOrWhiteSpace(serviceId)) throw new ArgumentNullException(nameof(serviceId));

            _heartbeatUri = BuildHeartbeatUri(centralServiceBaseUrl, serviceId);
            _ignoreSslErrors = ignoreSslErrors;
            _reconnectDelay = reconnectDelay ?? TimeSpan.FromSeconds(3);
        }

        /// <summary>
        /// 启动并保持心跳 WebSocket 连接；该方法会在断线后自动重连，直到取消或发生不可恢复错误。
        /// </summary>
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using var webSocket = new ClientWebSocket();
                ConfigureWebSocket(webSocket);

                try
                {
                    await webSocket.ConnectAsync(_heartbeatUri, cancellationToken).ConfigureAwait(false);
                    IsConnected = true;
                    LastReceivedUtc = DateTimeOffset.UtcNow;

                    await ReceiveLoopAsync(webSocket, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch
                {
                    // 连接或通信异常：进入重连流程（由调用方决定是否记录日志）。
                }
                finally
                {
                    IsConnected = false;
                }

                await DelayReconnectAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        private void ConfigureWebSocket(ClientWebSocket webSocket)
        {
            // 连接级别保活：避免 NAT/代理回收空闲连接；业务心跳由中心服务主动下发。
            webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);

#if NET6_0_OR_GREATER
            if (_ignoreSslErrors && _heartbeatUri.Scheme.Equals("wss", StringComparison.OrdinalIgnoreCase))
            {
                webSocket.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
            }
#endif
        }

        private async Task ReceiveLoopAsync(ClientWebSocket webSocket, CancellationToken cancellationToken)
        {
            var buffer = new byte[4 * 1024];
            while (!cancellationToken.IsCancellationRequested && webSocket.State == WebSocketState.Open)
            {
                var message = await ReceiveTextMessageAsync(webSocket, buffer, cancellationToken).ConfigureAwait(false);
                if (message == null)
                {
                    // 连接关闭或收到非文本消息，结束循环由外层触发重连。
                    return;
                }

                LastReceivedUtc = DateTimeOffset.UtcNow;

                if (string.Equals(message, CentralServiceHeartbeatWebSocketProtocol.HeartbeatRequestMessage, StringComparison.OrdinalIgnoreCase))
                {
                    await SendTextMessageAsync(
                            webSocket,
                            CentralServiceHeartbeatWebSocketProtocol.HeartbeatResponseMessage,
                            cancellationToken)
                        .ConfigureAwait(false);

                    HeartbeatRequested?.Invoke(LastReceivedUtc);
                }
            }
        }

        private static async Task<string?> ReceiveTextMessageAsync(ClientWebSocket webSocket, byte[] buffer, CancellationToken cancellationToken)
        {
            WebSocketReceiveResult result;
            var builder = new StringBuilder();

            do
            {
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return null;
                }

                if (result.MessageType != WebSocketMessageType.Text)
                {
                    // 当前协议仅使用 Text；其他类型忽略并视为连接异常。
                    return null;
                }

                if (result.Count > 0)
                {
                    builder.Append(Utf8NoBom.GetString(buffer, 0, result.Count));
                }
            } while (!result.EndOfMessage);

            return builder.ToString();
        }

        private static Task SendTextMessageAsync(ClientWebSocket webSocket, string message, CancellationToken cancellationToken)
        {
            var payload = Utf8NoBom.GetBytes(message ?? string.Empty);
            return webSocket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, cancellationToken);
        }

        private async Task DelayReconnectAsync(CancellationToken cancellationToken)
        {
            if (_reconnectDelay <= TimeSpan.Zero)
            {
                return;
            }

            try
            {
                await Task.Delay(_reconnectDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }

        private static Uri BuildHeartbeatUri(string centralServiceBaseUrl, string serviceId)
        {
            var baseUri = new Uri(centralServiceBaseUrl.Trim().TrimEnd('/'), UriKind.Absolute);
            var scheme = baseUri.Scheme;
            if (scheme.Equals("http", StringComparison.OrdinalIgnoreCase))
            {
                scheme = "ws";
            }
            else if (scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            {
                scheme = "wss";
            }

            var builder = new UriBuilder(baseUri)
            {
                Scheme = scheme,
                Path = CentralServiceHeartbeatWebSocketProtocol.HeartbeatWebSocketPath,
                Query = CentralServiceHeartbeatWebSocketProtocol.ServiceIdQueryKey + "=" + Uri.EscapeDataString(serviceId ?? string.Empty),
            };

            // UriBuilder 在修改 Scheme 后会重置 Port，为确保保持原端口，这里显式保留。
            if (baseUri.IsDefaultPort)
            {
                builder.Port = -1;
            }
            else
            {
                builder.Port = baseUri.Port;
            }

            return builder.Uri;
        }
    }
}
