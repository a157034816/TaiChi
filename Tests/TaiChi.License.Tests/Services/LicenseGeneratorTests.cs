using System;
using TaiChi.License.Models;
using TaiChi.License.Services;
using TaiChi.License.Services.Serialization;
using Xunit;

namespace TaiChi.License.Tests.Services;

public class LicenseGeneratorTests
{
    [Fact]
    public void Create_Then_Validate_ShouldBeValid()
    {
        var serializer = DefaultSerializer.Instance;
        var gen = new LicenseGenerator(serializer);
        var val = new LicenseValidator(serializer);

        var (pub, prv) = CryptoService.GenerateRsaKeyPair();

        var data = new LicenseData
        {
            ProductId = "TaiChi.ERP",
            ProductName = "太极ERP",
            ProductVersion = "1.2.3",
            Constraints = new LicenseConstraints
            {
                NotBefore = DateTime.UtcNow.AddMinutes(-5),
                NotAfter = DateTime.UtcNow.AddMinutes(30),
                BindHardware = false,
                Features = new() { "A", "B" },
                MaxUsers = 100,
                VersionRange = "[1.0,2.0)"
            }
        };

        var key = gen.Create(data, prv, keyId: "k1");
        Assert.NotNull(key.Payload);
        Assert.NotNull(key.Signature);
        Assert.True(key.Payload.Length > 0);
        Assert.True(key.Signature.Length > 0);

        var options = new LicenseValidationOptions
        {
            ProductId = "TaiChi.ERP",
            ProductVersion = "1.2.3",
            CurrentUsers = 10,
            RequiredFeatures = new() { "A" }
        };

        var result = val.Validate(key, pub, options);
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
}

