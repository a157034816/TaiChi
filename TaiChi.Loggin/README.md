# TaiChi.Loggin - 日志抽象（MEL）+ Serilog 实现

TaiChi.Loggin 是 TaiChi 生态的日志库：以 `Microsoft.Extensions.Logging` 作为统一抽象/门面（面向 `ILogger/ILogger<T>` 编码），以 Serilog 作为结构化日志实现（丰富的 Sink 生态与活跃社区）。

## 设计目标

- **统一抽象**：业务代码只依赖 `Microsoft.Extensions.Logging`（`ILogger/ILogger<T>`）。
- **结构化日志**：使用 Serilog 输出结构化字段，便于查询、聚合与告警。
- **易于接入**：提供 `IServiceCollection` / `ILoggingBuilder` 扩展，方便外部程序通过依赖注入集成。

## 依赖说明

本库依赖：

- `Microsoft.Extensions.Logging`（抽象）
- `Serilog`（实现）
- `Serilog.Extensions.Logging`（桥接 Provider）

注意：**本库不内置任何 Sink**。请在你的应用项目中按需安装对应的 `Serilog.Sinks.*` 包（例如控制台、文件、Seq、Elastic、数据库等）。

## 快速开始（依赖注入）

### 1) 引用项目

推荐使用项目引用：

```xml
<ItemGroup>
  <ProjectReference Include="..\\TaiChi.Loggin\\TaiChi.Loggin.csproj" />
</ItemGroup>
```

### 2) 安装 Sink（示例：控制台）

在你的应用项目中添加（版本请按你的项目统一管理）：

```xml
<PackageReference Include="Serilog.Sinks.Console" Version="x.y.z" />
```

### 3) 注册到 IServiceCollection

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TaiChi.Loggin;

var services = new ServiceCollection();

services.AddLoggin(cfg =>
{
    cfg.WriteTo.Console();
    cfg.Enrich.WithProperty("App", "Demo");
});

using var provider = services.BuildServiceProvider();

var logger = provider.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Hello {Name}", "TaiChi");
```

说明：

- `AddLoggin(...)` 默认会清空现有 Provider（`clearProviders: true`），如需保留其它 Provider，请传入 `clearProviders: false`。
- 如果你传入的是外部维护的 `Serilog.ILogger`（例如多个容器共享），建议将 `dispose: false`，避免被 DI 释放。

### 4) 注册到 Host（推荐）

```csharp
using TaiChi.Loggin;

var builder = WebApplication.CreateBuilder(args);

// 一键接入（内部可选 ClearProviders）
builder.Host.UseLoggin(cfg => cfg.WriteTo.Console());
```

### 5) 注册到 ILoggingBuilder（兼容用法）

```csharp
using TaiChi.Loggin;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddLoggin(cfg => cfg.WriteTo.Console());
```

## 进阶用法

### 使用现成的 Serilog logger

```csharp
using Serilog;
using TaiChi.Loggin;

var serilog = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

services.AddLoggin(serilog, dispose: true, clearProviders: true);
```

### LogContext 作用域属性（结构化字段）

```csharp
using Serilog.Context;
using Microsoft.Extensions.Logging;

using (LogContext.PushProperty("TraceId", traceId))
{
    logger.LogInformation("处理请求 {RequestId}", requestId);
}
```

提示：需要 `Enrich.FromLogContext()`。本库默认创建的 Serilog 配置已启用该富化器。

### 全局 logger（可选）

当你的应用希望使用 `Serilog.Log.Logger`（全局 logger）时：

```csharp
TaiChiLogginFactory.SetGlobalLogger(serilog);

// 应用退出时建议刷新
TaiChiLogginFactory.CloseAndFlush();
```

## API 概览

- `IServiceCollection.AddLoggin(...)`：注册日志到 DI（推荐入口）。
- `ILoggingBuilder.AddLoggin(...)`：在 Host/WebApplicationBuilder 场景添加日志 Provider。
- `IHostBuilder.UseLoggin(...)`：在 Host/WebApplicationBuilder 场景一键集成日志。
- `ILoggingBuilder.AddSerilogProvider(...)`：更底层的 Provider 添加方法。
- `TaiChiLogginFactory`：创建/管理 Serilog logger 与 `ILoggerFactory`。

## 目标框架

- `netstandard2.1`
