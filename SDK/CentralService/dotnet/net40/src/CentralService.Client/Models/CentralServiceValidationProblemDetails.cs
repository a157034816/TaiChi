using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CentralService.Client.Models
{
    /// <summary>
    /// 表示中心服务返回的验证错误 ProblemDetails 结构。
    /// </summary>
    [DataContract]
    public sealed class CentralServiceValidationProblemDetails
    {
        /// <summary>
        /// 获取或设置错误标题。
        /// </summary>
        [DataMember(Name = "title", Order = 1, EmitDefaultValue = false)]
        public string Title { get; set; }

        /// <summary>
        /// 获取或设置与错误对应的 HTTP 状态码。
        /// </summary>
        [DataMember(Name = "status", Order = 2, EmitDefaultValue = false)]
        public int? Status { get; set; }

        /// <summary>
        /// 获取或设置字段级验证错误明细。
        /// </summary>
        [DataMember(Name = "errors", Order = 3, EmitDefaultValue = false)]
        public Dictionary<string, string[]> Errors { get; set; }
    }
}
