# CentralService Go SDK

返回根说明：[`../README.md`](../README.md)

## 项目定位

Go 目录使用 `go.work` 组织 `service`、`client` 与 `examples/e2e` 三个模块：

- `service`：模块 `ensoai.local/centralservice-service`
- `client`：模块 `ensoai.local/centralservice-client`
- `examples/e2e`：组合验证 service / client 的端到端示例

## 目录结构

```text
go/
  README.md
  go.mod
  go.work
  service/
    README.md
    go.mod
    *.go
  client/
    README.md
    go.mod
    *.go
  examples/
    e2e/
      go.mod
      main.go
```

根级 `go.mod` / `go.work` 用于统一工作区管理，不是聚合 SDK 包。

## 依赖

- Go `1.20+`
- 可访问的 CentralService 实例，默认 `http://127.0.0.1:5000`

## 快速开始

在 `TaiChi/SDK/CentralService/go/` 下执行：

```powershell
Push-Location "service"
go build ./...
Pop-Location

Push-Location "client"
go build ./...
Pop-Location

$env:CENTRAL_SERVICE_BASEURL = "http://127.0.0.1:5000"
$env:CENTRAL_SERVICE_TIMEOUT_MS = "800"
Push-Location "examples/e2e"
go run .
Pop-Location
```

如需多中心联调，可改用 `CENTRAL_SERVICE_ENDPOINTS_JSON` 传入端点数组，并通过
`CENTRAL_SERVICE_E2E_SCENARIO` 选择 `smoke / service_fanout / transport_failover / business_no_failover / max_attempts / circuit_open / circuit_recovery / half_open_reopen`。

```powershell
$env:CENTRAL_SERVICE_ENDPOINTS_JSON = '[{"baseUrl":"http://127.0.0.1:5001","priority":0,"maxAttempts":2,"circuitBreaker":{"failureThreshold":1,"breakDurationMinutes":1,"recoveryThreshold":1}},{"baseUrl":"http://127.0.0.1:5002","priority":10}]'
$env:CENTRAL_SERVICE_E2E_SCENARIO = "transport_failover"
$env:CENTRAL_SERVICE_TIMEOUT_MS = "800"
$env:CENTRAL_SERVICE_BREAK_WAIT_SECONDS = "2"
Push-Location "examples/e2e"
go run .
Pop-Location
```

- `CENTRAL_SERVICE_TIMEOUT_MS` 会同时应用到 discovery client 与 service client。
- `CENTRAL_SERVICE_BREAK_WAIT_SECONDS` / `CENTRAL_SERVICE_E2E_BREAK_WAIT_SECONDS` 可用于缩短 `circuit_recovery`、`half_open_reopen` 的半开等待。

## 构建 / 打包 / 验证

### 推荐：使用根级统一脚本

```powershell
python -X utf8 "../scripts/sdk.py" -Build -Languages "go"
python -X utf8 "../scripts/sdk.py" -E2E -Languages "go"
python -X utf8 "../scripts/sdk.py" -Pack -Languages "go"
```

### 打包产物

统一打包后输出到 `../dist/go/`：

- `service/centralservice-service-go-{version}.zip`
- `client/centralservice-client-go-{version}.zip`

### 验证建议

- `go build ./...` 用于检查单个模块可编译
- `examples/e2e/go run .` 用于联调验证
- 如需与统一发布口径一致，优先使用根级 `sdk.py`

## service / client 子项目

- 服务端模块说明：[`service/README.md`](./service/README.md)
- 客户端模块说明：[`client/README.md`](./client/README.md)

更多契约与统一说明请参考根说明：[`../README.md`](../README.md)。
