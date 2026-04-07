# TaiChi.LuaHost 使用指南

`TaiChi.LuaHost` 基于 `LuaCSharp 0.5.0`，提供：脚本执行、全局注入、.NET 代理壳封装与远程模块加载能力。

## 目录结构

- `LuaScriptHost`：宿主封装（`ExecuteAsync`/`ExecuteFileAsync`、全局注入等）
- `Proxies/*`：.NET 对象/静态类型代理壳（`LuaProxyTableFactory`、`LuaStaticProxyTableFactory` 等）
- `RemoteLuaModuleLoader`：从 `Lua.Script.Provider.Api` 与本地缓存加载 Lua 模块
- `Exceptions/*`：统一异常（如 `LuaMappingException`）
- `Attributes/LuaOverloadPreferredAttribute.cs`：代理方法重载选择提示（可选）

## 快速开始

```csharp
using Lua;
using TaiChi.LuaHost;

var host = new LuaScriptHost(new LuaScriptHostOptions
{
    LoadStandardLibraries = true,
    ScriptRoot = AppContext.BaseDirectory
});

host.SetGlobal("app_name", "ERP");
host.RegisterFunction("wait", async (ctx, ct) =>
{
    var milliseconds = ctx.GetArgument<int>(0);
    await Task.Delay(milliseconds, ct);
    return ctx.Return();
});

await host.ExecuteAsync("print(app_name); wait(1)");
```

## 绑定与注入建议

- **业务服务/对象**：优先 `SetGlobalProxy(name, instance)`，由代理壳负责成员访问与方法调用。
- **简单工具模块**：可手写 `LuaTable` + `LuaFunction` 注入到 `LuaState.Environment`；若脚本使用 `require()`，同时写入 `package.loaded`。
- **需要暴露大量静态成员**：使用 `RegisterStaticType(type, alias)` 注入到 Lua 全局 `static` 表。

更完整的示例与约定见：`docs/LuaHostBindingGuide.md`。

## 测试与验证

- 单元测试：`dotnet test TaiChi/Tests/TaiChi.LuaHost.Tests/TaiChi.LuaHost.Tests.csproj`
