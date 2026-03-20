use crate::error::{Result, SdkError};
use crate::json::JsonValue;
use std::time::Duration;

const DEFAULT_MAX_ATTEMPTS: usize = 2;

/// DiscoveryClient 的顶层配置。
#[derive(Debug, Clone)]
pub struct DiscoveryClientOptions {
    /// 已按优先级归一化的中心服务端点列表。
    pub endpoints: Vec<CentralServiceEndpointOptions>,
    /// 单次请求超时时间。
    pub timeout: Duration,
}

impl DiscoveryClientOptions {
    /// 使用单个中心服务地址创建配置。
    pub fn new(base_url: &str) -> Result<Self> {
        Self::from_endpoints(vec![CentralServiceEndpointOptions::new(base_url)])
    }

    /// 使用多个端点配置创建 DiscoveryClient 配置。
    pub fn from_endpoints(endpoints: Vec<CentralServiceEndpointOptions>) -> Result<Self> {
        let endpoints = normalize_endpoints(endpoints)?;
        Ok(Self {
            endpoints,
            timeout: Duration::from_secs(10),
        })
    }
}

/// 单个中心服务端点配置。
#[derive(Debug, Clone)]
pub struct CentralServiceEndpointOptions {
    /// 中心服务根地址。
    pub base_url: String,
    /// 优先级；数字越小越优先。
    pub priority: i32,
    /// 单端点最大尝试次数；为空时使用默认值 2。
    pub max_attempts: Option<usize>,
    /// 单端点熔断器配置。
    pub circuit_breaker: Option<CentralServiceCircuitBreakerOptions>,
    order: usize,
}

impl CentralServiceEndpointOptions {
    /// 使用基地址创建端点配置。
    pub fn new(base_url: &str) -> Self {
        Self {
            base_url: base_url.to_string(),
            priority: 0,
            max_attempts: None,
            circuit_breaker: None,
            order: 0,
        }
    }

    /// 从 JSON 对象解析端点配置。
    pub fn from_json(value: &JsonValue) -> Result<Self> {
        let obj = value
            .as_object()
            .ok_or_else(|| SdkError::Model("EndpointOptions must be object".to_string()))?;
        let base_url = obj
            .get("baseUrl")
            .and_then(|v| v.as_str())
            .ok_or_else(|| SdkError::Model("EndpointOptions.baseUrl is required".to_string()))?;

        let priority = obj
            .get("priority")
            .and_then(|v| v.as_i64())
            .unwrap_or(0) as i32;

        let max_attempts = match obj.get("maxAttempts") {
            None | Some(JsonValue::Null) => None,
            Some(v) => Some(
                v.as_i64()
                    .ok_or_else(|| {
                        SdkError::Model("EndpointOptions.maxAttempts must be number".to_string())
                    })?
                    .max(0) as usize,
            ),
        };

        let circuit_breaker = match obj.get("circuitBreaker") {
            None | Some(JsonValue::Null) => None,
            Some(v) => Some(CentralServiceCircuitBreakerOptions::from_json(v)?),
        };

        Ok(Self {
            base_url: base_url.to_string(),
            priority,
            max_attempts,
            circuit_breaker,
            order: 0,
        })
    }

    pub(crate) fn normalize(&self, order: usize) -> Result<Self> {
        let base_url = self.base_url.trim().trim_end_matches('/').to_string();
        if base_url.is_empty() {
            return Err(SdkError::InvalidUrl(
                "EndpointOptions.base_url is required".to_string(),
            ));
        }

        Ok(Self {
            base_url,
            priority: self.priority,
            max_attempts: Some(normalize_max_attempts(self.max_attempts)),
            circuit_breaker: self
                .circuit_breaker
                .as_ref()
                .map(CentralServiceCircuitBreakerOptions::normalize),
            order,
        })
    }

    pub(crate) fn normalized_max_attempts(&self) -> usize {
        normalize_max_attempts(self.max_attempts)
    }

}

/// 单个端点的熔断器配置。
#[derive(Debug, Clone)]
pub struct CentralServiceCircuitBreakerOptions {
    /// 连续失败阈值。
    pub failure_threshold: usize,
    /// 熔断时长（分钟）。
    pub break_duration_minutes: u64,
    /// 半开恢复所需的连续成功阈值。
    pub recovery_threshold: usize,
}

impl CentralServiceCircuitBreakerOptions {
    /// 从 JSON 对象解析熔断器配置。
    pub fn from_json(value: &JsonValue) -> Result<Self> {
        let obj = value
            .as_object()
            .ok_or_else(|| SdkError::Model("CircuitBreaker must be object".to_string()))?;
        Ok(Self {
            failure_threshold: obj
                .get("failureThreshold")
                .and_then(|v| v.as_i64())
                .unwrap_or(1)
                .max(1) as usize,
            break_duration_minutes: obj
                .get("breakDurationMinutes")
                .and_then(|v| v.as_i64())
                .unwrap_or(1)
                .max(1) as u64,
            recovery_threshold: obj
                .get("recoveryThreshold")
                .and_then(|v| v.as_i64())
                .unwrap_or(1)
                .max(1) as usize,
        })
    }

    pub(crate) fn normalize(&self) -> Self {
        Self {
            failure_threshold: self.failure_threshold.max(1),
            break_duration_minutes: self.break_duration_minutes.max(1),
            recovery_threshold: self.recovery_threshold.max(1),
        }
    }
}

pub(crate) fn normalize_endpoints(
    endpoints: Vec<CentralServiceEndpointOptions>,
) -> Result<Vec<CentralServiceEndpointOptions>> {
    let mut normalized = endpoints
        .into_iter()
        .enumerate()
        .map(|(index, endpoint)| endpoint.normalize(index))
        .collect::<Result<Vec<_>>>()?;

    if normalized.is_empty() {
        return Err(SdkError::InvalidUrl(
            "At least one central service endpoint is required".to_string(),
        ));
    }

    normalized.sort_by(|left, right| {
        left.priority
            .cmp(&right.priority)
            .then(left.order.cmp(&right.order))
    });
    Ok(normalized)
}

fn normalize_max_attempts(value: Option<usize>) -> usize {
    match value.unwrap_or(DEFAULT_MAX_ATTEMPTS) {
        0 => DEFAULT_MAX_ATTEMPTS,
        n => n,
    }
}

#[cfg(test)]
mod tests {
    use super::{
        normalize_endpoints, CentralServiceCircuitBreakerOptions, CentralServiceEndpointOptions,
    };

    #[test]
    fn normalize_endpoints_sorts_and_applies_defaults() {
        let endpoints = vec![
            CentralServiceEndpointOptions {
                base_url: "http://b.example/".to_string(),
                priority: 2,
                max_attempts: Some(0),
                circuit_breaker: None,
                order: 0,
            },
            CentralServiceEndpointOptions {
                base_url: "http://a.example/".to_string(),
                priority: 1,
                max_attempts: None,
                circuit_breaker: Some(CentralServiceCircuitBreakerOptions {
                    failure_threshold: 0,
                    break_duration_minutes: 0,
                    recovery_threshold: 0,
                }),
                order: 0,
            },
        ];

        let normalized = normalize_endpoints(endpoints).expect("normalize endpoints");
        assert_eq!("http://a.example", normalized[0].base_url);
        assert_eq!(2, normalized[0].normalized_max_attempts());
        let breaker = normalized[0]
            .circuit_breaker
            .as_ref()
            .expect("breaker should exist");
        assert_eq!(1, breaker.failure_threshold);
        assert_eq!(1, breaker.break_duration_minutes);
        assert_eq!(1, breaker.recovery_threshold);
        assert_eq!("http://b.example", normalized[1].base_url);
        assert_eq!(2, normalized[1].normalized_max_attempts());
    }
}
