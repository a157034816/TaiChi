use crate::error::{Result, SdkError};
use crate::http::url_encode_component;
use crate::json::JsonValue;
use crate::models::{
    ApiResponse, ServiceRegistrationRequest,
    ServiceRegistrationResponse,
};
use crate::options::ServiceClientOptions;
use crate::transport::{MultiEndpointTransport, TransportResponse};

/// 面向服务提供方的中心服务客户端。
///
/// 该类型负责调用注册与注销接口，不包含服务发现相关能力。
#[derive(Debug, Clone)]
pub struct ServiceClient {
    transport: MultiEndpointTransport,
}

impl ServiceClient {
    /// 使用单个中心服务基地址创建客户端。
    pub fn new(base_url: &str) -> Result<Self> {
        Self::from_options(ServiceClientOptions::new(base_url)?)
    }

    /// 使用完整配置创建客户端。
    pub fn from_options(options: ServiceClientOptions) -> Result<Self> {
        Ok(Self {
            transport: MultiEndpointTransport::new(&options),
        })
    }

    /// 注册一个服务实例，并返回服务端确认的注册结果。
    pub fn register(
        &self,
        request: &ServiceRegistrationRequest,
    ) -> Result<ServiceRegistrationResponse> {
        let body = request.to_json().to_string();
        let response = self.transport.send(
            "POST",
            "/api/Service/register",
            Some(body.as_bytes()),
            Some("application/json"),
        )?;
        parse_api_response(&response, ServiceRegistrationResponse::from_json)
            .and_then(|api| ok_or_api_error(api, "register", &response))
    }

    /// 按服务实例 ID 注销一个已注册的节点。
    pub fn deregister(&self, id: &str) -> Result<ApiResponse<JsonValue>> {
        let path = format!("/api/Service/deregister/{}", url_encode_component(id));
        let response = self.transport.send("DELETE", &path, None, None)?;
        let api = parse_api_response(&response, |value| Ok(value.clone()))?;
        ensure_api_success(api, "deregister", &response)
    }
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
            message: format!("{message} ({})", transport_context(response)),
        })
    }
}

fn ensure_api_success<T>(
    api: ApiResponse<T>,
    ctx: &str,
    response: &TransportResponse,
) -> Result<ApiResponse<T>> {
    if api.success {
        Ok(api)
    } else {
        let message = api
            .error_message
            .unwrap_or_else(|| format!("{ctx}: unknown API error"));
        Err(SdkError::Api {
            code: api.error_code,
            message: format!("{message} ({})", transport_context(response)),
        })
    }
}

fn parse_api_response<T, F>(
    response: &TransportResponse,
    parse_data: F,
) -> Result<ApiResponse<T>>
where
    F: FnOnce(&JsonValue) -> Result<T>,
{
    if !(200..=299).contains(&response.status) {
        return Err(SdkError::HttpStatus {
            status: response.status,
            message: format!("Non-success status ({})", transport_context(response)),
            body: Some(String::from_utf8_lossy(&response.body).to_string()),
        });
    }
    let text = String::from_utf8(response.body.clone())
        .map_err(|error| SdkError::JsonParse(error.to_string()))?;
    let json = JsonValue::parse(&text)?;
    ApiResponse::from_json(&json, parse_data)
}

fn transport_context(response: &TransportResponse) -> String {
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

    segments.join("; ")
}
