# TaiChi.LuaHost 使用指南

`TaiChi.LuaHost` 封装了 `LuaCSharp 0.5.0` 运行时在 ERP 体系中的通用能力，聚焦模块注册、参数绑定、远程脚本加载与异常统一处理，旨在让业务开发者专注于 handler 逻辑而非胶水代码。

## 目录结构
- `Helpers/LuaModuleHelper.cs`：注册模块表、写入 `LuaState.Environment`。
- `Builders/LuaModuleBuilder.cs`：提供 `AddSyncFunction`、`AddAsyncFunction`、`AddValueTaskFunction` 及 `MapStaticClass`/`MapObject`。
- `Contexts/LuaCallContext.cs`：封装参数解析、默认值和返回值写栈。
- `Attributes`：`LuaModuleMethod`、`LuaArg`、`LuaModuleIgnore` 等特性。
- `Options/LuaModuleMapOptions.cs` 与 `Naming/*`：控制命名策略、依赖注入、参数解析等高级行为。
- `RemoteLuaModuleLoader`：支持从 `ERP.ScriptHost.WebApi`/本地缓存加载 Lua 模块。

## 快速开始：静态绑定
```csharp
public static class FileHelperBindings
{
    [LuaModuleMethod("read_text")]
    public static string ReadText(string path, string? encoding = null)
        => TaiChi.IO.File.FileHelper.ReadFile(path, Encoding.GetEncoding(encoding ?? "utf-8")) ?? string.Empty;

    [LuaModuleMethod("write_text")]
    public static void WriteText(string path, string content, bool append = false)
        => TaiChi.IO.File.FileHelper.WriteFile(path, content, Encoding.UTF8, append);
}

LuaModuleHelper.RegisterModule(state, "FileHelper", builder =>
{
    builder.MapStaticClass<FileHelperBindings>(); // 零 LuaFunction 手写
});
```

## 绑定实例与 DI
```csharp
public sealed class ReportBindings
{
    private readonly IReportService _service;
    public ReportBindings(IReportService service) => _service = service;

    [LuaModuleMethod("generate")]
    public Task<string> GenerateAsync([LuaArg("report_id")] Guid id, CancellationToken token)
        => _service.GenerateAsync(id, token);
}

builder.MapObject(new ReportBindings(serviceProvider.GetRequiredService<IReportService>()), options =>
{
    options.ParameterResolvers.Add((parameter, context, out object? value) =>
    {
        if (parameter.ParameterType == typeof(ILogger))
        {
            value = serviceProvider.GetRequiredService<ILogger<ReportBindings>>();
            return true;
        }
        value = null;
        return false;
    });
});
```

## 参数与默认值
- `[LuaArg]` 可声明 Lua 端别名、可空策略与默认值提供器（实现 `ILuaArgumentDefaultValueProvider`）。
- C# 可选参数会自动作为缺省值；值类型默认以 `Activator.CreateInstance` 处理。
- `params` 参数会消费所有剩余 Lua 实参并组装为数组。

## 常用 API
| 类型 | 说明 |
| ---- | ---- |
| `LuaModuleHelper.RegisterModule(state, name, action)` | 注册模块至全局环境 |
| `LuaModuleBuilder.MapStaticClass<T>(options?)` | 扫描静态类型（默认仅导出带 `[LuaModuleMethod]` 的方法） |
| `LuaModuleBuilder.MapObject(instance, options?)` | 支持实例状态、依赖注入 |
| `LuaModuleBuilder.AddSyncFunction / AddAsyncFunction / AddValueTaskFunction` | 自定义委托注册 |
| `LuaCallContext.Require/Get/Return` | 强类型获取参数、写入返回值 |
| `LuaModuleMapOptions.IncludeImplicitMembers` | 是否导出未标记的方法 |
| `LuaModuleMapOptions.ParameterResolvers/ServiceResolver` | 自定义参数或服务注入 |

## 测试与验证
- 单元测试：`dotnet test ERP.LuaMapping.Tests/ERP.LuaMapping.Tests.csproj`，覆盖默认值、异步、异常与参数冲突。
- Lua 端验证：使用 `LuaState.DoString` 执行 `docs/LuaModuleHelperBindingGuide.md` 提供的脚本示例，确认模块表行为与预期一致。

## 文档扩展
- `docs/LuaModuleHelperBindingGuide.md`：包含更完整的示例、最佳实践与 Lua 脚本。
- `LuaModuleHelperPlan.md`、`优化文档.md`：记录演进计划、风险与 TODO。
- 若需自定义命名策略，可实现 `ILuaNamingStrategy` 并在 `LuaModuleMapOptions.NamingStrategy` 中替换。

> 建议所有 Lua 模块在合入前**至少**覆盖：函数导出、缺参提示、可选参数/默认值、Task/ValueTask 返回、异常包装等测试场景，并同步更新模块文档。
