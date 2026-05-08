using CentralService.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CentralService.Services
{
    /// <summary>
    /// 服务网络评估器。
    /// 以 WebSocket 心跳链路作为主数据源，维护网络状态缓存并为服务发现提供评分能力。
    /// </summary>
    public class ServiceNetworkEvaluator
    {
        // 缓存缺少明确 RTT 时使用的默认响应时间（毫秒）。
        private const long DefaultResponseTimeMs = 200;

        // 服务不可用时记录的响应时间（毫秒），用于与可用服务在评分上拉开差距。
        private const long UnavailableResponseTimeMs = 3000;

        private readonly ServiceRegistry _serviceRegistry;
        private readonly ILogger<ServiceNetworkEvaluator> _logger;

        // 存储服务网络状态的线程安全字典。
        private readonly ConcurrentDictionary<string, ServiceNetworkStatus> _networkStatuses = new ConcurrentDictionary<string, ServiceNetworkStatus>();

        // 用于线程同步的锁对象（主要用于读取快照）。
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        /// <summary>
        /// 初始化服务网络评估器。
        /// </summary>
        /// <param name="serviceRegistry">服务注册表。</param>
        /// <param name="logger">日志记录器。</param>
        public ServiceNetworkEvaluator(
            ServiceRegistry serviceRegistry,
            ILogger<ServiceNetworkEvaluator> logger)
        {
            _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 使用心跳回包信息更新网络状态缓存。
        /// </summary>
        /// <param name="serviceId">服务ID。</param>
        /// <param name="responseTimeMs">心跳往返时间（毫秒）。</param>
        /// <returns>更新后的网络状态。</returns>
        public ServiceNetworkStatus UpdateNetworkStatusFromHeartbeat(string serviceId, long responseTimeMs)
        {
            if (string.IsNullOrWhiteSpace(serviceId))
            {
                return null;
            }

            var now = DateTime.Now;
            var normalizedResponseTime = NormalizeResponseTime(responseTimeMs);

            return _networkStatuses.AddOrUpdate(
                serviceId,
                _ => new ServiceNetworkStatus
                {
                    ServiceId = serviceId,
                    ResponseTime = normalizedResponseTime,
                    PacketLoss = 0,
                    LastCheckTime = now,
                    ConsecutiveSuccesses = 1,
                    ConsecutiveFailures = 0,
                    IsAvailable = true,
                },
                (_, existing) => new ServiceNetworkStatus
                {
                    ServiceId = serviceId,
                    ResponseTime = normalizedResponseTime,
                    PacketLoss = 0,
                    LastCheckTime = now,
                    ConsecutiveSuccesses = existing.IsAvailable
                        ? existing.ConsecutiveSuccesses + 1
                        : 1,
                    ConsecutiveFailures = 0,
                    IsAvailable = true,
                });
        }

        /// <summary>
        /// 将服务网络状态标记为不可用（离线/故障）。
        /// </summary>
        /// <param name="serviceId">服务ID。</param>
        /// <returns>更新后的网络状态。</returns>
        public ServiceNetworkStatus MarkServiceUnavailable(string serviceId)
        {
            if (string.IsNullOrWhiteSpace(serviceId))
            {
                return null;
            }

            var now = DateTime.Now;

            return _networkStatuses.AddOrUpdate(
                serviceId,
                _ => new ServiceNetworkStatus
                {
                    ServiceId = serviceId,
                    ResponseTime = UnavailableResponseTimeMs,
                    PacketLoss = 100,
                    LastCheckTime = now,
                    ConsecutiveSuccesses = 0,
                    ConsecutiveFailures = 1,
                    IsAvailable = false,
                },
                (_, existing) => new ServiceNetworkStatus
                {
                    ServiceId = serviceId,
                    ResponseTime = Math.Max(existing.ResponseTime, UnavailableResponseTimeMs),
                    PacketLoss = 100,
                    LastCheckTime = now,
                    ConsecutiveSuccesses = 0,
                    ConsecutiveFailures = existing.IsAvailable
                        ? 1
                        : existing.ConsecutiveFailures + 1,
                    IsAvailable = false,
                });
        }

        /// <summary>
        /// 评估指定服务的网络状态（轻量路径）。
        /// 不执行实时 Ping，仅基于心跳缓存与服务在线状态生成评估结果。
        /// </summary>
        /// <param name="serviceId">服务ID。</param>
        /// <returns>服务网络状态。</returns>
        public Task<ServiceNetworkStatus> EvaluateServiceNetworkAsync(string serviceId)
        {
            if (string.IsNullOrWhiteSpace(serviceId))
            {
                return Task.FromResult<ServiceNetworkStatus>(null);
            }

            try
            {
                var service = _serviceRegistry.GetServiceById(serviceId);
                if (service == null)
                {
                    _logger.LogWarning("未找到服务 {ServiceId}", serviceId);
                    return Task.FromResult<ServiceNetworkStatus>(null);
                }

                if (service.Status != 1)
                {
                    var unavailableStatus = MarkServiceUnavailable(serviceId);
                    return Task.FromResult(unavailableStatus);
                }

                if (_networkStatuses.TryGetValue(serviceId, out var existing) && existing != null)
                {
                    // 在线服务优先沿用心跳记录的 RTT，仅刷新可用状态与检测时间。
                    var refreshed = new ServiceNetworkStatus
                    {
                        ServiceId = serviceId,
                        ResponseTime = NormalizeResponseTime(existing.ResponseTime),
                        PacketLoss = 0,
                        LastCheckTime = DateTime.Now,
                        ConsecutiveSuccesses = existing.ConsecutiveSuccesses > 0 ? existing.ConsecutiveSuccesses : 1,
                        ConsecutiveFailures = 0,
                        IsAvailable = true,
                    };

                    _networkStatuses[serviceId] = refreshed;
                    return Task.FromResult(refreshed);
                }

                // 缓存缺失时使用“距最近心跳时间”作为轻量估算，不触发网络探测。
                var fallbackRtt = EstimateResponseTimeFromHeartbeat(service);
                var status = UpdateNetworkStatusFromHeartbeat(serviceId, fallbackRtt);
                return Task.FromResult(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "评估服务 {ServiceId} 网络状态时发生错误", serviceId);
                return Task.FromResult<ServiceNetworkStatus>(null);
            }
        }

        /// <summary>
        /// 评估指定类型所有服务的网络状态。
        /// </summary>
        /// <param name="serviceName">服务名称/类型。</param>
        /// <returns>所有服务的网络状态。</returns>
        public async Task<List<ServiceNetworkStatus>> EvaluateServiceTypeNetworkAsync(string serviceName)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                return new List<ServiceNetworkStatus>();
            }

            try
            {
                // 获取指定类型的所有在线服务。
                var services = _serviceRegistry.GetServicesByName(serviceName);
                if (services == null || services.Count == 0)
                {
                    _logger.LogInformation("未找到类型为 {ServiceName} 的在线服务", serviceName);
                    return new List<ServiceNetworkStatus>();
                }

                // 并行执行轻量评估（仅缓存/状态合成）。
                var tasks = services.Select(service => EvaluateServiceNetworkAsync(service.Id)).ToList();
                var results = await Task.WhenAll(tasks);
                return results.Where(result => result != null).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "评估服务类型 {ServiceName} 网络状态时发生错误", serviceName);
                return new List<ServiceNetworkStatus>();
            }
        }

        /// <summary>
        /// 获取指定类型中网络状态最佳的服务。
        /// </summary>
        /// <param name="serviceName">服务名称/类型。</param>
        /// <returns>网络状态最佳的服务。</returns>
        public async Task<ServiceInfo> GetBestServiceInstanceAsync(string serviceName)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                return null;
            }

            try
            {
                var networkStatuses = await EvaluateServiceTypeNetworkAsync(serviceName);
                if (networkStatuses == null || networkStatuses.Count == 0)
                {
                    _logger.LogWarning("无法评估服务类型 {ServiceName} 的网络状态，回退到轮询算法", serviceName);
                    return _serviceRegistry.GetServiceInstanceRoundRobin(serviceName);
                }

                var sortedStatuses = networkStatuses
                    .Where(status => status.IsAvailable)
                    .OrderByDescending(status => status.CalculateScore())
                    .ToList();

                if (sortedStatuses.Count == 0)
                {
                    _logger.LogWarning("服务类型 {ServiceName} 没有可用服务，回退到轮询算法", serviceName);
                    return _serviceRegistry.GetServiceInstanceRoundRobin(serviceName);
                }

                var bestServiceId = sortedStatuses.First().ServiceId;
                var bestService = _serviceRegistry.GetServiceById(bestServiceId);
                if (bestService == null)
                {
                    _logger.LogWarning("无法找到ID为 {ServiceId} 的服务信息，回退到轮询算法", bestServiceId);
                    return _serviceRegistry.GetServiceInstanceRoundRobin(serviceName);
                }

                _logger.LogInformation(
                    "选择了网络状态最佳的服务: {ServiceName} ({ServiceId}), 评分: {Score}",
                    bestService.Name,
                    bestService.Id,
                    sortedStatuses.First().CalculateScore());
                return bestService;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取服务类型 {ServiceName} 最佳服务实例时发生错误", serviceName);
                return _serviceRegistry.GetServiceInstanceRoundRobin(serviceName);
            }
        }

        /// <summary>
        /// 获取所有服务的网络状态。
        /// </summary>
        /// <returns>所有服务的网络状态。</returns>
        public List<ServiceNetworkStatus> GetAllNetworkStatuses()
        {
            try
            {
                _lock.EnterReadLock();
                return _networkStatuses.Values.ToList();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// 获取指定服务的网络状态。
        /// </summary>
        /// <param name="serviceId">服务ID。</param>
        /// <returns>服务网络状态。</returns>
        public ServiceNetworkStatus GetServiceNetworkStatus(string serviceId)
        {
            if (string.IsNullOrWhiteSpace(serviceId))
            {
                return null;
            }

            try
            {
                _lock.EnterReadLock();
                return _networkStatuses.TryGetValue(serviceId, out var status) ? status : null;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        private static long NormalizeResponseTime(long responseTimeMs)
        {
            if (responseTimeMs <= 0)
            {
                return 1;
            }

            return Math.Min(responseTimeMs, UnavailableResponseTimeMs);
        }

        private static long EstimateResponseTimeFromHeartbeat(ServiceInfo service)
        {
            if (service == null || service.LastHeartbeatTime <= DateTime.MinValue)
            {
                return DefaultResponseTimeMs;
            }

            var elapsed = DateTime.Now - service.LastHeartbeatTime;
            if (elapsed <= TimeSpan.Zero)
            {
                return 1;
            }

            // 仅用于缓存缺失时的轻量估算，避免过大抖动。
            var estimate = (long)Math.Ceiling(elapsed.TotalMilliseconds);
            if (estimate <= 0)
            {
                return DefaultResponseTimeMs;
            }

            return Math.Min(estimate, UnavailableResponseTimeMs);
        }
    }
}
