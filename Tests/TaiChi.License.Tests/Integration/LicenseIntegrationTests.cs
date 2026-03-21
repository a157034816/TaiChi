using System;
using System.Diagnostics;
using System.IO;
using TaiChi.License;
using TaiChi.License.Models;
using TaiChi.License.Services;
using TaiChi.License.Services.Serialization;
using Xunit;

namespace TaiChi.License.Tests.Integration;

/// <summary>
/// 许可证模块的端到端集成测试。
/// </summary>
/// <remarks>
/// 该测试覆盖“生成密钥对 -> 生成许可证 -> 落盘 -> 读取 -> 校验”的完整链路，并验证常见故障（文件被篡改）下的失败表现。
/// </remarks>
public class LicenseIntegrationTests
{
    /// <summary>
    /// 端到端验证：生成并保存许可证后，能够成功加载并校验通过，同时校验耗时满足基本性能阈值。
    /// </summary>
    /// <remarks>
    /// 使用临时目录作为存储介质，避免污染本地固定路径；测试结束会尝试清理目录。
    /// </remarks>
    [Fact]
    public void EndToEnd_Generate_Store_Load_Validate_Success_And_Performance()
    {
        var serializer = DefaultSerializer.Instance;
        var baseDir = Path.Combine(Path.GetTempPath(), "TaiChi.License.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(baseDir);
        try
        {
            var storage = new LicenseStorage(baseDir, serializer);
            var manager = new LicenseManager(storage, serializer);

            var (pub, prv) = manager.GenerateRsaKeyPair();
            var data = new LicenseData
            {
                ProductId = "TaiChi.ERP",
                ProductName = "太极ERP",
                ProductVersion = "1.1.0",
                Constraints = new LicenseConstraints
                {
                    NotBefore = DateTime.UtcNow.AddMinutes(-1),
                    NotAfter = DateTime.UtcNow.AddMinutes(5),
                    BindHardware = false,
                    Features = new() { "A", "B" },
                    MaxUsers = 20,
                    VersionRange = "[1.0,2.0)"
                }
            };

            manager.CreateAndSave(data, prv, name: "lic1", keyId: "k1");

            var options = new LicenseValidationOptions
            {
                ProductId = "TaiChi.ERP",
                ProductVersion = "1.1.0",
                RequiredFeatures = new() { "A" },
                CurrentUsers = 5
            };

            var sw = Stopwatch.StartNew();
            var result = manager.LoadAndValidate("lic1", pub, options);
            sw.Stop();

            Assert.True(result.IsValid);
            Assert.True(sw.ElapsedMilliseconds < 100, $"验证耗时: {sw.ElapsedMilliseconds}ms，应小于100ms");
        }
        finally
        {
            try { Directory.Delete(baseDir, true); } catch { /* 忽略清理过程中的异常 */ }
        }
    }

    /// <summary>
    /// 端到端验证：当许可证存储文件被篡改导致无法解析时，加载与校验应失败，并给出“格式无效”等错误信息。
    /// </summary>
    [Fact]
    public void EndToEnd_Tamper_File_Should_Fail_To_Load()
    {
        var serializer = DefaultSerializer.Instance;
        var baseDir = Path.Combine(Path.GetTempPath(), "TaiChi.License.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(baseDir);
        try
        {
            var storage = new LicenseStorage(baseDir, serializer);
            var manager = new LicenseManager(storage, serializer);
            var (pub, prv) = manager.GenerateRsaKeyPair();

            var data = new LicenseData
            {
                ProductId = "TaiChi.ERP",
                ProductVersion = "1.0.0",
                Constraints = new LicenseConstraints { NotBefore = DateTime.UtcNow.AddMinutes(-1), NotAfter = DateTime.UtcNow.AddMinutes(1) }
            };
            manager.CreateAndSave(data, prv, name: "lic2");

            // 篡改存储文件使其不可解析
            var path = storage.ResolvePath("lic2");
            var bytes = File.ReadAllBytes(path);
            Array.Reverse(bytes);
            File.WriteAllBytes(path, bytes);

            var result = manager.LoadAndValidate("lic2", pub, new LicenseValidationOptions { ProductId = "TaiChi.ERP", ProductVersion = "1.0.0" });
            Assert.False(result.IsValid);
            // 文件损坏 -> Load 返回 null -> 格式无效
            Assert.Contains(Enums.ErrorCode.许可证_格式无效, result.Errors);
        }
        finally
        {
            try { Directory.Delete(baseDir, true); } catch { /* 忽略清理过程中的异常 */ }
        }
    }
}

