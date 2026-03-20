using System.Collections.Generic;

namespace CentralService.Models
{
    /// <summary>
    /// 服务发现请求
    /// </summary>
    public class ServiceDiscoveryRequest
    {
        /// <summary>
        /// 服务名称（必填）
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 负载均衡策略（可选）
        /// 可选值：RoundRobin（轮询）, Weighted（权重）, Random（随机）
        /// 默认为 RoundRobin
        /// </summary>
        public string LoadBalanceStrategy { get; set; } = "RoundRobin";
    }
} 