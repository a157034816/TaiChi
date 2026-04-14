using CentralService.Internal;
using CentralService.Models;
using CentralService.Service;
using CentralService.Service.Models;
using CentralService.Services;
using CentralService.Services.ServiceCircuiting;
using ApiResponseFactory = CentralService.Internal.CentralServiceApiResponseFactory;
using RuntimeServiceInfo = CentralService.Models.ServiceInfo;
using ServiceApiListResponse = CentralService.Service.Models.ServiceListResponse;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;

/*
 * 这个控制器负责服务的其他服务的主动注册,退出注销,均衡负载,客户端自动定向
 */

namespace CentralService.Controllers
{
    /// <summary>
    /// 服务注册与发现控制器
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Produces("application/json")]
    public class ServiceController : ControllerBase
    {
        private readonly ServiceRegistry _serviceRegistry;
        private readonly ServiceCircuitJsonStore _serviceCircuitStore;
        private readonly ServiceCircuitTomlStore _serviceCircuitDefaults;
        private readonly ServiceAccessService _serviceAccessService;
        private readonly ILogger<ServiceController> _logger;

        public ServiceController(
            ServiceRegistry serviceRegistry,
            ServiceCircuitJsonStore serviceCircuitStore,
            ServiceCircuitTomlStore serviceCircuitDefaults,
            ServiceAccessService serviceAccessService,
            ILogger<ServiceController> logger)
        {
            _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
            _serviceCircuitStore = serviceCircuitStore ?? throw new ArgumentNullException(nameof(serviceCircuitStore));
            _serviceCircuitDefaults = serviceCircuitDefaults ?? throw new ArgumentNullException(nameof(serviceCircuitDefaults));
            _serviceAccessService = serviceAccessService ?? throw new ArgumentNullException(nameof(serviceAccessService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 服务注册
        /// </summary>
        [HttpPost("register")]
        [ProducesResponseType(typeof(ApiResponse<ServiceRegistrationResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Register([FromBody] ServiceRegistrationRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ApiResponseFactory.Error<object>("请求不能为空"));
                }

                if (string.IsNullOrEmpty(request.Name))
                {
                    return BadRequest(ApiResponseFactory.Error<object>("服务名称不能为空"));
                }

                var localIp = NormalizeServiceLocalIp(request);
                if (string.IsNullOrEmpty(localIp))
                {
                    return BadRequest(ApiResponseFactory.Error<object>("服务局域网IP不能为空"));
                }

                if (request.Port <= 0)
                {
                    return BadRequest(ApiResponseFactory.Error<object>("服务端口无效"));
                }

                if (!IsValidServiceType(request.ServiceType))
                {
                    return BadRequest(ApiResponseFactory.Error<object>("无效的服务类型"));
                }

                if (request.HeartbeatIntervalSeconds < 0)
                {
                    return BadRequest(ApiResponseFactory.Error<object>("心跳频率不能小于 0"));
                }

                var isReachable = await CheckHostPortReachableAsync(localIp, request.Port);
                if (!isReachable)
                {
                    _logger.LogWarning("无法连接到服务 {Name} 在 {Host}:{Port}", request.Name, localIp, request.Port);
                }

                var operatorIp = NormalizeIpValue(request.OperatorIp);
                var publicIp = NormalizeIpValue(request.PublicIp);
                var existingServiceId = _serviceRegistry.ServiceExists(request.Name, localIp, operatorIp, publicIp, request.Port);
                if (!string.IsNullOrEmpty(existingServiceId))
                {
                    _logger.LogInformation(
                        "服务 {Name} 在 {LocalIp}:{Port} 已存在，将更新现有服务，ID: {ServiceId}",
                        request.Name,
                        localIp,
                        request.Port,
                        existingServiceId);
                    request.Id = existingServiceId;
                }
                else if (string.IsNullOrEmpty(request.Id))
                {
                    request.Id = Guid.NewGuid().ToString("N");
                }

                var serviceInfo = new RuntimeServiceInfo
                {
                    Id = request.Id,
                    Name = request.Name,
                    Host = localIp,
                    LocalIp = localIp,
                    OperatorIp = operatorIp,
                    PublicIp = publicIp,
                    Port = request.Port,
                    ServiceType = request.ServiceType,
                    HealthCheckUrl = BuildFullHealthCheckUrl(request, localIp),
                    HealthCheckPort = request.HealthCheckPort > 0 ? request.HealthCheckPort : request.Port,
                    HeartbeatIntervalSeconds = request.HeartbeatIntervalSeconds,
                    Weight = request.Weight,
                    Metadata = request.Metadata ?? new Dictionary<string, string>(),
                };

                serviceInfo.IsLocalNetwork = IsInSameSubnet(GetLocalIpAddress(), localIp);

                if (!_serviceRegistry.RegisterService(serviceInfo))
                {
                    return StatusCode(500, ApiResponseFactory.Error<object>("服务注册失败"));
                }

                // Persist instance circuit configuration for this service registration.
                await _serviceCircuitStore.EnsureServiceAsync(serviceInfo, _serviceCircuitDefaults.Defaults);

                var response = new ServiceRegistrationResponse
                {
                    Id = serviceInfo.Id,
                    RegisterTimestamp = new DateTimeOffset(serviceInfo.RegisterTime).ToUnixTimeMilliseconds()
                };

                _logger.LogInformation("服务 {Name} 成功注册，ID: {ServiceId}", request.Name, serviceInfo.Id);
                return Ok(ApiResponseFactory.Success(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "服务注册过程中发生错误");
                return StatusCode(500, ApiResponseFactory.Error<object>($"服务注册失败: {ex.Message}"));
            }
        }

        /// <summary>
        /// 服务注销
        /// </summary>
        [HttpDelete("deregister/{id}")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public IActionResult Deregister(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    return BadRequest(ApiResponseFactory.Error<object>("服务ID不能为空"));
                }

                var service = _serviceRegistry.GetServiceById(id);
                var success = _serviceRegistry.DeregisterService(id);
                if (!success)
                {
                    return NotFound(ApiResponseFactory.Error<object>("服务不存在或注销失败", 404));
                }

                _serviceAccessService.ClearServiceState(service);

                _logger.LogInformation("服务 {ServiceId} 注销成功", id);
                return Ok(ApiResponseFactory.Success<object>(new { Message = "服务注销成功" }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "服务注销过程中发生错误");
                return StatusCode(500, ApiResponseFactory.Error<object>($"服务注销失败: {ex.Message}"));
            }
        }

        /// <summary>
        /// 服务心跳 WebSocket 通道（由周边服务主动连接）。
        /// 中心服务会按照服务注册时的 <see cref="ServiceRegistrationRequest.HeartbeatIntervalSeconds"/> 周期向服务端发送心跳请求，
        /// 周边服务收到请求后需回复 <see cref="CentralServiceHeartbeatWebSocketProtocol.HeartbeatResponseMessage"/>。
        /// </summary>
        [HttpGet("heartbeat/ws")]
        public async Task HeartbeatWebSocket(CancellationToken cancellationToken)
        {
            if (!HttpContext.WebSockets.IsWebSocketRequest)
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            var serviceId = HttpContext.Request.Query[CentralServiceHeartbeatWebSocketProtocol.ServiceIdQueryKey].ToString();
            if (string.IsNullOrWhiteSpace(serviceId))
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            var service = _serviceRegistry.GetServiceById(serviceId);
            if (service == null)
            {
                HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var intervalSeconds = Math.Max(0, service.HeartbeatIntervalSeconds);
            var interval = intervalSeconds <= 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(intervalSeconds);

            using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
            if (interval <= TimeSpan.Zero)
            {
                _serviceRegistry.UpdateHeartbeat(serviceId);
            }

            var responseTimeout = TimeSpan.FromSeconds(5);
            var pendingAckSync = new object();
            TaskCompletionSource<bool>? pendingAck = null;

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var linkedToken = linkedCts.Token;

            var receiveTask = ReceiveHeartbeatLoopAsync(
                serviceId,
                webSocket,
                () =>
                {
                    TaskCompletionSource<bool>? toComplete;
                    lock (pendingAckSync)
                    {
                        toComplete = pendingAck;
                    }

                    toComplete?.TrySetResult(true);
                },
                linkedToken);

            var sendTask = interval <= TimeSpan.Zero
                ? Task.Delay(Timeout.InfiniteTimeSpan, linkedToken)
                : SendHeartbeatLoopAsync(
                    serviceId,
                    webSocket,
                    interval,
                    responseTimeout,
                    () =>
                    {
                        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        lock (pendingAckSync)
                        {
                            pendingAck = tcs;
                        }

                        return tcs.Task;
                    },
                    () =>
                    {
                        lock (pendingAckSync)
                        {
                            pendingAck = null;
                        }
                    },
                    linkedToken);

            try
            {
                await Task.WhenAny(receiveTask, sendTask).ConfigureAwait(false);
            }
            finally
            {
                linkedCts.Cancel();

                try
                {
                    await Task.WhenAll(receiveTask, sendTask).ConfigureAwait(false);
                }
                catch
                {
                }

                var current = _serviceRegistry.GetServiceById(serviceId);
                if (current == null || current.Status != 2)
                {
                    _serviceRegistry.MarkOffline(serviceId);
                }
            }
        }

        private async Task ReceiveHeartbeatLoopAsync(
            string serviceId,
            WebSocket webSocket,
            Action heartbeatAckReceived,
            CancellationToken cancellationToken)
        {
            var buffer = new byte[4 * 1024];
            while (!cancellationToken.IsCancellationRequested && webSocket.State == WebSocketState.Open)
            {
                var message = await ReceiveTextMessageAsync(webSocket, buffer, cancellationToken).ConfigureAwait(false);
                if (message == null)
                {
                    return;
                }

                if (!string.Equals(message, CentralServiceHeartbeatWebSocketProtocol.HeartbeatResponseMessage, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _serviceRegistry.UpdateHeartbeat(serviceId);
                heartbeatAckReceived();
            }
        }

        private async Task SendHeartbeatLoopAsync(
            string serviceId,
            WebSocket webSocket,
            TimeSpan interval,
            TimeSpan responseTimeout,
            Func<Task> registerPendingAck,
            Action clearPendingAck,
            CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && webSocket.State == WebSocketState.Open)
            {
                var ackTask = registerPendingAck();
                try
                {
                    await SendTextMessageAsync(webSocket, CentralServiceHeartbeatWebSocketProtocol.HeartbeatRequestMessage, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch
                {
                    clearPendingAck();
                    return;
                }

                try
                {
                    var timeoutTask = Task.Delay(responseTimeout, cancellationToken);
                    var completed = await Task.WhenAny(ackTask, timeoutTask).ConfigureAwait(false);
                    if (completed != ackTask)
                    {
                        _serviceRegistry.MarkFault(serviceId);
                        return;
                    }
                }
                finally
                {
                    clearPendingAck();
                }

                await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task<string?> ReceiveTextMessageAsync(
            WebSocket webSocket,
            byte[] buffer,
            CancellationToken cancellationToken)
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
                    return null;
                }

                if (result.Count > 0)
                {
                    builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
            } while (!result.EndOfMessage);

            return builder.ToString();
        }

        private static Task SendTextMessageAsync(WebSocket webSocket, string message, CancellationToken cancellationToken)
        {
            var payload = Encoding.UTF8.GetBytes(message ?? string.Empty);
            return webSocket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, cancellationToken);
        }

        /// <summary>
        /// 获取所有服务实例
        /// </summary>
        [HttpGet("list")]
        [ProducesResponseType(typeof(ApiResponse<ServiceApiListResponse>), StatusCodes.Status200OK)]
        public IActionResult ListServices([FromQuery] string? name = null)
        {
            try
            {
                List<RuntimeServiceInfo> services;
                if (string.IsNullOrEmpty(name))
                {
                    services = _serviceRegistry.GetAllServices();
                }
                else
                {
                    services = _serviceRegistry.GetServicesByName(name);
                }

                var response = new ServiceApiListResponse
                {
                    Services = services.Select(CentralServiceServiceContractMapper.ToApiModel).ToArray()
                };

                return Ok(ApiResponseFactory.Success(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取服务列表过程中发生错误");
                return StatusCode(500, ApiResponseFactory.Error<object>($"获取服务列表失败: {ex.Message}"));
            }
        }

        /// <summary>
        /// 获取本地IP地址
        /// </summary>
        private string GetLocalIpAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }

                return "127.0.0.1";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取本地IP地址失败");
                return "127.0.0.1";
            }
        }

        /// <summary>
        /// 规范化请求中的局域网IP。
        /// </summary>
        private static string NormalizeServiceLocalIp(ServiceRegistrationRequest request)
        {
            if (request == null)
            {
                return string.Empty;
            }

            var localIp = NormalizeIpValue(request.LocalIp);
            if (!string.IsNullOrEmpty(localIp))
            {
                return localIp;
            }

            return NormalizeIpValue(request.Host);
        }

        private static string NormalizeIpValue(string? value)
        {
            return value?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// 判断两个IP是否在同一子网
        /// </summary>
        private bool IsInSameSubnet(string ip1, string ip2)
        {
            try
            {
                if (IPAddress.TryParse(ip1, out var address1) && IPAddress.TryParse(ip2, out var address2))
                {
                    var bytes1 = address1.GetAddressBytes();
                    var bytes2 = address2.GetAddressBytes();
                    if (bytes1.Length == 4 && bytes2.Length == 4)
                    {
                        return bytes1[0] == bytes2[0]
                               && bytes1[1] == bytes2[1]
                               && bytes1[2] == bytes2[2];
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "判断子网失败");
                return false;
            }
        }

        private bool IsValidServiceType(string serviceType)
        {
            if (string.IsNullOrEmpty(serviceType))
                return false;

            string[] validTypes = { "Web", "Socket" };
            return validTypes.Contains(serviceType, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 检查主机和端口是否可以连通
        /// </summary>
        private async Task<bool> CheckHostPortReachableAsync(string host, int port)
        {
            try
            {
                if (!IPAddress.TryParse(host, out _))
                {
                    try
                    {
                        var hostEntry = await Dns.GetHostEntryAsync(host);
                        if (hostEntry.AddressList.Length == 0)
                        {
                            _logger.LogWarning("无法解析主机名 {Host}", host);
                            return false;
                        }
                    }
                    catch (SocketException ex)
                    {
                        _logger.LogWarning("主机名解析失败: {Message}", ex.Message);
                        return false;
                    }
                }

                using (var ping = new Ping())
                {
                    try
                    {
                        var reply = await ping.SendPingAsync(host, 1000);
                        _logger.LogInformation("Ping {Host} 结果: {Status}", host, reply.Status);
                    }
                    catch (PingException ex)
                    {
                        _logger.LogWarning("Ping {Host} 失败: {Message}", host, ex.Message);
                    }
                }

                using (var cts = new CancellationTokenSource(3000))
                using (var client = new TcpClient())
                {
                    try
                    {
                        await client.ConnectAsync(host, port, cts.Token);
                        if (client.Connected)
                        {
                            _logger.LogInformation("TCP连接到 {Host}:{Port} 成功", host, port);
                            return true;
                        }

                        _logger.LogWarning("TCP连接到 {Host}:{Port} 失败", host, port);
                        return false;
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogWarning("TCP连接到 {Host}:{Port} 超时", host, port);
                        return false;
                    }
                    catch (SocketException ex)
                    {
                        _logger.LogWarning("TCP连接到 {Host}:{Port} 失败: {Message}", host, port, ex.Message);
                        return false;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("TCP连接到 {Host}:{Port} 异常: {Message}", host, port, ex.Message);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查主机 {Host}:{Port} 可达性时发生错误", host, port);
                return false;
            }
        }

        /// <summary>
        /// 构建完整的健康检查URL
        /// </summary>
        private string BuildFullHealthCheckUrl(ServiceRegistrationRequest request, string host)
        {
            if (string.IsNullOrEmpty(request.HealthCheckUrl))
            {
                return string.Empty;
            }

            if (request.HealthCheckUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || request.HealthCheckUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return request.HealthCheckUrl;
            }

            var healthCheckPath = request.HealthCheckUrl.StartsWith("/")
                ? request.HealthCheckUrl
                : $"/{request.HealthCheckUrl}";

            return $"http://{host}:{request.Port}{healthCheckPath}";
        }
    }
}
