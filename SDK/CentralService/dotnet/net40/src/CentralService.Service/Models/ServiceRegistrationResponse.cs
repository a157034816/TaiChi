using System.Runtime.Serialization;

namespace CentralService.Service.Models
{
    /// <summary>
    /// 表示服务注册成功后返回的结果。
    /// </summary>
    [DataContract]
    public sealed class ServiceRegistrationResponse
    {
        /// <summary>
        /// 获取或设置最终持久化到注册中心中的服务实例 ID。
        /// </summary>
        [DataMember(Name = "id", Order = 1)]
        public string Id { get; set; }

        /// <summary>
        /// 获取或设置注册时间戳，单位为 Unix 毫秒。
        /// </summary>
        [DataMember(Name = "registerTimestamp", Order = 2)]
        public long RegisterTimestamp { get; set; }
    }
}
