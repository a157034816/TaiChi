using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CentralService.Client.Models
{
    /// <summary>
    /// 表示中心服务中登记的服务实例信息。
    /// </summary>
    [DataContract]
    public sealed class ServiceInfo
    {
        /// <summary>
        /// 获取或设置服务实例标识。
        /// </summary>
        [DataMember(Name = "id", Order = 1)]
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
        /// 获取或设置服务完整访问地址。
        /// </summary>
        [DataMember(Name = "url", Order = 5, EmitDefaultValue = false)]
        public string Url { get; set; }

        /// <summary>
        /// 获取或设置服务类型。
        /// </summary>
        [DataMember(Name = "serviceType", Order = 6, EmitDefaultValue = false)]
        public string ServiceType { get; set; }

        /// <summary>
        /// 获取或设置服务状态值。
        /// </summary>
        [DataMember(Name = "status", Order = 7, EmitDefaultValue = false)]
        public int Status { get; set; }

        /// <summary>
        /// 获取或设置健康检查地址。
        /// </summary>
        [DataMember(Name = "healthCheckUrl", Order = 8, EmitDefaultValue = false)]
        public string HealthCheckUrl { get; set; }

        /// <summary>
        /// 获取或设置健康检查端口。
        /// </summary>
        [DataMember(Name = "healthCheckPort", Order = 9, EmitDefaultValue = false)]
        public int HealthCheckPort { get; set; }

        /// <summary>
        /// 获取或设置健康检查类型。
        /// </summary>
        [DataMember(Name = "healthCheckType", Order = 10, EmitDefaultValue = false)]
        public string HealthCheckType { get; set; }

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
        /// 获取或设置负载均衡权重。
        /// </summary>
        [DataMember(Name = "weight", Order = 13, EmitDefaultValue = false)]
        public int Weight { get; set; }

        /// <summary>
        /// 获取或设置服务元数据。
        /// </summary>
        [DataMember(Name = "metadata", Order = 14, EmitDefaultValue = false)]
        public Dictionary<string, string> Metadata { get; set; }

        /// <summary>
        /// 获取或设置当前服务是否位于本地网络。
        /// </summary>
        [DataMember(Name = "isLocalNetwork", Order = 15, EmitDefaultValue = false)]
        public bool IsLocalNetwork { get; set; }
    }
}
