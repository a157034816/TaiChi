use crate::error::{Result, SdkError};
use crate::json::{JsonNumber, JsonValue};
use std::collections::BTreeMap;

/// 中心服务统一的 API 响应包裹。
#[derive(Debug, Clone)]
pub struct ApiResponse<T> {
    /// 服务端是否将本次业务处理视为成功。
    pub success: bool,
    /// 业务错误码；成功时通常为空。
    pub error_code: Option<i64>,
    /// 业务错误消息；成功时通常为空。
    pub error_message: Option<String>,
    /// 业务返回数据；部分接口在成功时也可能为空。
    pub data: Option<T>,
}

impl<T> ApiResponse<T> {
    /// 从 JSON 对象解析统一响应，并使用 `parse_data` 处理 `data` 字段。
    pub fn from_json<F>(value: &JsonValue, parse_data: F) -> Result<ApiResponse<T>>
    where
        F: FnOnce(&JsonValue) -> Result<T>,
    {
        let obj = require_object(value, "ApiResponse")?;
        let success = get_bool(obj, "success")?;
        let error_code = get_i64_opt(obj, "errorCode")?;
        let error_message = get_string_opt(obj, "errorMessage")?;

        let data = match obj.get("data") {
            None | Some(JsonValue::Null) => None,
            Some(v) => Some(parse_data(v)?),
        };

        Ok(ApiResponse {
            success,
            error_code,
            error_message,
            data,
        })
    }
}

/// 服务注册请求。
#[derive(Debug, Clone)]
pub struct ServiceRegistrationRequest {
    /// 服务实例 ID；首次注册时可留空，由服务端分配。
    pub id: Option<String>,
    /// 逻辑服务名称。
    pub name: String,
    /// 服务对外暴露的主机名或 IP。
    pub host: String,
    /// 服务对外暴露的端口。
    pub port: u16,
    /// 服务分类或协议类型。
    pub service_type: String,
    /// 健康检查接口路径或 URL。
    pub health_check_url: String,
    /// 健康检查端口；使用 `i64` 以保持与后端合同一致。
    pub health_check_port: i64,
    /// 健康检查方式标识。
    pub health_check_type: String,
    /// 负载均衡权重。
    pub weight: i64,
    /// 附加元数据，仅支持字符串键值。
    pub metadata: BTreeMap<String, String>,
}

impl ServiceRegistrationRequest {
    /// 将注册请求序列化为服务端期望的 JSON 对象。
    pub fn to_json(&self) -> JsonValue {
        let mut obj = BTreeMap::new();
        if let Some(id) = &self.id {
            obj.insert("id".to_string(), JsonValue::String(id.clone()));
        }
        obj.insert("name".to_string(), JsonValue::String(self.name.clone()));
        obj.insert("host".to_string(), JsonValue::String(self.host.clone()));
        obj.insert("port".to_string(), JsonValue::Number(JsonNumber::I64(self.port as i64)));
        obj.insert(
            "serviceType".to_string(),
            JsonValue::String(self.service_type.clone()),
        );
        obj.insert(
            "healthCheckUrl".to_string(),
            JsonValue::String(self.health_check_url.clone()),
        );
        obj.insert(
            "healthCheckPort".to_string(),
            JsonValue::Number(JsonNumber::I64(self.health_check_port)),
        );
        obj.insert(
            "healthCheckType".to_string(),
            JsonValue::String(self.health_check_type.clone()),
        );
        obj.insert("weight".to_string(), JsonValue::Number(JsonNumber::I64(self.weight)));

        let mut meta = BTreeMap::new();
        for (k, v) in &self.metadata {
            meta.insert(k.clone(), JsonValue::String(v.clone()));
        }
        obj.insert("metadata".to_string(), JsonValue::Object(meta));
        JsonValue::Object(obj)
    }
}

/// 服务注册成功后的返回数据。
#[derive(Debug, Clone)]
pub struct ServiceRegistrationResponse {
    /// 服务端确认的服务实例 ID。
    pub id: String,
    /// 注册时间戳。
    pub register_timestamp: i64,
}

impl ServiceRegistrationResponse {
    /// 从 JSON 解析注册响应模型。
    pub fn from_json(value: &JsonValue) -> Result<ServiceRegistrationResponse> {
        let obj = require_object(value, "ServiceRegistrationResponse")?;
        Ok(ServiceRegistrationResponse {
            id: get_string(obj, "id")?,
            register_timestamp: get_i64(obj, "registerTimestamp")?,
        })
    }
}

/// 服务心跳请求。
#[derive(Debug, Clone)]
pub struct ServiceHeartbeatRequest {
    /// 需要续约的服务实例 ID。
    pub id: String,
}

impl ServiceHeartbeatRequest {
    /// 将心跳请求序列化为 JSON 对象。
    pub fn to_json(&self) -> JsonValue {
        let mut obj = BTreeMap::new();
        obj.insert("id".to_string(), JsonValue::String(self.id.clone()));
        JsonValue::Object(obj)
    }
}

/// 服务列表查询结果。
#[derive(Debug, Clone)]
pub struct ServiceListResponse {
    /// 返回的服务实例集合。
    pub services: Vec<ServiceInfo>,
}

impl ServiceListResponse {
    /// 从 JSON 解析服务列表结果。
    pub fn from_json(value: &JsonValue) -> Result<ServiceListResponse> {
        let obj = require_object(value, "ServiceListResponse")?;
        let services_v = obj.get("services").ok_or_else(|| {
            SdkError::Model("ServiceListResponse.services missing".to_string())
        })?;
        let arr = services_v.as_array().ok_or_else(|| {
            SdkError::Model("ServiceListResponse.services must be array".to_string())
        })?;
        let mut services = Vec::new();
        for item in arr {
            services.push(ServiceInfo::from_json(item)?);
        }
        Ok(ServiceListResponse { services })
    }
}

/// 已注册服务实例的完整描述。
#[derive(Debug, Clone)]
pub struct ServiceInfo {
    /// 服务实例 ID。
    pub id: String,
    /// 逻辑服务名称。
    pub name: String,
    /// 主机名或 IP。
    pub host: String,
    /// 对外服务端口。
    pub port: u16,
    /// 服务端可能返回的完整 URL。
    pub url: Option<String>,
    /// 服务分类或协议类型。
    pub service_type: String,
    /// 服务状态码。
    pub status: i64,
    /// 健康检查接口路径或 URL。
    pub health_check_url: String,
    /// 健康检查端口。
    pub health_check_port: i64,
    /// 健康检查方式标识。
    pub health_check_type: String,
    /// 注册时间文本。
    pub register_time: Option<String>,
    /// 最近一次心跳时间文本。
    pub last_heartbeat_time: Option<String>,
    /// 负载均衡权重。
    pub weight: i64,
    /// 附加元数据，仅支持字符串键值。
    pub metadata: BTreeMap<String, String>,
    /// 是否位于与当前调用方相同的局域网。
    pub is_local_network: bool,
}

impl ServiceInfo {
    /// 从 JSON 解析服务实例信息。
    pub fn from_json(value: &JsonValue) -> Result<ServiceInfo> {
        let obj = require_object(value, "ServiceInfo")?;
        Ok(ServiceInfo {
            id: get_string(obj, "id")?,
            name: get_string(obj, "name")?,
            host: get_string(obj, "host")?,
            port: get_u16(obj, "port")?,
            url: get_string_opt(obj, "url")?,
            service_type: get_string(obj, "serviceType")?,
            status: get_i64(obj, "status")?,
            health_check_url: get_string(obj, "healthCheckUrl")?,
            health_check_port: get_i64(obj, "healthCheckPort")?,
            health_check_type: get_string(obj, "healthCheckType")?,
            register_time: get_string_opt(obj, "registerTime")?,
            last_heartbeat_time: get_string_opt(obj, "lastHeartbeatTime")?,
            weight: get_i64(obj, "weight")?,
            metadata: get_string_map(obj.get("metadata"))?,
            is_local_network: get_bool(obj, "isLocalNetwork")?,
        })
    }
}

/// 服务网络质量评估结果。
#[derive(Debug, Clone)]
pub struct ServiceNetworkStatus {
    /// 服务实例 ID。
    pub service_id: String,
    /// 响应时间，单位通常为毫秒。
    pub response_time: i64,
    /// 丢包率，按百分比表示。
    pub packet_loss: f64,
    /// 最近一次检测时间文本。
    pub last_check_time: Option<String>,
    /// 连续成功次数。
    pub consecutive_successes: i64,
    /// 连续失败次数。
    pub consecutive_failures: i64,
    /// 当前是否可用。
    pub is_available: bool,
}

impl ServiceNetworkStatus {
    /// 从 JSON 解析网络状态模型。
    pub fn from_json(value: &JsonValue) -> Result<ServiceNetworkStatus> {
        let obj = require_object(value, "ServiceNetworkStatus")?;
        Ok(ServiceNetworkStatus {
            service_id: get_string(obj, "serviceId")?,
            response_time: get_i64(obj, "responseTime")?,
            packet_loss: get_f64(obj, "packetLoss")?,
            last_check_time: get_string_opt(obj, "lastCheckTime")?,
            consecutive_successes: get_i64(obj, "consecutiveSuccesses")?,
            consecutive_failures: get_i64(obj, "consecutiveFailures")?,
            is_available: get_bool(obj, "isAvailable")?,
        })
    }

    /// 按响应时间与丢包率计算 0 到 100 的综合评分。
    pub fn calculate_score(&self) -> f64 {
        if !self.is_available {
            return 0.0;
        }
        let rt = self.response_time as f64;
        let response_time_score = if rt <= 50.0 {
            50.0
        } else if rt >= 1000.0 {
            0.0
        } else {
            (1000.0 - rt) / (1000.0 - 50.0) * 50.0
        };

        let pl = self.packet_loss;
        let packet_loss_score = if pl <= 0.0 {
            50.0
        } else if pl >= 50.0 {
            0.0
        } else {
            (50.0 - pl) / 50.0 * 50.0
        };

        let total = response_time_score + packet_loss_score;
        if total < 0.0 {
            0.0
        } else if total > 100.0 {
            100.0
        } else {
            total
        }
    }
}

fn require_object<'a>(
    value: &'a JsonValue,
    ctx: &'static str,
) -> Result<&'a BTreeMap<String, JsonValue>> {
    value
        .as_object()
        .ok_or_else(|| SdkError::Model(format!("{ctx} must be object")))
}

fn get_string(obj: &BTreeMap<String, JsonValue>, key: &str) -> Result<String> {
    obj.get(key)
        .and_then(|v| v.as_str().map(|s| s.to_string()))
        .ok_or_else(|| SdkError::Model(format!("Missing or invalid string field '{key}'")))
}

fn get_string_opt(obj: &BTreeMap<String, JsonValue>, key: &str) -> Result<Option<String>> {
    match obj.get(key) {
        None | Some(JsonValue::Null) => Ok(None),
        Some(JsonValue::String(s)) => Ok(Some(s.clone())),
        Some(_) => Err(SdkError::Model(format!(
            "Invalid optional string field '{key}'"
        ))),
    }
}

fn get_bool(obj: &BTreeMap<String, JsonValue>, key: &str) -> Result<bool> {
    obj.get(key)
        .and_then(|v| v.as_bool())
        .ok_or_else(|| SdkError::Model(format!("Missing or invalid bool field '{key}'")))
}

fn get_i64(obj: &BTreeMap<String, JsonValue>, key: &str) -> Result<i64> {
    obj.get(key)
        .and_then(|v| v.as_i64())
        .ok_or_else(|| SdkError::Model(format!("Missing or invalid number field '{key}'")))
}

fn get_i64_opt(obj: &BTreeMap<String, JsonValue>, key: &str) -> Result<Option<i64>> {
    match obj.get(key) {
        None | Some(JsonValue::Null) => Ok(None),
        Some(v) => Ok(Some(
            v.as_i64()
                .ok_or_else(|| SdkError::Model(format!("Invalid number field '{key}'")))?,
        )),
    }
}

fn get_u16(obj: &BTreeMap<String, JsonValue>, key: &str) -> Result<u16> {
    let v = get_i64(obj, key)?;
    if v < 0 || v > u16::MAX as i64 {
        return Err(SdkError::Model(format!("Field '{key}' out of range")));
    }
    Ok(v as u16)
}

fn get_f64(obj: &BTreeMap<String, JsonValue>, key: &str) -> Result<f64> {
    obj.get(key)
        .and_then(|v| v.as_f64())
        .ok_or_else(|| SdkError::Model(format!("Missing or invalid number field '{key}'")))
}

fn get_string_map(value: Option<&JsonValue>) -> Result<BTreeMap<String, String>> {
    let mut out = BTreeMap::new();
    match value {
        None | Some(JsonValue::Null) => return Ok(out),
        Some(JsonValue::Object(obj)) => {
            // metadata 当前只接受字符串值，以保持与服务端现有合同一致。
            for (k, v) in obj {
                let s = v.as_str().ok_or_else(|| {
                    SdkError::Model("metadata values must be string".to_string())
                })?;
                out.insert(k.clone(), s.to_string());
            }
        }
        Some(_) => return Err(SdkError::Model("metadata must be object".to_string())),
    }
    Ok(out)
}
