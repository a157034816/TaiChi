using System;

namespace TaiChi.License.Models
{
    /// <summary>
    /// 许可证密钥载体（序列化后的载荷 + 数字签名）。
    /// 约定：Payload 为 MessagePack 序列化后的 LicenseData 二进制；
    /// Signature 为对 Payload 的签名结果（例如：RSA-PSS/SHA256）。
    /// </summary>
    public class LicenseKey
    {
        /// <summary>
        /// 负载二进制（MessagePack 序列化后的 LicenseData）。
        /// </summary>
        public byte[] Payload { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// 数字签名。
        /// </summary>
        public byte[] Signature { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// 签名算法标识（例如：RS256、RSASSA-PSS-SHA256）。
        /// </summary>
        public string? Alg { get; set; }

        /// <summary>
        /// 密钥标识（KeyId），用于选择验证公钥。
        /// </summary>
        public string? Kid { get; set; }
    }
}

