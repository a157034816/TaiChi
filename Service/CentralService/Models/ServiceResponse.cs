using System.Collections.Generic;

namespace CentralService.Models
{
    /// <summary>
    /// 服务发现响应
    /// </summary>
    public class ServiceDiscoveryResponse
    {
        /// <summary>
        /// 服务实例
        /// </summary>
        public ServiceInfo Service { get; set; }
    }

    /// <summary>
    /// 服务列表响应
    /// </summary>
    public class ServiceListResponse
    {
        /// <summary>
        /// 服务列表
        /// </summary>
        public List<ServiceInfo> Services { get; set; }
    }
}