TaiChi.License 许可证管理库

概述
- 加密：RSA-PSS(SHA256) 签名，AES‑256‑CBC 加解密
- 硬件：跨平台硬件指纹（Windows WMI，Linux/macOS 系统文件与网卡MAC）
- 数据：POCO 数据模型，默认零依赖 XML 序列化（可切换 MessagePack）
- 存储：文件存储实现（原子写入），可扩展数据库
- 门面：统一入口 LicenseManager，简化常见用法

安装与引用
- 在解决方案中项目引用：..\\TaiChi.License\\TaiChi.License.csproj
- 库目标框架 netstandard2.1，可用于 .NET 5/6/7/8/9

快速上手（示例）

```csharp
using System;
using System.IO;
using TaiChi.License;
using TaiChi.License.Models;
using TaiChi.License.Services;
using TaiChi.License.Services.Serialization;

class Demo
{
    static void Main()
    {
        var serializer = DefaultSerializer.Instance;
        var baseDir = Path.Combine(Environment.CurrentDirectory, "licenses");
        var storage  = new LicenseStorage(baseDir, serializer);
        var manager  = new LicenseManager(storage, serializer);

        var (pub, prv) = manager.GenerateRsaKeyPair();

        var data = new LicenseData
        {
            ProductId = "TaiChi.ERP",
            ProductName = "太极ERP",
            ProductVersion = "1.2.3",
            Constraints = new LicenseConstraints
            {
                NotBefore = DateTime.UtcNow.AddMinutes(-1),
                NotAfter  = DateTime.UtcNow.AddDays(30),
                BindHardware = false,
                Features = new() { "A", "B" },
                MaxUsers = 100,
                VersionRange = "[1.0,2.0)"
            }
        };

        manager.CreateAndSave(data, prv, name: "trial", keyId: "k1");

        var options = new LicenseValidationOptions
        {
            ProductId = "TaiChi.ERP",
            ProductVersion = "1.2.3",
            RequiredFeatures = new() { "A" },
            CurrentUsers = 3
        };
        var result = manager.LoadAndValidate("trial", pub, options);
        Console.WriteLine($"Valid={result.IsValid}, Errors={string.Join(',', result.Errors)}");
    }
}
```

错误码与约束（节选）
- 时间：尚未生效/已过期
- 产品/版本：产品不匹配/版本不匹配
- 硬件：硬件不匹配
- 功能/用户数：功能未授权/用户数超限
- 数据/签名：反序列化失败/签名无效

最佳实践
- 私钥仅在签发端保管，客户端只分发公钥
- 固定可预期的网卡设备，降低指纹波动
- 根据产品策略设计版本区间（如主版本兼容）
- 如需体积/性能，注入 MessagePack 实现替代默认 XML

更多示例
- 参见 TaiChi/Examples/TaiChi.License.Example 控制台工程

切换为 MessagePack 序列化（可选）
- 安装依赖（到使用 License 的启动项目或库）：
  - dotnet add <YourProject>.csproj package MessagePack
  - dotnet add <YourProject>.csproj package MessagePack.Annotations
- 在 `TaiChi.License` 项目或引用处开启条件编译常量以启用适配器源码：
  - 在 csproj 内添加：
    <PropertyGroup>
      <DefineConstants>$(DefineConstants);TAI_CHI_USE_MESSAGEPACK</DefineConstants>
    </PropertyGroup>
- 代码注入：
  var serializer = new TaiChi.License.Services.Serialization.MessagePackSerializerAdapter();
  var storage    = new TaiChi.License.Services.LicenseStorage(path, serializer);
  var manager    = new TaiChi.License.LicenseManager(storage, serializer);

文本分发（Base64）
- 将许可证序列化为 Base64 字符串便于通过文本渠道分发：
  var base64 = manager.EncodeToBase64(licenseKey);
  var result = manager.ValidateBase64(base64, publicKeySpki, options);
