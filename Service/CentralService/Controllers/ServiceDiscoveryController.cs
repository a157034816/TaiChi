using CentralService.Models;
using CentralService.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace CentralService.Controllers
{
    /// <summary>
    /// 服务发现控制器 - 提供服务发现API
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ServiceDiscoveryController : ControllerBase
    {
        private readonly ServiceRegistry _serviceRegistry;
        private readonly ServiceNetworkEvaluator _networkEvaluator;
        private readonly CentralServiceServiceSelector _serviceSelector;
        private readonly ILogger<ServiceDiscoveryController> _logger;

        /// <summary>
        /// 初始化服务发现控制器
        /// </summary>
        /// <param name="serviceRegistry">服务注册表</param>
        /// <param name="networkEvaluator">网络评估器</param>
        /// <param name="logger">日志记录器</param>
        public ServiceDiscoveryController(
            ServiceRegistry serviceRegistry,
            ServiceNetworkEvaluator networkEvaluator,
            CentralServiceServiceSelector serviceSelector,
            ILogger<ServiceDiscoveryController> logger)
        {
            _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
            _networkEvaluator = networkEvaluator ?? throw new ArgumentNullException(nameof(networkEvaluator));
            _serviceSelector = serviceSelector ?? throw new ArgumentNullException(nameof(serviceSelector));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 获取所有服务
        /// </summary>
        /// <returns>所有服务列表</returns>
        [HttpGet("all")]
        public ActionResult<IEnumerable<ServiceInfo>> GetAllServices()
        {
            try
            {
                var services = _serviceRegistry.GetAllServices();
                return Ok(services);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取所有服务时发生错误");
                return StatusCode(500, "内部服务器错误");
            }
        }

        /// <summary>
        /// 获取指定类型的所有服务
        /// </summary>
        /// <param name="serviceName">服务名称/类型</param>
        /// <returns>指定类型的服务列表</returns>
        [HttpGet("type/{serviceName}")]
        public ActionResult<IEnumerable<ServiceInfo>> GetServicesByName(string serviceName)
        {
            try
            {
                if (string.IsNullOrEmpty(serviceName))
                {
                    return BadRequest("服务名称不能为空");
                }

                var services = _serviceRegistry.GetServicesByName(serviceName);
                return Ok(services);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"获取服务类型 {serviceName} 的服务时发生错误");
                return StatusCode(500, "内部服务器错误");
            }
        }

        /// <summary>
        /// 使用轮询算法发现服务实例
        /// </summary>
        /// <param name="serviceName">服务名称/类型</param>
        /// <returns>服务实例</returns>
        [HttpGet("discover/roundrobin/{serviceName}")]
        public ActionResult<ServiceInfo> DiscoverServiceRoundRobin(string serviceName)
        {
            try
            {
                if (string.IsNullOrEmpty(serviceName))
                {
                    return BadRequest("服务名称不能为空");
                }

                var service = _serviceSelector.DiscoverServiceRoundRobin(serviceName, GetRequesterIpAddress());

                if (service == null)
                {
                    return NotFound($"未找到类型为 {serviceName} 的服务");
                }

                return Ok(service);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"使用轮询算法发现服务 {serviceName} 时发生错误");
                return StatusCode(500, "内部服务器错误");
            }
        }

        /// <summary>
        /// 使用权重随机算法发现服务实例
        /// </summary>
        /// <param name="serviceName">服务名称/类型</param>
        /// <returns>服务实例</returns>
        [HttpGet("discover/weighted/{serviceName}")]
        public ActionResult<ServiceInfo> DiscoverServiceWeighted(string serviceName)
        {
            try
            {
                if (string.IsNullOrEmpty(serviceName))
                {
                    return BadRequest("服务名称不能为空");
                }

                var service = _serviceSelector.DiscoverServiceWeighted(serviceName, GetRequesterIpAddress());

                if (service == null)
                {
                    return NotFound($"未找到类型为 {serviceName} 的服务");
                }

                return Ok(service);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"使用权重随机算法发现服务 {serviceName} 时发生错误");
                return StatusCode(500, "内部服务器错误");
            }
        }

        /// <summary>
        /// 使用网络状态评估算法发现服务实例
        /// </summary>
        /// <param name="serviceName">服务名称/类型</param>
        /// <returns>网络状态最佳的服务实例</returns>
        [HttpGet("discover/best/{serviceName}")]
        public async Task<ActionResult<ServiceInfo>> DiscoverBestServiceAsync(string serviceName)
        {
            try
            {
                if (string.IsNullOrEmpty(serviceName))
                {
                    return BadRequest("服务名称不能为空");
                }

                var service = await _serviceSelector.DiscoverBestServiceAsync(serviceName, GetRequesterIpAddress());

                if (service == null)
                {
                    return NotFound($"未找到类型为 {serviceName} 的服务");
                }

                return Ok(service);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"使用网络状态评估算法发现服务 {serviceName} 时发生错误");
                return StatusCode(500, "内部服务器错误");
            }
        }

        /// <summary>
        /// 获取所有服务的网络状态
        /// </summary>
        /// <returns>所有服务的网络状态</returns>
        [HttpGet("network/all")]
        public ActionResult<IEnumerable<ServiceNetworkStatus>> GetAllNetworkStatuses()
        {
            try
            {
                var networkStatuses = _networkEvaluator.GetAllNetworkStatuses();
                return Ok(networkStatuses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取所有服务网络状态时发生错误");
                return StatusCode(500, "内部服务器错误");
            }
        }

        /// <summary>
        /// 获取指定服务的网络状态
        /// </summary>
        /// <param name="serviceId">服务ID</param>
        /// <returns>服务网络状态</returns>
        [HttpGet("network/{serviceId}")]
        public ActionResult<ServiceNetworkStatus> GetServiceNetworkStatus(string serviceId)
        {
            try
            {
                if (string.IsNullOrEmpty(serviceId))
                {
                    return BadRequest("服务ID不能为空");
                }

                var networkStatus = _networkEvaluator.GetServiceNetworkStatus(serviceId);

                if (networkStatus == null)
                {
                    return NotFound($"未找到ID为 {serviceId} 的服务网络状态");
                }

                return Ok(networkStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"获取服务 {serviceId} 网络状态时发生错误");
                return StatusCode(500, "内部服务器错误");
            }
        }

        /// <summary>
        /// 立即评估指定服务的网络状态
        /// </summary>
        /// <param name="serviceId">服务ID</param>
        /// <returns>服务网络状态</returns>
        [HttpPost("network/evaluate/{serviceId}")]
        public async Task<ActionResult<ServiceNetworkStatus>> EvaluateServiceNetworkAsync(string serviceId)
        {
            try
            {
                if (string.IsNullOrEmpty(serviceId))
                {
                    return BadRequest("服务ID不能为空");
                }

                var service = _serviceRegistry.GetAllServices().FirstOrDefault(s => s.Id == serviceId);

                if (service == null)
                {
                    return NotFound($"未找到ID为 {serviceId} 的服务");
                }

                var networkStatus = await _networkEvaluator.EvaluateServiceNetworkAsync(serviceId);

                if (networkStatus == null)
                {
                    return StatusCode(500, $"评估服务 {serviceId} 网络状态失败");
                }

                return Ok(networkStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"评估服务 {serviceId} 网络状态时发生错误");
                return StatusCode(500, "内部服务器错误");
            }
        }

        /// <summary>
        /// 获取当前请求方访问中心服务时使用的IP地址。
        /// </summary>
        /// <returns>请求方IP地址。</returns>
        private string GetRequesterIpAddress()
        {
            try
            {
                return HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "获取请求方IP失败");
                return string.Empty;
            }
        }
    }
}
