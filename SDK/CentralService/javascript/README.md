# CentralService JavaScript SDK

返回根说明：[`../README.md`](../README.md)

## 项目定位

当前目录为 JavaScript 双项目工作区：

- `service`：包名 `@ensoai/erp-centralservice-service`，面向服务提供方
- `client`：包名 `@ensoai/erp-centralservice-client`，面向服务消费方

根级 `package.json` 仅声明 workspace，不提供聚合 SDK 入口。

## 目录结构

```text
javascript/
  README.md
  package.json
  service/
    package.json
    README.md
    src/index.js
  client/
    package.json
    README.md
    src/index.js
  examples/
    e2e.js
```

## 依赖

- Node.js
- `npm`
- 可访问的 CentralService 实例，默认 `http://127.0.0.1:5000`

## 快速开始

### 验证包入口可加载

在 `TaiChi/SDK/CentralService/javascript/` 下执行：

```powershell
Push-Location "service"
node -e "require('./src/index.js')"
Pop-Location

Push-Location "client"
node -e "require('./src/index.js')"
Pop-Location
```

### 运行端到端示例

```powershell
$env:CENTRAL_SERVICE_BASEURL = "http://127.0.0.1:5000"
$env:CENTRAL_SERVICE_TIMEOUT_MS = "800"
node "examples/e2e.js"

> 如需多中心联调，可改用 `CENTRAL_SERVICE_ENDPOINTS_JSON` 传入端点数组，并通过 `CENTRAL_SERVICE_E2E_SCENARIO` 选择 `smoke / service_fanout / transport_failover / business_no_failover / max_attempts / circuit_open / circuit_recovery / half_open_reopen`。
```

## 构建 / 打包 / 验证

### 本地构建与验证

JavaScript 项目当前没有额外编译步骤，默认通过加载 `src/index.js` 进行快速验证。

### 单包打包

```powershell
Push-Location "service"
npm pack
Pop-Location

Push-Location "client"
npm pack
Pop-Location
```

### 推荐：使用根级统一脚本

```powershell
python -X utf8 "../scripts/sdk.py" -Build -Languages "javascript"
python -X utf8 "../scripts/sdk.py" -E2E -Languages "javascript"
python -X utf8 "../scripts/sdk.py" -Pack -Languages "javascript"
```

统一打包后输出到 `../dist/javascript/service/` 与 `../dist/javascript/client/`，产物为 `npm pack` 生成的 `.tgz` 文件。

## service / client 子项目

- 服务端包说明：[`service/README.md`](./service/README.md)
- 客户端包说明：[`client/README.md`](./client/README.md)

接口边界、契约链接与跨语言说明请参考根说明：[`../README.md`](../README.md)。
