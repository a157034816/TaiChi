# CentralService SDK（多语言 / 双项目）

CentralService SDK 为 `TaiChi/Service/CentralService/` 提供多语言接入层，统一按 `service` 与 `client` 两类项目拆分交付：

- `service`：面向服务提供方，负责注册、心跳、注销。
- `client`：面向服务消费方，负责服务列表、发现与网络状态查询/评估。

当前目录版本来自 `VERSION`，当前值为 `0.1.0`。各语言的工作区文件仅用于组织双项目源码，不代表仍提供单一“聚合 SDK”入口。

## 总览

| 维度 | 说明 |
| --- | --- |
| 服务端 SDK | 封装 `POST /api/Service/register`、`POST /api/Service/heartbeat`、`DELETE /api/Service/deregister/{id}` |
| 客户端 SDK | 封装 `GET /api/Service/list`、`GET /api/ServiceDiscovery/discover/*`、`GET /api/ServiceDiscovery/network/*`、`POST /api/ServiceDiscovery/network/evaluate/{serviceId}` |
| 覆盖语言 | `.NET`、`JavaScript`、`Python`、`Java`、`Go`、`Rust` |
| 运行时覆盖 | `.NET Framework 4.0`、`.NET Core 2.0`、`.NET 6`、`.NET 10` |
| 统一入口 | `scripts/sdk.py`（构建 / E2E / 打包），`scripts/e2e.py`（仅 E2E） |
| 统一产物目录 | `dist/` |

## 能力边界

### 已包含

- `service` 侧的服务注册、心跳、注销能力
- `client` 侧的服务列表、服务发现、网络状态查询与评估能力
- 跨语言一致的契约模型、E2E 示例与打包清单

### 未包含

- `/api/auth/*`
- `/api/admin/*`
- 中心服务本体部署、鉴权管理后台、非契约内自定义接口

以上接口或职责不在本目录 SDK 交付边界内，实际行为以中心服务实现与契约文档为准。

## 按语言快速导航

| 语言 | 语言入口 | 服务端项目 | 客户端项目 |
| --- | --- | --- | --- |
| .NET | [`dotnet/README.md`](./dotnet/README.md) | `dotnet/src/CentralService.Service`、`dotnet/net40/src/CentralService.Service` | `dotnet/src/CentralService.Client`、`dotnet/net40/src/CentralService.Client` |
| JavaScript | [`javascript/README.md`](./javascript/README.md) | `javascript/service` | `javascript/client` |
| Python | [`python/README.md`](./python/README.md) | `python/service` | `python/client` |
| Java | [`java/README.md`](./java/README.md) | `java/service` | `java/client` |
| Go | [`go/README.md`](./go/README.md) | `go/service` | `go/client` |
| Rust | [`rust/README.md`](./rust/README.md) | `rust/service` | `rust/client` |

## 目录结构

```text
TaiChi/SDK/CentralService/
  VERSION
  README.md
  contract/
    service-api.md
    discovery-api.md
    centralservice-api.md
    examples/
  scripts/
    sdk.py
    e2e.py
    e2e_fault_proxy.py
    sdk.ps1
    e2e.ps1
  dotnet/
  javascript/
  python/
  java/
  go/
  rust/
  dist/
```

补充说明：

- `contract/` 保存跨语言共享契约与错误样例。
- `scripts/sdk.py` 负责统一构建、E2E 与打包，保留同名 `.ps1` 兼容包装。
- `dist/` 为统一输出目录，每次重新打包会按脚本逻辑重建。

## 构建 / E2E / 打包入口

### 统一脚本

在 `TaiChi/SDK/CentralService/` 下执行：

```powershell
python -X utf8 "scripts/sdk.py" -Build
python -X utf8 "scripts/sdk.py" -E2E
python -X utf8 "scripts/sdk.py" -Pack
python -X utf8 "scripts/sdk.py" -All
```

常用参数：

- `-Languages dotnet,javascript,python,rust,java,go`
- `-SdkKinds service,client`
- `-BaseUrl http://127.0.0.1:5000`

### 仅执行 E2E

```powershell
python -X utf8 "scripts/e2e.py" -Languages "dotnet,javascript,python,rust,java,go"
```

### Windows 批处理入口（.bat）

如果不方便使用 PowerShell，可以直接运行同目录下的 `.bat` 包装：

```bat
scripts\sdk.bat -E2E
scripts\e2e.bat -Languages "dotnet,python"
```

### 手动启动中心服务

```powershell
dotnet run --project "TaiChi/Service/CentralService/CentralService.csproj" --urls "http://127.0.0.1:5000"
```

## 前置依赖

| 场景 | 依赖 |
| --- | --- |
| 通用 | Python `>=3.10`、`dotnet`；执行 E2E 时脚本会自托管 CentralService 实例 |
| .NET | 支持 `netstandard2.0`、`net6.0`、`net10.0` 的 .NET SDK；`net40` 额外需要 Windows 与 `%WINDIR%/Microsoft.NET/Framework/v4.0.30319/csc.exe` |
| JavaScript | Node.js 与 `npm` |
| Python | Python `>=3.10`，打包时需要 `setuptools>=61` 与 `wheel` |
| Java | JDK 8（`java`、`javac`、`jar`） |
| Go | Go `1.20+` |
| Rust | Rust stable 与 `cargo` |

## 产物说明

统一打包后，产物写入 `dist/`，并由 `dist/manifest.json` 记录版本、构建时间、`BaseUrl`、产物路径、大小与 `SHA256`。

```text
dist/
  manifest.json
  dotnet/
    service/
    client/
  javascript/
    service/
    client/
  python/
    service/
    client/
  java/
    service/
    client/
  go/
    service/
    client/
  rust/
    service/
    client/
```

各语言产物形态：

- `.NET`：`.nupkg`，按现代包与 `Net40` 包分别输出
- `JavaScript`：`npm pack` 生成的 `.tgz`
- `Python`：`build_dist.py` 生成的源码包与 wheel
- `Java`：按项目生成 `.jar`
- `Go`：按项目目录打包的 `.zip`
- `Rust`：`cargo package` 生成的 `.crate`

## 常见问题

### 为什么语言目录下还有工作区文件？

例如 `javascript/package.json`、`python/pyproject.toml`、`go/go.work`、`rust/Cargo.toml` 只是双项目工作区元数据，用于管理 `service` / `client` 两个项目，不是聚合 SDK 入口。

### 如何只构建某个语言或某个项目类型？

使用：

```powershell
python -X utf8 "scripts/sdk.py" -Build -Languages "python" -SdkKinds "service"
```

### E2E 失败时先检查什么？

- `CENTRAL_SERVICE_BASEURL` 或 `-BaseUrl` 是否可访问
- `TaiChi/Service/CentralService/CentralService.csproj` 是否能在本机成功构建并启动
- 当前语言运行时是否已安装
- `.NET Core 2.0` 示例运行时是否存在；脚本在缺失时会跳过对应示例

### 为什么打包后 `dist/` 内容消失或被覆盖？

`scripts/sdk.py -Pack` 会重置 `dist/`，再生成新的产物与 `manifest.json`。如需保留历史产物，请先手动备份。

## 相关契约链接

- 服务端契约：[`contract/service-api.md`](./contract/service-api.md)
- 客户端契约：[`contract/discovery-api.md`](./contract/discovery-api.md)
- 兼容入口：[`contract/centralservice-api.md`](./contract/centralservice-api.md)
- 契约样例：[`contract/examples/`](./contract/examples/)
- 服务实现基准：`TaiChi/Service/CentralService/Controllers/ServiceController.cs`
- 发现实现基准：`TaiChi/Service/CentralService/Controllers/ServiceDiscoveryController.cs`
