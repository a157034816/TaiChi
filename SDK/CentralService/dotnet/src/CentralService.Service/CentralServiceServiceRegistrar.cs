using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CentralService.Service.Errors;
using CentralService.Service.Models;

namespace CentralService.Service
{
    /// <summary>
    /// 周边服务侧的中心服务注册器：
    /// 负责将当前服务实例注册到一个或多个中心服务端点，并维护 WebSocket 心跳连接与自动补注册。
    /// </summary>
    /// <remarks>
    /// <para>设计说明：</para>
    /// <list type="bullet">
    /// <item>
    /// <description>该类型不负责“如何构造注册请求”，由调用方通过委托提供 <see cref="ServiceRegistrationRequest"/>（例如填充本机 IP、外网 IP、版本号、元数据等）。</description>
    /// </item>
    /// <item>
    /// <description>每个中心服务端点使用独立的注册会话（serviceId 不共享），以确保多中心环境下每个中心均可独立发现服务。</description>
    /// </item>
    /// <item>
    /// <description>心跳机制采用 <see cref="CentralServiceHeartbeatWebSocketClient"/>：注册成功后连接中心服务，收到心跳请求时自动响应并刷新“最近成功联系时间”。</description>
    /// </item>
    /// </list>
    /// </remarks>
    public sealed class CentralServiceServiceRegistrar : IDisposable
    {
        private readonly CentralServiceServiceRegistrarOptions _options;
        private readonly List<EndpointRegistrationSession> _sessions;

        private Timer? _reconnectTimer;
        private Func<CancellationToken, Task<ServiceRegistrationRequest>>? _requestProviderAsync;
        private bool _isRunning;
        private int _reconnectInProgress;
        private bool _disposed;

        /// <summary>
        /// 创建中心服务注册器实例。
        /// </summary>
        /// <param name="options">注册器配置。</param>
        public CentralServiceServiceRegistrar(CentralServiceServiceRegistrarOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _sessions = _options.Endpoints
                .Select((endpoint, index) => new EndpointRegistrationSession(index, endpoint, _options))
                .ToList();
        }

        /// <summary>
        /// 获取当前是否处于运行中（启动后未停止）。
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// 获取当前是否至少已成功注册到一个中心服务端点。
        /// </summary>
        public bool IsRegistered => _sessions.Any(x => x.IsRegistered);

        /// <summary>
        /// 获取当前注册会话快照信息（为避免外部修改，返回副本）。
        /// </summary>
        public IReadOnlyList<CentralServiceServiceRegistrarEndpointSnapshot> GetEndpointSnapshots()
        {
            return _sessions
                .Select(x => x.ToSnapshot())
                .ToList();
        }

        /// <summary>
        /// 启动注册器：执行首次注册，并开启心跳与补注册机制。
        /// </summary>
        /// <param name="requestProviderAsync">注册请求提供者：每次注册/补注册前都会调用，用于刷新请求中的动态字段。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>启动结果摘要。</returns>
        public async Task<CentralServiceServiceRegistrarStartResult> StartAsync(
            Func<CancellationToken, Task<ServiceRegistrationRequest>> requestProviderAsync,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (requestProviderAsync == null) throw new ArgumentNullException(nameof(requestProviderAsync));

            _requestProviderAsync = requestProviderAsync;
            _isRunning = true;

            if (_sessions.Count == 0)
            {
                _options.LogInfo?.Invoke("未配置中心服务端点，跳过中心服务注册。");
                return new CentralServiceServiceRegistrarStartResult(0, 0, Array.Empty<CentralServiceServiceRegistrarEndpointSnapshot>());
            }

            await RegisterAllEndpointsAsync(cancellationToken).ConfigureAwait(false);
            StartTimers();

            var snapshots = GetEndpointSnapshots();
            return new CentralServiceServiceRegistrarStartResult(
                snapshots.Count(x => x.IsRegistered),
                snapshots.Count,
                snapshots);
        }

        /// <summary>
        /// 停止注册器：注销已注册实例并释放定时器与心跳资源。
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
            {
                return;
            }

            _isRunning = false;
            StopTimers();

            foreach (var session in _sessions)
            {
                session.StopHeartbeatWebSocket();
            }

            foreach (var session in _sessions.Where(x => x.IsRegistered && !string.IsNullOrWhiteSpace(x.ServiceId)))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    session.Client.Deregister(session.ServiceId!);
                    _options.LogInfo?.Invoke($"已从中心服务注销：{session.Endpoint.BaseUrl}");
                }
                catch (CentralServiceException ex)
                {
                    _options.LogError?.Invoke(
                        $"从中心服务注销失败：{session.Endpoint.BaseUrl} {(int)ex.Error.HttpStatus} {ex.Error.Message}",
                        ex);
                }
                catch (Exception ex)
                {
                    _options.LogError?.Invoke($"从中心服务注销异常：{session.Endpoint.BaseUrl}", ex);
                }
                finally
                {
                    session.MarkUnregistered();
                }
            }

            await Task.CompletedTask;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                StopAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
            catch
            {
                // 释放阶段禁止抛出，避免影响宿主退出流程。
            }

            StopTimers();

            foreach (var session in _sessions)
            {
                session.Client.Dispose();
            }
        }

        private void StartTimers()
        {
            StopTimers();

            _reconnectTimer = new Timer(
                _ => _ = CheckAndReconnectAsync(),
                null,
                _options.ReconnectInterval,
                _options.ReconnectInterval);
        }

        private void StopTimers()
        {
            try
            {
                _reconnectTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            }
            catch
            {
            }

            try
            {
                _reconnectTimer?.Dispose();
            }
            catch
            {
            }

            _reconnectTimer = null;
        }

        private async Task RegisterAllEndpointsAsync(CancellationToken cancellationToken)
        {
            ServiceRegistrationRequest request;
            try
            {
                request = await BuildRequestAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                foreach (var session in _sessions)
                {
                    session.MarkUnregistered();
                }

                _options.LogError?.Invoke("构造中心服务注册请求失败，跳过本次注册。", ex);
                return;
            }

            foreach (var session in _sessions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await RegisterEndpointAsync(session, request, cancellationToken).ConfigureAwait(false);
            }

            var successCount = _sessions.Count(x => x.IsRegistered);
            if (successCount == 0)
            {
                _options.LogWarning?.Invoke("未能成功注册到任何中心服务端点，后续将由补注册定时器继续尝试。");
                return;
            }

            _options.LogInfo?.Invoke($"中心服务注册完成：成功 {successCount}/{_sessions.Count}。");
        }

        private Task RegisterEndpointAsync(
            EndpointRegistrationSession session,
            ServiceRegistrationRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var response = session.Client.Register(request);
                session.ServiceId = response.Id;
                session.IsRegistered = true;
                session.TouchSuccess(_options.UtcNowProvider());
                session.RestartHeartbeatWebSocket();
                _options.LogInfo?.Invoke($"已成功注册到中心服务：{session.Endpoint.BaseUrl}");
            }
            catch (CentralServiceException ex)
            {
                session.MarkUnregistered();
                _options.LogError?.Invoke(
                    $"注册到中心服务失败：{session.Endpoint.BaseUrl} {(int)ex.Error.HttpStatus} {ex.Error.Message}",
                    ex);
            }
            catch (Exception ex)
            {
                session.MarkUnregistered();
                _options.LogError?.Invoke($"注册到中心服务异常：{session.Endpoint.BaseUrl}", ex);
            }

            return Task.CompletedTask;
        }

        private async Task CheckAndReconnectAsync()
        {
            if (!_isRunning || Interlocked.CompareExchange(ref _reconnectInProgress, 1, 0) != 0)
            {
                return;
            }

            try
            {
                var reconnectTargets = _sessions
                    .Where(NeedReconnect)
                    .ToList();
                if (reconnectTargets.Count == 0)
                {
                    return;
                }

                ServiceRegistrationRequest request;
                try
                {
                    request = await BuildRequestAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _options.LogError?.Invoke("构造中心服务补注册请求失败，跳过本次补注册。", ex);
                    return;
                }

                foreach (var session in reconnectTargets)
                {
                    _options.LogInfo?.Invoke($"检测到需要补注册：{session.Endpoint.BaseUrl}");
                    await RegisterEndpointAsync(session, request, CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _options.LogError?.Invoke("补注册流程异常", ex);
            }
            finally
            {
                Interlocked.Exchange(ref _reconnectInProgress, 0);
            }
        }

        private bool NeedReconnect(EndpointRegistrationSession session)
        {
            if (!session.IsRegistered)
            {
                return true;
            }

            if (_options.EnableHeartbeatWebSocket && _options.SkipReconnectWhenHeartbeatConnected && session.IsHeartbeatWebSocketConnected)
            {
                return false;
            }

            return _options.UtcNowProvider() - session.LastSuccessfulContactUtc > _options.ReconnectThreshold;
        }

        private async Task<ServiceRegistrationRequest> BuildRequestAsync(CancellationToken cancellationToken)
        {
            var provider = _requestProviderAsync;
            if (provider == null)
            {
                throw new InvalidOperationException("注册请求提供者尚未设置。请先调用 StartAsync。");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var request = await provider(cancellationToken).ConfigureAwait(false);
            if (request == null)
            {
                throw new InvalidOperationException("注册请求提供者返回了空对象。");
            }

            return request;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        private sealed class EndpointRegistrationSession
        {
            private readonly CentralServiceServiceRegistrarOptions _options;

            public EndpointRegistrationSession(int order, CentralServiceSdkOptions.CentralServiceEndpointOptions endpoint, CentralServiceServiceRegistrarOptions options)
            {
                Order = order;
                Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
                _options = options ?? throw new ArgumentNullException(nameof(options));

                Client = new CentralServiceServiceClient(
                    new CentralServiceSdkOptions(
                        new[]
                        {
                            endpoint
                        })
                    {
                        IgnoreSslErrors = _options.IgnoreSslErrors,
                        Timeout = _options.Timeout,
                        HttpMessageHandler = _options.HttpMessageHandler,
                    });

                ServiceId = null;
                IsRegistered = false;
                LastSuccessfulContactUtc = DateTime.MinValue;
            }

            public int Order { get; }

            public CentralServiceSdkOptions.CentralServiceEndpointOptions Endpoint { get; }

            public CentralServiceServiceClient Client { get; }

            public string? ServiceId { get; set; }

            public bool IsRegistered { get; set; }

            public DateTime LastSuccessfulContactUtc { get; private set; }

            public CentralServiceHeartbeatWebSocketClient? HeartbeatClient { get; private set; }

            public bool IsHeartbeatWebSocketConnected => HeartbeatClient?.IsConnected == true;

            public void MarkUnregistered()
            {
                ServiceId = null;
                IsRegistered = false;
                StopHeartbeatWebSocket();
            }

            public void TouchSuccess(DateTime utcNow)
            {
                LastSuccessfulContactUtc = utcNow;
            }

            public void RestartHeartbeatWebSocket()
            {
                if (!_options.EnableHeartbeatWebSocket)
                {
                    StopHeartbeatWebSocket();
                    return;
                }

                if (string.IsNullOrWhiteSpace(ServiceId))
                {
                    StopHeartbeatWebSocket();
                    return;
                }

                StopHeartbeatWebSocket();

                var cts = new CancellationTokenSource();
                _heartbeatCancellation = cts;

                var client = new CentralServiceHeartbeatWebSocketClient(Endpoint.BaseUrl, ServiceId!, _options.IgnoreSslErrors);
                client.HeartbeatRequested += _ => LastSuccessfulContactUtc = _options.UtcNowProvider();
                HeartbeatClient = client;
                LastSuccessfulContactUtc = _options.UtcNowProvider();

                _heartbeatTask = Task.Run(() => client.RunAsync(cts.Token));
            }

            public void StopHeartbeatWebSocket()
            {
                try
                {
                    _heartbeatCancellation?.Cancel();
                }
                catch
                {
                }

                try
                {
                    _heartbeatCancellation?.Dispose();
                }
                catch
                {
                }

                _heartbeatCancellation = null;
                _heartbeatTask = null;
                HeartbeatClient = null;
            }

            public CentralServiceServiceRegistrarEndpointSnapshot ToSnapshot()
            {
                return new CentralServiceServiceRegistrarEndpointSnapshot(
                    Endpoint.BaseUrl,
                    ServiceId,
                    IsRegistered,
                    LastSuccessfulContactUtc,
                    IsHeartbeatWebSocketConnected,
                    Order);
            }

            private CancellationTokenSource? _heartbeatCancellation;
            private Task? _heartbeatTask;
        }
    }

    /// <summary>
    /// 中心服务注册器配置。
    /// </summary>
    public sealed class CentralServiceServiceRegistrarOptions
    {
        /// <summary>
        /// 使用端点列表创建注册器配置。
        /// </summary>
        /// <param name="endpoints">中心服务端点列表。</param>
        public CentralServiceServiceRegistrarOptions(IEnumerable<CentralServiceSdkOptions.CentralServiceEndpointOptions> endpoints)
        {
            Endpoints = (endpoints ?? Array.Empty<CentralServiceSdkOptions.CentralServiceEndpointOptions>())
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.BaseUrl))
                .ToList();

            Timeout = TimeSpan.FromSeconds(5);
            IgnoreSslErrors = false;
            ReconnectInterval = TimeSpan.FromSeconds(30);
            ReconnectThreshold = TimeSpan.FromMinutes(5);
            EnableHeartbeatWebSocket = true;
            SkipReconnectWhenHeartbeatConnected = true;
            UtcNowProvider = () => DateTime.UtcNow;
        }

        /// <summary>
        /// 获取中心服务端点列表。
        /// </summary>
        public IReadOnlyList<CentralServiceSdkOptions.CentralServiceEndpointOptions> Endpoints { get; }

        /// <summary>
        /// 获取或设置单次请求超时时间。
        /// </summary>
        public TimeSpan Timeout { get; set; }

        /// <summary>
        /// 获取或设置是否忽略 SSL 证书错误。
        /// </summary>
        public bool IgnoreSslErrors { get; set; }

        /// <summary>
        /// 获取或设置自定义的 HTTP 消息处理器（通常用于测试环境集成 TestServer）。
        /// </summary>
        public System.Net.Http.HttpMessageHandler? HttpMessageHandler { get; set; }

        /// <summary>
        /// 获取或设置补注册检查间隔。
        /// </summary>
        public TimeSpan ReconnectInterval { get; set; }

        /// <summary>
        /// 获取或设置补注册判定阈值：当超过该阈值未刷新成功联系时间，则判定需要补注册。
        /// </summary>
        public TimeSpan ReconnectThreshold { get; set; }

        /// <summary>
        /// 获取或设置是否启用 WebSocket 心跳连接。
        /// </summary>
        public bool EnableHeartbeatWebSocket { get; set; }

        /// <summary>
        /// 获取或设置当心跳 WebSocket 已连接时，是否跳过补注册。
        /// </summary>
        public bool SkipReconnectWhenHeartbeatConnected { get; set; }

        /// <summary>
        /// 获取或设置 UTC 时间提供者（用于测试或定制时间源）。
        /// </summary>
        public Func<DateTime> UtcNowProvider { get; set; }

        /// <summary>
        /// 信息级别日志回调（可选）。
        /// </summary>
        public Action<string>? LogInfo { get; set; }

        /// <summary>
        /// 警告级别日志回调（可选）。
        /// </summary>
        public Action<string>? LogWarning { get; set; }

        /// <summary>
        /// 错误级别日志回调（可选）。
        /// </summary>
        public Action<string, Exception?>? LogError { get; set; }
    }

    /// <summary>
    /// 中心服务注册器启动结果摘要。
    /// </summary>
    public sealed class CentralServiceServiceRegistrarStartResult
    {
        /// <summary>
        /// 创建启动结果摘要。
        /// </summary>
        public CentralServiceServiceRegistrarStartResult(
            int successCount,
            int totalCount,
            IReadOnlyList<CentralServiceServiceRegistrarEndpointSnapshot> endpoints)
        {
            SuccessCount = successCount;
            TotalCount = totalCount;
            Endpoints = endpoints ?? Array.Empty<CentralServiceServiceRegistrarEndpointSnapshot>();
        }

        /// <summary>
        /// 获取首次注册成功的端点数量。
        /// </summary>
        public int SuccessCount { get; }

        /// <summary>
        /// 获取总端点数量。
        /// </summary>
        public int TotalCount { get; }

        /// <summary>
        /// 获取端点快照列表。
        /// </summary>
        public IReadOnlyList<CentralServiceServiceRegistrarEndpointSnapshot> Endpoints { get; }
    }

    /// <summary>
    /// 中心服务端点注册会话快照。
    /// </summary>
    public sealed class CentralServiceServiceRegistrarEndpointSnapshot
    {
        /// <summary>
        /// 创建端点快照。
        /// </summary>
        public CentralServiceServiceRegistrarEndpointSnapshot(
            string baseUrl,
            string? serviceId,
            bool isRegistered,
            DateTime lastSuccessfulContactUtc,
            bool isHeartbeatWebSocketConnected,
            int order)
        {
            BaseUrl = baseUrl ?? string.Empty;
            ServiceId = serviceId;
            IsRegistered = isRegistered;
            LastSuccessfulContactUtc = lastSuccessfulContactUtc;
            IsHeartbeatWebSocketConnected = isHeartbeatWebSocketConnected;
            Order = order;
        }

        /// <summary>
        /// 获取中心服务根地址。
        /// </summary>
        public string BaseUrl { get; }

        /// <summary>
        /// 获取已注册的服务实例 id（未注册时为空）。
        /// </summary>
        public string? ServiceId { get; }

        /// <summary>
        /// 获取当前是否已注册。
        /// </summary>
        public bool IsRegistered { get; }

        /// <summary>
        /// 获取最近一次成功联系中心服务的时间（UTC）。
        /// </summary>
        public DateTime LastSuccessfulContactUtc { get; }

        /// <summary>
        /// 获取当前心跳 WebSocket 是否已连接（仅表示连接建立成功）。
        /// </summary>
        public bool IsHeartbeatWebSocketConnected { get; }

        /// <summary>
        /// 获取端点顺序（用于稳定排序/诊断）。
        /// </summary>
        public int Order { get; }
    }
}
