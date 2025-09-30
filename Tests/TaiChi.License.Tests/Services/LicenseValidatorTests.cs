using System;
using System.Linq;
using TaiChi.License.Enums;
using TaiChi.License.Models;
using TaiChi.License.Services;
using TaiChi.License.Services.Serialization;
using Xunit;

namespace TaiChi.License.Tests.Services;

public class LicenseValidatorTests
{
    private static (byte[] Pub, byte[] Prv) NewKeyPair() => CryptoService.GenerateRsaKeyPair();

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

