using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CentralService.Client.Models
{
    /// <summary>
    /// 表示服务端返回的字段校验错误结构。
    /// </summary>
    [DataContract]
    public sealed class CentralServiceValidationProblemDetails
    {
        /// <summary>
        /// 获取或设置校验错误标题。
        /// </summary>
        [DataMember(Name = "title", Order = 1, EmitDefaultValue = false)]
        public string Title { get; set; }

        /// <summary>
        /// 获取或设置 HTTP 状态码。
        /// </summary>
        [DataMember(Name = "status", Order = 2, EmitDefaultValue = false)]
        public int? Status { get; set; }

        /// <summary>
        /// 获取或设置字段级错误集合，键为字段名，值为对应错误消息列表。
        /// </summary>
        [DataMember(Name = "errors", Order = 3, EmitDefaultValue = false)]
        public Dictionary<string, string[]> Errors { get; set; }
    }
}
