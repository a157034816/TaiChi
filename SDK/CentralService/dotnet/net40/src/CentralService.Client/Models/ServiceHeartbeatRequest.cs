using System.Runtime.Serialization;

namespace CentralService.Client.Models
{
    /// <summary>
    /// 表示服务心跳请求。
    /// </summary>
    [DataContract]
    public sealed class ServiceHeartbeatRequest
    {
        /// <summary>
        /// 获取或设置服务实例标识。
        /// </summary>
        [DataMember(Name = "id", Order = 1)]
        public string Id { get; set; }
    }
}
