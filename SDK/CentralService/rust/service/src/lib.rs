//! `centralservice_service` 为服务提供方提供轻量级 Rust SDK。
//!
//! 该 crate 面向需要向中心服务注册、发送心跳以及主动注销的服务实例。
//! 如果调用方的职责是查询注册列表、执行服务发现或读取网络评估信息，
//! 应使用配套的 `centralservice_client` crate，而不是本 crate。
//!
//! 对外 API 由以下几类组成：
//! - [`ServiceClient`]：服务生命周期相关 HTTP 调用入口。
//! - [`ServiceRegistrationRequest`] 等模型：与注册协议对应的数据结构。
//! - [`SdkError`] 与 [`Result`]：统一错误模型。
//! - [`JsonValue`] 与 [`JsonNumber`]：用于原始 JSON 载荷的轻量表示。

mod error;
mod http;
mod json;
mod models;
mod options;
mod service_client;
mod transport;

/// 统一错误与返回结果类型。
pub use error::{Result, SdkError};
/// 多中心 service 配置。
pub use options::{
    CentralServiceCircuitBreakerOptions, CentralServiceEndpointOptions, ServiceClientOptions,
};
/// 轻量 JSON 值模型与数字表示。
pub use json::{JsonNumber, JsonValue};
/// 服务注册协议使用的共享模型。
pub use models::{
    ApiResponse, ServiceHeartbeatRequest, ServiceInfo, ServiceListResponse,
    ServiceNetworkStatus, ServiceRegistrationRequest, ServiceRegistrationResponse,
};
/// 服务端 SDK 的主要入口类型。
pub use service_client::ServiceClient;
