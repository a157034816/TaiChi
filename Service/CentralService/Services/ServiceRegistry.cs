using CentralService.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace CentralService.Services
{
    /// <summary>
    /// 服务注册表 - 管理所有注册的服务
    /// </summary>
    public class ServiceRegistry
    {
        // 使用线程安全的集合存储服务信息
        private readonly ConcurrentDictionary<string, ServiceInfo> _services = new ConcurrentDictionary<string, ServiceInfo>();

        // 按服务类型分组的服务字典
        private readonly ConcurrentDictionary<string, List<string>> _servicesByType = new ConcurrentDictionary<string, List<string>>();

        // 用于负载均衡的计数器
        private readonly ConcurrentDictionary<string, int> _roundRobinCounters = new ConcurrentDictionary<string, int>();

        // 服务心跳超时时间（秒）
        // 作为最小阈值；实际超时时间会根据服务注册时的 HeartbeatIntervalSeconds 动态放大。
        private readonly int _heartbeatTimeoutSeconds = 30;

        // 用于线程同步的锁对象
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        /// <summary>
        /// 注册服务
        /// </summary>
        /// <param name="serviceInfo">服务信息</param>
        /// <returns>是否注册成功</returns>
        public bool RegisterService(ServiceInfo serviceInfo)
        {
            if (serviceInfo == null)
                return false;

            try
            {
                _lock.EnterWriteLock();

                // 设置注册时间和心跳时间
                serviceInfo.RegisterTime = DateTime.Now;
                serviceInfo.LastHeartbeatTime = DateTime.Now;

                // WebSocket 心跳模式下：
                // - 当 HeartbeatIntervalSeconds > 0 时，注册仅表示“登记”，在线状态由心跳通道建立并成功响应后更新。
                // - 当 HeartbeatIntervalSeconds <= 0 时，表示禁用心跳监控，注册即视为在线。
                serviceInfo.Status = serviceInfo.HeartbeatIntervalSeconds > 0 ? 0 : 1;

                // 添加或更新服务信息
                _services[serviceInfo.Id] = serviceInfo;

                // 按服务类型分组
                if (!_servicesByType.ContainsKey(serviceInfo.Name))
                {
                    _servicesByType[serviceInfo.Name] = new List<string>();
                }

                if (!_servicesByType[serviceInfo.Name].Contains(serviceInfo.Id))
                {
                    _servicesByType[serviceInfo.Name].Add(serviceInfo.Id);
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// 注销服务
        /// </summary>
        /// <param name="serviceId">服务ID</param>
        /// <returns>是否注销成功</returns>
        public bool DeregisterService(string serviceId)
        {
            if (string.IsNullOrEmpty(serviceId))
                return false;

            try
            {
                _lock.EnterWriteLock();

                if (_services.TryRemove(serviceId, out ServiceInfo removedService))
                {
                    // 从服务类型分组中移除
                    if (_servicesByType.TryGetValue(removedService.Name, out List<string> serviceIds))
                    {
                        serviceIds.Remove(serviceId);

                        // 如果该类型没有服务了，移除该类型
                        if (serviceIds.Count == 0)
                        {
                            _servicesByType.TryRemove(removedService.Name, out _);
                        }
                    }

                    return true;
                }

                return false;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// 更新服务心跳
        /// </summary>
        /// <param name="serviceId">服务ID</param>
        /// <returns>是否更新成功</returns>
        public bool UpdateHeartbeat(string serviceId)
        {
            if (string.IsNullOrEmpty(serviceId))
                return false;

            try
            {
                _lock.EnterReadLock();

                if (_services.TryGetValue(serviceId, out ServiceInfo service))
                {
                    service.LastHeartbeatTime = DateTime.Now;
                    service.Status = 1; // 在线状态
                    return true;
                }

                return false;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// 将服务标记为离线状态（不会移除注册信息）。
        /// </summary>
        /// <param name="serviceId">服务ID</param>
        /// <returns>是否更新成功</returns>
        public bool MarkOffline(string serviceId)
        {
            if (string.IsNullOrEmpty(serviceId))
                return false;

            try
            {
                _lock.EnterReadLock();

                if (_services.TryGetValue(serviceId, out ServiceInfo service))
                {
                    service.Status = 0; // 离线状态
                    return true;
                }

                return false;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// 将服务标记为故障状态（不会移除注册信息）。
        /// </summary>
        /// <param name="serviceId">服务ID</param>
        /// <returns>是否更新成功</returns>
        public bool MarkFault(string serviceId)
        {
            if (string.IsNullOrEmpty(serviceId))
                return false;

            try
            {
                _lock.EnterReadLock();

                if (_services.TryGetValue(serviceId, out ServiceInfo service))
                {
                    service.Status = 2; // 故障状态
                    return true;
                }

                return false;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// 获取所有服务
        /// </summary>
        /// <returns>服务列表</returns>
        public List<ServiceInfo> GetAllServices()
        {
            try
            {
                _lock.EnterReadLock();
                return _services.Values.ToList();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// 根据服务ID获取指定服务。
        /// </summary>
        public ServiceInfo? GetServiceById(string? serviceId)
        {
            if (string.IsNullOrWhiteSpace(serviceId))
            {
                return null;
            }

            try
            {
                _lock.EnterReadLock();
                return _services.TryGetValue(serviceId, out var service) ? service : null;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// 获取指定类型的所有服务
        /// </summary>
        /// <param name="serviceName">服务名称/类型</param>
        /// <returns>服务列表</returns>
        public List<ServiceInfo> GetServicesByName(string serviceName)
        {
            if (string.IsNullOrEmpty(serviceName))
                return new List<ServiceInfo>();

            try
            {
                _lock.EnterReadLock();

                if (_servicesByType.TryGetValue(serviceName, out List<string> serviceIds))
                {
                    return serviceIds
                        .Where(id => _services.ContainsKey(id))
                        .Select(id => _services[id])
                        .Where(s => s.Status == 1) // 只返回在线的服务
                        .ToList();
                }

                return new List<ServiceInfo>();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// 使用轮询算法获取服务实例
        /// </summary>
        /// <param name="serviceName">服务名称</param>
        /// <returns>服务实例</returns>
        public ServiceInfo GetServiceInstanceRoundRobin(string serviceName)
        {
            var services = GetServicesByName(serviceName);
            if (services == null || services.Count == 0)
                return null;

            try
            {
                _lock.EnterWriteLock();

                // 获取当前计数
                int currentCount = _roundRobinCounters.GetOrAdd(serviceName, 0);

                // 递增计数并取模，确保在服务列表范围内循环
                int nextCount = (currentCount + 1) % services.Count;
                _roundRobinCounters[serviceName] = nextCount;

                return services[currentCount];
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// 使用权重随机算法获取服务实例
        /// </summary>
        /// <param name="serviceName">服务名称</param>
        /// <returns>服务实例</returns>
        public ServiceInfo GetServiceInstanceWeighted(string serviceName)
        {
            var services = GetServicesByName(serviceName);
            if (services == null || services.Count == 0)
                return null;

            // 计算总权重
            int totalWeight = services.Sum(s => s.Weight);
            if (totalWeight <= 0)
                return GetServiceInstanceRoundRobin(serviceName); // 如果总权重为0，使用轮询算法

            // 生成随机数
            int randomWeight = new Random().Next(1, totalWeight + 1);

            // 根据权重选择服务
            int currentWeight = 0;
            foreach (var service in services)
            {
                currentWeight += service.Weight;
                if (randomWeight <= currentWeight)
                    return service;
            }

            // 默认返回第一个服务
            return services.FirstOrDefault();
        }

        /// <summary>
        /// 检查并移除超时的服务
        /// </summary>
        public void CheckAndRemoveDeadServices()
        {
            var now = DateTime.Now;
            var deadServices = new List<string>();

            try
            {
                _lock.EnterReadLock();

                // 找出超时的服务
                foreach (var service in _services.Values)
                {
                    var timeoutSeconds = ResolveHeartbeatTimeoutSeconds(service);
                    if (timeoutSeconds <= 0)
                    {
                        // 服务显式关闭心跳监控时，不参与超时剔除。
                        continue;
                    }

                    TimeSpan timeSinceLastHeartbeat = now - service.LastHeartbeatTime;
                    if (timeSinceLastHeartbeat.TotalSeconds > timeoutSeconds)
                    {
                        deadServices.Add(service.Id);
                    }
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            // 移除超时的服务
            foreach (var serviceId in deadServices)
            {
                DeregisterService(serviceId);
            }
        }

        private int ResolveHeartbeatTimeoutSeconds(ServiceInfo service)
        {
            if (service == null)
            {
                return _heartbeatTimeoutSeconds;
            }

            if (service.HeartbeatIntervalSeconds <= 0)
            {
                return 0;
            }

            // 允许短暂抖动：默认容忍连续 3 个周期未收到心跳响应。
            var dynamicTimeoutSeconds = service.HeartbeatIntervalSeconds * 3;
            return Math.Max(_heartbeatTimeoutSeconds, dynamicTimeoutSeconds);
        }

        /// <summary>
        /// 检查服务是否已经存在（通过服务名称、三态IP和端口）。
        /// </summary>
        public string ServiceExists(string name, string localIp, string operatorIp, string publicIp, int port)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(localIp) || port <= 0)
                return null;

            try
            {
                _lock.EnterReadLock();

                // 获取指定名称的所有服务ID
                if (_servicesByType.TryGetValue(name, out List<string> serviceIds))
                {
                    // 查找匹配主机和端口的服务
                    foreach (var id in serviceIds)
                    {
                        if (_services.TryGetValue(id, out ServiceInfo service))
                        {
                            if (service.Port == port
                                && string.Equals(service.Name, name, StringComparison.OrdinalIgnoreCase)
                                && string.Equals(service.LocalIp ?? string.Empty, localIp ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                                && string.Equals(service.OperatorIp ?? string.Empty, operatorIp ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                                && string.Equals(service.PublicIp ?? string.Empty, publicIp ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                            {
                                return service.Id;
                            }
                        }
                    }
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
