# TaiChi.I18n - 多语言国际化框架

TaiChi.I18n 是一个功能强大且易于使用的.NET多语言国际化框架，支持WPF和ASP.NET Core应用程序。

## 主要特性

### 🚀 核心功能
- **多格式支持**: JSON（主要）+ ResX（传统）双格式支持
- **运行时切换**: 无需重启应用即可切换语言
- **智能缓存**: 高性能缓存机制，支持过期管理
- **文件监控**: 开发时资源文件变化自动重载
- **异步支持**: 所有核心API提供异步版本

### 🎨 WPF集成
- **XAML绑定**: `{i18n:LocalizedBinding ResourceKey}`
- **图片本地化**: `{i18n:LocalizedImageBinding ResourceKey}`
- **扩展方法**: 便捷的代码中本地化API
- **自动更新**: 语言切换时UI自动更新

### 🌐 Web集成
- **中间件支持**: ASP.NET Core中间件集成
- **文化检测**: 自动检测请求语言（URL、Cookie、Header）
- **API端点**: RESTful资源访问API
- **依赖注入**: 完整的DI容器支持

### 🛠️ 开发工具
- **资源生成器**: 自动扫描代码生成资源文件模板
- **资源验证器**: 检查资源完整性和一致性
- **转换工具**: ResX到JSON格式转换
- **使用统计**: 资源键使用情况分析

## 快速开始

### 1. 安装

```xml
<PackageReference Include="TaiChi.I18n" Version="1.0.0" />
```

### 2. 创建资源文件

创建 `Resources/zh-CN.json`:
```json
{
  "Common": {
    "Save": "保存",
    "Cancel": "取消",
    "Welcome": "欢迎使用 {0}"
  }
}
```

创建 `Resources/en-US.json`:
```json
{
  "Common": {
    "Save": "Save",
    "Cancel": "Cancel",
    "Welcome": "Welcome to {0}"
  }
}
```

### 3. WPF应用集成

#### App.xaml.cs 初始化
```csharp
public partial class App : Application
{
    public static ILocalizationService LocalizationService { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 创建配置
        var config = new LocalizationConfig
        {
            DefaultCulture = new CultureInfo("zh-CN"),
            SupportedCultures = new List<CultureInfo>
            {
                new CultureInfo("zh-CN"),
                new CultureInfo("en-US")
            },
            ResourceDirectory = "Resources"
        };

        // 创建服务
        LocalizationService = new LocalizationService(config);

        // 初始化WPF扩展
        WpfExtensions.InitializeWpfLocalization(LocalizationService);
    }
}
```

#### XAML中使用
```xml
<Window xmlns:i18n="clr-namespace:TaiChi.I18n;assembly=TaiChi.I18n"
        Title="{i18n:LocalizedBinding Common.WindowTitle}">

    <StackPanel>
        <Button Content="{i18n:LocalizedBinding Common.Save}" />
        <Button Content="{i18n:LocalizedBinding Common.Cancel}" />

        <!-- 本地化图片 -->
        <Image Source="{i18n:LocalizedImageBinding Images.Logo}" />
    </StackPanel>
</Window>
```

#### 代码中使用
```csharp
// 基本用法
var text = LocalizationService.GetString("Common.Save");

// 格式化字符串
var welcome = LocalizationService.GetFormattedString("Common.Welcome", "TaiChi.I18n");

// 切换语言
LocalizationService.SetLanguage(new CultureInfo("en-US"));

// 扩展方法
this.Localize("Common.Save"); // 为控件设置本地化
```

### 4. ASP.NET Core集成

#### Startup.cs / Program.cs
```csharp
// 配置服务
services.AddTaiChiLocalization(config =>
{
    config.DefaultCulture = new CultureInfo("zh-CN");
    config.ResourceDirectory = "Resources";
});

// 配置中间件
app.UseTaiChiLocalization();
```

#### 控制器中使用
```csharp
public class HomeController : Controller
{
    public IActionResult Index()
    {
        var welcomeMessage = HttpContext.Localize("Common.Welcome", "TaiChi.I18n");
        ViewBag.Message = welcomeMessage;
        return View();
    }
}
```

## 高级功能

### 资源生成工具

自动扫描代码中的资源键并生成资源文件模板：

```csharp
var generator = new ResourceGenerator();
var config = new ResourceGenerator.GeneratorConfig
{
    ScanDirectories = new List<string> { @"C:\MyProject\Src" },
    OutputDirectory = "Resources",
    FilePatterns = new List<string> { "*.cs", "*.xaml" },
    KeyPatterns = new List<string>
    {
        @"GetString\([""']([^""']+)[""']\)",
        @"LocalizedBinding\s+ResourceKey\s*=\s*[""']([^""']+)[""']"
    }
};

var report = await generator.GenerateResourceFilesAsync(config);
Console.WriteLine($"发现 {report.DiscoveredKeys} 个资源键");
```

### 资源验证工具

检查资源文件的完整性和一致性：

```csharp
var validator = new ResourceValidator();
var config = new ResourceValidator.ValidationConfig
{
    ResourceDirectory = "Resources",
    ReferenceCulture = new CultureInfo("zh-CN"),
    CheckEmptyValues = true,
    ValidateFormatStrings = true
};

var report = await validator.ValidateAsync(config);
Console.WriteLine($"验证完成: {report.ErrorCount} 错误, {report.WarningCount} 警告");
```

### ResX转换工具

将传统的ResX文件转换为JSON格式：

```csharp
var converter = new ResXToJsonConverter();

// 转换单个文件
await converter.ConvertFileAsync("Resources.resx", "zh-CN.json");

// 批量转换
var report = await converter.ConvertDirectoryAsync("OldResources", "NewResources");
```

## 配置选项

```csharp
var config = new LocalizationConfig
{
    // 基本设置
    DefaultCulture = new CultureInfo("zh-CN"),
    SupportedCultures = new List<CultureInfo> { /* ... */ },
    ResourceDirectory = "Resources",

    // 性能设置
    EnableCaching = true,
    CacheExpirationMinutes = 60,
    MaxCacheSize = 1000,

    // 开发设置
    EnableFileMonitoring = true,
    MonitoringInterval = 1000,

    // 回退设置
    FallbackToParentCulture = true,
    FallbackToDefaultCulture = true
};
```

## 最佳实践

### 1. 资源键命名约定
```
分类.功能.具体项
例如: UI.Buttons.Save, Messages.Errors.ValidationFailed
```

### 2. 格式化字符串
```json
{
  "Messages": {
    "Welcome": "欢迎，{0}！今天是 {1:yyyy-MM-dd}",
    "ItemCount": "找到 {0} 个项目"
  }
}
```

### 3. 图片和音频资源
```json
{
  "Images": {
    "Logo": "Resources/Images/logo-zh.png",
    "Icon": "Resources/Images/icon-zh.png"
  },
  "Audio": {
    "Notification": "Resources/Audio/notify-zh.wav"
  }
}
```

### 4. 错误处理
```csharp
// 使用Try方法避免异常
if (LocalizationService.TryGetString("SomeKey", out string value))
{
    // 使用 value
}
else
{
    // 处理缺失的键
}
```

## 示例项目

查看 `TaiChi.I18n.WpfExample` 项目了解完整的使用示例，包括：

- 基本的XAML绑定用法
- 动态语言切换
- 格式化字符串示例
- 表单验证本地化
- 图片资源本地化
- 工具使用演示

## 性能特性

- **懒加载**: 资源按需加载，减少内存占用
- **智能缓存**: 多级缓存策略，支持过期和LRU清理
- **异步处理**: 所有I/O操作支持异步，不阻塞UI线程
- **内存优化**: 使用WeakReference避免内存泄漏
- **批量操作**: 支持批量加载和验证，提升性能

## 支持的平台

- .NET 6.0+
- WPF应用程序
- ASP.NET Core 6.0+
- 控制台应用程序

## 许可证

MIT License

## 贡献

欢迎提交Issue和Pull Request！

---

更多详细信息请参考API文档和示例代码。