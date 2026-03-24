using System.Runtime.Serialization;

namespace CentralService.Service.Models
{
    /// <summary>
    /// 表示发送到 <c>POST /api/Service/heartbeat</c> 的心跳请求体。
    /// </summary>
    [DataContract]
    public sealed class ServiceHeartbeatRequest
    {
        /// <summary>
        /// 获取或设置已注册服务实例的唯一标识。
        /// </summary>
        [DataMember(Name = "id", Order = 1)]
        public string Id { get; set; }
    }
}
