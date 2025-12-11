using System;
using System.Collections.Generic;

namespace TaiChi.License.Models
{
    /// <summary>
    /// 许可证约束条件定义。
    /// 提醒：为保持 MessagePack 的兼容性，请保持属性名稳定且避免随意删除。
    /// </summary>
    public class LicenseConstraints
    {
        /// <summary>
        /// 生效时间（NotBefore）。为空表示不限制。
        /// </summary>
        public DateTime? NotBefore { get; set; }

        /// <summary>
        /// 过期时间（NotAfter）。为空表示不限制。
        /// </summary>
        public DateTime? NotAfter { get; set; }

        /// <summary>
        /// 是否绑定硬件。
        /// </summary>
        public bool BindHardware { get; set; }

        /// <summary>
        /// 指定的硬件指纹（当 BindHardware=true 时用于校验）。
        /// </summary>
        public string? HardwareFingerprint { get; set; }

        /// <summary>
        /// 授权的功能列表（功能标识集合）。为空表示不限制/按产品默认。
        /// </summary>
        public List<string>? Features { get; set; }

        /// <summary>
        /// 最多允许的用户数量。为空表示不限制。
        /// </summary>
        public int? MaxUsers { get; set; }

        /// <summary>
        /// 允许的产品主版本范围（例如："[1.0,2.0)"），为空表示不限制。
        /// 实际解释由验证器负责。
        /// </summary>
        public string? VersionRange { get; set; }

        /// <summary>
        /// 许可附加说明，便于审计。
        /// </summary>
        public string? Notes { get; set; }
    }
}

