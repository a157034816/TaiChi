//! `centralservice_client` 为服务消费方提供轻量级 Rust SDK。
//!
//! 该 crate 面向需要查询注册列表、执行服务发现以及读取网络评估结果的调用方。
//! 如果调用方的职责是向中心服务注册自身、发送心跳或主动注销实例，
//! 应使用配套的 `centralservice_service` crate，而不是本 crate。
//!
//! 对外 API 由以下几类组成：
//! - [`DiscoveryClient`]：服务列表、服务发现与网络评估相关 HTTP 调用入口。
//! - [`ServiceInfo`]、[`ServiceListResponse`] 与 [`ServiceNetworkStatus`]：发现侧返回模型。
//! - [`SdkError`] 与 [`Result`]：统一错误模型。
//! - [`JsonValue`] 与 [`JsonNumber`]：用于原始 JSON 载荷的轻量表示。

mod client;
mod error;
mod http;
mod json;
mod models;
mod options;
mod transport;

/// 发现端 SDK 的主要入口类型。
pub use client::DiscoveryClient;
/// 统一错误与返回结果类型。
pub use error::{Result, SdkError};
/// 多中心 discovery 配置。
pub use options::{
    CentralServiceCircuitBreakerOptions, CentralServiceEndpointOptions, DiscoveryClientOptions,
};
/// 轻量 JSON 值模型与数字表示。
pub use json::{JsonNumber, JsonValue};
/// 发现与网络评估协议使用的共享模型。
pub use models::{
    ApiResponse, ServiceHeartbeatRequest, ServiceInfo, ServiceListResponse,
    ServiceNetworkStatus, ServiceRegistrationRequest, ServiceRegistrationResponse,
};
