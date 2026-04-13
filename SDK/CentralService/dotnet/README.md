# CentralService .NET SDK

返回根说明：[`../README.md`](../README.md)

## 项目定位

`.NET` 目录按 `service` 与 `client` 两类项目拆分，并同时维护现代运行时与 `.NET Framework 4.0` 变体：

- `CentralService.Service`：服务提供方接入，负责 `register`、WebSocket 心跳响应与 `deregister`
- `CentralService.Client`：服务消费方接入，负责 `list`、`discover`、`network`
- `net40` 目录：为 `CentralService.Service.Net40` 与 `CentralService.Client.Net40` 提供单独源码、编译与打包入口

## 目录结构

```text
dotnet/
  README.md
  NuGet.Config
  src/
    CentralService.Service/
    CentralService.Client/
  samples/
    CentralService.Client.AccessSample/
    CentralService.Service.RegisterSample/
  examples/
    CentralService.DotNetE2e/
    CentralService.DotNet10E2e/
    CentralService.DotNetCore20E2e/
  net40/
    build.ps1
    src/
      CentralService.Service/
      CentralService.Client/
    examples/
      CentralService.Net40E2e/
```

## 依赖

- `dotnet` 命令可用
- 现代项目目标框架：`netstandard2.0`、`net6.0`、`net10.0`
- `net40` 构建需要 Windows 与 `%WINDIR%/Microsoft.NET/Framework/v4.0.30319/csc.exe`
- E2E 需要可访问的 CentralService 实例，默认 `http://127.0.0.1:5000`

## 快速开始

### 构建现代 service / client 项目

在 `TaiChi/SDK/CentralService/dotnet/` 下执行：

```powershell
dotnet build "src/CentralService.Service/CentralService.Service.csproj" -c Release
dotnet build "src/CentralService.Client/CentralService.Client.csproj" -c Release
```

### 构建 `.NET Framework 4.0` 变体

```powershell
powershell -ExecutionPolicy Bypass -File "net40/build.ps1" -Configuration Release
```

### 运行 E2E 示例

```powershell
dotnet run --project "examples/CentralService.DotNetE2e/CentralService.DotNetE2e.csproj" -c Release --no-build --no-restore
dotnet run --project "examples/CentralService.DotNet10E2e/CentralService.DotNet10E2e.csproj" -c Release --no-build --no-restore
dotnet run --project "examples/CentralService.DotNetCore20E2e/CentralService.DotNetCore20E2e.csproj" -c Release --no-build --no-restore
```

`.NET Core 2.0` 示例要求本机安装对应运行时；统一脚本会在缺失时跳过该示例。`net40` 示例由 `net40/build.ps1` 编译后生成 `net40/examples/CentralService.Net40E2e/bin/Release/CentralService.Net40E2e.exe`。

### 运行 Samples

Samples 用于快速演示 SDK 在真实中心服务上的最小闭环（不依赖统一脚本）：

```powershell
dotnet run --project "samples/CentralService.Service.RegisterSample/CentralService.Service.RegisterSample.csproj" -c Release
dotnet run --project "samples/CentralService.Client.AccessSample/CentralService.Client.AccessSample.csproj" -c Release
```

## 构建 / 打包 / 验证

### 推荐：使用根级统一脚本

```powershell
python -X utf8 "../scripts/sdk.py" -Build -Languages "dotnet"
python -X utf8 "../scripts/sdk.py" -E2E -Languages "dotnet"
python -X utf8 "../scripts/sdk.py" -Pack -Languages "dotnet"
```

### 打包产物

统一打包后输出到 `../dist/dotnet/`：

- `service/CentralService.Service.{version}.nupkg`
- `service/CentralService.Service.Net40.{version}.nupkg`
- `client/CentralService.Client.{version}.nupkg`
- `client/CentralService.Client.Net40.{version}.nupkg`

### 验证建议

- 先执行 `dotnet build` 确认现代项目编译通过
- 需要 `net40` 时执行 `net40/build.ps1`
- 需要联调验证时执行根级 `sdk.py -E2E -Languages "dotnet"`

## service / client 子项目

- 服务端源码：`src/CentralService.Service/`
- 客户端源码：`src/CentralService.Client/`
- `.NET Framework 4.0` 服务端源码：`net40/src/CentralService.Service/`
- `.NET Framework 4.0` 客户端源码：`net40/src/CentralService.Client/`

更多接口范围与契约说明请返回根说明：[`../README.md`](../README.md)。
