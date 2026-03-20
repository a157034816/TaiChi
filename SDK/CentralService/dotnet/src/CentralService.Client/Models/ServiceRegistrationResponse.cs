using System.Runtime.Serialization;

namespace CentralService.Client.Models
{
    /// <summary>
    /// 表示服务注册成功后的返回结果。
    /// </summary>
    [DataContract]
    public sealed class ServiceRegistrationResponse
    {
        /// <summary>
        /// 获取或设置服务实例标识。
        /// </summary>
        [DataMember(Name = "id", Order = 1)]
        public string Id { get; set; }

        /// <summary>
        /// 获取或设置注册时间戳。
        /// </summary>
        [DataMember(Name = "registerTimestamp", Order = 2)]
        public long RegisterTimestamp { get; set; }
    }
}
