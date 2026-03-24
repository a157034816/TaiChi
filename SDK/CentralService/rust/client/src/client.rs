use crate::error::{Result, SdkError};
use crate::http::url_encode_component;
use crate::json::JsonValue;
use crate::models::{ApiResponse, ServiceInfo, ServiceListResponse, ServiceNetworkStatus};
use crate::options::DiscoveryClientOptions;
use crate::transport::{MultiEndpointTransport, TransportResponse};

/// 面向服务消费方的中心服务客户端。
///
/// 该类型负责列出服务、按策略发现实例并读取网络评估信息，不包含服务自注册相关能力。
#[derive(Debug, Clone)]
pub struct DiscoveryClient {
    transport: MultiEndpointTransport,
}

impl DiscoveryClient {
    /// 使用单个中心服务基地址创建客户端。
    pub fn new(base_url: &str) -> Result<Self> {
        Self::from_options(DiscoveryClientOptions::new(base_url)?)
    }

    /// 使用完整配置创建客户端。
    pub fn from_options(options: DiscoveryClientOptions) -> Result<Self> {
        Ok(Self {
            transport: MultiEndpointTransport::new(&options),
        })
    }

    /// 查询已注册服务列表。
    pub fn list(&self, name: Option<&str>) -> Result<ServiceListResponse> {
        let path = match name {
            Some(n) if !n.is_empty() => format!("/api/Service/list?name={}", url_encode_component(n)),
            _ => "/api/Service/list".to_string(),
        };
        let response = self.transport.send("GET", &path, None, None)?;
        parse_api_response(&response, ServiceListResponse::from_json)
            .and_then(|api| ok_or_api_error(api, "list", &response))
    }

    /// 使用轮询策略发现一个服务实例。
    pub fn discover_roundrobin(&self, service_name: &str) -> Result<ServiceInfo> {
        let path = format!(
            "/api/ServiceDiscovery/discover/roundrobin/{}",
            url_encode_component(service_name)
        );
        self.get_model(&path, ServiceInfo::from_json)
    }

    /// 使用权重策略发现一个服务实例。
    pub fn discover_weighted(&self, service_name: &str) -> Result<ServiceInfo> {
        let path = format!(
            "/api/ServiceDiscovery/discover/weighted/{}",
            url_encode_component(service_name)
        );
        self.get_model(&path, ServiceInfo::from_json)
    }

    /// 使用综合评分策略发现一个服务实例。
    pub fn discover_best(&self, service_name: &str) -> Result<ServiceInfo> {
        let path = format!(
            "/api/ServiceDiscovery/discover/best/{}",
            url_encode_component(service_name)
        );
        self.get_model(&path, ServiceInfo::from_json)
    }

    /// 获取所有已注册服务的网络状态。
    pub fn network_all(&self) -> Result<Vec<ServiceNetworkStatus>> {
        let response = self
            .transport
            .send("GET", "/api/ServiceDiscovery/network/all", None, None)?;
        parse_discovery_body(&response, |value| {
            let array = value.as_array().ok_or_else(|| {
                SdkError::Model("ServiceNetworkStatus[] must be array".to_string())
            })?;
            let mut items = Vec::new();
            for item in array {
                items.push(ServiceNetworkStatus::from_json(item)?);
            }
            Ok(items)
        })
    }

    /// 获取单个服务实例的当前网络状态。
    pub fn network_get(&self, service_id: &str) -> Result<ServiceNetworkStatus> {
        let path = format!(
            "/api/ServiceDiscovery/network/{}",
            url_encode_component(service_id)
        );
        self.get_model(&path, ServiceNetworkStatus::from_json)
    }

    /// 触发一次网络评估，并返回最新结果。
    pub fn network_evaluate(&self, service_id: &str) -> Result<ServiceNetworkStatus> {
        let path = format!(
            "/api/ServiceDiscovery/network/evaluate/{}",
            url_encode_component(service_id)
        );
        let response = self.transport.send("POST", &path, Some(&[]), None)?;
        parse_discovery_body(&response, ServiceNetworkStatus::from_json)
    }

    fn get_model<T, F>(&self, path: &str, parse: F) -> Result<T>
    where
        F: FnOnce(&JsonValue) -> Result<T>,
    {
        let response = self.transport.send("GET", path, None, None)?;
        parse_discovery_body(&response, parse)
    }
}

fn parse_discovery_body<T, F>(response: &TransportResponse, parse: F) -> Result<T>
where
    F: FnOnce(&JsonValue) -> Result<T>,
{
    if !(200..=299).contains(&response.status) {
        return Err(SdkError::HttpStatus {
            status: response.status,
            message: transport_context("Non-success status", response),
            body: Some(String::from_utf8_lossy(&response.body).to_string()),
        });
    }
    let text = String::from_utf8(response.body.clone())
        .map_err(|error| SdkError::JsonParse(error.to_string()))?;
    let json = JsonValue::parse(&text)?;
    parse(&json)
}

fn ok_or_api_error<T>(
    api: ApiResponse<T>,
    ctx: &str,
    response: &TransportResponse,
) -> Result<T> {
    if api.success {
        api.data.ok_or_else(|| SdkError::Model(format!("{ctx}: missing data")))
    } else {
        let message = api
            .error_message
            .unwrap_or_else(|| format!("{ctx}: unknown API error"));
        Err(SdkError::Api {
            code: api.error_code,
            message: format!("{message} ({})", transport_context("transport", response)),
        })
    }
}

fn parse_api_response<T, F>(response: &TransportResponse, parse_data: F) -> Result<ApiResponse<T>>
where
    F: FnOnce(&JsonValue) -> Result<T>,
{
    if !(200..=299).contains(&response.status) {
        return Err(SdkError::HttpStatus {
            status: response.status,
            message: transport_context("Non-success status", response),
            body: Some(String::from_utf8_lossy(&response.body).to_string()),
        });
    }
    let text = String::from_utf8(response.body.clone())
        .map_err(|error| SdkError::JsonParse(error.to_string()))?;
    let json = JsonValue::parse(&text)?;
    ApiResponse::from_json(&json, parse_data)
}

fn transport_context(message: &str, response: &TransportResponse) -> String {
    let mut segments = vec![
        format!("端点={}", response.base_url),
        format!("URL={}", response.url),
        format!("尝试={}/{}", response.attempt, response.max_attempts),
    ];

    if !response.skipped_endpoints.is_empty() {
        segments.push(format!(
            "已跳过={}",
            response.skipped_endpoints.join("、")
        ));
    }

    format!("{message} ({})", segments.join("; "))
}
