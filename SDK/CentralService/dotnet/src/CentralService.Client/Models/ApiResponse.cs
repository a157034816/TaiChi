using System.Runtime.Serialization;

namespace CentralService.Client.Models
{
    /// <summary>
    /// 表示中心服务统一 API 响应包装模型。
    /// </summary>
    /// <typeparam name="T">业务数据类型。</typeparam>
    [DataContract]
    public sealed class ApiResponse<T>
    {
        /// <summary>
        /// 获取或设置服务端是否处理成功。
        /// </summary>
        [DataMember(Name = "success", Order = 1)]
        public bool Success { get; set; }

        /// <summary>
        /// 获取或设置服务端返回的业务错误码。
        /// </summary>
        [DataMember(Name = "errorCode", Order = 2, EmitDefaultValue = false)]
        public int? ErrorCode { get; set; }

        /// <summary>
        /// 获取或设置服务端返回的业务错误键。
        /// </summary>
        [DataMember(Name = "errorKey", Order = 3, EmitDefaultValue = false)]
        public string ErrorKey { get; set; }

        /// <summary>
        /// 获取或设置服务端返回的错误消息。
        /// </summary>
        [DataMember(Name = "errorMessage", Order = 4, EmitDefaultValue = false)]
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 获取或设置业务数据。
        /// </summary>
        [DataMember(Name = "data", Order = 5, EmitDefaultValue = false)]
        public T Data { get; set; }
    }

    /// <summary>
    /// 表示仅包含错误字段的最小 API 响应模型，供内部错误解析使用。
    /// </summary>
    [DataContract]
    internal sealed class ApiResponseErrorOnly
    {
        /// <summary>
        /// 获取或设置服务端是否处理成功。
        /// </summary>
        [DataMember(Name = "success", Order = 1)]
        public bool Success { get; set; }

        /// <summary>
        /// 获取或设置服务端返回的业务错误码。
        /// </summary>
        [DataMember(Name = "errorCode", Order = 2, EmitDefaultValue = false)]
        public int? ErrorCode { get; set; }

        /// <summary>
        /// 获取或设置服务端返回的业务错误键。
        /// </summary>
        [DataMember(Name = "errorKey", Order = 3, EmitDefaultValue = false)]
        public string ErrorKey { get; set; }

        /// <summary>
        /// 获取或设置服务端返回的错误消息。
        /// </summary>
        [DataMember(Name = "errorMessage", Order = 4, EmitDefaultValue = false)]
        public string ErrorMessage { get; set; }
    }
}
