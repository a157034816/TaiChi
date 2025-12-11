using System.Collections.Generic;
using TaiChi.License.Enums;

namespace TaiChi.License.Models
{
    /// <summary>
    /// 许可证验证结果。
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// 是否验证通过。
        /// </summary>
        public bool IsValid => Errors.Count == 0;

        /// <summary>
        /// 错误码集合。
        /// </summary>
        public List<ErrorCode> Errors { get; } = new List<ErrorCode>();

        /// <summary>
        /// 可选的人类可读消息。
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// 添加错误码并返回自身（便于链式调用）。
        /// </summary>
        public ValidationResult AddError(ErrorCode code)
        {
            Errors.Add(code);
            return this;
        }
    }
}

