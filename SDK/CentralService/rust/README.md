# CentralService Rust SDK

返回根说明：[`../README.md`](../README.md)

## 项目定位

Rust 目录使用 workspace 组织三个 crate：

- `service`：crate `centralservice_service`
- `client`：crate `centralservice_client`
- `e2e`：crate `centralservice_sdk_e2e`，用于联合验证 service / client

根级 `Cargo.toml` 仅定义 workspace 成员，不提供聚合 SDK crate。

## 目录结构

```text
rust/
  README.md
  Cargo.toml
  Cargo.lock
  service/
    README.md
    Cargo.toml
    src/
  client/
    README.md
    Cargo.toml
    src/
  e2e/
    Cargo.toml
    src/main.rs
```

## 依赖

- Rust stable
- `cargo`
- 可访问的 CentralService 实例，默认 `http://127.0.0.1:5000`

## 快速开始

在 `TaiChi/SDK/CentralService/rust/` 下执行：

```powershell
cargo build --release -p centralservice_service
cargo build --release -p centralservice_client

$env:CENTRAL_SERVICE_BASEURL = "http://127.0.0.1:5000"
cargo run --release -p centralservice_sdk_e2e
```

如需验证多中心 + 超时 / 熔断 / 切换场景，可改用：

```powershell
$env:CENTRAL_SERVICE_ENDPOINTS_JSON = @'
[
  {
    "baseUrl": "http://127.0.0.1:5000",
    "priority": 0,
    "maxAttempts": 2,
    "circuitBreaker": {
      "failureThreshold": 2,
      "breakDurationMinutes": 1,
      "recoveryThreshold": 1
    }
  },
  {
    "baseUrl": "http://127.0.0.1:5001",
    "priority": 1
  }
]
'@
$env:CENTRAL_SERVICE_TIMEOUT_MS = "800"
$env:CENTRAL_SERVICE_E2E_SCENARIO = "transport_failover"
cargo run --release -p centralservice_sdk_e2e
```

说明：

- `CENTRAL_SERVICE_BASEURL` 仍可作为单中心 fallback。
- `CENTRAL_SERVICE_ENDPOINTS_JSON` 生效时，e2e 会按 `priority` 升序构造 service/client SDK 配置。
- `CENTRAL_SERVICE_TIMEOUT_MS` 会同时应用到 discovery client 与 service client，请求超时可压到亚秒级或 1 秒级，便于故障端点悬挂场景联调。

## E2E 场景

`centralservice_sdk_e2e` 支持以下 `CENTRAL_SERVICE_E2E_SCENARIO`：

- `smoke`
- `service_fanout`
- `transport_failover`
- `business_no_failover`
- `max_attempts`
- `circuit_open`
- `circuit_recovery`
- `half_open_reopen`

其中 `service_fanout` 会在示例层对所有端点复用同一个 `serviceId` 执行 `register / heartbeat / deregister`，SDK 本身不会自动广播注册。

## 构建 / 打包 / 验证

### 推荐：使用根级统一脚本

```powershell
python -X utf8 "../scripts/sdk.py" -Build -Languages "rust"
python -X utf8 "../scripts/sdk.py" -E2E -Languages "rust"
python -X utf8 "../scripts/sdk.py" -Pack -Languages "rust"
```

### 单 crate 打包

如需手动打包，可分别进入 `service/` 与 `client/` 执行：

```powershell
cargo package --allow-dirty --no-verify
```

### 打包产物

统一打包后输出到 `../dist/rust/`：

- `service/centralservice_service-{version}.crate`
- `client/centralservice_client-{version}.crate`

## service / client 子项目

- 服务端 crate 说明：[`service/README.md`](./service/README.md)
- 客户端 crate 说明：[`client/README.md`](./client/README.md)

契约范围、统一脚本入口与其他语言说明请参考根说明：[`../README.md`](../README.md)。
