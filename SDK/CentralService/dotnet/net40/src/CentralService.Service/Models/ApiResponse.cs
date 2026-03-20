using System.Runtime.Serialization;

namespace CentralService.Service.Models
{
    /// <summary>
    /// 表示 <c>/api/Service/*</c> 接口使用的统一响应包裹。
    /// </summary>
    /// <typeparam name="T">业务数据节点的目标类型。</typeparam>
    [DataContract]
    public sealed class ApiResponse<T>
    {
        /// <summary>
        /// 获取或设置服务端是否将本次操作判定为成功。
        /// </summary>
        [DataMember(Name = "success", Order = 1)]
        public bool Success { get; set; }

        /// <summary>
        /// 获取或设置服务端返回的业务错误码。
        /// </summary>
        [DataMember(Name = "errorCode", Order = 2, EmitDefaultValue = false)]
        public int? ErrorCode { get; set; }

        /// <summary>
        /// 获取或设置服务端返回的业务错误描述。
        /// </summary>
        [DataMember(Name = "errorMessage", Order = 3, EmitDefaultValue = false)]
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 获取或设置成功时返回的业务数据。
        /// </summary>
        [DataMember(Name = "data", Order = 4, EmitDefaultValue = false)]
        public T Data { get; set; }
    }

    /// <summary>
    /// 仅用于错误探测阶段的轻量 <c>ApiResponse</c> 模型。
    /// </summary>
    [DataContract]
    internal sealed class ApiResponseErrorOnly
    {
        /// <summary>
        /// 获取或设置服务端是否将本次操作判定为成功。
        /// </summary>
        [DataMember(Name = "success", Order = 1)]
        public bool Success { get; set; }

        /// <summary>
        /// 获取或设置服务端返回的业务错误码。
        /// </summary>
        [DataMember(Name = "errorCode", Order = 2, EmitDefaultValue = false)]
        public int? ErrorCode { get; set; }

        /// <summary>
        /// 获取或设置服务端返回的业务错误描述。
        /// </summary>
        [DataMember(Name = "errorMessage", Order = 3, EmitDefaultValue = false)]
        public string ErrorMessage { get; set; }
    }
}
