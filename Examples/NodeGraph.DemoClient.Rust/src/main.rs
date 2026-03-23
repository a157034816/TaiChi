use std::collections::HashSet;
use std::env;
use std::sync::Arc;

use axum::extract::State;
use axum::http::StatusCode;
use axum::routing::{get, post};
use axum::{Json, Router};
use nodegraph_sdk::{
    CreateSessionRequest, CreateSessionResponse, NodeDefinition, NodeGraphClient, NodeGraphDocument,
    NodeGraphEdge, NodeGraphExecutionOptions, NodeGraphNode, NodeGraphRuntime,
    NodeGraphRuntimeOptions, NodeGraphViewport, NodeLibraryFieldDefinition, NodePortDefinition,
    Position, RuntimeRegistrationResponse, TypeMappingEntry,
};
use serde::Deserialize;
use serde_json::{json, Map, Value};
use tokio::net::TcpListener;
use tokio::sync::Mutex;

/// Demo 宿主共享状态。
type SharedHost = Arc<Mutex<DemoHost>>;

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
    force_refresh: Option<bool>,
}

/// Rust Demo 宿主。
struct DemoHost {
    config: DemoConfig,
    client: NodeGraphClient,
    runtime: NodeGraphRuntime,
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
            "message": "NodeGraph Rust Hello World demo host",
            "runtime": self.runtime_info(),
            "library": self.runtime.get_library(),
            "sampleGraph": create_graph(None, None),
            "endpoints": [
                "/api/health",
                "/api/runtime/library",
                "/api/runtime/register",
                "/api/runtime/execute",
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
                        "NodeGraph.DemoClient.Rust.HelloWorld".to_string(),
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

    /// 执行 Hello World 图。
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
    fn debug_sample(&mut self, graph_mode: Option<String>, graph_name: Option<String>) -> Value {
        let graph = create_graph(graph_name.as_deref(), graph_mode.as_deref());
        let mut debugger = self.runtime.create_debugger(
            &graph,
            Some(NodeGraphExecutionOptions {
                breakpoints: Some(HashSet::from(["node_output".to_string()])),
                max_steps: None,
                max_wall_time: None,
            }),
        );
        let first_step = debugger.step();
        let paused = debugger.continue_execution();
        let completed = debugger.continue_execution();

        let record = json!({
            "debuggedAt": chrono_like_now(),
            "graph": graph,
            "breakpoints": ["node_output"],
            "firstStep": first_step,
            "paused": paused,
            "completed": completed,
        });
        self.last_debug = Some(record.clone());
        record
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
        .route("/api/runtime/register", post(post_register_runtime))
        .route("/api/runtime/execute", post(post_execute))
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

/// 调试样例接口。
async fn post_debug_sample(
    State(host): State<SharedHost>,
    payload: Option<Json<GraphRequest>>,
) -> Json<Value> {
    let payload = payload.map(|body| body.0).unwrap_or_default();
    let mut host = host.lock().await;
    Json(host.debug_sample(payload.graph_mode, payload.graph_name))
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

/// 生成伪 ISO 时间戳。
fn chrono_like_now() -> String {
    format!("{:?}", std::time::SystemTime::now())
}

/// 创建 Hello World 运行时。
fn create_runtime(config: &DemoConfig) -> NodeGraphRuntime {
    let mut runtime = NodeGraphRuntime::new(NodeGraphRuntimeOptions {
        domain: config.demo_domain.clone(),
        client_name: Some(config.client_name.clone()),
        control_base_url: format!("{}/api/runtime", config.demo_client_base_url),
        library_version: "hello-world@1".to_string(),
        capabilities: None,
        runtime_id: None,
        cache_ttl: None,
        now: None,
    })
    .expect("failed to create rust demo runtime");

    runtime.register_type_mapping(TypeMappingEntry {
        canonical_id: "hello/text".to_string(),
        node_type: String::from("String"),
        color: Some("#2563eb".to_string()),
    });

    runtime.register_node(
        NodeDefinition::new("greeting_source", "Greeting Source", "Hello World", |context| {
            let name = context
                .values
                .get("name")
                .and_then(Value::as_str)
                .filter(|value| !value.trim().is_empty())
                .unwrap_or("World");
            context.emit("text", json!(format!("Hello, {name}!")));
            Ok(())
        })
        .with_description("Create the greeting text that will be sent to the output node.")
        .with_outputs(vec![NodePortDefinition {
            id: "text".to_string(),
            label: "Text".to_string(),
            data_type: Some("hello/text".to_string()),
        }])
        .with_fields(vec![NodeLibraryFieldDefinition {
            key: "name".to_string(),
            label: "Name".to_string(),
            placeholder: Some("Who should be greeted?".to_string()),
            kind: "text".to_string(),
            default_value: Some(json!("World")),
            options_endpoint: None,
        }]),
    );

    runtime.register_node(
        NodeDefinition::new("console_output", "Console Output", "Hello World", |context| {
            context.push_result(
                "console",
                context.read_input("text").unwrap_or_else(|| json!("Hello, World!")),
            );
            Ok(())
        })
        .with_description("Collect the final greeting into the runtime result buffer.")
        .with_inputs(vec![NodePortDefinition {
            id: "text".to_string(),
            label: "Text".to_string(),
            data_type: Some("hello/text".to_string()),
        }]),
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
            "Blank Hello World Graph"
        } else {
            "Hello World Pipeline"
        });

    if mode == "new" {
        return NodeGraphDocument {
            graph_id: None,
            name: resolved_name.to_string(),
            description: Some("Start from a blank Hello World graph.".to_string()),
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
        graph_id: Some("hello-world-demo-graph".to_string()),
        name: resolved_name.to_string(),
        description: Some("A runnable Hello World graph hosted by the Rust SDK demo.".to_string()),
        nodes: vec![
            NodeGraphNode {
                id: "node_source".to_string(),
                node_type: "default".to_string(),
                position: Position { x: 80.0, y: 160.0 },
                data: Map::from_iter([
                    ("label".to_string(), json!("Greeting Source")),
                    ("nodeType".to_string(), json!("greeting_source")),
                    (
                        "outputs".to_string(),
                        json!([
                            {
                                "id": "text",
                                "label": "Text",
                                "dataType": "hello/text"
                            }
                        ]),
                    ),
                    (
                        "values".to_string(),
                        json!({
                            "name": "Codex"
                        }),
                    ),
                ]),
                width: None,
                height: None,
                style: None,
            },
            NodeGraphNode {
                id: "node_output".to_string(),
                node_type: "default".to_string(),
                position: Position { x: 380.0, y: 160.0 },
                data: Map::from_iter([
                    ("label".to_string(), json!("Console Output")),
                    ("nodeType".to_string(), json!("console_output")),
                    (
                        "inputs".to_string(),
                        json!([
                            {
                                "id": "text",
                                "label": "Text",
                                "dataType": "hello/text"
                            }
                        ]),
                    ),
                    ("values".to_string(), json!({})),
                ]),
                width: None,
                height: None,
                style: None,
            },
        ],
        edges: vec![NodeGraphEdge {
            id: "edge_source_output".to_string(),
            source: "node_source".to_string(),
            target: "node_output".to_string(),
            source_handle: Some("text".to_string()),
            target_handle: Some("text".to_string()),
            label: None,
            edge_type: None,
            animated: None,
            invalid_reason: None,
        }],
        viewport: NodeGraphViewport {
            x: 40.0,
            y: 20.0,
            zoom: 0.95,
        },
    }
}
