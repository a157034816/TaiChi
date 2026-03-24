# CentralService Rust Service

## 定位

- 面向服务提供方的 Rust SDK，用于注册服务实例、发送心跳、主动注销。
- 读取服务列表、执行服务发现、查询网络评估结果请使用 `../client/README.md`。

## 包名

- crate：`centralservice_service`
- 主要入口类型：`ServiceClient`
- 主要模型：`ServiceRegistrationRequest`、`ServiceHeartbeatRequest`、`ServiceRegistrationResponse`

## 环境要求

- 稳定版 Rust + Cargo，支持 `edition = "2021"`
- 可访问 CentralService HTTP API，例如 `http://127.0.0.1:5000`
- 当前 crate 位于 workspace `../Cargo.toml`

## 安装/引用

- 推荐在消费方 `Cargo.toml` 使用 path dependency 引用当前 crate。

```toml
[dependencies]
centralservice_service = { path = "../service" }
```

```rust
use centralservice_service::{
    ServiceClient, ServiceHeartbeatRequest, ServiceRegistrationRequest,
};
```

## 快速示例

```rust
use centralservice_service::{
    ServiceClient, ServiceHeartbeatRequest, ServiceRegistrationRequest,
};
use std::collections::BTreeMap;

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let service = ServiceClient::new("http://127.0.0.1:5000")?;

    let mut metadata = BTreeMap::new();
    metadata.insert("sdk".to_string(), "rust".to_string());

    let reg = service.register(&ServiceRegistrationRequest {
        id: Some(String::new()),
        name: "demo-service".to_string(),
        host: "127.0.0.1".to_string(),
        port: 8081,
        service_type: "Web".to_string(),
        health_check_url: "/health".to_string(),
        health_check_port: 0,
        health_check_type: "Http".to_string(),
        weight: 1,
        metadata,
    })?;

    service.heartbeat(&ServiceHeartbeatRequest { id: reg.id.clone() })?;
    service.deregister(&reg.id)?;
    Ok(())
}
```

## 多中心端点配置

`ServiceClient::new(base_url)` 仍保留单中心兼容入口；如需统一的多中心配置模型，请使用 `ServiceClientOptions`：

```rust
use centralservice_service::{
    CentralServiceCircuitBreakerOptions, CentralServiceEndpointOptions, ServiceClient,
    ServiceClientOptions,
};
use std::time::Duration;

fn build_client() -> Result<ServiceClient, centralservice_service::SdkError> {
    let mut primary = CentralServiceEndpointOptions::new("http://127.0.0.1:5000");
    primary.priority = 0;
    primary.max_attempts = Some(2);
    primary.circuit_breaker = Some(CentralServiceCircuitBreakerOptions {
        failure_threshold: 2,
        break_duration_minutes: 1,
        recovery_threshold: 1,
    });

    let mut backup = CentralServiceEndpointOptions::new("http://127.0.0.1:5001");
    backup.priority = 1;

    let mut options = ServiceClientOptions::from_endpoints(vec![primary, backup])?;
    options.timeout = Duration::from_millis(800);
    ServiceClient::from_options(options)
}
```

说明：

- service SDK 已支持和 client 相同的端点配置、超时与 transport failover 基础能力。
- 当前 crate 不会自动对全部中心执行广播注册；如需 fan-out，请在业务层或 e2e/example 层显式遍历端点。

## 构建

当前 crate 的最小构建验证：

```powershell
cargo build --release
```

如需与仓库统一口径保持一致，建议在仓库根目录执行：

```powershell
python -X utf8 "TaiChi/SDK/CentralService/scripts/sdk.py" -Build -Languages rust -SdkKinds service
```

## 打包

```powershell
cargo package --allow-dirty --no-verify
```

如需生成统一产物目录：

```powershell
python -X utf8 "TaiChi/SDK/CentralService/scripts/sdk.py" -Pack -Languages rust -SdkKinds service
```

## 验证

- 当前 crate 的快速验证以 `cargo build --release` 为准。
- 完整联调可参考 workspace 内的 `../e2e/src/main.rs`。
- e2e 示例可通过 `CENTRAL_SERVICE_TIMEOUT_MS` 同步控制 service/discovery 超时，适合故障端点挂起场景联调。

```powershell
cargo build --release
```

## 相关链接

- [`../README.md`](../README.md)
- [`../Cargo.toml`](../Cargo.toml)
- [`../e2e/src/main.rs`](../e2e/src/main.rs)
- [`../client/README.md`](../client/README.md)

