using System;
using System.IO;
using TaiChi.License;
using TaiChi.License.Models;
using TaiChi.License.Services;
using TaiChi.License.Services.Serialization;

namespace TaiChi.Examples;

internal class LicenseUsageExample
{
    static void Main()
    {
        var serializer = DefaultSerializer.Instance;
        var baseDir = Path.Combine(Environment.CurrentDirectory, "licenses");
        Directory.CreateDirectory(baseDir);

        var storage = new LicenseStorage(baseDir, serializer);
        var manager = new LicenseManager(storage, serializer);

        var (pub, prv) = manager.GenerateRsaKeyPair();

        var data = new LicenseData
        {
            ProductId = "TaiChi.ERP",
            ProductName = "太极ERP",
            ProductVersion = "1.0.0",
            Constraints = new LicenseConstraints
            {
                NotBefore = DateTime.UtcNow.AddMinutes(-1),
                NotAfter  = DateTime.UtcNow.AddMinutes(10),
                BindHardware = false,
                Features = new() { "Basic", "Pro" },
                MaxUsers = 10,
                VersionRange = "[1.0,2.0)"
            }
        };

        manager.CreateAndSave(data, prv, name: "demo");
        Console.WriteLine("License saved to: " + storage.ResolvePath("demo"));

        var options = new LicenseValidationOptions
        {
            ProductId = "TaiChi.ERP",
            ProductVersion = "1.0.0",
            RequiredFeatures = new() { "Basic" },
            CurrentUsers = 3
        };

        var result = manager.LoadAndValidate("demo", pub, options);
        Console.WriteLine($"Valid={result.IsValid}, Errors={string.Join(',', result.Errors)}");
    }
}

