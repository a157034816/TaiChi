using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CentralService.Service.Models
{
    /// <summary>
    /// 表示发送到 <c>POST /api/Service/register</c> 的服务注册请求体。
    /// </summary>
    [DataContract]
    public sealed class ServiceRegistrationRequest
    {
        /// <summary>
        /// 获取或设置服务实例 ID；留空时由服务端生成。
        /// </summary>
        [DataMember(Name = "id", Order = 1, EmitDefaultValue = false)]
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
        /// 获取或设置服务实例所在机器的局域网地址。
        /// </summary>
        [DataMember(Name = "localIp", Order = 4, EmitDefaultValue = false)]
        public string LocalIp { get; set; }

        /// <summary>
        /// 获取或设置发起方所在网络标识，用于服务端入口地址选择。
        /// </summary>
        [DataMember(Name = "operatorIp", Order = 5, EmitDefaultValue = false)]
        public string OperatorIp { get; set; }

        /// <summary>
        /// 获取或设置服务实例对发现方暴露的公网或直连地址。
        /// </summary>
        [DataMember(Name = "publicIp", Order = 6, EmitDefaultValue = false)]
        public string PublicIp { get; set; }

        /// <summary>
        /// 获取或设置服务实例对外提供服务的端口。
        /// </summary>
        [DataMember(Name = "port", Order = 7)]
        public int Port { get; set; }

        /// <summary>
        /// 获取或设置服务类型，例如 <c>Web</c> 或 <c>Socket</c>。
        /// </summary>
        [DataMember(Name = "serviceType", Order = 8, EmitDefaultValue = false)]
        public string ServiceType { get; set; }

        /// <summary>
        /// 获取或设置健康检查使用的路径或 URL。
        /// </summary>
        [DataMember(Name = "healthCheckUrl", Order = 9, EmitDefaultValue = false)]
        public string HealthCheckUrl { get; set; }

        /// <summary>
        /// 获取或设置健康检查使用的端口。
        /// </summary>
        [DataMember(Name = "healthCheckPort", Order = 10, EmitDefaultValue = false)]
        public int HealthCheckPort { get; set; }

        /// <summary>
        /// 获取或设置健康检查传输类型。
        /// </summary>
        [DataMember(Name = "healthCheckType", Order = 11, EmitDefaultValue = false)]
        public string HealthCheckType { get; set; }

        /// <summary>
        /// 获取或设置加权发现策略使用的权重。
        /// </summary>
        [DataMember(Name = "weight", Order = 12, EmitDefaultValue = false)]
        public int Weight { get; set; }

        /// <summary>
        /// 获取或设置附加的字符串元数据。
        /// </summary>
        [DataMember(Name = "metadata", Order = 13, EmitDefaultValue = false)]
        public Dictionary<string, string> Metadata { get; set; }
    }
}
