using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CentralService.Service.Models
{
    /// <summary>
    /// 表示中心服务注册表中记录的服务实例信息。
    /// </summary>
    [DataContract]
    public sealed class ServiceInfo
    {
        /// <summary>
        /// 获取或设置服务实例的唯一标识。
        /// </summary>
        [DataMember(Name = "id", Order = 1)]
        public string Id { get; set; }

        /// <summary>
        /// 获取或设置逻辑服务名。
        /// </summary>
        [DataMember(Name = "name", Order = 2)]
        public string Name { get; set; }

        /// <summary>
        /// 获取或设置服务实例的主机名或 IP 地址。
        /// </summary>
        [DataMember(Name = "host", Order = 3)]
        public string Host { get; set; }

        /// <summary>
        /// 获取或设置服务实例对外提供服务的端口。
        /// </summary>
        [DataMember(Name = "port", Order = 4)]
        public int Port { get; set; }

        /// <summary>
        /// 获取或设置服务实例的完整访问地址。
        /// </summary>
        [DataMember(Name = "url", Order = 5, EmitDefaultValue = false)]
        public string Url { get; set; }

        /// <summary>
        /// 获取或设置服务类型，例如 <c>Web</c> 或 <c>Socket</c>。
        /// </summary>
        [DataMember(Name = "serviceType", Order = 6, EmitDefaultValue = false)]
        public string ServiceType { get; set; }

        /// <summary>
        /// 获取或设置服务端记录的状态码。
        /// </summary>
        [DataMember(Name = "status", Order = 7, EmitDefaultValue = false)]
        public int Status { get; set; }

        /// <summary>
        /// 获取或设置健康检查使用的路径或 URL。
        /// </summary>
        [DataMember(Name = "healthCheckUrl", Order = 8, EmitDefaultValue = false)]
        public string HealthCheckUrl { get; set; }

        /// <summary>
        /// 获取或设置健康检查使用的端口。
        /// </summary>
        [DataMember(Name = "healthCheckPort", Order = 9, EmitDefaultValue = false)]
        public int HealthCheckPort { get; set; }

        /// <summary>
        /// 获取或设置中心服务通过 WebSocket 向该服务发送心跳请求的频率（秒）。
        /// 为 0 时表示不发送心跳请求。
        /// </summary>
        [DataMember(Name = "heartbeatIntervalSeconds", Order = 10, EmitDefaultValue = false)]
        public int HeartbeatIntervalSeconds { get; set; }

        /// <summary>
        /// 获取或设置注册时间。
        /// </summary>
        [DataMember(Name = "registerTime", Order = 11, EmitDefaultValue = false)]
        public string RegisterTime { get; set; }

        /// <summary>
        /// 获取或设置最近一次心跳时间。
        /// </summary>
        [DataMember(Name = "lastHeartbeatTime", Order = 12, EmitDefaultValue = false)]
        public string LastHeartbeatTime { get; set; }

        /// <summary>
        /// 获取或设置加权发现策略使用的权重。
        /// </summary>
        [DataMember(Name = "weight", Order = 13, EmitDefaultValue = false)]
        public int Weight { get; set; }

        /// <summary>
        /// 获取或设置附加的字符串元数据。
        /// </summary>
        [DataMember(Name = "metadata", Order = 14, EmitDefaultValue = false)]
        public Dictionary<string, string> Metadata { get; set; }

        /// <summary>
        /// 获取或设置服务端是否将该实例识别为本地网络节点。
        /// </summary>
        [DataMember(Name = "isLocalNetwork", Order = 15, EmitDefaultValue = false)]
        public bool IsLocalNetwork { get; set; }
    }
}
