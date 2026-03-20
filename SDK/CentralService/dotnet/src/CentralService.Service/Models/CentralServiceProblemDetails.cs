using System.Runtime.Serialization;

namespace CentralService.Service.Models
{
    /// <summary>
    /// 表示中心服务返回的 ProblemDetails 错误结构。
    /// </summary>
    [DataContract]
    public sealed class CentralServiceProblemDetails
    {
        /// <summary>
        /// 获取或设置错误类型标识。
        /// </summary>
        [DataMember(Name = "type", Order = 1, EmitDefaultValue = false)]
        public string Type { get; set; }

        /// <summary>
        /// 获取或设置错误标题。
        /// </summary>
        [DataMember(Name = "title", Order = 2, EmitDefaultValue = false)]
        public string Title { get; set; }

        /// <summary>
        /// 获取或设置与错误对应的 HTTP 状态码。
        /// </summary>
        [DataMember(Name = "status", Order = 3, EmitDefaultValue = false)]
        public int? Status { get; set; }

        /// <summary>
        /// 获取或设置错误详情。
        /// </summary>
        [DataMember(Name = "detail", Order = 4, EmitDefaultValue = false)]
        public string Detail { get; set; }

        /// <summary>
        /// 获取或设置错误实例标识。
        /// </summary>
        [DataMember(Name = "instance", Order = 5, EmitDefaultValue = false)]
        public string Instance { get; set; }

        /// <summary>
        /// 获取或设置请求跟踪标识。
        /// </summary>
        [DataMember(Name = "traceId", Order = 6, EmitDefaultValue = false)]
        public string TraceId { get; set; }
    }
}
