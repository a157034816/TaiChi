# CentralService Rust Client

## 定位

- 面向服务消费方的 Rust SDK，用于读取服务列表、执行服务发现、查询与刷新网络评估结果。
- 服务注册、心跳与注销不在当前 crate 中，请使用 `../service/README.md`。

## 包名

- crate：`centralservice_client`
- 主要入口类型：`DiscoveryClient`
- 主要模型：`ServiceListResponse`、`ServiceInfo`、`ServiceNetworkStatus`

## 环境要求

- 稳定版 Rust + Cargo，支持 `edition = "2021"`
- 可访问 CentralService HTTP API，例如 `http://127.0.0.1:5000`
- 当前 crate 位于 workspace `../Cargo.toml`

## 安装/引用

- 推荐在消费方 `Cargo.toml` 使用 path dependency 引用当前 crate。

```toml
[dependencies]
centralservice_client = { path = "../client" }
```

```rust
use centralservice_client::DiscoveryClient;
```

## 快速示例

```rust
use centralservice_client::DiscoveryClient;

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let discovery = DiscoveryClient::new("http://127.0.0.1:5000")?;

    let listed = discovery.list(Some("demo-service"))?;
    let rr = discovery.discover_roundrobin("demo-service")?;
    let weighted = discovery.discover_weighted("demo-service")?;
    let best = discovery.discover_best("demo-service")?;
    let current = discovery.network_get(&rr.id)?;
    let refreshed = discovery.network_evaluate(&rr.id)?;
    let all = discovery.network_all()?;

    println!(
        "list={}, rr={}, weighted={}, best={}, current={:.2}, refreshed={:.2}, all={}",
        listed.services.len(),
        rr.id,
        weighted.id,
        best.id,
        current.calculate_score(),
        refreshed.calculate_score(),
        all.len()
    );
    Ok(())
}
```

## 多中心端点配置

`DiscoveryClient::new(base_url)` 提供单中心快捷入口；如需多中心、优先级、单端点最大尝试次数和熔断配置，请使用 `DiscoveryClientOptions`：

```rust
use centralservice_client::{
    CentralServiceCircuitBreakerOptions, CentralServiceEndpointOptions, DiscoveryClient,
    DiscoveryClientOptions,
};
use std::time::Duration;

fn build_client() -> Result<DiscoveryClient, centralservice_client::SdkError> {
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

    let mut options = DiscoveryClientOptions::from_endpoints(vec![primary, backup])?;
    options.timeout = Duration::from_millis(800);
    DiscoveryClient::from_options(options)
}
```

行为约定：

- discovery 请求按 `priority` 升序访问端点。
- 仅在传输层异常时才会按单端点 `max_attempts` 和熔断状态切换备用端点。
- HTTP / 业务失败会直接返回，不会切到下一个中心。

## 构建

当前 crate 的最小构建验证：

```powershell
cargo build --release
```

如需与仓库统一口径保持一致，建议在仓库根目录执行：

```powershell
python -X utf8 "TaiChi/SDK/CentralService/scripts/sdk.py" -Build -Languages rust -SdkKinds client
```

## 打包

```powershell
cargo package --allow-dirty --no-verify
```

如需生成统一产物目录：

```powershell
python -X utf8 "TaiChi/SDK/CentralService/scripts/sdk.py" -Pack -Languages rust -SdkKinds client
```

## 验证

- 当前 crate 的快速验证以 `cargo build --release` 为准。
- 完整联调可参考 workspace 内的 `../e2e/src/main.rs`。
- e2e 示例支持 `CENTRAL_SERVICE_ENDPOINTS_JSON`、`CENTRAL_SERVICE_BASEURL` fallback、`CENTRAL_SERVICE_TIMEOUT_MS`。

```powershell
cargo build --release
```

## 相关链接

- [`../README.md`](../README.md)
- [`../Cargo.toml`](../Cargo.toml)
- [`../e2e/src/main.rs`](../e2e/src/main.rs)
- [`../service/README.md`](../service/README.md)

