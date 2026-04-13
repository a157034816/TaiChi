using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CentralService.Client.Models
{
    /// <summary>
    /// 表示服务注册请求。
    /// </summary>
    [DataContract]
    public sealed class ServiceRegistrationRequest
    {
        /// <summary>
        /// 获取或设置服务实例标识。
        /// </summary>
        [DataMember(Name = "id", Order = 1, EmitDefaultValue = false)]
        public string Id { get; set; }

        /// <summary>
        /// 获取或设置服务名称。
        /// </summary>
        [DataMember(Name = "name", Order = 2)]
        public string Name { get; set; }

        /// <summary>
        /// 获取或设置服务主机地址。
        /// </summary>
        [DataMember(Name = "host", Order = 3)]
        public string Host { get; set; }

        /// <summary>
        /// 获取或设置服务端口。
        /// </summary>
        [DataMember(Name = "port", Order = 4)]
        public int Port { get; set; }

        /// <summary>
        /// 获取或设置服务类型。
        /// </summary>
        [DataMember(Name = "serviceType", Order = 5, EmitDefaultValue = false)]
        public string ServiceType { get; set; }

        /// <summary>
        /// 获取或设置健康检查地址。
        /// </summary>
        [DataMember(Name = "healthCheckUrl", Order = 6, EmitDefaultValue = false)]
        public string HealthCheckUrl { get; set; }

        /// <summary>
        /// 获取或设置健康检查端口。
        /// </summary>
        [DataMember(Name = "healthCheckPort", Order = 7, EmitDefaultValue = false)]
        public int HealthCheckPort { get; set; }

        /// <summary>
        /// 获取或设置中心服务通过 WebSocket 向该服务发送心跳请求的频率（秒）。
        /// 为 0 时表示不发送心跳请求。
        /// </summary>
        [DataMember(Name = "heartbeatIntervalSeconds", Order = 8, EmitDefaultValue = false)]
        public int HeartbeatIntervalSeconds { get; set; }

        /// <summary>
        /// 获取或设置负载均衡权重。
        /// </summary>
        [DataMember(Name = "weight", Order = 9, EmitDefaultValue = false)]
        public int Weight { get; set; }

        /// <summary>
        /// 获取或设置附加元数据。
        /// </summary>
        [DataMember(Name = "metadata", Order = 10, EmitDefaultValue = false)]
        public Dictionary<string, string> Metadata { get; set; }
    }
}
