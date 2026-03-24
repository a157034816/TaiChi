using System.Runtime.Serialization;

namespace CentralService.Service.Models
{
    /// <summary>
    /// 表示服务列表查询结果。
    /// </summary>
    [DataContract]
    public sealed class ServiceListResponse
    {
        /// <summary>
        /// 获取或设置返回的服务集合。
        /// </summary>
        [DataMember(Name = "services", Order = 1, EmitDefaultValue = false)]
        public ServiceInfo[] Services { get; set; }
    }
}
