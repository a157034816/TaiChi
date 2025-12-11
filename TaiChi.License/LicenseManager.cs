using System;
using TaiChi.License.Interfaces;
using TaiChi.License.Models;
using TaiChi.License.Services.Serialization;

namespace TaiChi.License
{
    /// <summary>
    /// 门面：统一封装生成、验证、存储入口。
    /// 内部组合 LicenseGenerator / LicenseValidator / ILicenseStorage。
    /// </summary>
    public class LicenseManager
    {
        private readonly ISerializer _serializer;
        private readonly Interfaces.ILicenseStorage _storage;
        private readonly Services.LicenseGenerator _generator;
        private readonly Services.LicenseValidator _validator;

        public LicenseManager(Interfaces.ILicenseStorage storage, ISerializer? serializer = null)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _serializer = serializer ?? DefaultSerializer.Instance;
            _generator = new Services.LicenseGenerator(_serializer);
            _validator = new Services.LicenseValidator(_serializer);
        }

        // ===================== 生成 =====================

        public LicenseKey Create(LicenseData data, byte[] privateKeyPkcs8, string? keyId = null, string alg = "RSASSA-PSS-SHA256")
            => _generator.Create(data, privateKeyPkcs8, keyId, alg);

        public LicenseKey CreateAndSave(LicenseData data, byte[] privateKeyPkcs8, string name, string? keyId = null, string alg = "RSASSA-PSS-SHA256")
        {
            var key = _generator.Create(data, privateKeyPkcs8, keyId, alg);
            _storage.Save(key, name);
            return key;
        }

        // 提供密钥对生成的便捷入口
        public (byte[] PublicKeySpki, byte[] PrivateKeyPkcs8) GenerateRsaKeyPair(int keySize = 2048)
            => Services.CryptoService.GenerateRsaKeyPair(keySize);

        // ===================== 存储 =====================

        public void Save(LicenseKey key, string name) => _storage.Save(key, name);
        public LicenseKey? Load(string name) => _storage.Load(name);
        public bool Delete(string name) => _storage.Delete(name);
        public bool Exists(string name) => _storage.Exists(name);
        public System.Collections.Generic.IEnumerable<string> ListNames() => _storage.ListNames();
        public string ResolvePath(string name) => _storage.ResolvePath(name);

        // ===================== 验证 =====================

        public ValidationResult Validate(LicenseKey key, byte[] publicKeySpki, LicenseValidationOptions? options = null, Func<DateTime>? nowProvider = null)
            => _validator.Validate(key, publicKeySpki, options, nowProvider);

        public ValidationResult LoadAndValidate(string name, byte[] publicKeySpki, LicenseValidationOptions? options = null, Func<DateTime>? nowProvider = null)
        {
            var key = _storage.Load(name);
            if (key == null)
            {
                var r = new ValidationResult();
                r.AddError(Enums.ErrorCode.许可证_格式无效);
                r.Message = "未找到指定名称的许可证或文件不可读取";
                return r;
            }
            return _validator.Validate(key, publicKeySpki, options, nowProvider);
        }

        // ===================== 辅助 =====================

        public LicenseData? ReadData(LicenseKey key)
        {
            try { return _serializer.Deserialize<LicenseData>(key.Payload); } catch { return null; }
        }

        // 将许可证整体序列化为 Base64，便于文本分发
        public string EncodeToBase64(LicenseKey key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            var bytes = _serializer.Serialize(key);
            return Convert.ToBase64String(bytes);
        }

        // 从 Base64 文本解码许可证
        public LicenseKey? DecodeFromBase64(string base64)
        {
            try
            {
                var bytes = Convert.FromBase64String(base64);
                return _serializer.Deserialize<LicenseKey>(bytes);
            }
            catch { return null; }
        }

        // 直接校验 Base64 文本许可证
        public ValidationResult ValidateBase64(string base64, byte[] publicKeySpki, LicenseValidationOptions? options = null, Func<DateTime>? nowProvider = null)
        {
            var key = DecodeFromBase64(base64);
            if (key == null)
            {
                var r = new ValidationResult();
                r.AddError(Enums.ErrorCode.许可证_反序列化失败);
                r.Message = "Base64 文本不是有效的许可证";
                return r;
            }
            return _validator.Validate(key, publicKeySpki, options, nowProvider);
        }
    }
}
