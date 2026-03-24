use std::collections::{HashMap, HashSet};
use std::env;
use std::sync::Arc;

use axum::extract::{Path, Query, State};
use axum::http::StatusCode;
use axum::routing::{get, post, put};
use axum::{Json, Router};
use nodegraph_sdk::{
    CreateSessionRequest, CreateSessionResponse, NodeAppearance, NodeDefinition, NodeExecutionContext,
    NodeGraphClient, NodeGraphDocument, NodeGraphEdge, NodeGraphExecutionOptions,
    NodeGraphExecutionSnapshot, NodeGraphNode, NodeGraphRuntime, NodeGraphRuntimeDebugSession,
    NodeGraphRuntimeOptions, NodeGraphViewport, NodeLibraryFieldDefinition, NodePortDefinition,
    Position, RuntimeRegistrationResponse, TypeMappingEntry,
};
use serde::Deserialize;
use serde_json::{json, Map, Value};
use tokio::net::TcpListener;
use tokio::sync::Mutex;

/// Demo Showcase 的节点库版本号（与其它语言 Demo 保持一致）。
const DEMO_LIBRARY_VERSION: &str = "demo-showcase@1";
/// Demo Showcase 的默认图名称（existing 模式）。
const DEFAULT_EXISTING_GRAPH_NAME: &str = "Demo Showcase Pipeline";
/// Demo Showcase 的默认图名称（new 模式）。
const DEFAULT_NEW_GRAPH_NAME: &str = "Blank Demo Showcase Graph";
/// 文本 canonical 类型。
const HELLO_TEXT_TYPE: &str = "hello/text";
/// 数字 canonical 类型。
const DEMO_NUMBER_TYPE: &str = "demo/number";
/// 布尔 canonical 类型。
const DEMO_BOOLEAN_TYPE: &str = "demo/boolean";
/// 日期 canonical 类型。
const DEMO_DATE_TYPE: &str = "demo/date";
/// 颜色 canonical 类型。
const DEMO_COLOR_TYPE: &str = "demo/color";
/// 小数 canonical 类型。
const DEMO_DECIMAL_TYPE: &str = "demo/decimal";
/// 文本插值节点的默认模板（与其它语言 Demo 保持一致）。
const DEFAULT_TEMPLATE: &str =
    "Greeting: {greeting}\nLucky: {lucky}\nDate: {today}\nTheme: {theme}\nAmount: {amount}";
/// 用于“只发射一次”的节点状态键（避免因多输入触发导致重复 emit）。
const EMITTED_STATE_KEY: &str = "__emitted";
/// Demo 调试样例默认会命中的节点级断点。
const DEFAULT_DEBUG_BREAKPOINT: &str = "node_output";

/// Demo 宿主共享状态。
type SharedHost = Arc<Mutex<DemoHost>>;

/// 创建端口定义。
fn create_port(id: &str, label: &str, data_type: &str) -> NodePortDefinition {
    NodePortDefinition {
        id: id.to_string(),
        label: label.to_string(),
        data_type: Some(data_type.to_string()),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    /// 创建仅用于测试的 Demo 宿主配置，避免依赖外部环境变量。
    fn create_test_config() -> DemoConfig {
        DemoConfig {
            nodegraph_base_url: "http://127.0.0.1:3000".to_string(),
            demo_client_base_url: "http://127.0.0.1:3300".to_string(),
            listen_addr: "127.0.0.1:3300".to_string(),
            demo_domain: "demo-hello-world-rust-test".to_string(),
            client_name: "NodeGraph Demo Client (Rust Test)".to_string(),
        }
    }

    #[tokio::test]
    async fn runtime_debug_sessions_support_dynamic_breakpoint_updates() {
        let mut host = DemoHost::new(create_test_config());
        let graph = create_graph(Some(DEFAULT_EXISTING_GRAPH_NAME), Some("existing"));

        let created = host.create_debug_session(graph, Some(Vec::new()));
        assert_eq!(created["snapshot"]["status"], "idle");
        assert_eq!(created["breakpoints"], json!([]));

        let debug_session_id = created["debugSessionId"]
            .as_str()
            .expect("debugSessionId should exist")
            .to_string();

        let updated = host
            .set_debug_session_breakpoints(&debug_session_id, Some(vec!["node_output".to_string()]))
            .expect("debug session should exist when updating breakpoints");
        assert_eq!(updated["breakpoints"], json!(["node_output"]));

        let paused = host
            .continue_debug_session(&debug_session_id)
            .expect("debug session should exist when continuing");
        assert_eq!(paused["snapshot"]["status"], "paused");
        assert_eq!(paused["snapshot"]["pauseReason"], "breakpoint");
        assert_eq!(paused["snapshot"]["pendingNodeId"], "node_output");

        let cleared = host
            .set_debug_session_breakpoints(&debug_session_id, Some(Vec::new()))
            .expect("debug session should exist when clearing breakpoints");
        assert_eq!(cleared["breakpoints"], json!([]));

        let completed = host
            .continue_debug_session(&debug_session_id)
            .expect("debug session should exist when finishing");
        assert_eq!(completed["snapshot"]["status"], "completed");
        assert_eq!(
            completed["snapshot"]["results"]["console"],
            json!(["Greeting: Hello, Codex!\nLucky: 12\nDate: 2026-03-21\nTheme: #2563eb\nAmount: 123.45"])
        );

        assert!(host.close_debug_session(&debug_session_id));
    }
}

/// 创建节点外观配置。
fn create_appearance(bg_color: &str, border_color: &str, text_color: &str) -> NodeAppearance {
    NodeAppearance {
        bg_color: Some(bg_color.to_string()),
        border_color: Some(border_color.to_string()),
        text_color: Some(text_color.to_string()),
    }
}

/// 判断输入是否为空白字符串。
fn is_blank_string(value: Option<&Value>) -> bool {
    match value.and_then(Value::as_str) {
        Some(text) => text.trim().is_empty(),
        None => true,
    }
}

/// 将值尽量转换成非空字符串。
fn coerce_string(value: Option<&Value>, fallback: &str) -> String {
    match value.and_then(Value::as_str).map(str::trim).filter(|text| !text.is_empty()) {
        Some(text) => text.to_string(),
        None => fallback.to_string(),
    }
}

/// 将值尽量转换成“非空字符串”，但保留首尾空白（适用于前缀、模板等需要精确空白的场景）。
fn coerce_non_blank_string(value: Option<&Value>, fallback: &str) -> String {
    match value.and_then(Value::as_str) {
        Some(text) if !text.trim().is_empty() => text.to_string(),
        _ => fallback.to_string(),
    }
}

/// 将值尽量转换成布尔值。
fn coerce_boolean(value: Option<&Value>, fallback: bool) -> bool {
    match value {
        Some(Value::Bool(value)) => *value,
        Some(Value::String(value)) => {
            let normalized = value.trim().to_lowercase();
            if normalized == "true" {
                return true;
            }
            if normalized == "false" {
                return false;
            }
            fallback
        }
        Some(Value::Number(value)) => value.as_f64().map(|value| value != 0.0).unwrap_or(fallback),
        _ => fallback,
    }
}

/// 将值尽量转换成数字（f64）。
fn coerce_number(value: Option<&Value>, fallback: f64) -> f64 {
    match value {
        Some(Value::Number(value)) => value.as_f64().unwrap_or(fallback),
        Some(Value::String(value)) => value.trim().parse::<f64>().unwrap_or(fallback),
        Some(Value::Bool(value)) => (*value as u8) as f64,
        _ => fallback,
    }
}

/// 判断当前节点是否已在本次执行过程中发射过值。
fn has_emitted_once(context: &NodeExecutionContext) -> bool {
    context
        .state
        .get(EMITTED_STATE_KEY)
        .and_then(|value| value.as_bool())
        .unwrap_or(false)
}

/// 标记当前节点已在本次执行过程中发射过值。
fn mark_emitted_once(context: &NodeExecutionContext) {
    context.state.insert(EMITTED_STATE_KEY, json!(true));
}

/// Demo 宿主配置。
#[derive(Debug, Clone)]
struct DemoConfig {
    nodegraph_base_url: String,
    demo_client_base_url: String,
    listen_addr: String,
    demo_domain: String,
    client_name: String,
}

impl DemoConfig {
    /// 从环境变量读取配置。
    fn from_env() -> Self {
        let port = env::var("DEMO_CLIENT_PORT")
            .ok()
            .and_then(|value| value.parse::<u16>().ok())
            .unwrap_or(3300);
        let host = env::var("DEMO_CLIENT_HOST").unwrap_or_else(|_| "127.0.0.1".to_string());
        let demo_client_base_url =
            env::var("DEMO_CLIENT_BASE_URL").unwrap_or_else(|_| format!("http://localhost:{port}"));

        Self {
            nodegraph_base_url: env::var("NODEGRAPH_BASE_URL")
                .unwrap_or_else(|_| "http://localhost:3000".to_string()),
            demo_client_base_url: demo_client_base_url.trim_end_matches('/').to_string(),
            listen_addr: format!("{host}:{port}"),
            demo_domain: env::var("DEMO_CLIENT_DOMAIN")
                .unwrap_or_else(|_| "demo-hello-world-rust".to_string()),
            client_name: env::var("DEMO_CLIENT_NAME")
                .unwrap_or_else(|_| "NodeGraph Demo Client (Rust)".to_string()),
        }
    }
}

/// 注册接口请求体。
#[derive(Debug, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
struct RegisterRequest {
    force: bool,
}

/// 图相关请求体。
#[derive(Debug, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
struct GraphRequest {
    graph_mode: Option<String>,
    graph_name: Option<String>,
    graph: Option<NodeGraphDocument>,
    breakpoints: Option<Vec<String>>,
    force_refresh: Option<bool>,
}

/// 更新断点接口的请求体。
#[derive(Debug, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
struct BreakpointsRequest {
    breakpoints: Option<Vec<String>>,
}

/// 字段选项接口查询参数。
#[derive(Debug, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
struct FieldOptionsQuery {
    domain: Option<String>,
    node_type: Option<String>,
    field_key: Option<String>,
    locale: Option<String>,
}

/// Rust Demo 宿主。
struct StoredDebugSession {
    debug_session_id: String,
    graph: NodeGraphDocument,
    breakpoints: Vec<String>,
    snapshot: NodeGraphExecutionSnapshot,
    debugger_session: NodeGraphRuntimeDebugSession,
}

/// Rust Demo 宿主。
struct DemoHost {
    config: DemoConfig,
    client: NodeGraphClient,
    runtime: NodeGraphRuntime,
    debug_sessions: HashMap<String, StoredDebugSession>,
    next_debug_session_id: u64,
    last_registration: Option<Value>,
    last_session: Option<Value>,
    last_execution: Option<Value>,
    last_debug: Option<Value>,
    latest_completion: Option<Value>,
    callback_history: Vec<Value>,
}

impl DemoHost {
    /// 创建 Demo 宿主。
    fn new(config: DemoConfig) -> Self {
        let client = NodeGraphClient::new(config.nodegraph_base_url.clone());
        let runtime = create_runtime(&config);

        Self {
            config,
            client,
            runtime,
            debug_sessions: HashMap::new(),
            next_debug_session_id: 1,
            last_registration: None,
            last_session: None,
            last_execution: None,
            last_debug: None,
            latest_completion: None,
            callback_history: Vec::new(),
        }
    }

    /// 返回当前运行时元数据。
    fn runtime_info(&self) -> Value {
        json!({
            "runtimeId": self.runtime.runtime_id(),
            "domain": self.runtime.domain(),
            "libraryVersion": self.runtime.library_version(),
            "controlBaseUrl": self.runtime.control_base_url(),
            "capabilities": self.runtime.capabilities(),
        })
    }

    /// 返回对外概览。
    fn overview(&self) -> Value {
        json!({
            "message": "NodeGraph Rust Demo Showcase host",
            "runtime": self.runtime_info(),
            "library": self.runtime.get_library(),
            "sampleGraph": create_graph(None, None),
            "endpoints": [
                "/api/health",
                "/api/runtime/library",
                "/api/runtime/field-options",
                "/api/runtime/register",
                "/api/runtime/execute",
                "/api/runtime/debug/sessions",
                "/api/runtime/debug/sample",
                "/api/create-session",
                "/api/completed",
                "/api/results/latest"
            ]
        })
    }

    /// 返回健康检查响应。
    fn health(&self) -> Value {
        json!({
            "status": "ok",
            "service": "NodeGraph Demo Client (Rust)",
            "demoClientBaseUrl": self.config.demo_client_base_url,
            "nodeGraphBaseUrl": self.config.nodegraph_base_url,
            "demoDomain": self.config.demo_domain,
            "runtime": self.runtime_info(),
        })
    }

    /// 返回嵌入式节点库。
    fn library_payload(&self) -> Value {
        json!({
            "runtime": self.runtime_info(),
            "library": self.runtime.get_library(),
        })
    }

    /// 注册当前运行时。
    async fn register_runtime(&mut self, force: bool) -> Result<RuntimeRegistrationResponse, String> {
        let response = self
            .runtime
            .ensure_registered(&self.client, force)
            .await
            .map_err(|error| error.to_string())?;

        self.last_registration = Some(json!({
            "registeredAt": chrono_like_now(),
            "force": force,
            "request": self.runtime.create_registration_request(),
            "response": response.clone(),
        }));

        Ok(response)
    }

    /// 创建编辑会话。
    async fn create_session(
        &mut self,
        graph_mode: Option<String>,
        graph_name: Option<String>,
        force_refresh: bool,
    ) -> Result<CreateSessionResponse, String> {
        let registration = self.register_runtime(force_refresh).await?;
        let graph = create_graph(graph_name.as_deref(), graph_mode.as_deref());
        let resolved_mode = normalize_graph_mode(graph_mode.as_deref());
        let resolved_name = graph.name.clone();

        let response = self
            .client
            .create_session(&CreateSessionRequest {
                runtime_id: self.runtime.runtime_id().to_string(),
                completion_webhook: format!("{}/api/completed", self.config.demo_client_base_url),
                graph: Some(graph),
                metadata: Some(std::collections::HashMap::from([
                    ("graphMode".to_string(), resolved_mode.to_string()),
                    (
                        "source".to_string(),
                        "NodeGraph.DemoClient.Rust.Showcase".to_string(),
                    ),
                ])),
            })
            .await
            .map_err(|error| error.to_string())?;

        self.last_session = Some(json!({
            "createdAt": chrono_like_now(),
            "request": {
                "graphMode": resolved_mode,
                "graphName": resolved_name,
                "forceRefresh": force_refresh,
            },
            "registration": registration,
            "response": response.clone(),
        }));

        Ok(response)
    }

    /// 执行 Demo Showcase 图。
    fn execute(&mut self, graph_mode: Option<String>, graph_name: Option<String>) -> Value {
        let graph = create_graph(graph_name.as_deref(), graph_mode.as_deref());
        let snapshot = self.runtime.execute_graph(&graph, None);

        let record = json!({
            "executedAt": chrono_like_now(),
            "graph": graph,
            "snapshot": snapshot,
        });
        self.last_execution = Some(record.clone());
        record
    }

    /// 运行断点调试样例。
    fn debug_sample(
        &mut self,
        graph_mode: Option<String>,
        graph_name: Option<String>,
        graph: Option<NodeGraphDocument>,
        breakpoints: Option<Vec<String>>,
    ) -> Value {
        let resolved_mode = normalize_graph_mode(graph_mode.as_deref());
        let resolved_graph =
            graph.unwrap_or_else(|| create_graph(graph_name.as_deref(), graph_mode.as_deref()));
        let resolved_name = resolve_graph_name(&resolved_graph, graph_name.as_deref(), resolved_mode);
        let normalized_breakpoints = normalize_breakpoints(
            breakpoints,
            Some(vec![DEFAULT_DEBUG_BREAKPOINT.to_string()]),
        );
        let created = self.create_debug_session(resolved_graph, Some(normalized_breakpoints.clone()));
        let debug_session_id = created["debugSessionId"]
            .as_str()
            .expect("created debug session should contain debugSessionId")
            .to_string();
        let first_step = self
            .step_debug_session(&debug_session_id)
            .expect("created debug session should be available for stepping")["snapshot"]
            .clone();
        let paused = self
            .continue_debug_session(&debug_session_id)
            .expect("created debug session should be available for continue")["snapshot"]
            .clone();
        let completed = self
            .continue_debug_session(&debug_session_id)
            .expect("created debug session should be available for completion")["snapshot"]
            .clone();
        self.debug_sessions.remove(&debug_session_id);

        let record = json!({
            "debuggedAt": chrono_like_now(),
            "graphMode": resolved_mode,
            "graphName": resolved_name,
            "graph": created["graph"].clone(),
            "breakpoints": normalized_breakpoints,
            "firstStep": first_step,
            "paused": paused,
            "completed": completed,
        });
        self.last_debug = Some(record.clone());
        record
    }

    /// 创建宿主内调试会话，供可视化调试页反复拉取和推进。
    fn create_debug_session(
        &mut self,
        graph: NodeGraphDocument,
        breakpoints: Option<Vec<String>>,
    ) -> Value {
        let normalized_breakpoints = normalize_breakpoints(breakpoints, None);
        let debug_session_id = format!("ngd_{}", self.next_debug_session_id);
        self.next_debug_session_id += 1;
        let debugger_session = self.runtime.create_debugger(
            &graph,
            Some(NodeGraphExecutionOptions {
                breakpoints: Some(HashSet::from_iter(normalized_breakpoints.iter().cloned())),
                max_steps: None,
                max_wall_time: None,
            }),
        );
        let stored_session = StoredDebugSession {
            debug_session_id: debug_session_id.clone(),
            graph,
            breakpoints: normalized_breakpoints,
            snapshot: create_idle_debug_snapshot(),
            debugger_session,
        };
        let payload = build_debug_session_payload(&stored_session);
        self.last_debug = Some(build_debug_session_state_record(&stored_session));
        self.debug_sessions
            .insert(debug_session_id, stored_session);
        payload
    }

    /// 查询指定调试会话；若会话不存在则返回 None。
    fn get_debug_session(&self, debug_session_id: &str) -> Option<Value> {
        self.debug_sessions
            .get(debug_session_id)
            .map(build_debug_session_payload)
    }

    /// 单步推进指定调试会话。
    fn step_debug_session(&mut self, debug_session_id: &str) -> Option<Value> {
        let snapshot = self
            .debug_sessions
            .get_mut(debug_session_id)
            .map(|stored_session| stored_session.debugger_session.step())?;
        self.update_debug_session_snapshot(debug_session_id, snapshot)
    }

    /// 继续运行指定调试会话。
    fn continue_debug_session(&mut self, debug_session_id: &str) -> Option<Value> {
        let snapshot = self
            .debug_sessions
            .get_mut(debug_session_id)
            .map(|stored_session| stored_session.debugger_session.continue_execution())?;
        self.update_debug_session_snapshot(debug_session_id, snapshot)
    }

    /// 动态替换指定调试会话的断点集合。
    fn set_debug_session_breakpoints(
        &mut self,
        debug_session_id: &str,
        breakpoints: Option<Vec<String>>,
    ) -> Option<Value> {
        let (payload, state_record) = {
            let stored_session = self.debug_sessions.get_mut(debug_session_id)?;
            stored_session.breakpoints = normalize_breakpoints(breakpoints, None);
            stored_session
                .debugger_session
                .set_breakpoints(HashSet::from_iter(stored_session.breakpoints.iter().cloned()));
            (
                build_debug_session_payload(stored_session),
                build_debug_session_state_record(stored_session),
            )
        };
        self.last_debug = Some(state_record);
        Some(payload)
    }

    /// 关闭指定调试会话。
    fn close_debug_session(&mut self, debug_session_id: &str) -> bool {
        self.debug_sessions.remove(debug_session_id).is_some()
    }

    /// 更新指定调试会话的最新快照，并同步 lastDebug 记录。
    fn update_debug_session_snapshot(
        &mut self,
        debug_session_id: &str,
        snapshot: NodeGraphExecutionSnapshot,
    ) -> Option<Value> {
        let (payload, state_record) = {
            let stored_session = self.debug_sessions.get_mut(debug_session_id)?;
            stored_session.snapshot = snapshot;
            (
                build_debug_session_payload(stored_session),
                build_debug_session_state_record(stored_session),
            )
        };
        self.last_debug = Some(state_record);
        Some(payload)
    }

    /// 保存最近一次编辑完成回调。
    fn store_completion(&mut self, payload: Value) -> Value {
        let record = json!({
            "receivedAt": chrono_like_now(),
            "payload": payload,
        });

        self.latest_completion = Some(record.clone());
        self.callback_history.push(record.clone());
        if self.callback_history.len() > 10 {
            self.callback_history.remove(0);
        }

        json!({
            "success": true,
            "receivedAt": record["receivedAt"],
        })
    }

    /// 返回最近状态快照。
    fn latest(&self) -> Value {
        json!({
            "runtime": self.runtime_info(),
            "lastRegistration": self.last_registration,
            "lastSession": self.last_session,
            "lastExecution": self.last_execution,
            "lastDebug": self.last_debug,
            "latestCompletion": self.latest_completion,
            "callbackCount": self.callback_history.len(),
        })
    }
}

#[tokio::main]
async fn main() {
    let config = DemoConfig::from_env();
    let host = Arc::new(Mutex::new(DemoHost::new(config.clone())));
    let app = Router::new()
        .route("/", get(get_overview))
        .route("/api/health", get(get_health))
        .route("/api/runtime/library", get(get_library))
        .route("/api/runtime/field-options", get(get_field_options))
        .route("/api/runtime/register", post(post_register_runtime))
        .route("/api/runtime/execute", post(post_execute))
        .route("/api/runtime/debug/sessions", post(post_create_debug_session))
        .route(
            "/api/runtime/debug/sessions/{debug_session_id}",
            get(get_debug_session).delete(delete_debug_session),
        )
        .route(
            "/api/runtime/debug/sessions/{debug_session_id}/step",
            post(post_debug_session_step),
        )
        .route(
            "/api/runtime/debug/sessions/{debug_session_id}/continue",
            post(post_debug_session_continue),
        )
        .route(
            "/api/runtime/debug/sessions/{debug_session_id}/breakpoints",
            put(put_debug_session_breakpoints),
        )
        .route("/api/runtime/debug/sample", post(post_debug_sample))
        .route("/api/create-session", post(post_create_session))
        .route("/api/completed", post(post_completed))
        .route("/api/results/latest", get(get_latest))
        .with_state(host);

    let listener = TcpListener::bind(&config.listen_addr)
        .await
        .expect("failed to bind demo host listener");
    println!(
        "[NodeGraph Demo Client][Rust] listening on {}",
        config.listen_addr
    );
    println!(
        "[NodeGraph Demo Client][Rust] runtime library: {}/api/runtime/library",
        config.demo_client_base_url
    );
    println!(
        "[NodeGraph Demo Client][Rust] completion webhook: {}/api/completed",
        config.demo_client_base_url
    );
    axum::serve(listener, app)
        .await
        .expect("failed to serve rust demo host");
}

/// 概览接口。
async fn get_overview(State(host): State<SharedHost>) -> Json<Value> {
    Json(host.lock().await.overview())
}

/// 健康检查接口。
async fn get_health(State(host): State<SharedHost>) -> Json<Value> {
    Json(host.lock().await.health())
}

/// 节点库接口。
async fn get_library(State(host): State<SharedHost>) -> Json<Value> {
    Json(host.lock().await.library_payload())
}

/// 远端字段选项接口。
async fn get_field_options(query: Query<FieldOptionsQuery>) -> Json<Value> {
    let _domain = query.domain.as_deref().unwrap_or_default();
    let node_type = query.node_type.as_deref().unwrap_or_default();
    let field_key = query.field_key.as_deref().unwrap_or_default();
    let locale = query.locale.as_deref().unwrap_or("en");
    let is_zh = locale.to_lowercase().starts_with("zh");

    if node_type == "demo_source" && field_key == "punctuation" {
        return Json(json!({
            "options": [
                { "value": "!", "label": if is_zh { "感叹号 (!)" } else { "Exclamation (!)" } },
                { "value": "?", "label": if is_zh { "问号 (?)" } else { "Question (?)" } },
                { "value": ".", "label": if is_zh { "句号 (.)" } else { "Dot (.)" } },
                { "value": "...", "label": if is_zh { "省略号 (...)" } else { "Ellipsis (...)" } }
            ]
        }));
    }

    Json(json!({ "options": [] }))
}

/// 最近状态接口。
async fn get_latest(State(host): State<SharedHost>) -> Json<Value> {
    Json(host.lock().await.latest())
}

/// 注册运行时接口。
async fn post_register_runtime(
    State(host): State<SharedHost>,
    payload: Option<Json<RegisterRequest>>,
) -> Result<Json<Value>, (StatusCode, Json<Value>)> {
    let force = payload.map(|body| body.force).unwrap_or(false);
    let mut host = host.lock().await;
    host.register_runtime(force)
        .await
        .map(|response| Json(json!(response)))
        .map_err(to_error_response)
}

/// 执行图接口。
async fn post_execute(
    State(host): State<SharedHost>,
    payload: Option<Json<GraphRequest>>,
) -> Json<Value> {
    let payload = payload.map(|body| body.0).unwrap_or_default();
    let mut host = host.lock().await;
    Json(host.execute(payload.graph_mode, payload.graph_name))
}

/// 创建调试会话接口。
async fn post_create_debug_session(
    State(host): State<SharedHost>,
    payload: Option<Json<GraphRequest>>,
) -> (StatusCode, Json<Value>) {
    let payload = payload.map(|body| body.0).unwrap_or_default();
    let graph = payload
        .graph
        .unwrap_or_else(|| create_graph(payload.graph_name.as_deref(), payload.graph_mode.as_deref()));
    let mut host = host.lock().await;
    (
        StatusCode::CREATED,
        Json(host.create_debug_session(graph, payload.breakpoints)),
    )
}

/// 获取调试会话接口。
async fn get_debug_session(
    State(host): State<SharedHost>,
    Path(debug_session_id): Path<String>,
) -> Result<Json<Value>, (StatusCode, Json<Value>)> {
    let host = host.lock().await;
    host.get_debug_session(&debug_session_id)
        .map(Json)
        .ok_or_else(|| debug_session_not_found_response(&debug_session_id))
}

/// 调试会话单步接口。
async fn post_debug_session_step(
    State(host): State<SharedHost>,
    Path(debug_session_id): Path<String>,
) -> Result<Json<Value>, (StatusCode, Json<Value>)> {
    let mut host = host.lock().await;
    host.step_debug_session(&debug_session_id)
        .map(Json)
        .ok_or_else(|| debug_session_not_found_response(&debug_session_id))
}

/// 调试会话继续接口。
async fn post_debug_session_continue(
    State(host): State<SharedHost>,
    Path(debug_session_id): Path<String>,
) -> Result<Json<Value>, (StatusCode, Json<Value>)> {
    let mut host = host.lock().await;
    host.continue_debug_session(&debug_session_id)
        .map(Json)
        .ok_or_else(|| debug_session_not_found_response(&debug_session_id))
}

/// 调试会话断点更新接口。
async fn put_debug_session_breakpoints(
    State(host): State<SharedHost>,
    Path(debug_session_id): Path<String>,
    payload: Option<Json<BreakpointsRequest>>,
) -> Result<Json<Value>, (StatusCode, Json<Value>)> {
    let payload = payload.map(|body| body.0).unwrap_or_default();
    let mut host = host.lock().await;
    host.set_debug_session_breakpoints(&debug_session_id, payload.breakpoints)
        .map(Json)
        .ok_or_else(|| debug_session_not_found_response(&debug_session_id))
}

/// 删除调试会话接口。
async fn delete_debug_session(
    State(host): State<SharedHost>,
    Path(debug_session_id): Path<String>,
) -> (StatusCode, Json<Value>) {
    let mut host = host.lock().await;
    let closed = host.close_debug_session(&debug_session_id);
    (
        if closed {
            StatusCode::OK
        } else {
            StatusCode::NOT_FOUND
        },
        Json(json!({
            "closed": closed,
        })),
    )
}

/// 调试样例接口。
async fn post_debug_sample(
    State(host): State<SharedHost>,
    payload: Option<Json<GraphRequest>>,
) -> Json<Value> {
    let payload = payload.map(|body| body.0).unwrap_or_default();
    let mut host = host.lock().await;
    Json(host.debug_sample(
        payload.graph_mode,
        payload.graph_name,
        payload.graph,
        payload.breakpoints,
    ))
}

/// 创建会话接口。
async fn post_create_session(
    State(host): State<SharedHost>,
    payload: Option<Json<GraphRequest>>,
) -> Result<Json<Value>, (StatusCode, Json<Value>)> {
    let payload = payload.map(|body| body.0).unwrap_or_default();
    let mut host = host.lock().await;
    host.create_session(
        payload.graph_mode,
        payload.graph_name,
        payload.force_refresh.unwrap_or(false),
    )
    .await
    .map(|response| Json(json!(response)))
    .map_err(to_error_response)
}

/// 完成回调接口。
async fn post_completed(
    State(host): State<SharedHost>,
    payload: Option<Json<Value>>,
) -> Json<Value> {
    let mut host = host.lock().await;
    Json(host.store_completion(payload.map(|body| body.0).unwrap_or_else(|| json!({}))))
}

/// 将字符串错误转换成 HTTP 结果。
fn to_error_response(message: String) -> (StatusCode, Json<Value>) {
    (
        StatusCode::BAD_GATEWAY,
        Json(json!({
            "error": message,
        })),
    )
}

/// 规范化图模式。
fn normalize_graph_mode(value: Option<&str>) -> &'static str {
    if matches!(value, Some("new")) {
        "new"
    } else {
        "existing"
    }
}

/// 根据请求显式名称或图快照中的名称补齐最终图名。
fn resolve_graph_name(graph: &NodeGraphDocument, graph_name: Option<&str>, graph_mode: &str) -> String {
    if let Some(value) = graph_name.map(str::trim).filter(|value| !value.is_empty()) {
        return value.to_string();
    }

    if !graph.name.trim().is_empty() {
        return graph.name.trim().to_string();
    }

    if graph_mode == "new" {
        DEFAULT_NEW_GRAPH_NAME.to_string()
    } else {
        DEFAULT_EXISTING_GRAPH_NAME.to_string()
    }
}

/// 规范化断点列表，移除空白项并按出现顺序去重。
fn normalize_breakpoints(
    breakpoints: Option<Vec<String>>,
    fallback: Option<Vec<String>>,
) -> Vec<String> {
    let mut normalized = Vec::new();
    for entry in breakpoints.or(fallback).unwrap_or_default() {
        let trimmed = entry.trim();
        if !trimmed.is_empty() && !normalized.iter().any(|current| current == trimmed) {
            normalized.push(trimmed.to_string());
        }
    }

    normalized
}

/// 创建“尚未开始执行”的调试快照。
fn create_idle_debug_snapshot() -> NodeGraphExecutionSnapshot {
    NodeGraphExecutionSnapshot {
        status: "idle".to_string(),
        pause_reason: None,
        pending_node_id: None,
        last_error: None,
        last_event: None,
        profiler: HashMap::new(),
        results: HashMap::new(),
        events: Vec::new(),
    }
}

/// 构造调试会话标准返回体。
fn build_debug_session_payload(stored_session: &StoredDebugSession) -> Value {
    json!({
        "debugSessionId": stored_session.debug_session_id,
        "graph": stored_session.graph,
        "breakpoints": stored_session.breakpoints,
        "snapshot": stored_session.snapshot,
    })
}

/// 构造 `/api/results/latest` 需要保存的最近一次调试状态。
fn build_debug_session_state_record(stored_session: &StoredDebugSession) -> Value {
    json!({
        "debuggedAt": chrono_like_now(),
        "debugSessionId": stored_session.debug_session_id,
        "graph": stored_session.graph,
        "breakpoints": stored_session.breakpoints,
        "snapshot": stored_session.snapshot,
    })
}

/// 生成统一的“调试会话不存在”错误响应。
fn debug_session_not_found_response(debug_session_id: &str) -> (StatusCode, Json<Value>) {
    (
        StatusCode::NOT_FOUND,
        Json(json!({
            "error": format!("Debug session \"{debug_session_id}\" was not found."),
        })),
    )
}

/// 生成伪 ISO 时间戳。
fn chrono_like_now() -> String {
    format!("{:?}", std::time::SystemTime::now())
}

/// 创建 Demo Showcase 运行时。
fn create_runtime(config: &DemoConfig) -> NodeGraphRuntime {
    let mut runtime = NodeGraphRuntime::new(NodeGraphRuntimeOptions {
        domain: config.demo_domain.clone(),
        client_name: Some(config.client_name.clone()),
        control_base_url: format!("{}/api/runtime", config.demo_client_base_url),
        library_version: DEMO_LIBRARY_VERSION.to_string(),
        capabilities: None,
        runtime_id: None,
        cache_ttl: None,
        now: None,
    })
    .expect("failed to create rust demo runtime");

    runtime.register_type_mapping(TypeMappingEntry {
        canonical_id: HELLO_TEXT_TYPE.to_string(),
        node_type: "DemoText".to_string(),
        color: Some("#2563eb".to_string()),
    });

    runtime.register_type_mapping(TypeMappingEntry {
        canonical_id: DEMO_NUMBER_TYPE.to_string(),
        node_type: "DemoNumber".to_string(),
        color: Some("#f97316".to_string()),
    });

    runtime.register_type_mapping(TypeMappingEntry {
        canonical_id: DEMO_BOOLEAN_TYPE.to_string(),
        node_type: "DemoBoolean".to_string(),
        color: Some("#22c55e".to_string()),
    });

    runtime.register_type_mapping(TypeMappingEntry {
        canonical_id: DEMO_DATE_TYPE.to_string(),
        node_type: "DemoDate".to_string(),
        color: Some("#a855f7".to_string()),
    });

    runtime.register_type_mapping(TypeMappingEntry {
        canonical_id: DEMO_COLOR_TYPE.to_string(),
        node_type: "DemoColor".to_string(),
        color: Some("#e11d48".to_string()),
    });

    runtime.register_type_mapping(TypeMappingEntry {
        canonical_id: DEMO_DECIMAL_TYPE.to_string(),
        node_type: "DemoDecimal".to_string(),
        color: Some("#06b6d4".to_string()),
    });

    runtime.register_node(
        NodeDefinition::new("greeting_source", "Greeting Source", "Hello World", |context| {
            let name = coerce_string(context.values.get("name"), "World");
            context.emit("text", json!(format!("Hello, {name}!")));
            Ok(())
        })
        .with_description("Create the greeting text that will be sent to the output node.")
        .with_outputs(vec![create_port("text", "Text", HELLO_TEXT_TYPE)])
        .with_fields(vec![NodeLibraryFieldDefinition {
            key: "name".to_string(),
            label: "Name".to_string(),
            placeholder: Some("Who should be greeted?".to_string()),
            kind: "text".to_string(),
            default_value: Some(json!("World")),
            options_endpoint: None,
        }])
        .with_appearance(create_appearance("#eff6ff", "#2563eb", "#1e3a8a")),
    );

    runtime.register_node(
        NodeDefinition::new("console_output", "Console Output", "Debug", |context| {
            context.push_result(
                "console",
                context.read_input("text").unwrap_or_else(|| json!("Hello, World!")),
            );
            Ok(())
        })
        .with_description("Collect the final greeting into the runtime result buffer.")
        .with_inputs(vec![create_port("text", "Text", HELLO_TEXT_TYPE)])
        .with_appearance(create_appearance("#f0fdf4", "#16a34a", "#14532d")),
    );

    runtime.register_node(
        NodeDefinition::new("demo_source", "Demo Source", "Playground", move |context| {
            let name = coerce_string(context.values.get("name"), "Codex");
            let punctuation = coerce_string(context.values.get("punctuation"), "!");
            let enabled = coerce_boolean(context.values.get("enabled"), true);
            let base_number = coerce_number(context.values.get("baseNumber"), 7.0);
            let delta = coerce_number(context.values.get("delta"), 5.0);
            let today = coerce_string(context.values.get("today"), "2026-03-21");
            let theme = coerce_string(context.values.get("theme"), "#2563eb");
            let amount = coerce_string(context.values.get("amount"), "123.45");

            context.emit("name", json!(name));
            context.emit("punctuation", json!(punctuation));
            context.emit("enabled", json!(enabled));
            context.emit("baseNumber", json!(base_number));
            context.emit("delta", json!(delta));
            context.emit("today", json!(today));
            context.emit("theme", json!(theme));
            context.emit("amount", json!(amount));
            Ok(())
        })
        .with_description(
            "Emit a bundle of typed values so downstream nodes can demonstrate rich editor features.",
        )
        .with_outputs(vec![
            create_port("name", "Name", HELLO_TEXT_TYPE),
            create_port("punctuation", "Punctuation", HELLO_TEXT_TYPE),
            create_port("enabled", "Enabled", DEMO_BOOLEAN_TYPE),
            create_port("baseNumber", "Base", DEMO_NUMBER_TYPE),
            create_port("delta", "Delta", DEMO_NUMBER_TYPE),
            create_port("today", "Date", DEMO_DATE_TYPE),
            create_port("theme", "Theme", DEMO_COLOR_TYPE),
            create_port("amount", "Amount", DEMO_DECIMAL_TYPE),
        ])
        .with_fields(vec![
            NodeLibraryFieldDefinition {
                key: "name".to_string(),
                label: "Name".to_string(),
                placeholder: Some("The name used in the greeting pipeline.".to_string()),
                kind: "text".to_string(),
                default_value: Some(json!("Codex")),
                options_endpoint: None,
            },
            NodeLibraryFieldDefinition {
                key: "punctuation".to_string(),
                label: "Punctuation".to_string(),
                placeholder: None,
                kind: "select".to_string(),
                default_value: Some(json!("!")),
                options_endpoint: Some(format!(
                    "{}/api/runtime/field-options",
                    config.demo_client_base_url
                )),
            },
            NodeLibraryFieldDefinition {
                key: "enabled".to_string(),
                label: "Enabled".to_string(),
                placeholder: None,
                kind: "boolean".to_string(),
                default_value: Some(json!(true)),
                options_endpoint: None,
            },
            NodeLibraryFieldDefinition {
                key: "baseNumber".to_string(),
                label: "Base Number".to_string(),
                placeholder: None,
                kind: "int".to_string(),
                default_value: Some(json!(7)),
                options_endpoint: None,
            },
            NodeLibraryFieldDefinition {
                key: "delta".to_string(),
                label: "Delta".to_string(),
                placeholder: None,
                kind: "double".to_string(),
                default_value: Some(json!(5)),
                options_endpoint: None,
            },
            NodeLibraryFieldDefinition {
                key: "today".to_string(),
                label: "Date".to_string(),
                placeholder: None,
                kind: "date".to_string(),
                default_value: Some(json!("2026-03-21")),
                options_endpoint: None,
            },
            NodeLibraryFieldDefinition {
                key: "theme".to_string(),
                label: "Theme Color".to_string(),
                placeholder: None,
                kind: "color".to_string(),
                default_value: Some(json!("#2563eb")),
                options_endpoint: None,
            },
            NodeLibraryFieldDefinition {
                key: "amount".to_string(),
                label: "Amount (decimal string)".to_string(),
                placeholder: None,
                kind: "decimal".to_string(),
                default_value: Some(json!("123.45")),
                options_endpoint: None,
            },
        ])
        .with_appearance(create_appearance("#0b1220", "#38bdf8", "#e0f2fe")),
    );

    runtime.register_node(
        NodeDefinition::new("greeting_builder", "Greeting Builder", "Text", |context| {
            if has_emitted_once(context) {
                return Ok(());
            }

            let name = context.read_input("name");
            let punctuation = context.read_input("punctuation");
            if name.is_none() || punctuation.is_none() {
                return Ok(());
            }

            let prefix = coerce_non_blank_string(context.values.get("prefix"), "Hello, ");
            context.emit(
                "text",
                json!(format!(
                    "{}{}{}",
                    prefix,
                    coerce_string(name.as_ref(), "World"),
                    coerce_string(punctuation.as_ref(), "!")
                )),
            );
            mark_emitted_once(context);
            Ok(())
        })
        .with_description(
            "Build a greeting message from inputs and emit it only when both inputs are ready.",
        )
        .with_inputs(vec![
            create_port("name", "Name", HELLO_TEXT_TYPE),
            create_port("punctuation", "Punctuation", HELLO_TEXT_TYPE),
        ])
        .with_outputs(vec![create_port("text", "Greeting", HELLO_TEXT_TYPE)])
        .with_fields(vec![NodeLibraryFieldDefinition {
            key: "prefix".to_string(),
            label: "Prefix".to_string(),
            placeholder: Some("Text inserted before the name.".to_string()),
            kind: "text".to_string(),
            default_value: Some(json!("Hello, ")),
            options_endpoint: None,
        }])
        .with_appearance(create_appearance("#eff6ff", "#2563eb", "#1e3a8a")),
    );

    runtime.register_node(
        NodeDefinition::new("math_add", "Add Numbers", "Math", |context| {
            if has_emitted_once(context) {
                return Ok(());
            }

            let a = context.read_input("a");
            let b = context.read_input("b");
            if a.is_none() || b.is_none() {
                return Ok(());
            }

            let sum = coerce_number(a.as_ref(), 0.0) + coerce_number(b.as_ref(), 0.0);
            context.emit("sum", json!(sum));
            mark_emitted_once(context);
            Ok(())
        })
        .with_description("Add two numeric inputs and emit a sum once both values are available.")
        .with_inputs(vec![
            create_port("a", "A", DEMO_NUMBER_TYPE),
            create_port("b", "B", DEMO_NUMBER_TYPE),
        ])
        .with_outputs(vec![create_port("sum", "Sum", DEMO_NUMBER_TYPE)])
        .with_appearance(create_appearance("#fff7ed", "#f97316", "#7c2d12")),
    );

    runtime.register_node(
        NodeDefinition::new("if_text", "If (Text)", "Logic", |context| {
            if has_emitted_once(context) {
                return Ok(());
            }

            let condition = context.read_input("condition");
            let when_true = context.read_input("whenTrue");
            if condition.is_none() || when_true.is_none() {
                return Ok(());
            }

            if coerce_boolean(condition.as_ref(), false) {
                context.emit("text", json!(coerce_string(when_true.as_ref(), "")));
                mark_emitted_once(context);
                return Ok(());
            }

            let when_false = context.read_input("whenFalse");
            if when_false.is_some() && !is_blank_string(when_false.as_ref()) {
                context.emit("text", json!(coerce_string(when_false.as_ref(), "")));
            } else {
                context.emit(
                    "text",
                    json!(coerce_string(context.values.get("fallback"), "(disabled)")),
                );
            }

            mark_emitted_once(context);
            Ok(())
        })
        .with_description("Select between two text branches based on a boolean condition.")
        .with_inputs(vec![
            create_port("condition", "Condition", DEMO_BOOLEAN_TYPE),
            create_port("whenTrue", "When True", HELLO_TEXT_TYPE),
            create_port("whenFalse", "When False", HELLO_TEXT_TYPE),
        ])
        .with_outputs(vec![create_port("text", "Text", HELLO_TEXT_TYPE)])
        .with_fields(vec![NodeLibraryFieldDefinition {
            key: "fallback".to_string(),
            label: "Fallback".to_string(),
            placeholder: Some(
                "Used when condition=false and the whenFalse port is not connected.".to_string(),
            ),
            kind: "text".to_string(),
            default_value: Some(json!("(disabled)")),
            options_endpoint: None,
        }])
        .with_appearance(create_appearance("#f0fdf4", "#22c55e", "#14532d")),
    );

    runtime.register_node(
        NodeDefinition::new("text_interpolate", "Text Interpolate", "Text", |context| {
            if has_emitted_once(context) {
                return Ok(());
            }

            let greeting = context.read_input("greeting");
            let lucky = context.read_input("lucky");
            let today = context.read_input("today");
            let theme = context.read_input("theme");
            let amount = context.read_input("amount");

            if greeting.is_none()
                || lucky.is_none()
                || today.is_none()
                || theme.is_none()
                || amount.is_none()
            {
                return Ok(());
            }

            let template = coerce_string(context.values.get("template"), DEFAULT_TEMPLATE);
            let rendered = template
                .replace("{greeting}", &coerce_string(greeting.as_ref(), ""))
                .replace(
                    "{lucky}",
                    &coerce_number(lucky.as_ref(), 0.0).to_string(),
                )
                .replace("{today}", &coerce_string(today.as_ref(), ""))
                .replace("{theme}", &coerce_string(theme.as_ref(), ""))
                .replace("{amount}", &coerce_string(amount.as_ref(), ""));

            context.emit("text", json!(rendered));
            mark_emitted_once(context);
            Ok(())
        })
        .with_description("Render a multi-line template by interpolating typed inputs.")
        .with_inputs(vec![
            create_port("greeting", "Greeting", HELLO_TEXT_TYPE),
            create_port("lucky", "Lucky Number", DEMO_NUMBER_TYPE),
            create_port("today", "Date", DEMO_DATE_TYPE),
            create_port("theme", "Theme Color", DEMO_COLOR_TYPE),
            create_port("amount", "Amount", DEMO_DECIMAL_TYPE),
        ])
        .with_outputs(vec![create_port("text", "Text", HELLO_TEXT_TYPE)])
        .with_fields(vec![NodeLibraryFieldDefinition {
            key: "template".to_string(),
            label: "Template".to_string(),
            placeholder: Some("Use tokens like {greeting} to interpolate values.".to_string()),
            kind: "textarea".to_string(),
            default_value: Some(json!(DEFAULT_TEMPLATE)),
            options_endpoint: None,
        }])
        .with_appearance(create_appearance("#eff6ff", "#2563eb", "#1e3a8a")),
    );

    runtime.register_node(
        NodeDefinition::new("const_text", "Const (Text)", "Inputs", |context| {
            if has_emitted_once(context) {
                return Ok(());
            }

            context.emit("text", json!(coerce_string(context.values.get("text"), "")));
            mark_emitted_once(context);
            Ok(())
        })
        .with_description("Emit a constant text value.")
        .with_outputs(vec![create_port("text", "Text", HELLO_TEXT_TYPE)])
        .with_fields(vec![NodeLibraryFieldDefinition {
            key: "text".to_string(),
            label: "Text".to_string(),
            placeholder: Some("Constant text emitted by this node.".to_string()),
            kind: "textarea".to_string(),
            default_value: Some(json!("Hello")),
            options_endpoint: None,
        }])
        .with_appearance(create_appearance("#eff6ff", "#2563eb", "#1e3a8a")),
    );

    runtime.register_node(
        NodeDefinition::new("const_number", "Const (Number)", "Inputs", |context| {
            if has_emitted_once(context) {
                return Ok(());
            }

            context.emit(
                "number",
                json!(coerce_number(context.values.get("value"), 0.0)),
            );
            mark_emitted_once(context);
            Ok(())
        })
        .with_description("Emit a constant numeric value.")
        .with_outputs(vec![create_port("number", "Number", DEMO_NUMBER_TYPE)])
        .with_fields(vec![NodeLibraryFieldDefinition {
            key: "value".to_string(),
            label: "Value".to_string(),
            placeholder: Some("Constant number emitted by this node.".to_string()),
            kind: "double".to_string(),
            default_value: Some(json!(0)),
            options_endpoint: None,
        }])
        .with_appearance(create_appearance("#fff7ed", "#f97316", "#7c2d12")),
    );

    runtime.register_node(
        NodeDefinition::new("const_boolean", "Const (Boolean)", "Inputs", |context| {
            if has_emitted_once(context) {
                return Ok(());
            }

            context.emit(
                "value",
                json!(coerce_boolean(context.values.get("value"), true)),
            );
            mark_emitted_once(context);
            Ok(())
        })
        .with_description("Emit a constant boolean value.")
        .with_outputs(vec![create_port("value", "Value", DEMO_BOOLEAN_TYPE)])
        .with_fields(vec![NodeLibraryFieldDefinition {
            key: "value".to_string(),
            label: "Value".to_string(),
            placeholder: None,
            kind: "boolean".to_string(),
            default_value: Some(json!(true)),
            options_endpoint: None,
        }])
        .with_appearance(create_appearance("#f0fdf4", "#22c55e", "#14532d")),
    );

    runtime.register_node(
        NodeDefinition::new("const_date", "Const (Date)", "Inputs", |context| {
            if has_emitted_once(context) {
                return Ok(());
            }

            context.emit(
                "date",
                json!(coerce_string(context.values.get("date"), "2026-03-21")),
            );
            mark_emitted_once(context);
            Ok(())
        })
        .with_description("Emit a constant date string (YYYY-MM-DD).")
        .with_outputs(vec![create_port("date", "Date", DEMO_DATE_TYPE)])
        .with_fields(vec![NodeLibraryFieldDefinition {
            key: "date".to_string(),
            label: "Date".to_string(),
            placeholder: None,
            kind: "date".to_string(),
            default_value: Some(json!("2026-03-21")),
            options_endpoint: None,
        }])
        .with_appearance(create_appearance("#faf5ff", "#a855f7", "#581c87")),
    );

    runtime.register_node(
        NodeDefinition::new("const_color", "Const (Color)", "Inputs", |context| {
            if has_emitted_once(context) {
                return Ok(());
            }

            context.emit(
                "color",
                json!(coerce_string(context.values.get("color"), "#2563eb")),
            );
            mark_emitted_once(context);
            Ok(())
        })
        .with_description("Emit a constant color string (#RRGGBB).")
        .with_outputs(vec![create_port("color", "Color", DEMO_COLOR_TYPE)])
        .with_fields(vec![NodeLibraryFieldDefinition {
            key: "color".to_string(),
            label: "Color".to_string(),
            placeholder: None,
            kind: "color".to_string(),
            default_value: Some(json!("#2563eb")),
            options_endpoint: None,
        }])
        .with_appearance(create_appearance("#fff1f2", "#e11d48", "#881337")),
    );

    runtime.register_node(
        NodeDefinition::new("const_decimal", "Const (Decimal)", "Inputs", |context| {
            if has_emitted_once(context) {
                return Ok(());
            }

            context.emit(
                "decimal",
                json!(coerce_string(context.values.get("value"), "123.45")),
            );
            mark_emitted_once(context);
            Ok(())
        })
        .with_description(
            "Emit a decimal value as a string to demonstrate the decimal field editor.",
        )
        .with_outputs(vec![create_port("decimal", "Decimal", DEMO_DECIMAL_TYPE)])
        .with_fields(vec![NodeLibraryFieldDefinition {
            key: "value".to_string(),
            label: "Value".to_string(),
            placeholder: Some("A decimal string like 123.45".to_string()),
            kind: "decimal".to_string(),
            default_value: Some(json!("123.45")),
            options_endpoint: None,
        }])
        .with_appearance(create_appearance("#ecfeff", "#06b6d4", "#164e63")),
    );

    runtime
}

/// 创建默认图或空白图。
fn create_graph(graph_name: Option<&str>, graph_mode: Option<&str>) -> NodeGraphDocument {
    let mode = normalize_graph_mode(graph_mode);
    let resolved_name = graph_name
        .map(str::trim)
        .filter(|value| !value.is_empty())
        .unwrap_or(if mode == "new" {
            DEFAULT_NEW_GRAPH_NAME
        } else {
            DEFAULT_EXISTING_GRAPH_NAME
        });

    if mode == "new" {
        return NodeGraphDocument {
            graph_id: None,
            name: resolved_name.to_string(),
            description: Some("Start from a blank Demo Showcase graph.".to_string()),
            nodes: Vec::new(),
            edges: Vec::new(),
            viewport: NodeGraphViewport {
                x: 0.0,
                y: 0.0,
                zoom: 1.0,
            },
        };
    }

    NodeGraphDocument {
        graph_id: Some("demo-showcase-graph".to_string()),
        name: resolved_name.to_string(),
        description: Some("A runnable Demo Showcase graph hosted by the Rust SDK demo.".to_string()),
        nodes: vec![
            NodeGraphNode {
                id: "node_source".to_string(),
                node_type: "default".to_string(),
                position: Position { x: 80.0, y: 220.0 },
                data: Map::from_iter([
                    ("label".to_string(), json!("Demo Source")),
                    (
                        "description".to_string(),
                        json!("Emit typed values to drive the showcase pipeline."),
                    ),
                    ("category".to_string(), json!("Playground")),
                    ("nodeType".to_string(), json!("demo_source")),
                    ("inputs".to_string(), json!([])),
                    (
                        "outputs".to_string(),
                        json!([
                            create_port("name", "Name", HELLO_TEXT_TYPE),
                            create_port("punctuation", "Punctuation", HELLO_TEXT_TYPE),
                            create_port("enabled", "Enabled", DEMO_BOOLEAN_TYPE),
                            create_port("baseNumber", "Base", DEMO_NUMBER_TYPE),
                            create_port("delta", "Delta", DEMO_NUMBER_TYPE),
                            create_port("today", "Date", DEMO_DATE_TYPE),
                            create_port("theme", "Theme", DEMO_COLOR_TYPE),
                            create_port("amount", "Amount", DEMO_DECIMAL_TYPE)
                        ]),
                    ),
                    (
                        "values".to_string(),
                        json!({
                            "name": "Codex",
                            "punctuation": "!",
                            "enabled": true,
                            "baseNumber": 7,
                            "delta": 5,
                            "today": "2026-03-21",
                            "theme": "#2563eb",
                            "amount": "123.45"
                        }),
                    ),
                    (
                        "appearance".to_string(),
                        json!(create_appearance("#0b1220", "#38bdf8", "#e0f2fe")),
                    ),
                ]),
                width: None,
                height: None,
                style: Some(Map::from_iter([
                    ("background".to_string(), json!("#0b1220")),
                    ("borderColor".to_string(), json!("#38bdf8")),
                    ("borderRadius".to_string(), json!(18)),
                    ("borderWidth".to_string(), json!(1)),
                    ("color".to_string(), json!("#e0f2fe")),
                ])),
            },
            NodeGraphNode {
                id: "node_greet".to_string(),
                node_type: "default".to_string(),
                position: Position { x: 420.0, y: 140.0 },
                data: Map::from_iter([
                    ("label".to_string(), json!("Greeting Builder")),
                    ("description".to_string(), json!("Build the greeting message.")),
                    ("category".to_string(), json!("Text")),
                    ("nodeType".to_string(), json!("greeting_builder")),
                    (
                        "inputs".to_string(),
                        json!([
                            create_port("name", "Name", HELLO_TEXT_TYPE),
                            create_port("punctuation", "Punctuation", HELLO_TEXT_TYPE)
                        ]),
                    ),
                    (
                        "outputs".to_string(),
                        json!([create_port("text", "Greeting", HELLO_TEXT_TYPE)]),
                    ),
                    ("values".to_string(), json!({ "prefix": "Hello, " })),
                    (
                        "appearance".to_string(),
                        json!(create_appearance("#eff6ff", "#2563eb", "#1e3a8a")),
                    ),
                ]),
                width: None,
                height: None,
                style: Some(Map::from_iter([
                    ("background".to_string(), json!("#eff6ff")),
                    ("borderColor".to_string(), json!("#2563eb")),
                    ("borderRadius".to_string(), json!(18)),
                    ("borderWidth".to_string(), json!(1)),
                    ("color".to_string(), json!("#1e3a8a")),
                ])),
            },
            NodeGraphNode {
                id: "node_add".to_string(),
                node_type: "default".to_string(),
                position: Position { x: 420.0, y: 360.0 },
                data: Map::from_iter([
                    ("label".to_string(), json!("Add Numbers")),
                    (
                        "description".to_string(),
                        json!("Compute a derived lucky number."),
                    ),
                    ("category".to_string(), json!("Math")),
                    ("nodeType".to_string(), json!("math_add")),
                    (
                        "inputs".to_string(),
                        json!([
                            create_port("a", "A", DEMO_NUMBER_TYPE),
                            create_port("b", "B", DEMO_NUMBER_TYPE)
                        ]),
                    ),
                    (
                        "outputs".to_string(),
                        json!([create_port("sum", "Sum", DEMO_NUMBER_TYPE)]),
                    ),
                    ("values".to_string(), json!({})),
                    (
                        "appearance".to_string(),
                        json!(create_appearance("#fff7ed", "#f97316", "#7c2d12")),
                    ),
                ]),
                width: None,
                height: None,
                style: Some(Map::from_iter([
                    ("background".to_string(), json!("#fff7ed")),
                    ("borderColor".to_string(), json!("#f97316")),
                    ("borderRadius".to_string(), json!(18)),
                    ("borderWidth".to_string(), json!(1)),
                    ("color".to_string(), json!("#7c2d12")),
                ])),
            },
            NodeGraphNode {
                id: "node_gate".to_string(),
                node_type: "default".to_string(),
                position: Position { x: 720.0, y: 140.0 },
                data: Map::from_iter([
                    ("label".to_string(), json!("If (Text)")),
                    (
                        "description".to_string(),
                        json!("Gate the greeting pipeline with a boolean condition."),
                    ),
                    ("category".to_string(), json!("Logic")),
                    ("nodeType".to_string(), json!("if_text")),
                    (
                        "inputs".to_string(),
                        json!([
                            create_port("condition", "Condition", DEMO_BOOLEAN_TYPE),
                            create_port("whenTrue", "When True", HELLO_TEXT_TYPE),
                            create_port("whenFalse", "When False", HELLO_TEXT_TYPE)
                        ]),
                    ),
                    (
                        "outputs".to_string(),
                        json!([create_port("text", "Text", HELLO_TEXT_TYPE)]),
                    ),
                    ("values".to_string(), json!({ "fallback": "(disabled)" })),
                    (
                        "appearance".to_string(),
                        json!(create_appearance("#f0fdf4", "#22c55e", "#14532d")),
                    ),
                ]),
                width: None,
                height: None,
                style: Some(Map::from_iter([
                    ("background".to_string(), json!("#f0fdf4")),
                    ("borderColor".to_string(), json!("#22c55e")),
                    ("borderRadius".to_string(), json!(18)),
                    ("borderWidth".to_string(), json!(1)),
                    ("color".to_string(), json!("#14532d")),
                ])),
            },
            NodeGraphNode {
                id: "node_format".to_string(),
                node_type: "default".to_string(),
                position: Position { x: 980.0, y: 240.0 },
                data: Map::from_iter([
                    ("label".to_string(), json!("Text Interpolate")),
                    (
                        "description".to_string(),
                        json!("Combine typed inputs into a multi-line summary."),
                    ),
                    ("category".to_string(), json!("Text")),
                    ("nodeType".to_string(), json!("text_interpolate")),
                    (
                        "inputs".to_string(),
                        json!([
                            create_port("greeting", "Greeting", HELLO_TEXT_TYPE),
                            create_port("lucky", "Lucky Number", DEMO_NUMBER_TYPE),
                            create_port("today", "Date", DEMO_DATE_TYPE),
                            create_port("theme", "Theme Color", DEMO_COLOR_TYPE),
                            create_port("amount", "Amount", DEMO_DECIMAL_TYPE)
                        ]),
                    ),
                    (
                        "outputs".to_string(),
                        json!([create_port("text", "Text", HELLO_TEXT_TYPE)]),
                    ),
                    ("values".to_string(), json!({ "template": DEFAULT_TEMPLATE })),
                    (
                        "appearance".to_string(),
                        json!(create_appearance("#eff6ff", "#2563eb", "#1e3a8a")),
                    ),
                ]),
                width: None,
                height: None,
                style: Some(Map::from_iter([
                    ("background".to_string(), json!("#eff6ff")),
                    ("borderColor".to_string(), json!("#2563eb")),
                    ("borderRadius".to_string(), json!(18)),
                    ("borderWidth".to_string(), json!(1)),
                    ("color".to_string(), json!("#1e3a8a")),
                ])),
            },
            NodeGraphNode {
                id: "node_output".to_string(),
                node_type: "default".to_string(),
                position: Position { x: 1320.0, y: 240.0 },
                data: Map::from_iter([
                    ("label".to_string(), json!("Console Output")),
                    (
                        "description".to_string(),
                        json!("Collect the final text into the runtime result buffer."),
                    ),
                    ("category".to_string(), json!("Debug")),
                    ("nodeType".to_string(), json!("console_output")),
                    (
                        "inputs".to_string(),
                        json!([create_port("text", "Text", HELLO_TEXT_TYPE)]),
                    ),
                    ("outputs".to_string(), json!([])),
                    ("values".to_string(), json!({})),
                    (
                        "appearance".to_string(),
                        json!(create_appearance("#f0fdf4", "#16a34a", "#14532d")),
                    ),
                ]),
                width: None,
                height: None,
                style: Some(Map::from_iter([
                    ("background".to_string(), json!("#f0fdf4")),
                    ("borderColor".to_string(), json!("#16a34a")),
                    ("borderRadius".to_string(), json!(18)),
                    ("borderWidth".to_string(), json!(1)),
                    ("color".to_string(), json!("#14532d")),
                ])),
            },
        ],
        edges: vec![
            NodeGraphEdge {
                id: "edge_source_name".to_string(),
                source: "node_source".to_string(),
                target: "node_greet".to_string(),
                source_handle: Some("name".to_string()),
                target_handle: Some("name".to_string()),
                label: None,
                edge_type: None,
                animated: None,
                invalid_reason: None,
            },
            NodeGraphEdge {
                id: "edge_source_punct".to_string(),
                source: "node_source".to_string(),
                target: "node_greet".to_string(),
                source_handle: Some("punctuation".to_string()),
                target_handle: Some("punctuation".to_string()),
                label: None,
                edge_type: None,
                animated: None,
                invalid_reason: None,
            },
            NodeGraphEdge {
                id: "edge_greet_text".to_string(),
                source: "node_greet".to_string(),
                target: "node_gate".to_string(),
                source_handle: Some("text".to_string()),
                target_handle: Some("whenTrue".to_string()),
                label: None,
                edge_type: None,
                animated: None,
                invalid_reason: None,
            },
            NodeGraphEdge {
                id: "edge_source_enabled".to_string(),
                source: "node_source".to_string(),
                target: "node_gate".to_string(),
                source_handle: Some("enabled".to_string()),
                target_handle: Some("condition".to_string()),
                label: None,
                edge_type: None,
                animated: None,
                invalid_reason: None,
            },
            NodeGraphEdge {
                id: "edge_source_base".to_string(),
                source: "node_source".to_string(),
                target: "node_add".to_string(),
                source_handle: Some("baseNumber".to_string()),
                target_handle: Some("a".to_string()),
                label: None,
                edge_type: None,
                animated: None,
                invalid_reason: None,
            },
            NodeGraphEdge {
                id: "edge_source_delta".to_string(),
                source: "node_source".to_string(),
                target: "node_add".to_string(),
                source_handle: Some("delta".to_string()),
                target_handle: Some("b".to_string()),
                label: None,
                edge_type: None,
                animated: None,
                invalid_reason: None,
            },
            NodeGraphEdge {
                id: "edge_add_sum".to_string(),
                source: "node_add".to_string(),
                target: "node_format".to_string(),
                source_handle: Some("sum".to_string()),
                target_handle: Some("lucky".to_string()),
                label: None,
                edge_type: None,
                animated: None,
                invalid_reason: None,
            },
            NodeGraphEdge {
                id: "edge_gate_text".to_string(),
                source: "node_gate".to_string(),
                target: "node_format".to_string(),
                source_handle: Some("text".to_string()),
                target_handle: Some("greeting".to_string()),
                label: None,
                edge_type: None,
                animated: None,
                invalid_reason: None,
            },
            NodeGraphEdge {
                id: "edge_source_today".to_string(),
                source: "node_source".to_string(),
                target: "node_format".to_string(),
                source_handle: Some("today".to_string()),
                target_handle: Some("today".to_string()),
                label: None,
                edge_type: None,
                animated: None,
                invalid_reason: None,
            },
            NodeGraphEdge {
                id: "edge_source_theme".to_string(),
                source: "node_source".to_string(),
                target: "node_format".to_string(),
                source_handle: Some("theme".to_string()),
                target_handle: Some("theme".to_string()),
                label: None,
                edge_type: None,
                animated: None,
                invalid_reason: None,
            },
            NodeGraphEdge {
                id: "edge_source_amount".to_string(),
                source: "node_source".to_string(),
                target: "node_format".to_string(),
                source_handle: Some("amount".to_string()),
                target_handle: Some("amount".to_string()),
                label: None,
                edge_type: None,
                animated: None,
                invalid_reason: None,
            },
            NodeGraphEdge {
                id: "edge_format_text".to_string(),
                source: "node_format".to_string(),
                target: "node_output".to_string(),
                source_handle: Some("text".to_string()),
                target_handle: Some("text".to_string()),
                label: None,
                edge_type: None,
                animated: None,
                invalid_reason: None,
            },
        ],
        viewport: NodeGraphViewport {
            x: 40.0,
            y: 80.0,
            zoom: 0.85,
        },
    }
}
