using CentralService.Internal;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CentralService.Services
{
    /// <summary>
    /// 服务健康检查器 - 定期清理超时未响应心跳的服务
    /// <para>心跳请求仅通过 WebSocket 方式下发。</para>
    /// </summary>
    public class ServiceHealthChecker : BackgroundService
    {
        private readonly ServiceRegistry _serviceRegistry;
        private readonly ILogger<ServiceHealthChecker> _logger;
        private readonly CentralServiceBackgroundTaskMonitor _taskMonitor;

        // 检查间隔时间（秒）
        private readonly int _checkIntervalSeconds = 15;

        public ServiceHealthChecker(
            ServiceRegistry serviceRegistry,
            ILogger<ServiceHealthChecker> logger,
            CentralServiceBackgroundTaskMonitor taskMonitor)
        {
            _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _taskMonitor = taskMonitor ?? throw new ArgumentNullException(nameof(taskMonitor));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("服务健康检查器已启动（WebSocket心跳模式）");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // 检查并移除超时的服务
                    _serviceRegistry.CheckAndRemoveDeadServices();

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
    }
}
