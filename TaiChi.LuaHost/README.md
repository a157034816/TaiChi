# TaiChi.LuaHost 使用指南

`TaiChi.LuaHost` 是基于 `LuaCSharp 0.5.0` 的轻量 Lua 宿主，负责脚本执行、全局注入、对象/静态类型代理、工厂构造与远程模块加载。

## 快速入口

- `LuaScriptHost`：创建宿主、执行脚本、注册函数与全局值
- `LuaScriptHostOptions`：脚本根目录、标准库、静态自动注册、工厂自动解析、模块加载器
- `LuaProxyTableFactory` / `LuaStaticProxyTableFactory`：对象代理与静态类型代理
- `LuaFactoryTypeRegistry`：`create(...)` 的类型白名单与解析规则
- `RemoteLuaModuleLoader`：本地优先、远程回退、缓存复用的 `require()` 加载器

## 快速开始

```csharp
using System;
using System.Text;
using System.Threading.Tasks;
using TaiChi.IO.File;
using TaiChi.LuaHost;

var host = new LuaScriptHost(new LuaScriptHostOptions
{
    ScriptRoot = AppContext.BaseDirectory,
    LoadStandardLibraries = true
});

host.SetGlobal("app_name", "ERP");
host.RegisterFunction("wait", async (ctx, ct) =>
{
    var milliseconds = ctx.GetArgument<int>(0);
    await Task.Delay(milliseconds, ct);
    return ctx.Return();
});

host.RegisterStaticType(typeof(FileHelper));
host.RegisterFactoryType<StringBuilder>("StringBuilder");

await host.ExecuteAsync(@"
print(app_name)
print(static.FileHelper.GetAbsolutePath('./data/config.json'))
local sb = create('StringBuilder', 'hello')
print(sb:ToString())
wait(1)
");
```

## 详细文档

- [LuaHostBindingGuide](../../docs/LuaHostBindingGuide.md)：完整绑定说明、配置项、示例与常见错误

## 测试

- `dotnet test TaiChi/Tests/TaiChi.LuaHost.Tests/TaiChi.LuaHost.Tests.csproj`
