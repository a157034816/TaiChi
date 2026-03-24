using System.Runtime.Serialization;

namespace CentralService.Service.Models
{
    /// <summary>
    /// 表示服务端返回的标准 <c>ProblemDetails</c> 错误模型。
    /// </summary>
    [DataContract]
    public sealed class CentralServiceProblemDetails
    {
        /// <summary>
        /// 获取或设置问题类型标识或文档地址。
        /// </summary>
        [DataMember(Name = "type", Order = 1, EmitDefaultValue = false)]
        public string Type { get; set; }

        /// <summary>
        /// 获取或设置问题摘要标题。
        /// </summary>
        [DataMember(Name = "title", Order = 2, EmitDefaultValue = false)]
        public string Title { get; set; }

        /// <summary>
        /// 获取或设置对应的 HTTP 状态码。
        /// </summary>
        [DataMember(Name = "status", Order = 3, EmitDefaultValue = false)]
        public int? Status { get; set; }

        /// <summary>
        /// 获取或设置问题的详细说明。
        /// </summary>
        [DataMember(Name = "detail", Order = 4, EmitDefaultValue = false)]
        public string Detail { get; set; }

        /// <summary>
        /// 获取或设置服务端记录的问题实例标识。
        /// </summary>
        [DataMember(Name = "instance", Order = 5, EmitDefaultValue = false)]
        public string Instance { get; set; }

        /// <summary>
        /// 获取或设置服务端诊断链路上的 TraceId。
        /// </summary>
        [DataMember(Name = "traceId", Order = 6, EmitDefaultValue = false)]
        public string TraceId { get; set; }
    }
}
