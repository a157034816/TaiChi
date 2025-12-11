using System;
using System.Collections.Generic;
using System.Linq;
using TaiChi.License.Enums;
using TaiChi.License.Interfaces;
using TaiChi.License.Models;

namespace TaiChi.License.Services
{
    /// <summary>
    /// 许可证验证器：验证签名与各项业务约束。
    /// </summary>
    public class LicenseValidator
    {
        private readonly ISerializer _serializer;

        public LicenseValidator(ISerializer serializer)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        /// <summary>
        /// 验证许可证并返回详细结果。
        /// </summary>
        /// <param name="key">许可证密钥载体</param>
        /// <param name="publicKeySpki">RSA SPKI 公钥</param>
        /// <param name="options">可选的校验上下文</param>
        /// <param name="nowProvider">可选的时间提供者（默认 UTC Now）</param>
        public ValidationResult Validate(LicenseKey key, byte[] publicKeySpki, LicenseValidationOptions? options = null, Func<DateTime>? nowProvider = null)
        {
            var result = new ValidationResult();
            options ??= new LicenseValidationOptions();
            var now = (nowProvider ?? (() => DateTime.UtcNow))();

            if (key == null || key.Payload == null || key.Payload.Length == 0)
            {
                return result.AddError(ErrorCode.许可证_格式无效);
            }

            // 1) 签名验证
            var signatureOk = CryptoService.VerifyData(key.Payload, key.Signature ?? Array.Empty<byte>(), publicKeySpki);
            if (!signatureOk)
            {
                result.AddError(ErrorCode.许可证_签名无效);
                // 继续尝试反序列化，以便返回更多错误上下文
            }

            // 2) 反序列化 payload
            LicenseData? data = null;
            try
            {
                data = _serializer.Deserialize<LicenseData>(key.Payload);
            }
            catch
            {
                result.AddError(ErrorCode.许可证_反序列化失败);
                return result; // 无法继续校验
            }

            if (data == null)
            {
                result.AddError(ErrorCode.许可证_反序列化失败);
                return result;
            }

            var c = data.Constraints ?? new LicenseConstraints();

            // 3) 生效/过期
            if (c.NotBefore.HasValue && now < c.NotBefore.Value.ToUniversalTime())
                result.AddError(ErrorCode.许可证_尚未生效);

            if (c.NotAfter.HasValue && now > c.NotAfter.Value.ToUniversalTime())
                result.AddError(ErrorCode.许可证_已过期);

            // 4) 产品/版本校验（若提供上下文）
            if (!string.IsNullOrWhiteSpace(options.ProductId) && !string.Equals(options.ProductId, data.ProductId, StringComparison.Ordinal))
                result.AddError(ErrorCode.许可证_产品不匹配);

            if (!string.IsNullOrWhiteSpace(options.ProductVersion) && !string.IsNullOrWhiteSpace(c.VersionRange))
            {
                if (!IsVersionInRange(options.ProductVersion!, c.VersionRange!))
                    result.AddError(ErrorCode.许可证_版本不匹配);
            }

            // 5) 硬件绑定
            if (c.BindHardware)
            {
                var actualFp = !string.IsNullOrWhiteSpace(options.HardwareFingerprintOverride)
                    ? options.HardwareFingerprintOverride
                    : HardwareFingerprint.GetFingerprint();

                if (string.IsNullOrWhiteSpace(c.HardwareFingerprint) ||
                    !string.Equals(actualFp, c.HardwareFingerprint, StringComparison.OrdinalIgnoreCase))
                {
                    result.AddError(ErrorCode.许可证_硬件不匹配);
                }
            }

            // 6) 功能授权
            if (options.RequiredFeatures != null && options.RequiredFeatures.Count > 0)
            {
                var allowed = c.Features ?? new List<string>();
                foreach (var rf in options.RequiredFeatures)
                {
                    if (!allowed.Contains(rf, StringComparer.Ordinal))
                    {
                        result.AddError(ErrorCode.许可证_功能未授权);
                        break;
                    }
                }
            }

            // 7) 用户数限制
            if (c.MaxUsers.HasValue && options.CurrentUsers.HasValue && options.CurrentUsers.Value > c.MaxUsers.Value)
            {
                result.AddError(ErrorCode.许可证_用户数超限);
            }

            return result;
        }

        // =============== 版本范围解析/比较（简单实现） ===============

        private static bool IsVersionInRange(string version, string range)
        {
            // 允许形式：[1.0,2.0)  (1.0,2.0]  [1.0,1.0]  单点：1.2.3
            range = range.Trim();
            if (range.Length == 0) return true;

            if (range[0] != '[' && range[0] != '(')
            {
                // 简单等于
                return CompareVersion(version, range) == 0;
            }

            var inclusiveMin = range[0] == '[';
            var inclusiveMax = range[^1] == ']';
            var inner = range.Substring(1, range.Length - 2);
            var parts = inner.Split(',');
            if (parts.Length != 2) return false;

            var min = parts[0].Trim();
            var max = parts[1].Trim();

            if (!string.IsNullOrEmpty(min))
            {
                var cmp = CompareVersion(version, min);
                if (cmp < 0 || (cmp == 0 && !inclusiveMin)) return false;
            }

            if (!string.IsNullOrEmpty(max))
            {
                var cmp = CompareVersion(version, max);
                if (cmp > 0 || (cmp == 0 && !inclusiveMax)) return false;
            }

            return true;
        }

        private static int CompareVersion(string a, string b)
        {
            // 仅比较数字段，非数字截断；空视为 0
            var pa = a.Split('.');
            var pb = b.Split('.');
            var n = Math.Max(pa.Length, pb.Length);
            for (int i = 0; i < n; i++)
            {
                var ai = i < pa.Length && int.TryParse(pa[i], out var av) ? av : 0;
                var bi = i < pb.Length && int.TryParse(pb[i], out var bv) ? bv : 0;
                if (ai != bi) return ai.CompareTo(bi);
            }
            return 0;
        }
    }
}

