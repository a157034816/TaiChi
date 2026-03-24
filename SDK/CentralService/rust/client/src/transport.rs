use crate::error::{Result, SdkError};
use crate::http::HttpClient;
use crate::options::{CentralServiceEndpointOptions, DiscoveryClientOptions};
use std::sync::{Arc, Mutex};
use std::time::{Duration, Instant};

/// 多中心端点传输层的原始 HTTP 结果。
#[derive(Debug, Clone)]
pub struct TransportResponse {
    /// 实际命中的端点根地址。
    pub base_url: String,
    /// 实际请求 URL。
    pub url: String,
    /// 当前端点第几次尝试。
    pub attempt: usize,
    /// 当前端点允许的最大尝试次数。
    pub max_attempts: usize,
    /// HTTP 状态码。
    pub status: u16,
    /// 原始响应体。
    pub body: Vec<u8>,
    /// 本次请求前已被熔断跳过的端点摘要。
    pub skipped_endpoints: Vec<String>,
}

#[derive(Debug, Clone)]
pub struct MultiEndpointTransport {
    endpoints: Vec<TransportEndpoint>,
    timeout: Duration,
}

#[derive(Debug, Clone)]
struct TransportEndpoint {
    base_url: String,
    max_attempts: usize,
    circuit_breaker: Option<Arc<Mutex<CircuitBreakerState>>>,
}

#[derive(Debug)]
struct CircuitBreakerState {
    failure_threshold: usize,
    break_duration: Duration,
    recovery_threshold: usize,
    mode: CircuitBreakerMode,
    failure_count: usize,
    half_open_success_count: usize,
    open_until: Option<Instant>,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum CircuitBreakerMode {
    Closed,
    Open,
    HalfOpen,
}

impl MultiEndpointTransport {
    /// 基于顶层配置创建多端点传输层。
    pub fn new(options: &DiscoveryClientOptions) -> Self {
        let endpoints = options
            .endpoints
            .iter()
            .map(TransportEndpoint::from_options)
            .collect::<Vec<_>>();

        Self {
            endpoints,
            timeout: options.timeout,
        }
    }

    /// 发送一次带端点切换能力的请求。
    pub fn send(
        &self,
        method: &str,
        path_and_query: &str,
        body: Option<&[u8]>,
        content_type: Option<&str>,
    ) -> Result<TransportResponse> {
        let mut skipped_endpoints = Vec::new();
        let mut failures = Vec::new();
        let mut last_url = String::new();

        for endpoint in &self.endpoints {
            if let Some(reason) = endpoint.circuit_breaker_skip_reason() {
                skipped_endpoints.push(format!("{}（{}）", endpoint.base_url, reason));
                continue;
            }

            for attempt in 1..=endpoint.max_attempts {
                let client = HttpClient::new(&endpoint.base_url)?.with_timeout(self.timeout);
                let url = build_full_url(&endpoint.base_url, path_and_query);
                last_url = url.clone();

                match client.request(method, path_and_query, body, content_type) {
                    Ok(response) => {
                        endpoint.report_success();
                        return Ok(TransportResponse {
                            base_url: endpoint.base_url.clone(),
                            url,
                            attempt,
                            max_attempts: endpoint.max_attempts,
                            status: response.status,
                            body: response.body,
                            skipped_endpoints,
                        });
                    }
                    Err(SdkError::Io(error)) => {
                        endpoint.report_failure();
                        failures.push(format!(
                            "{} 第 {attempt}/{} 次失败：{}",
                            endpoint.base_url, endpoint.max_attempts, error
                        ));
                    }
                    Err(other) => return Err(other),
                }
            }
        }

        let mut segments = Vec::new();
        if !skipped_endpoints.is_empty() {
            segments.push(format!(
                "跳过端点: {}",
                skipped_endpoints.join("; ")
            ));
        }
        if !failures.is_empty() {
            segments.push(format!("失败详情: {}", failures.join("; ")));
        }
        if segments.is_empty() {
            segments.push("未找到可用的中心服务端点".to_string());
        }

        Err(SdkError::Transport {
            message: format!(
                "中心服务调用失败，所有可用端点均已耗尽。 {}",
                segments.join(" | ")
            ),
            detail: format!("method={method}; path={path_and_query}; lastUrl={last_url}"),
        })
    }
}

impl TransportEndpoint {
    fn from_options(options: &CentralServiceEndpointOptions) -> Self {
        Self {
            base_url: options.base_url.clone(),
            max_attempts: options.normalized_max_attempts(),
            circuit_breaker: options.circuit_breaker.as_ref().map(|breaker| {
                Arc::new(Mutex::new(CircuitBreakerState::new(
                    breaker.failure_threshold,
                    Duration::from_secs(breaker.break_duration_minutes * 60),
                    breaker.recovery_threshold,
                )))
            }),
        }
    }

    fn circuit_breaker_skip_reason(&self) -> Option<String> {
        let state = self.circuit_breaker.as_ref()?;
        let mut state = state.lock().expect("circuit breaker lock poisoned");
        state.try_allow_request()
    }

    fn report_success(&self) {
        if let Some(state) = &self.circuit_breaker {
            state
                .lock()
                .expect("circuit breaker lock poisoned")
                .report_success();
        }
    }

    fn report_failure(&self) {
        if let Some(state) = &self.circuit_breaker {
            state
                .lock()
                .expect("circuit breaker lock poisoned")
                .report_failure();
        }
    }
}

impl CircuitBreakerState {
    fn new(failure_threshold: usize, break_duration: Duration, recovery_threshold: usize) -> Self {
        Self {
            failure_threshold: failure_threshold.max(1),
            break_duration: if break_duration.is_zero() {
                Duration::from_secs(60)
            } else {
                break_duration
            },
            recovery_threshold: recovery_threshold.max(1),
            mode: CircuitBreakerMode::Closed,
            failure_count: 0,
            half_open_success_count: 0,
            open_until: None,
        }
    }

    fn try_allow_request(&mut self) -> Option<String> {
        if self.mode != CircuitBreakerMode::Open {
            return None;
        }

        let Some(open_until) = self.open_until else {
            self.mode = CircuitBreakerMode::HalfOpen;
            return None;
        };

        let now = Instant::now();
        if now >= open_until {
            self.mode = CircuitBreakerMode::HalfOpen;
            self.failure_count = 0;
            self.half_open_success_count = 0;
            self.open_until = None;
            return None;
        }

        let remaining = open_until.saturating_duration_since(now).as_secs().max(1);
        Some(format!("熔断开启，剩余约 {remaining} 秒"))
    }

    fn report_success(&mut self) {
        if self.mode == CircuitBreakerMode::HalfOpen {
            self.half_open_success_count += 1;
            if self.half_open_success_count >= self.recovery_threshold {
                self.reset_to_closed();
            }
            return;
        }

        self.failure_count = 0;
    }

    fn report_failure(&mut self) {
        if self.mode == CircuitBreakerMode::HalfOpen {
            self.open();
            return;
        }

        self.failure_count += 1;
        if self.failure_count >= self.failure_threshold {
            self.open();
        }
    }

    fn open(&mut self) {
        self.mode = CircuitBreakerMode::Open;
        self.failure_count = 0;
        self.half_open_success_count = 0;
        self.open_until = Some(Instant::now() + self.break_duration);
    }

    fn reset_to_closed(&mut self) {
        self.mode = CircuitBreakerMode::Closed;
        self.failure_count = 0;
        self.half_open_success_count = 0;
        self.open_until = None;
    }
}

fn build_full_url(base_url: &str, path_and_query: &str) -> String {
    let base = base_url.trim_end_matches('/');
    if path_and_query == "/" {
        return base.to_string();
    }
    format!("{base}{path_and_query}")
}

#[cfg(test)]
mod tests {
    use super::{CircuitBreakerMode, CircuitBreakerState, TransportEndpoint};
    use crate::options::{CentralServiceCircuitBreakerOptions, CentralServiceEndpointOptions};

    #[test]
    fn circuit_breaker_reopens_after_half_open_failure() {
        let mut state = CircuitBreakerState::new(1, std::time::Duration::from_millis(10), 1);
        state.report_failure();
        assert_eq!(Some("熔断开启，剩余约 1 秒".to_string()), state.try_allow_request());
        std::thread::sleep(std::time::Duration::from_millis(15));
        assert_eq!(None, state.try_allow_request());
        assert_eq!(CircuitBreakerMode::HalfOpen, state.mode);
        state.report_failure();
        assert_eq!(CircuitBreakerMode::Open, state.mode);
    }

    #[test]
    fn transport_endpoint_uses_default_attempts() {
        let mut endpoint = CentralServiceEndpointOptions::new("http://127.0.0.1:15700");
        endpoint.circuit_breaker = Some(CentralServiceCircuitBreakerOptions {
            failure_threshold: 1,
            break_duration_minutes: 1,
            recovery_threshold: 1,
        });
        let endpoint = endpoint.normalize(0).expect("normalize endpoint");
        let transport = TransportEndpoint::from_options(&endpoint);
        assert_eq!(2, transport.max_attempts);
    }
}
