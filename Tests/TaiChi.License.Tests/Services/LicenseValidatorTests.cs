using System;
using System.Linq;
using TaiChi.License.Enums;
using TaiChi.License.Models;
using TaiChi.License.Services;
using TaiChi.License.Services.Serialization;
using Xunit;

namespace TaiChi.License.Tests.Services;

/// <summary>
/// <see cref="LicenseValidator"/> 的单元测试。
/// </summary>
/// <remarks>
/// 这些测试通过生成临时 RSA 密钥对与许可证数据，验证校验器在常见失败场景下能返回对应的错误码，
/// 同时避免依赖外部文件系统或真实硬件信息。
/// </remarks>
public class LicenseValidatorTests
{
    /// <summary>
    /// 生成一对用于测试的 RSA 公钥与私钥。
    /// </summary>
    /// <returns>元组：公钥字节数组与私钥字节数组。</returns>
    private static (byte[] Pub, byte[] Prv) NewKeyPair() => CryptoService.GenerateRsaKeyPair();

    /// <summary>
    /// 构造一份“可通过校验”的基础许可证数据。
    /// </summary>
    /// <remarks>
    /// 各测试用例在此基础上按场景修改约束条件，以聚焦验证单一规则的行为。
    /// </remarks>
    private static LicenseData NewBaseData()
    {
        return new LicenseData
        {
            ProductId = "TaiChi.ERP",
            ProductVersion = "1.0.0",
            Constraints = new LicenseConstraints
            {
                NotBefore = DateTime.UtcNow.AddMinutes(-10),
                NotAfter = DateTime.UtcNow.AddMinutes(10),
                BindHardware = false,
                Features = new() { "A", "B" },
                MaxUsers = 50,
                VersionRange = "[1.0,2.0)"
            }
        };
    }

    /// <summary>
    /// 当许可证签名被篡改时，应返回“签名无效”错误码。
    /// </summary>
    [Fact]
    public void TamperedSignature_ShouldReturn_SignatureInvalid()
    {
        var ser = DefaultSerializer.Instance;
        var gen = new LicenseGenerator(ser);
        var val = new LicenseValidator(ser);
        var (pub, prv) = NewKeyPair();

        var key = gen.Create(NewBaseData(), prv);
        key.Signature[^1] ^= 0xFF; // 篡改
        var result = val.Validate(key, pub, new LicenseValidationOptions { ProductId = "TaiChi.ERP", ProductVersion = "1.0.0" });
        Assert.Contains(ErrorCode.许可证_签名无效, result.Errors);
    }

    /// <summary>
    /// 当许可证尚未到达生效时间（NotBefore 在未来）时，应返回“尚未生效”错误码。
    /// </summary>
    [Fact]
    public void NotBefore_InFuture_ShouldReturn_NotYetValid()
    {
        var ser = DefaultSerializer.Instance;
        var gen = new LicenseGenerator(ser);
        var val = new LicenseValidator(ser);
        var (pub, prv) = NewKeyPair();

        var data = NewBaseData();
        data.Constraints.NotBefore = DateTime.UtcNow.AddMinutes(5);
        var key = gen.Create(data, prv);
        var result = val.Validate(key, pub, new LicenseValidationOptions { ProductId = "TaiChi.ERP", ProductVersion = "1.0.0" });
        Assert.Contains(ErrorCode.许可证_尚未生效, result.Errors);
    }

    /// <summary>
    /// 当许可证已超过失效时间（NotAfter 在过去）时，应返回“已过期”错误码。
    /// </summary>
    [Fact]
    public void NotAfter_InPast_ShouldReturn_Expired()
    {
        var ser = DefaultSerializer.Instance;
        var gen = new LicenseGenerator(ser);
        var val = new LicenseValidator(ser);
        var (pub, prv) = NewKeyPair();

        var data = NewBaseData();
        data.Constraints.NotAfter = DateTime.UtcNow.AddMinutes(-1);
        var key = gen.Create(data, prv);
        var result = val.Validate(key, pub, new LicenseValidationOptions { ProductId = "TaiChi.ERP", ProductVersion = "1.0.0" });
        Assert.Contains(ErrorCode.许可证_已过期, result.Errors);
    }

    /// <summary>
    /// 当启用硬件绑定且指纹不匹配时，应返回“硬件不匹配”错误码。
    /// </summary>
    [Fact]
    public void HardwareFingerprint_Mismatch_ShouldReturn_HardwareMismatch()
    {
        var ser = DefaultSerializer.Instance;
        var gen = new LicenseGenerator(ser);
        var val = new LicenseValidator(ser);
        var (pub, prv) = NewKeyPair();

        var data = NewBaseData();
        data.Constraints.BindHardware = true;
        data.Constraints.HardwareFingerprint = "fp-expected";
        var key = gen.Create(data, prv);

        var options = new LicenseValidationOptions
        {
            ProductId = "TaiChi.ERP",
            ProductVersion = "1.0.0",
            HardwareFingerprintOverride = "fp-actual"
        };
        var result = val.Validate(key, pub, options);
        Assert.Contains(ErrorCode.许可证_硬件不匹配, result.Errors);
    }

    /// <summary>
    /// 当产品标识不匹配时，应返回“产品不匹配”错误码。
    /// </summary>
    [Fact]
    public void ProductId_Mismatch_ShouldReturn_ProductMismatch()
    {
        var ser = DefaultSerializer.Instance;
        var gen = new LicenseGenerator(ser);
        var val = new LicenseValidator(ser);
        var (pub, prv) = NewKeyPair();

        var key = gen.Create(NewBaseData(), prv);
        var result = val.Validate(key, pub, new LicenseValidationOptions { ProductId = "Other.Product", ProductVersion = "1.0.0" });
        Assert.Contains(ErrorCode.许可证_产品不匹配, result.Errors);
    }

    /// <summary>
    /// 当当前版本不在许可版本范围内时，应返回“版本不匹配”错误码。
    /// </summary>
    [Fact]
    public void Version_OutOfRange_ShouldReturn_VersionMismatch()
    {
        var ser = DefaultSerializer.Instance;
        var gen = new LicenseGenerator(ser);
        var val = new LicenseValidator(ser);
        var (pub, prv) = NewKeyPair();

        var key = gen.Create(NewBaseData(), prv);
        var result = val.Validate(key, pub, new LicenseValidationOptions { ProductId = "TaiChi.ERP", ProductVersion = "2.0.0" });
        Assert.Contains(ErrorCode.许可证_版本不匹配, result.Errors);
    }

    /// <summary>
    /// 当请求的功能点未被授权时，应返回“功能未授权”错误码。
    /// </summary>
    [Fact]
    public void Feature_NotAuthorized_ShouldReturn_FeatureNotGranted()
    {
        var ser = DefaultSerializer.Instance;
        var gen = new LicenseGenerator(ser);
        var val = new LicenseValidator(ser);
        var (pub, prv) = NewKeyPair();

        var key = gen.Create(NewBaseData(), prv);
        var options = new LicenseValidationOptions
        {
            ProductId = "TaiChi.ERP",
            ProductVersion = "1.0.0",
            RequiredFeatures = new() { "X" }
        };
        var result = val.Validate(key, pub, options);
        Assert.Contains(ErrorCode.许可证_功能未授权, result.Errors);
    }

    /// <summary>
    /// 当当前用户数超过许可证上限时，应返回“用户数超限”错误码。
    /// </summary>
    [Fact]
    public void MaxUsers_Exceeded_ShouldReturn_UserLimitExceeded()
    {
        var ser = DefaultSerializer.Instance;
        var gen = new LicenseGenerator(ser);
        var val = new LicenseValidator(ser);
        var (pub, prv) = NewKeyPair();

        var data = NewBaseData();
        data.Constraints.MaxUsers = 5;
        var key = gen.Create(data, prv);
        var result = val.Validate(key, pub, new LicenseValidationOptions { ProductId = "TaiChi.ERP", ProductVersion = "1.0.0", CurrentUsers = 10 });
        Assert.Contains(ErrorCode.许可证_用户数超限, result.Errors);
    }

    /// <summary>
    /// 当许可证负载无法反序列化时，应返回“反序列化失败”错误码。
    /// </summary>
    [Fact]
    public void Deserialize_Failure_ShouldReturn_DeserializeError()
    {
        var ser = DefaultSerializer.Instance;
        var val = new LicenseValidator(ser);
        var (pub, _) = NewKeyPair();

        var bad = new LicenseKey
        {
            Payload = new byte[] { 0x00, 0x01, 0x02, 0x03 }, // 非法 XML
            Signature = Array.Empty<byte>(),
            Alg = "RSASSA-PSS-SHA256",
            Kid = "k1"
        };

        var result = val.Validate(bad, pub, new LicenseValidationOptions { ProductId = "TaiChi.ERP", ProductVersion = "1.0.0" });
        Assert.Contains(ErrorCode.许可证_反序列化失败, result.Errors);
    }
}

