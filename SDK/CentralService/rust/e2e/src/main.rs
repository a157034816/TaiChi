use centralservice_client::{
    CentralServiceCircuitBreakerOptions as ClientCircuitBreakerOptions,
    CentralServiceEndpointOptions as ClientEndpointOptions, DiscoveryClient,
    DiscoveryClientOptions, JsonValue,
};
use centralservice_service::{
    CentralServiceCircuitBreakerOptions as ServiceCircuitBreakerOptions,
    CentralServiceEndpointOptions as ServiceEndpointOptions, ServiceClient,
    ServiceClientOptions, ServiceHeartbeatRequest, ServiceRegistrationRequest,
};
use std::collections::BTreeMap;
use std::env;
use std::time::{Duration, SystemTime, UNIX_EPOCH};

#[derive(Debug, Clone, Copy)]
enum Scenario {
    Smoke,
    ServiceFanout,
    TransportFailover,
    BusinessNoFailover,
    MaxAttempts,
    CircuitOpen,
    CircuitRecovery,
    HalfOpenReopen,
}

impl Scenario {
    fn parse(value: &str) -> Result<Self, String> {
        match value.trim().to_ascii_lowercase().as_str() {
            "" | "smoke" => Ok(Self::Smoke),
            "service_fanout" => Ok(Self::ServiceFanout),
            "transport_failover" => Ok(Self::TransportFailover),
            "business_no_failover" => Ok(Self::BusinessNoFailover),
            "max_attempts" => Ok(Self::MaxAttempts),
            "circuit_open" => Ok(Self::CircuitOpen),
            "circuit_recovery" => Ok(Self::CircuitRecovery),
            "half_open_reopen" => Ok(Self::HalfOpenReopen),
            other => Err(format!("[rust-e2e] 未知场景: {other}")),
        }
    }

    fn as_str(&self) -> &'static str {
        match self {
            Self::Smoke => "smoke",
            Self::ServiceFanout => "service_fanout",
            Self::TransportFailover => "transport_failover",
            Self::BusinessNoFailover => "business_no_failover",
            Self::MaxAttempts => "max_attempts",
            Self::CircuitOpen => "circuit_open",
            Self::CircuitRecovery => "circuit_recovery",
            Self::HalfOpenReopen => "half_open_reopen",
        }
    }
}

#[derive(Debug, Clone)]
struct EndpointConfig {
    base_url: String,
    priority: i32,
    max_attempts: Option<usize>,
    circuit_breaker: Option<CircuitBreakerConfig>,
}

#[derive(Debug, Clone)]
struct CircuitBreakerConfig {
    failure_threshold: usize,
    break_duration_minutes: u64,
    recovery_threshold: usize,
}

fn main() {
    if let Err(error) = run() {
        eprintln!("{error}");
        std::process::exit(1);
    }
}

fn run() -> Result<(), String> {
    let endpoints = load_endpoints()?;
    let timeout = load_timeout();
    let scenario = Scenario::parse(
        &env::var("CENTRAL_SERVICE_E2E_SCENARIO").unwrap_or_else(|_| "smoke".to_string()),
    )?;

    println!(
        "[rust-e2e] scenario={} timeoutMs={} endpoints={}",
        scenario.as_str(),
        timeout.as_millis(),
        endpoints
            .iter()
            .map(|endpoint| endpoint.base_url.as_str())
            .collect::<Vec<_>>()
            .join(",")
    );

    match scenario {
        Scenario::Smoke => run_smoke(&endpoints, timeout),
        Scenario::ServiceFanout => run_service_fanout(&endpoints, timeout),
        Scenario::TransportFailover => run_transport_failover(&endpoints, timeout),
        Scenario::BusinessNoFailover => run_business_no_failover(&endpoints, timeout),
        Scenario::MaxAttempts => run_max_attempts(&endpoints, timeout),
        Scenario::CircuitOpen => run_circuit_open(&endpoints, timeout),
        Scenario::CircuitRecovery => run_circuit_recovery(&endpoints, timeout),
        Scenario::HalfOpenReopen => run_half_open_reopen(&endpoints, timeout),
    }
}

fn run_smoke(endpoints: &[EndpointConfig], timeout: Duration) -> Result<(), String> {
    let seed_endpoint = select_seed_endpoint(endpoints)?;
    let mut request = create_registration_request("rust-smoke");
    let service =
        ServiceClient::from_options(to_service_options(&[seed_endpoint.clone()], timeout)?)
            .map_err(|error| format!("[rust-e2e] smoke 构造 service client 失败: {error}"))?;
    let discovery =
        DiscoveryClient::from_options(to_discovery_options(&[seed_endpoint.clone()], timeout)?)
            .map_err(|error| format!("[rust-e2e] smoke 构造 discovery client 失败: {error}"))?;

    let response = service
        .register(&request)
        .map_err(|error| format!("[rust-e2e] smoke register 失败: {error}"))?;
    request.id = Some(response.id.clone());
    println!(
        "[rust-e2e] smoke registered id={} ts={}",
        response.id, response.register_timestamp
    );

    let result = (|| -> Result<(), String> {
        service
            .heartbeat(&ServiceHeartbeatRequest {
                id: response.id.clone(),
            })
            .map_err(|error| format!("[rust-e2e] smoke heartbeat 失败: {error}"))?;

        let list = discovery
            .list(Some(&request.name))
            .map_err(|error| format!("[rust-e2e] smoke list 失败: {error}"))?;
        ensure(!list.services.is_empty(), "smoke list 未返回任何服务实例")?;

        let service_info = discovery
            .discover_best(&request.name)
            .map_err(|error| format!("[rust-e2e] smoke discover_best 失败: {error}"))?;
        ensure(
            service_info.id == response.id,
            "smoke discover_best 返回了错误的服务实例",
        )?;

        let network_all = discovery
            .network_all()
            .map_err(|error| format!("[rust-e2e] smoke network_all 失败: {error}"))?;
        ensure(
            !network_all.is_empty(),
            "smoke network_all 未返回任何网络状态",
        )?;

        let network_get = discovery
            .network_get(&response.id)
            .map_err(|error| format!("[rust-e2e] smoke network_get 失败: {error}"))?;
        println!(
            "[rust-e2e] smoke network_get available={} score={:.2}",
            network_get.is_available,
            network_get.calculate_score()
        );

        discovery
            .network_evaluate(&response.id)
            .map_err(|error| format!("[rust-e2e] smoke network_evaluate 失败: {error}"))?;
        Ok(())
    })();

    if let Err(error) = service.deregister(&response.id) {
        eprintln!("[rust-e2e] smoke deregister 警告: {error}");
    }
    result
}

fn run_service_fanout(endpoints: &[EndpointConfig], timeout: Duration) -> Result<(), String> {
    ensure(endpoints.len() >= 2, "service_fanout 至少需要 2 个端点")?;
    let shared_id = generate_service_id("rust-fanout");
    let mut request = create_registration_request("rust-fanout");
    request.id = Some(shared_id.clone());

    for endpoint in endpoints {
        let service =
            ServiceClient::from_options(to_service_options(&[endpoint.clone()], timeout)?)
                .map_err(|error| {
                    format!(
                        "[rust-e2e] service_fanout 构造 service client 失败: endpoint={} {error}",
                        endpoint.base_url
                    )
                })?;
        let discovery =
            DiscoveryClient::from_options(to_discovery_options(&[endpoint.clone()], timeout)?)
                .map_err(|error| {
                    format!(
                        "[rust-e2e] service_fanout 构造 discovery client 失败: endpoint={} {error}",
                        endpoint.base_url
                    )
                })?;

        let response = service.register(&request).map_err(|error| {
            format!(
                "[rust-e2e] service_fanout register 失败: endpoint={} {error}",
                endpoint.base_url
            )
        })?;
        ensure(
            response.id == shared_id,
            "service_fanout register 未复用同一 serviceId",
        )?;

        service
            .heartbeat(&ServiceHeartbeatRequest {
                id: shared_id.clone(),
            })
            .map_err(|error| {
                format!(
                    "[rust-e2e] service_fanout heartbeat 失败: endpoint={} {error}",
                    endpoint.base_url
                )
            })?;

        let list = discovery.list(Some(&request.name)).map_err(|error| {
            format!(
                "[rust-e2e] service_fanout list 失败: endpoint={} {error}",
                endpoint.base_url
            )
        })?;
        ensure(
            list.services.iter().any(|item| item.id == shared_id),
            "service_fanout list 未在目标中心找到共享 serviceId",
        )?;
    }

    for endpoint in endpoints.iter().rev() {
        let service =
            ServiceClient::from_options(to_service_options(&[endpoint.clone()], timeout)?)
                .map_err(|error| {
                    format!(
                        "[rust-e2e] service_fanout 构造 service client 失败: endpoint={} {error}",
                        endpoint.base_url
                    )
                })?;
        service.deregister(&shared_id).map_err(|error| {
            format!(
                "[rust-e2e] service_fanout deregister 失败: endpoint={} {error}",
                endpoint.base_url
            )
        })?;
    }

    Ok(())
}

fn run_transport_failover(endpoints: &[EndpointConfig], timeout: Duration) -> Result<(), String> {
    let seed = seed_service(endpoints, timeout, "rust-transport-failover")?;
    let discovery = DiscoveryClient::from_options(to_discovery_options(endpoints, timeout)?)
        .map_err(|error| {
            format!("[rust-e2e] transport_failover 构造 discovery client 失败: {error}")
        })?;
    let result = discovery
        .discover_best(&seed.name)
        .map_err(|error| format!("[rust-e2e] transport_failover discover_best 失败: {error}"))?;
    ensure(result.id == seed.id.clone().unwrap_or_default(), "transport_failover 返回了错误的服务实例")
}

fn run_business_no_failover(
    endpoints: &[EndpointConfig],
    timeout: Duration,
) -> Result<(), String> {
    let discovery = DiscoveryClient::from_options(to_discovery_options(endpoints, timeout)?)
        .map_err(|error| {
            format!("[rust-e2e] business_no_failover 构造 discovery client 失败: {error}")
        })?;
    match discovery.discover_best("rust-business-no-failover") {
        Ok(service) => Err(format!(
            "[rust-e2e] business_no_failover 预期失败但返回了服务: {}",
            service.id
        )),
        Err(error) => {
            println!("[rust-e2e] business_no_failover observed error={error}");
            Ok(())
        }
    }
}

fn run_max_attempts(endpoints: &[EndpointConfig], timeout: Duration) -> Result<(), String> {
    let seed = seed_service(endpoints, timeout, "rust-max-attempts")?;
    let discovery = DiscoveryClient::from_options(to_discovery_options(endpoints, timeout)?)
        .map_err(|error| format!("[rust-e2e] max_attempts 构造 discovery client 失败: {error}"))?;
    let result = discovery
        .discover_best(&seed.name)
        .map_err(|error| format!("[rust-e2e] max_attempts discover_best 失败: {error}"))?;
    ensure(result.id == seed.id.clone().unwrap_or_default(), "max_attempts 返回了错误的服务实例")
}

fn run_circuit_open(endpoints: &[EndpointConfig], timeout: Duration) -> Result<(), String> {
    let seed = seed_service(endpoints, timeout, "rust-circuit-open")?;
    let discovery = DiscoveryClient::from_options(to_discovery_options(endpoints, timeout)?)
        .map_err(|error| format!("[rust-e2e] circuit_open 构造 discovery client 失败: {error}"))?;
    for attempt in 1..=3 {
        let result = discovery.discover_best(&seed.name).map_err(|error| {
            format!("[rust-e2e] circuit_open 第 {attempt} 次调用失败: {error}")
        })?;
        ensure(result.id == seed.id.clone().unwrap_or_default(), "circuit_open 返回了错误的服务实例")?;
    }
    Ok(())
}

fn run_circuit_recovery(
    endpoints: &[EndpointConfig],
    timeout: Duration,
) -> Result<(), String> {
    let seed = seed_service(endpoints, timeout, "rust-circuit-recovery")?;
    let discovery = DiscoveryClient::from_options(to_discovery_options(endpoints, timeout)?)
        .map_err(|error| {
            format!("[rust-e2e] circuit_recovery 构造 discovery client 失败: {error}")
        })?;
    discovery
        .discover_best(&seed.name)
        .map_err(|error| format!("[rust-e2e] circuit_recovery 预热失败: {error}"))?;

    let wait_seconds = wait_for_half_open_seconds(endpoints);
    println!("[rust-e2e] circuit_recovery waiting {} seconds", wait_seconds);
    std::thread::sleep(Duration::from_secs(wait_seconds));

    for attempt in 1..=2 {
        let result = discovery.discover_best(&seed.name).map_err(|error| {
            format!("[rust-e2e] circuit_recovery 第 {attempt} 次恢复调用失败: {error}")
        })?;
        ensure(result.id == seed.id.clone().unwrap_or_default(), "circuit_recovery 返回了错误的服务实例")?;
    }
    Ok(())
}

fn run_half_open_reopen(
    endpoints: &[EndpointConfig],
    timeout: Duration,
) -> Result<(), String> {
    let seed = seed_service(endpoints, timeout, "rust-half-open-reopen")?;
    let discovery = DiscoveryClient::from_options(to_discovery_options(endpoints, timeout)?)
        .map_err(|error| {
            format!("[rust-e2e] half_open_reopen 构造 discovery client 失败: {error}")
        })?;
    discovery
        .discover_best(&seed.name)
        .map_err(|error| format!("[rust-e2e] half_open_reopen 预热失败: {error}"))?;

    let wait_seconds = wait_for_half_open_seconds(endpoints);
    println!("[rust-e2e] half_open_reopen waiting {} seconds", wait_seconds);
    std::thread::sleep(Duration::from_secs(wait_seconds));

    discovery
        .discover_best(&seed.name)
        .map_err(|error| format!("[rust-e2e] half_open_reopen 半开探测失败: {error}"))?;
    discovery
        .discover_best(&seed.name)
        .map_err(|error| format!("[rust-e2e] half_open_reopen 重新熔断后的备用调用失败: {error}"))?;
    Ok(())
}

fn seed_service(
    endpoints: &[EndpointConfig],
    timeout: Duration,
    service_name: &str,
) -> Result<ServiceRegistrationRequest, String> {
    let endpoint = select_seed_endpoint(endpoints)?;
    let mut request = create_registration_request(service_name);
    let service = ServiceClient::from_options(to_service_options(&[endpoint.clone()], timeout)?)
        .map_err(|error| {
            format!(
                "[rust-e2e] 构造种子 service client 失败: endpoint={} {error}",
                endpoint.base_url
            )
        })?;
    let response = service
        .register(&request)
        .map_err(|error| format!("[rust-e2e] 注册种子服务失败: endpoint={} {error}", endpoint.base_url))?;
    request.id = Some(response.id.clone());
    println!(
        "[rust-e2e] seeded service endpoint={} id={}",
        endpoint.base_url, response.id
    );
    Ok(request)
}

fn load_endpoints() -> Result<Vec<EndpointConfig>, String> {
    if let Ok(json) = env::var("CENTRAL_SERVICE_ENDPOINTS_JSON") {
        let json = JsonValue::parse(&json)
            .map_err(|error| format!("[rust-e2e] 解析 CENTRAL_SERVICE_ENDPOINTS_JSON 失败: {error}"))?;
        let array = json
            .as_array()
            .ok_or_else(|| "[rust-e2e] CENTRAL_SERVICE_ENDPOINTS_JSON 必须是数组".to_string())?;

        let mut endpoints = Vec::new();
        for item in array {
            let endpoint = ClientEndpointOptions::from_json(item)
                .map_err(|error| format!("[rust-e2e] 解析 endpoint 配置失败: {error}"))?;
            endpoints.push(EndpointConfig {
                base_url: endpoint.base_url,
                priority: endpoint.priority,
                max_attempts: endpoint.max_attempts,
                circuit_breaker: endpoint.circuit_breaker.map(|breaker| CircuitBreakerConfig {
                    failure_threshold: breaker.failure_threshold,
                    break_duration_minutes: breaker.break_duration_minutes,
                    recovery_threshold: breaker.recovery_threshold,
                }),
            });
        }
        ensure(!endpoints.is_empty(), "CENTRAL_SERVICE_ENDPOINTS_JSON 不能为空数组")?;
        return Ok(endpoints);
    }

    let base_url = env::var("CENTRAL_SERVICE_BASEURL")
        .unwrap_or_else(|_| "http://127.0.0.1:5000".to_string());
    Ok(vec![EndpointConfig {
        base_url: base_url.trim().trim_end_matches('/').to_string(),
        priority: 0,
        max_attempts: None,
        circuit_breaker: None,
    }])
}

fn load_timeout() -> Duration {
    let millis = parse_env_u64(&[
        "CENTRAL_SERVICE_TIMEOUT_MS",
        "CENTRAL_SERVICE_E2E_TIMEOUT_MS",
    ])
    .unwrap_or(10_000)
    .max(1);
    Duration::from_millis(millis)
}

fn load_service_port() -> u16 {
    parse_env_u64(&["CENTRAL_SERVICE_E2E_SERVICE_PORT"])
        .map(|value| value.clamp(1, u16::MAX as u64) as u16)
        .unwrap_or(18085)
}

fn to_discovery_options(
    endpoints: &[EndpointConfig],
    timeout: Duration,
) -> Result<DiscoveryClientOptions, String> {
    let mut options = DiscoveryClientOptions::from_endpoints(
        endpoints
            .iter()
            .map(|endpoint| {
                let mut options = ClientEndpointOptions::new(&endpoint.base_url);
                options.priority = endpoint.priority;
                options.max_attempts = endpoint.max_attempts;
                options.circuit_breaker = endpoint
                    .circuit_breaker
                    .as_ref()
                    .map(|breaker| ClientCircuitBreakerOptions {
                        failure_threshold: breaker.failure_threshold,
                        break_duration_minutes: breaker.break_duration_minutes,
                        recovery_threshold: breaker.recovery_threshold,
                    });
                options
            })
            .collect(),
    )
    .map_err(|error| format!("[rust-e2e] 构造 discovery options 失败: {error}"))?;
    options.timeout = timeout;
    Ok(options)
}

fn to_service_options(
    endpoints: &[EndpointConfig],
    timeout: Duration,
) -> Result<ServiceClientOptions, String> {
    let mut options = ServiceClientOptions::from_endpoints(
        endpoints
            .iter()
            .map(|endpoint| {
                let mut options = ServiceEndpointOptions::new(&endpoint.base_url);
                options.priority = endpoint.priority;
                options.max_attempts = endpoint.max_attempts;
                options.circuit_breaker = endpoint
                    .circuit_breaker
                    .as_ref()
                    .map(|breaker| ServiceCircuitBreakerOptions {
                        failure_threshold: breaker.failure_threshold,
                        break_duration_minutes: breaker.break_duration_minutes,
                        recovery_threshold: breaker.recovery_threshold,
                    });
                options
            })
            .collect(),
    )
    .map_err(|error| format!("[rust-e2e] 构造 service options 失败: {error}"))?;
    options.timeout = timeout;
    Ok(options)
}

fn create_registration_request(service_name: &str) -> ServiceRegistrationRequest {
    let mut metadata = BTreeMap::new();
    metadata.insert("sdk".to_string(), "rust".to_string());
    metadata.insert("scenario".to_string(), service_name.to_string());

    ServiceRegistrationRequest {
        id: None,
        name: service_name.to_string(),
        host: "127.0.0.1".to_string(),
        local_ip: "127.0.0.1".to_string(),
        operator_ip: "127.0.0.1".to_string(),
        public_ip: "127.0.0.1".to_string(),
        port: load_service_port(),
        service_type: "Web".to_string(),
        health_check_url: "/health".to_string(),
        health_check_port: 0,
        health_check_type: "Http".to_string(),
        weight: 1,
        metadata,
    }
}

fn generate_service_id(prefix: &str) -> String {
    let millis = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap_or_default()
        .as_millis();
    format!("{prefix}-{millis}")
}

fn select_seed_endpoint(endpoints: &[EndpointConfig]) -> Result<EndpointConfig, String> {
    endpoints
        .iter()
        .max_by(|left, right| {
            left.priority
                .cmp(&right.priority)
                .then(left.base_url.cmp(&right.base_url))
        })
        .cloned()
        .ok_or_else(|| "[rust-e2e] 至少需要一个端点".to_string())
}

fn longest_break_duration_minutes(endpoints: &[EndpointConfig]) -> u64 {
    endpoints
        .iter()
        .filter_map(|endpoint| endpoint.circuit_breaker.as_ref())
        .map(|breaker| breaker.break_duration_minutes.max(1))
        .max()
        .unwrap_or(1)
}

fn wait_for_half_open_seconds(endpoints: &[EndpointConfig]) -> u64 {
    parse_env_u64(&[
        "CENTRAL_SERVICE_BREAK_WAIT_SECONDS",
        "CENTRAL_SERVICE_E2E_BREAK_WAIT_SECONDS",
    ])
    .filter(|value| *value > 0)
    .unwrap_or_else(|| longest_break_duration_minutes(endpoints) * 60 + 2)
}

fn parse_env_u64(names: &[&str]) -> Option<u64> {
    names
        .iter()
        .find_map(|name| {
            env::var(name)
                .ok()
                .map(|value| value.trim().to_string())
                .filter(|value| !value.is_empty())
                .and_then(|value| value.parse::<u64>().ok())
        })
}

fn ensure(condition: bool, message: &str) -> Result<(), String> {
    if condition {
        Ok(())
    } else {
        Err(format!("[rust-e2e] {message}"))
    }
}
