using System.Runtime.Serialization;

namespace CentralService.Client.Models
{
    /// <summary>
    /// 表示中心服务统一包装的 API 响应。
    /// </summary>
    /// <typeparam name="T">响应数据的类型。</typeparam>
    [DataContract]
    public sealed class ApiResponse<T>
    {
        /// <summary>
        /// 获取或设置本次请求是否成功。
        /// </summary>
        [DataMember(Name = "success", Order = 1)]
        public bool Success { get; set; }

        /// <summary>
        /// 获取或设置业务错误码。
        /// </summary>
        [DataMember(Name = "errorCode", Order = 2, EmitDefaultValue = false)]
        public int? ErrorCode { get; set; }

        /// <summary>
        /// 获取或设置业务错误消息。
        /// </summary>
        [DataMember(Name = "errorMessage", Order = 3, EmitDefaultValue = false)]
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 获取或设置响应数据。
        /// </summary>
        [DataMember(Name = "data", Order = 4, EmitDefaultValue = false)]
        public T Data { get; set; }
    }

    /// <summary>
    /// 表示只关心错误字段时使用的统一响应投影。
    /// </summary>
    [DataContract]
    internal sealed class ApiResponseErrorOnly
    {
        /// <summary>
        /// 获取或设置本次请求是否成功。
        /// </summary>
        [DataMember(Name = "success", Order = 1)]
        public bool Success { get; set; }

        /// <summary>
        /// 获取或设置业务错误码。
        /// </summary>
        [DataMember(Name = "errorCode", Order = 2, EmitDefaultValue = false)]
        public int? ErrorCode { get; set; }

        /// <summary>
        /// 获取或设置业务错误消息。
        /// </summary>
        [DataMember(Name = "errorMessage", Order = 3, EmitDefaultValue = false)]
        public string ErrorMessage { get; set; }
    }
}
