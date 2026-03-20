using CentralService.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CentralService.Services
{
    /// <summary>
    /// 服务网络评估后台服务 - 定期评估所有已注册服务的网络状态
    /// </summary>
    public class ServiceNetworkEvaluatorBackgroundService : BackgroundService
    {
        private readonly ServiceRegistry _serviceRegistry;
        private readonly ServiceNetworkEvaluator _networkEvaluator;
        private readonly ILogger<ServiceNetworkEvaluatorBackgroundService> _logger;
        private readonly CentralServiceBackgroundTaskMonitor _taskMonitor;
        
        // 评估间隔时间（秒）
        private readonly int _evaluationIntervalSeconds = 60;

        /// <summary>
        /// 初始化服务网络评估后台服务
        /// </summary>
        /// <param name="serviceRegistry">服务注册表</param>
        /// <param name="networkEvaluator">网络评估器</param>
        /// <param name="logger">日志记录器</param>
        public ServiceNetworkEvaluatorBackgroundService(
            ServiceRegistry serviceRegistry,
            ServiceNetworkEvaluator networkEvaluator,
            CentralServiceBackgroundTaskMonitor taskMonitor,
            ILogger<ServiceNetworkEvaluatorBackgroundService> logger)
        {
            _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
            _networkEvaluator = networkEvaluator ?? throw new ArgumentNullException(nameof(networkEvaluator));
            _taskMonitor = taskMonitor ?? throw new ArgumentNullException(nameof(taskMonitor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 执行后台服务
        /// </summary>
        /// <param name="stoppingToken">取消令牌</param>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("服务网络评估后台服务已启动");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // 获取所有服务
                    var services = _serviceRegistry.GetAllServices();
                    _logger.LogInformation($"开始评估 {services.Count} 个服务的网络状态");

                    var evaluatedCount = 0;
                    var errorCount = 0;
                    var availableCount = 0;

                    // 评估每个服务的网络状态
                    foreach (var service in services)
                    {
                        if (stoppingToken.IsCancellationRequested)
                            break;

                        try
                        {
                            evaluatedCount++;

                            // 评估服务的网络状态
                            var networkStatus = await _networkEvaluator.EvaluateServiceNetworkAsync(service.Id);
                            
                            if (networkStatus != null)
                            {
                                if (networkStatus.IsAvailable)
                                {
                                    availableCount++;
                                }

                                _logger.LogDebug($"服务 {service.Name} ({service.Id}) 网络状态评分: {networkStatus.CalculateScore()}, " +
                                               $"响应时间: {networkStatus.ResponseTime}ms, " +
                                               $"丢包率: {networkStatus.PacketLoss}%");
                            }
                        }
                        catch (Exception ex)
                        {
                            errorCount++;
                            _logger.LogError(ex, $"评估服务 {service.Name} ({service.Id}) 网络状态时发生错误");
                        }
                    }

                    _logger.LogInformation(
                        "服务网络状态评估完成：总数 {Total}，评估 {Evaluated}，异常 {Errors}，可用 {Available}",
                        services.Count,
                        evaluatedCount,
                        errorCount,
                        availableCount);
                    _taskMonitor.MarkSuccess(nameof(ServiceNetworkEvaluatorBackgroundService));
                    
                    // 等待下一次评估
                    await Task.Delay(TimeSpan.FromSeconds(_evaluationIntervalSeconds), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // 正常取消,忽略异常
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "服务网络评估过程中发生错误");
                    _taskMonitor.MarkError(nameof(ServiceNetworkEvaluatorBackgroundService), ex);
                    
                    // 出错后等待一段时间再继续
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }

            _logger.LogInformation("服务网络评估后台服务已停止");
        }
        
        /// <summary>
        /// 停止后台服务
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("服务网络评估后台服务正在停止");
            await base.StopAsync(cancellationToken);
        }
    }
} 
