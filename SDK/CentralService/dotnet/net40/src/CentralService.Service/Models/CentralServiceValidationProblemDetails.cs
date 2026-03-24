using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CentralService.Service.Models
{
    /// <summary>
    /// 表示服务端返回的标准 <c>ValidationProblemDetails</c> 错误模型。
    /// </summary>
    [DataContract]
    public sealed class CentralServiceValidationProblemDetails
    {
        /// <summary>
        /// 获取或设置验证失败的摘要标题。
        /// </summary>
        [DataMember(Name = "title", Order = 1, EmitDefaultValue = false)]
        public string Title { get; set; }

        /// <summary>
        /// 获取或设置对应的 HTTP 状态码。
        /// </summary>
        [DataMember(Name = "status", Order = 2, EmitDefaultValue = false)]
        public int? Status { get; set; }

        /// <summary>
        /// 获取或设置按字段分组的验证错误集合。
        /// </summary>
        [DataMember(Name = "errors", Order = 3, EmitDefaultValue = false)]
        public Dictionary<string, string[]> Errors { get; set; }
    }
}
