using System.Runtime.Serialization;

namespace CentralService.Service.Models
{
    /// <summary>
    /// 表示服务列表查询接口的返回模型。
    /// </summary>
    [DataContract]
    public sealed class ServiceListResponse
    {
        /// <summary>
        /// 获取或设置服务实例集合。
        /// </summary>
        [DataMember(Name = "services", Order = 1, EmitDefaultValue = false)]
        public ServiceInfo[] Services { get; set; }
    }
}
