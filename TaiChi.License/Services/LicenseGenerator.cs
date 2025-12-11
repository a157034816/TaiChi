using System;
using TaiChi.License.Interfaces;
using TaiChi.License.Models;

namespace TaiChi.License.Services
{
    /// <summary>
    /// 许可证生成器：序列化 LicenseData、数字签名并打包为 LicenseKey。
    /// 默认使用零依赖的 XML 序列化器；可替换为 MessagePack 实现。
    /// </summary>
    public class LicenseGenerator
    {
        private readonly ISerializer _serializer;

        public LicenseGenerator(ISerializer serializer)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        /// <summary>
        /// 基于给定数据与私钥生成许可证密钥对象。
        /// </summary>
        /// <param name="data">许可证业务数据</param>
        /// <param name="privateKeyPkcs8">RSA PKCS#8 私钥</param>
        /// <param name="keyId">可选的密钥标识</param>
        /// <param name="alg">算法标识，默认 RSASSA-PSS-SHA256</param>
        public LicenseKey Create(LicenseData data, byte[] privateKeyPkcs8, string? keyId = null, string alg = "RSASSA-PSS-SHA256")
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (privateKeyPkcs8 == null || privateKeyPkcs8.Length == 0) throw new ArgumentException("私钥不能为空", nameof(privateKeyPkcs8));

            ValidateConstraints(data);

            // 1) 序列化 Payload（默认 JSON，占位，后续可切换 MessagePack）
            var payload = _serializer.Serialize(data);

            // 2) 数字签名
            var signature = CryptoService.SignData(payload, privateKeyPkcs8);

            // 3) 打包 LicenseKey
            return new LicenseKey
            {
                Payload = payload,
                Signature = signature,
                Alg = alg,
                Kid = keyId,
            };
        }

        private static void ValidateConstraints(LicenseData data)
        {
            if (data.Constraints == null)
                throw new ArgumentException("Constraints 不能为空", nameof(data));

            var c = data.Constraints;
            if (c.NotBefore.HasValue && c.NotAfter.HasValue && c.NotAfter < c.NotBefore)
                throw new ArgumentException("约束条件无效：NotAfter < NotBefore");

            if (c.BindHardware && string.IsNullOrWhiteSpace(c.HardwareFingerprint))
            {
                // 允许上层注入当前设备指纹，但若已标记绑定且未提供，给出提示
                // 此处不抛异常，以支持先生成空指纹再由安装器注入场景
            }
        }
    }
}
