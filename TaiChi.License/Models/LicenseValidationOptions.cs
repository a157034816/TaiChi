using System.Collections.Generic;

namespace TaiChi.License.Models
{
    /// <summary>
    /// 许可证验证时的可选上下文参数。
    /// 未提供的上下文将不参与对应校验（除必需校验外）。
    /// </summary>
    public class LicenseValidationOptions
    {
        /// <summary>
        /// 期望的产品标识（用于比对 LicenseData.ProductId）。
        /// </summary>
        public string? ProductId { get; set; }

        /// <summary>
        /// 当前产品版本（用于与约束 VersionRange 比对）。
        /// </summary>
        public string? ProductVersion { get; set; }

        /// <summary>
        /// 当前使用中的用户数（用于与 MaxUsers 比对）。
        /// </summary>
        public int? CurrentUsers { get; set; }

        /// <summary>
        /// 本次访问所需功能集合（用于与 Constraints.Features 比对）。
        /// </summary>
        public List<string>? RequiredFeatures { get; set; }

        /// <summary>
        /// 覆盖硬件指纹（若不提供则自动读取当前主机指纹）。
        /// </summary>
        public string? HardwareFingerprintOverride { get; set; }
    }
}

