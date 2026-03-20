using CentralService.Models;
using CentralService.Internal;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CentralService.Services
{
    /// <summary>
    /// 服务健康检查器 - 定期检查已注册服务的健康状态
    /// </summary>
    public class ServiceHealthChecker : BackgroundService
    {
        private readonly ServiceRegistry _serviceRegistry;
        private readonly ILogger<ServiceHealthChecker> _logger;
        private readonly HttpClient _httpClient;
        private readonly CentralServiceBackgroundTaskMonitor _taskMonitor;

        // 检查间隔时间（秒）
        private readonly int _checkIntervalSeconds = 15;

        // Socket连接超时时间（毫秒）
        private readonly int _socketTimeoutMs = 3000;

        public ServiceHealthChecker(
            ServiceRegistry serviceRegistry,
            ILogger<ServiceHealthChecker> logger,
            CentralServiceBackgroundTaskMonitor taskMonitor)
        {
            _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _taskMonitor = taskMonitor ?? throw new ArgumentNullException(nameof(taskMonitor));

            _httpClient = CentralServiceHttpClientFactory.Create(
                ignoreSslErrors: true,
                timeout: TimeSpan.FromSeconds(5));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("服务健康检查器已启动");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // 检查并移除超时的服务
                    _serviceRegistry.CheckAndRemoveDeadServices();

                    // 检查服务健康状态
                    await CheckServicesHealthAsync();

                    _taskMonitor.MarkSuccess(nameof(ServiceHealthChecker));

                    // 等待下一次检查
                    await Task.Delay(TimeSpan.FromSeconds(_checkIntervalSeconds), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // 正常取消，忽略异常
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "服务健康检查过程中发生错误");
                    _taskMonitor.MarkError(nameof(ServiceHealthChecker), ex);

                    // 出错后等待一段时间再继续
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }

            _logger.LogInformation("服务健康检查器已停止");
        }

        /// <summary>
        /// 检查所有服务的健康状态
        /// </summary>
        private async Task CheckServicesHealthAsync()
        {
            var services = _serviceRegistry.GetAllServices();

            var faultCount = 0;
            var onlineCount = 0;

            foreach (var service in services)
            {
                try
                {
                    bool isHealthy = false;

                    // 根据健康检查类型选择不同的检查方法
                    switch (service.HealthCheckType?.ToLower())
                    {
                        case "http":
                            isHealthy = await CheckHttpHealthAsync(service);
                            break;
                        case "socket":
                            isHealthy = await CheckSocketHealthAsync(service);
                            break;
                        default:
                            _logger.LogWarning($"服务 {service.Name} ({service.Id}) 使用了未知的健康检查类型: {service.HealthCheckType}");
                            continue;
                    }

                    // 更新服务状态
                    if (isHealthy)
                    {
                        // 服务正常
                        _serviceRegistry.UpdateHeartbeat(service.Id);
                        onlineCount++;
                    }
                    else
                    {
                        // 服务异常
                        service.Status = 2; // 故障状态
                        faultCount++;
                        _logger.LogWarning($"服务 {service.Name} ({service.Id}) 健康检查失败");
                    }
                }
                catch (Exception ex)
                {
                    // 服务不可达
                    service.Status = 2; // 故障状态
                    faultCount++;
                    _logger.LogWarning(ex, $"服务 {service.Name} ({service.Id}) 健康检查异常");
                }
            }

            if (faultCount > 0)
            {
                _logger.LogWarning("健康检查汇总：总数 {Total}，在线 {Online}，故障 {Fault}", services.Count, onlineCount, faultCount);
            }
            else
            {
                _logger.LogDebug("健康检查汇总：总数 {Total}，在线 {Online}", services.Count, onlineCount);
            }
        }

        /// <summary>
        /// 检查HTTP服务健康
        /// </summary>
        /// <param name="service">服务信息</param>
        /// <returns>是否健康</returns>
        private async Task<bool> CheckHttpHealthAsync(ServiceInfo service)
        {
            if (string.IsNullOrEmpty(service.HealthCheckUrl))
            {
                _logger.LogWarning($"服务 {service.Name} ({service.Id}) 未配置健康检查URL");
                return false;
            }

            try
            {
                // 验证URL格式，确保是绝对URI
                if (!Uri.IsWellFormedUriString(service.HealthCheckUrl, UriKind.Absolute))
                {
                    _logger.LogWarning($"服务 {service.Name} ({service.Id}) 健康检查URL格式无效: {service.HealthCheckUrl}");
                    return false;
                }

                // 确保URL以http://或https://开头
                if (!service.HealthCheckUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !service.HealthCheckUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning($"服务 {service.Name} ({service.Id}) 健康检查URL必须以http://或https://开头: {service.HealthCheckUrl}");
                    return false;
                }

                // 发送健康检查请求
                var response = await _httpClient.GetAsync(service.HealthCheckUrl);

                // 检查响应状态码
                bool isHealthy = response.IsSuccessStatusCode;

                if (!isHealthy)
                {
                    _logger.LogWarning($"服务 {service.Name} ({service.Id}) HTTP健康检查失败: {response.StatusCode}");
                }

                return isHealthy;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"服务 {service.Name} ({service.Id}) HTTP健康检查异常");
                return false;
            }
        }

        /// <summary>
        /// 检查Socket服务健康
        /// </summary>
        /// <param name="service">服务信息</param>
        /// <returns>是否健康</returns>
        private async Task<bool> CheckSocketHealthAsync(ServiceInfo service)
        {
            // 确定检查端口，如果未指定则使用服务端口
            int port = service.HealthCheckPort > 0 ? service.HealthCheckPort : service.Port;

            if (port <= 0)
            {
                _logger.LogWarning($"服务 {service.Name} ({service.Id}) 未配置有效的Socket检查端口");
                return false;
            }

            try
            {
                using (var tcpClient = new TcpClient())
                {
                    // 设置连接超时
                    var connectTask = tcpClient.ConnectAsync(service.Host, port);

                    // 等待连接或超时
                    var completedTask = await Task.WhenAny(connectTask, Task.Delay(_socketTimeoutMs));

                    if (completedTask != connectTask)
                    {
                        // 连接超时
                        _logger.LogWarning($"服务 {service.Name} ({service.Id}) Socket连接超时");
                        return false;
                    }

                    // 检查连接是否成功
                    bool isConnected = tcpClient.Connected;

                    if (!isConnected)
                    {
                        _logger.LogWarning($"服务 {service.Name} ({service.Id}) Socket连接失败");
                    }

                    return isConnected;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"服务 {service.Name} ({service.Id}) Socket健康检查异常");
                return false;
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _httpClient.Dispose();
            await base.StopAsync(cancellationToken);
        }
    }
}
