using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace TaiChi.License.Models
{
    /// <summary>
    /// 许可证的核心业务数据。
    /// </summary>
    public class LicenseData
    {
        /// <summary>
        /// 许可证唯一标识。
        /// </summary>
        public string LicenseId { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// 产品标识（例如：TaiChi.ERP）。
        /// </summary>
        public string ProductId { get; set; } = string.Empty;

        /// <summary>
        /// 产品名称（用于显示）。
        /// </summary>
        public string? ProductName { get; set; }

        /// <summary>
        /// 产品版本（例如：1.2.3）。
        /// </summary>
        public string? ProductVersion { get; set; }

        /// <summary>
        /// 客户名称。
        /// </summary>
        public string? CustomerName { get; set; }

        /// <summary>
        /// 客户邮箱/联系方式。
        /// </summary>
        public string? CustomerContact { get; set; }

        /// <summary>
        /// 发行者（例如：Company/CA/Issuer）。
        /// </summary>
        public string? Issuer { get; set; }

        /// <summary>
        /// 签发时间（UTC）。
        /// </summary>
        public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 约束条件。
        /// </summary>
        public LicenseConstraints Constraints { get; set; } = new LicenseConstraints();

        /// <summary>
        /// 自定义元数据键值对（非关键字段，供扩展用）。
        /// </summary>
        [XmlIgnore]
        public Dictionary<string, string>? Metadata { get; set; }
    }
}
