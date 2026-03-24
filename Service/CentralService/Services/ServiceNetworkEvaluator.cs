using CentralService.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CentralService.Services
{
    /// <summary>
    /// 服务网络评估器 - 评估已注册服务的网络状态并找出最佳服务
    /// </summary>
    public class ServiceNetworkEvaluator
    {
        private readonly ServiceRegistry _serviceRegistry;
        private readonly ILogger<ServiceNetworkEvaluator> _logger;
        
        // 存储服务网络状态的线程安全字典
        private readonly ConcurrentDictionary<string, ServiceNetworkStatus> _networkStatuses = new ConcurrentDictionary<string, ServiceNetworkStatus>();
        
        // 用于线程同步的锁对象
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
        
        // Ping检测超时时间(毫秒)
        private readonly int _pingTimeoutMs = 3000;
        
        // Ping检测次数
        private readonly int _pingCount = 5;

        /// <summary>
        /// 初始化服务网络评估器
        /// </summary>
        /// <param name="serviceRegistry">服务注册表</param>
        /// <param name="logger">日志记录器</param>
        public ServiceNetworkEvaluator(
            ServiceRegistry serviceRegistry,
            ILogger<ServiceNetworkEvaluator> logger)
        {
            _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 评估指定服务的网络状态
        /// </summary>
        /// <param name="serviceId">服务ID</param>
        /// <returns>服务网络状态</returns>
        public async Task<ServiceNetworkStatus> EvaluateServiceNetworkAsync(string serviceId)
        {
            if (string.IsNullOrEmpty(serviceId))
                return null;

            try
            {
                // 获取服务信息
                var service = _serviceRegistry.GetAllServices().FirstOrDefault(s => s.Id == serviceId);
                if (service == null)
                {
                    _logger.LogWarning($"未找到服务 {serviceId}");
                    return null;
                }

                // 创建网络状态对象
                var networkStatus = new ServiceNetworkStatus
                {
                    ServiceId = serviceId,
                    LastCheckTime = DateTime.Now
                };

                // 执行Ping测试
                var pingResults = await PingHostAsync(service.Host, _pingCount);
                
                // 计算响应时间和丢包率
                long totalResponseTime = 0;
                int successCount = 0;

                foreach (var result in pingResults)
                {
                    if (result != null && result.Status == IPStatus.Success)
                    {
                        totalResponseTime += result.RoundtripTime;
                        successCount++;
                    }
                }

                // 计算平均响应时间和丢包率
                networkStatus.ResponseTime = successCount > 0 ? totalResponseTime / successCount : _pingTimeoutMs;
                networkStatus.PacketLoss = 100.0 * (_pingCount - successCount) / _pingCount;
                networkStatus.IsAvailable = successCount > 0;

                // 更新连续成功/失败次数
                if (_networkStatuses.TryGetValue(serviceId, out var existingStatus))
                {
                    if (networkStatus.IsAvailable)
                    {
                        networkStatus.ConsecutiveSuccesses = existingStatus.ConsecutiveSuccesses + 1;
                        networkStatus.ConsecutiveFailures = 0;
                    }
                    else
                    {
                        networkStatus.ConsecutiveSuccesses = 0;
                        networkStatus.ConsecutiveFailures = existingStatus.ConsecutiveFailures + 1;
                    }
                }
                else
                {
                    if (networkStatus.IsAvailable)
                    {
                        networkStatus.ConsecutiveSuccesses = 1;
                        networkStatus.ConsecutiveFailures = 0;
                    }
                    else
                    {
                        networkStatus.ConsecutiveSuccesses = 0;
                        networkStatus.ConsecutiveFailures = 1;
                    }
                }

                // 更新状态缓存
                _networkStatuses[serviceId] = networkStatus;

                return networkStatus;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"评估服务 {serviceId} 网络状态时发生错误");
                return null;
            }
        }

        /// <summary>
        /// 评估指定类型所有服务的网络状态
        /// </summary>
        /// <param name="serviceName">服务名称/类型</param>
        /// <returns>所有服务的网络状态</returns>
        public async Task<List<ServiceNetworkStatus>> EvaluateServiceTypeNetworkAsync(string serviceName)
        {
            if (string.IsNullOrEmpty(serviceName))
                return new List<ServiceNetworkStatus>();

            try
            {
                // 获取指定类型的所有服务
                var services = _serviceRegistry.GetServicesByName(serviceName);
                if (services == null || services.Count == 0)
                {
                    _logger.LogInformation($"未找到类型为 {serviceName} 的服务");
                    return new List<ServiceNetworkStatus>();
                }

                // 并行评估所有服务的网络状态
                var tasks = services.Select(service => EvaluateServiceNetworkAsync(service.Id)).ToList();
                
                // 等待所有任务完成
                var results = await Task.WhenAll(tasks);
                
                // 过滤掉失败的结果
                return results.Where(result => result != null).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"评估服务类型 {serviceName} 网络状态时发生错误");
                return new List<ServiceNetworkStatus>();
            }
        }

        /// <summary>
        /// 获取指定类型中网络状态最佳的服务
        /// </summary>
        /// <param name="serviceName">服务名称/类型</param>
        /// <returns>网络状态最佳的服务</returns>
        public async Task<ServiceInfo> GetBestServiceInstanceAsync(string serviceName)
        {
            if (string.IsNullOrEmpty(serviceName))
                return null;

            try
            {
                // 评估所有服务的网络状态
                var networkStatuses = await EvaluateServiceTypeNetworkAsync(serviceName);
                if (networkStatuses == null || networkStatuses.Count == 0)
                {
                    // 如果无法评估网络状态,则回退到轮询算法
                    _logger.LogWarning($"无法评估服务类型 {serviceName} 的网络状态,回退到轮询算法");
                    return _serviceRegistry.GetServiceInstanceRoundRobin(serviceName);
                }

                // 根据网络状态评分排序
                var sortedStatuses = networkStatuses
                    .Where(status => status.IsAvailable)
                    .OrderByDescending(status => status.CalculateScore())
                    .ToList();

                if (sortedStatuses.Count == 0)
                {
                    // 如果没有可用的服务,则回退到轮询算法
                    _logger.LogWarning($"服务类型 {serviceName} 没有可用的服务,回退到轮询算法");
                    return _serviceRegistry.GetServiceInstanceRoundRobin(serviceName);
                }

                // 获取得分最高的服务ID
                string bestServiceId = sortedStatuses.First().ServiceId;
                
                // 获取服务信息
                var bestService = _serviceRegistry.GetAllServices().FirstOrDefault(s => s.Id == bestServiceId);
                
                if (bestService == null)
                {
                    // 如果找不到服务信息,则回退到轮询算法
                    _logger.LogWarning($"无法找到ID为 {bestServiceId} 的服务信息,回退到轮询算法");
                    return _serviceRegistry.GetServiceInstanceRoundRobin(serviceName);
                }

                _logger.LogInformation($"选择了网络状态最佳的服务: {bestService.Name} ({bestService.Id}), 评分: {sortedStatuses.First().CalculateScore()}");
                return bestService;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"获取服务类型 {serviceName} 最佳服务实例时发生错误");
                // 出错时回退到轮询算法
                return _serviceRegistry.GetServiceInstanceRoundRobin(serviceName);
            }
        }

        /// <summary>
        /// 对主机执行Ping测试
        /// </summary>
        /// <param name="host">主机地址</param>
        /// <param name="count">Ping次数</param>
        /// <returns>Ping结果列表</returns>
        private async Task<List<PingReply>> PingHostAsync(string host, int count)
        {
            var results = new List<PingReply>();
            
            try
            {
                // 尝试解析主机名
                IPAddress ipAddress;
                if (!IPAddress.TryParse(host, out ipAddress))
                {
                    try
                    {
                        var hostEntry = await Dns.GetHostEntryAsync(host);
                        ipAddress = hostEntry.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
                        
                        if (ipAddress == null)
                        {
                            _logger.LogWarning($"无法解析主机名 {host} 到IPv4地址");
                            return results;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"解析主机名 {host} 时发生错误");
                        return results;
                    }
                }

                // 创建Ping对象
                using (var ping = new Ping())
                {
                    // 创建Ping选项
                    var options = new PingOptions
                    {
                        DontFragment = true
                    };
                    
                    // 创建32字节的缓冲区
                    byte[] buffer = new byte[32];
                    new Random().NextBytes(buffer);

                    // 执行指定次数的Ping测试
                    for (int i = 0; i < count; i++)
                    {
                        try
                        {
                            var reply = await ping.SendPingAsync(ipAddress, _pingTimeoutMs, buffer, options);
                            results.Add(reply);
                            
                            // 在Ping之间等待一小段时间
                            if (i < count - 1)
                            {
                                await Task.Delay(100);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, $"Ping {host} 时发生错误");
                            // 不再尝试创建PingReply对象，而是记录一次失败
                            // 由于无法直接实例化PingReply对象（构造函数是internal的）
                            results.Add(null);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"执行Ping测试 {host} 时发生错误");
            }
            
            return results;
        }

        /// <summary>
        /// 获取所有服务的网络状态
        /// </summary>
        /// <returns>所有服务的网络状态</returns>
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
        /// 获取指定服务的网络状态
        /// </summary>
        /// <param name="serviceId">服务ID</param>
        /// <returns>服务网络状态</returns>
        public ServiceNetworkStatus GetServiceNetworkStatus(string serviceId)
        {
            if (string.IsNullOrEmpty(serviceId))
                return null;
                
            try
            {
                _lock.EnterReadLock();
                
                if (_networkStatuses.TryGetValue(serviceId, out var status))
                {
                    return status;
                }
                
                return null;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }
} 