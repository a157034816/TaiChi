using System;
using System.Collections.Generic;

namespace CentralService.Models
{
    /// <summary>
    /// 服务信息模型
    /// </summary>
    public class ServiceInfo
    {
        /// <summary>
        /// 服务ID
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 服务名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 服务地址
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// 服务实例所在主机的局域网IP。
        /// </summary>
        public string LocalIp { get; set; } = string.Empty;

        /// <summary>
        /// 服务实例所在主机的运营商IP。
        /// </summary>
        public string OperatorIp { get; set; } = string.Empty;

        /// <summary>
        /// 服务实例所在主机的公网IP。
        /// </summary>
        public string PublicIp { get; set; } = string.Empty;

        /// <summary>
        /// 服务端口
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// 服务URL
        /// </summary>
        public string Url => $"http://{Host}:{Port}";

        /// <summary>
        /// 服务类型 (Web, Socket)
        /// </summary>
        public string ServiceType { get; set; } = "Web";

        /// <summary>
        /// 服务状态 (0-离线, 1-在线, 2-故障)
        /// </summary>
        public int Status { get; set; }

        /// <summary>
        /// 健康检查URL（Web服务使用）
        /// </summary>
        public string HealthCheckUrl { get; set; }

        /// <summary>
        /// 健康检查端口（Socket服务使用，如果为0则使用服务端口）
        /// </summary>
        public int HealthCheckPort { get; set; }

        /// <summary>
        /// 健康检查类型 (Http, Socket)
        /// </summary>
        public string HealthCheckType { get; set; } = "Http";

        /// <summary>
        /// 服务注册时间
        /// </summary>
        public DateTime RegisterTime { get; set; }

        /// <summary>
        /// 最后心跳时间
        /// </summary>
        public DateTime LastHeartbeatTime { get; set; }

        /// <summary>
        /// 服务权重（用于负载均衡）
        /// </summary>
        public int Weight { get; set; } = 1;

        /// <summary>
        /// 服务元数据（可存储额外信息）
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
        
        /// <summary>
        /// 是否与中心服务在同一局域网
        /// </summary>
        public bool IsLocalNetwork { get; set; }
    }
} 
