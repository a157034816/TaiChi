//! Rust 版 NodeGraph SDK。
//!
//! 提供运行时注册、节点图执行、性能分析与断点调试能力。

use std::collections::{HashMap, HashSet, VecDeque};
use std::fmt;
use std::sync::{Arc, Mutex};
use std::time::{Duration, Instant, SystemTime};

use async_trait::async_trait;
use chrono::{DateTime, SecondsFormat, Utc};
use indexmap::IndexMap;
use reqwest::{Client, Method};
use serde::de::DeserializeOwned;
use serde::{Deserialize, Serialize};
use serde_json::{Map, Value};
use thiserror::Error;
use uuid::Uuid;

const DEFAULT_RUNTIME_CACHE_TTL: Duration = Duration::from_secs(30 * 60);
const DEFAULT_MAX_STEPS: usize = 1_000;
const DEFAULT_MAX_WALL_TIME: Duration = Duration::from_secs(5);
const STATUS_IDLE: &str = "idle";
const STATUS_RUNNING: &str = "running";
const STATUS_PAUSED: &str = "paused";
const STATUS_COMPLETED: &str = "completed";
const STATUS_FAILED: &str = "failed";
const STATUS_BUDGET_EXCEEDED: &str = "budget_exceeded";
const REASON_INITIAL: &str = "initial";
const REASON_MESSAGE: &str = "message";
const REASON_STEP: &str = "step";
const REASON_BREAKPOINT: &str = "breakpoint";
const REASON_ERROR: &str = "error";
const FALLBACK_PORT: &str = "__default__";

/// SDK 统一结果类型。
pub type NodeGraphResult<T> = Result<T, NodeGraphError>;

/// 节点图视口。
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize, Default)]
pub struct NodeGraphViewport {
    /// 横向偏移。
    pub x: f64,
    /// 纵向偏移。
    pub y: f64,
    /// 缩放倍率。
    pub zoom: f64,
}

/// 节点坐标。
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize, Default)]
pub struct Position {
    /// 横坐标。
    pub x: f64,
    /// 纵坐标。
    pub y: f64,
}

/// 图中的节点实例。
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize, Default)]
pub struct NodeGraphNode {
    /// 节点实例 ID。
    pub id: String,
    /// React Flow 层类型。
    #[serde(rename = "type")]
    pub node_type: String,
    /// 节点位置。
    pub position: Position,
    /// 节点数据对象。
    #[serde(default)]
    pub data: Map<String, Value>,
    /// 节点宽度。
    pub width: Option<f64>,
    /// 节点高度。
    pub height: Option<f64>,
    /// 样式对象。
    pub style: Option<Map<String, Value>>,
}

/// 图中的连线。
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct NodeGraphEdge {
    /// 连线 ID。
    pub id: String,
    /// 源节点 ID。
    pub source: String,
    /// 目标节点 ID。
    pub target: String,
    /// 源端口 ID。
    #[serde(skip_serializing_if = "Option::is_none")]
    pub source_handle: Option<String>,
    /// 目标端口 ID。
    #[serde(skip_serializing_if = "Option::is_none")]
    pub target_handle: Option<String>,
    /// 连线标签。
    #[serde(skip_serializing_if = "Option::is_none")]
    pub label: Option<String>,
    /// React Flow 边类型。
    #[serde(rename = "type")]
    #[serde(skip_serializing_if = "Option::is_none")]
    pub edge_type: Option<String>,
    /// 是否动画。
    #[serde(skip_serializing_if = "Option::is_none")]
    pub animated: Option<bool>,
    /// 失效原因。
    #[serde(skip_serializing_if = "Option::is_none")]
    pub invalid_reason: Option<String>,
}

/// 完整节点图文档。
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct NodeGraphDocument {
    /// 图 ID。
    #[serde(skip_serializing_if = "Option::is_none")]
    pub graph_id: Option<String>,
    /// 图名称。
    pub name: String,
    /// 图说明。
    #[serde(skip_serializing_if = "Option::is_none")]
    pub description: Option<String>,
    /// 节点列表。
    #[serde(default)]
    pub nodes: Vec<NodeGraphNode>,
    /// 连线列表。
    #[serde(default)]
    pub edges: Vec<NodeGraphEdge>,
    /// 视口状态。
    pub viewport: NodeGraphViewport,
}

/// 创建编辑会话请求。
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct CreateSessionRequest {
    /// 宿主运行时 ID。
    pub runtime_id: String,
    /// 完成回调地址。
    pub completion_webhook: String,
    /// 初始图。
    #[serde(skip_serializing_if = "Option::is_none")]
    pub graph: Option<NodeGraphDocument>,
    /// 业务元数据。
    #[serde(skip_serializing_if = "Option::is_none")]
    pub metadata: Option<HashMap<String, String>>,
}

/// 创建编辑会话响应。
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct CreateSessionResponse {
    /// 会话 ID。
    pub session_id: String,
    /// 运行时 ID。
    pub runtime_id: String,
    /// 编辑器地址。
    pub editor_url: String,
    /// 访问类型。
    pub access_type: String,
}

/// 运行时能力声明。
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct RuntimeCapabilities {
    /// 是否支持执行。
    pub can_execute: bool,
    /// 是否支持调试。
    pub can_debug: bool,
    /// 是否支持性能分析。
    pub can_profile: bool,
}

impl Default for RuntimeCapabilities {
    fn default() -> Self {
        Self {
            can_execute: true,
            can_debug: true,
            can_profile: true,
        }
    }
}

/// 节点端口定义。
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct NodePortDefinition {
    /// 端口 ID。
    pub id: String,
    /// 展示文本。
    pub label: String,
    /// canonical 数据类型。
    #[serde(skip_serializing_if = "Option::is_none")]
    pub data_type: Option<String>,
}

/// 节点字段定义。
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct NodeLibraryFieldDefinition {
    /// 字段键。
    pub key: String,
    /// 展示文本。
    pub label: String,
    /// 占位文本。
    #[serde(skip_serializing_if = "Option::is_none")]
    pub placeholder: Option<String>,
    /// 字段类型。
    pub kind: String,
    /// 默认值。
    #[serde(skip_serializing_if = "Option::is_none")]
    pub default_value: Option<Value>,
    /// 远端选项接口。
    #[serde(skip_serializing_if = "Option::is_none")]
    pub options_endpoint: Option<String>,
}

/// 节点外观定义。
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct NodeAppearance {
    /// 背景色。
    #[serde(skip_serializing_if = "Option::is_none")]
    pub bg_color: Option<String>,
    /// 边框色。
    #[serde(skip_serializing_if = "Option::is_none")]
    pub border_color: Option<String>,
    /// 文本色。
    #[serde(skip_serializing_if = "Option::is_none")]
    pub text_color: Option<String>,
}

/// 节点模板定义。
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct NodeLibraryItem {
    /// 节点模板类型。
    #[serde(rename = "type")]
    pub node_type: String,
    /// 展示名称。
    pub display_name: String,
    /// 描述信息。
    #[serde(skip_serializing_if = "Option::is_none")]
    pub description: Option<String>,
    /// 节点分类。
    pub category: String,
    /// 输入端口。
    #[serde(skip_serializing_if = "Option::is_none")]
    pub inputs: Option<Vec<NodePortDefinition>>,
    /// 输出端口。
    #[serde(skip_serializing_if = "Option::is_none")]
    pub outputs: Option<Vec<NodePortDefinition>>,
    /// 表单字段。
    #[serde(skip_serializing_if = "Option::is_none")]
    pub fields: Option<Vec<NodeLibraryFieldDefinition>>,
    /// 默认数据。
    #[serde(skip_serializing_if = "Option::is_none")]
    pub default_data: Option<Map<String, Value>>,
    /// 外观定义。
    #[serde(skip_serializing_if = "Option::is_none")]
    pub appearance: Option<NodeAppearance>,
}

/// 数据类型映射。
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct TypeMappingEntry {
    /// canonical id。
    pub canonical_id: String,
    /// 宿主真实类型名。
    #[serde(rename = "type")]
    pub node_type: String,
    /// 展示颜色。
    #[serde(skip_serializing_if = "Option::is_none")]
    pub color: Option<String>,
}

/// 节点库载荷。
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct NodeLibraryEnvelope {
    /// 节点模板列表。
    #[serde(default)]
    pub nodes: Vec<NodeLibraryItem>,
    /// 类型映射列表。
    #[serde(skip_serializing_if = "Option::is_none")]
    pub type_mappings: Option<Vec<TypeMappingEntry>>,
}

/// 运行时注册请求。
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct RuntimeRegistrationRequest {
    /// 运行时 ID。
    pub runtime_id: String,
    /// 业务域。
    pub domain: String,
    /// 宿主名称。
    #[serde(skip_serializing_if = "Option::is_none")]
    pub client_name: Option<String>,
    /// 控制端点基础地址。
    pub control_base_url: String,
    /// 节点库版本。
    pub library_version: String,
    /// 运行时能力。
    #[serde(skip_serializing_if = "Option::is_none")]
    pub capabilities: Option<RuntimeCapabilities>,
    /// 节点库数据。
    pub library: NodeLibraryEnvelope,
}

/// 运行时注册响应。
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct RuntimeRegistrationResponse {
    /// 运行时 ID。
    pub runtime_id: String,
    /// 是否命中缓存。
    pub cached: bool,
    /// 缓存过期时间。
    pub expires_at: String,
    /// 已缓存的节点库版本。
    pub library_version: String,
}

/// 运行时初始化参数。
pub struct NodeGraphRuntimeOptions {
    /// 业务域。
    pub domain: String,
    /// 宿主名称。
    pub client_name: Option<String>,
    /// 控制端点基础地址。
    pub control_base_url: String,
    /// 节点库版本。
    pub library_version: String,
    /// 运行时能力。
    pub capabilities: Option<RuntimeCapabilities>,
    /// 固定运行时 ID；为空时自动生成。
    pub runtime_id: Option<String>,
    /// SDK 本地缓存 TTL。
    pub cache_ttl: Option<Duration>,
    /// 当前时间提供器。
    pub now: Option<Arc<dyn Fn() -> SystemTime + Send + Sync>>,
}

/// 节点执行触发信息。
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct NodeExecutionTrigger {
    /// 触发原因。
    pub reason: String,
    /// 触发端口。
    pub port_id: Option<String>,
    /// 触发值。
    pub value: Option<Value>,
}

/// 跨步保留的节点状态。
#[derive(Clone, Default)]
pub struct NodeExecutionState {
    inner: Arc<Mutex<Map<String, Value>>>,
}

impl fmt::Debug for NodeExecutionState {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        formatter
            .debug_struct("NodeExecutionState")
            .field("snapshot", &self.snapshot())
            .finish()
    }
}

impl NodeExecutionState {
    /// 读取状态值快照。
    pub fn get(&self, key: &str) -> Option<Value> {
        self.inner
            .lock()
            .expect("state lock poisoned")
            .get(key)
            .cloned()
    }

    /// 写入状态值。
    pub fn insert(&self, key: impl Into<String>, value: Value) {
        self.inner
            .lock()
            .expect("state lock poisoned")
            .insert(key.into(), value);
    }

    /// 删除状态值。
    pub fn remove(&self, key: &str) -> Option<Value> {
        self.inner.lock().expect("state lock poisoned").remove(key)
    }

    /// 获取完整状态快照。
    pub fn snapshot(&self) -> Map<String, Value> {
        self.inner.lock().expect("state lock poisoned").clone()
    }
}

/// 节点执行上下文。
pub struct NodeExecutionContext {
    /// 当前图快照。
    pub graph: NodeGraphDocument,
    /// 当前节点实例。
    pub node: NodeGraphNode,
    /// 节点状态句柄。
    pub state: NodeExecutionState,
    /// 节点表单值。
    pub values: Map<String, Value>,
    /// 触发信息。
    pub trigger: NodeExecutionTrigger,
    inbox: Arc<Mutex<HashMap<String, HashMap<String, Vec<Value>>>>>,
    results: Arc<Mutex<HashMap<String, Vec<Value>>>>,
    queue: Arc<Mutex<VecDeque<ExecutionQueueItem>>>,
    outgoing_edges: Arc<HashMap<String, Vec<NodeGraphEdge>>>,
}

impl NodeExecutionContext {
    /// 读取当前节点输入快照。
    pub fn get_inputs(&self) -> HashMap<String, Vec<Value>> {
        self.inbox
            .lock()
            .expect("inbox lock poisoned")
            .get(&self.node.id)
            .cloned()
            .unwrap_or_default()
            .into_iter()
            .map(|(port_id, values)| {
                let normalized = if port_id == FALLBACK_PORT {
                    "default".to_string()
                } else {
                    port_id
                };
                (normalized, values)
            })
            .collect()
    }

    /// 读取指定输入端口最近一次值。
    ///
    /// 若指定端口为空，会自动回退到默认端口。
    pub fn read_input(&self, port_id: &str) -> Option<Value> {
        let inbox = self.inbox.lock().expect("inbox lock poisoned");
        let inputs = inbox.get(&self.node.id)?;
        inputs
            .get(port_id)
            .and_then(|values| values.last().cloned())
            .or_else(|| {
                inputs
                    .get(FALLBACK_PORT)
                    .and_then(|values| values.last().cloned())
            })
    }

    /// 向输出端口发射值并投递到下游队列。
    pub fn emit(&self, port_id: &str, value: Value) {
        let direct_key = create_edge_key(&self.node.id, Some(port_id));
        let fallback_key = create_edge_key(&self.node.id, None);
        let mut edges = Vec::new();

        if let Some(found) = self.outgoing_edges.get(&direct_key) {
            edges.extend(found.iter().cloned());
        }

        if let Some(found) = self.outgoing_edges.get(&fallback_key) {
            edges.extend(found.iter().cloned());
        }

        if edges.is_empty() {
            return;
        }

        let mut inbox = self.inbox.lock().expect("inbox lock poisoned");
        let mut queue = self.queue.lock().expect("queue lock poisoned");

        for edge in edges {
            let target_port = edge
                .target_handle
                .clone()
                .unwrap_or_else(|| FALLBACK_PORT.to_string());
            inbox
                .entry(edge.target.clone())
                .or_default()
                .entry(target_port)
                .or_default()
                .push(value.clone());
            queue.push_back(ExecutionQueueItem {
                node_id: edge.target,
                reason: REASON_MESSAGE.to_string(),
                port_id: edge.target_handle,
                value: Some(value.clone()),
            });
        }
    }

    /// 将结果写入宿主输出通道。
    pub fn push_result(&self, channel: &str, value: Value) {
        self.results
            .lock()
            .expect("results lock poisoned")
            .entry(channel.to_string())
            .or_default()
            .push(value);
    }
}

type NodeExecuteHandler =
    Arc<dyn Fn(&mut NodeExecutionContext) -> NodeGraphResult<()> + Send + Sync>;

/// 可执行节点定义。
#[derive(Clone)]
pub struct NodeDefinition {
    /// 节点模板类型。
    pub node_type: String,
    /// 展示名称。
    pub display_name: String,
    /// 描述文本。
    pub description: Option<String>,
    /// 节点分类。
    pub category: String,
    /// 输入端口。
    pub inputs: Vec<NodePortDefinition>,
    /// 输出端口。
    pub outputs: Vec<NodePortDefinition>,
    /// 表单字段。
    pub fields: Vec<NodeLibraryFieldDefinition>,
    /// 默认节点数据。
    pub default_data: Option<Map<String, Value>>,
    /// 外观定义。
    pub appearance: Option<NodeAppearance>,
    execute: NodeExecuteHandler,
}

impl fmt::Debug for NodeDefinition {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        formatter
            .debug_struct("NodeDefinition")
            .field("node_type", &self.node_type)
            .field("display_name", &self.display_name)
            .field("description", &self.description)
            .field("category", &self.category)
            .field("inputs", &self.inputs)
            .field("outputs", &self.outputs)
            .field("fields", &self.fields)
            .field("default_data", &self.default_data)
            .field("appearance", &self.appearance)
            .finish()
    }
}

impl NodeDefinition {
    /// 创建一个最小节点定义。
    pub fn new(
        node_type: impl Into<String>,
        display_name: impl Into<String>,
        category: impl Into<String>,
        execute: impl Fn(&mut NodeExecutionContext) -> NodeGraphResult<()> + Send + Sync + 'static,
    ) -> Self {
        Self {
            node_type: node_type.into(),
            display_name: display_name.into(),
            description: None,
            category: category.into(),
            inputs: Vec::new(),
            outputs: Vec::new(),
            fields: Vec::new(),
            default_data: None,
            appearance: None,
            execute: Arc::new(execute),
        }
    }

    /// 设置节点描述。
    pub fn with_description(mut self, description: impl Into<String>) -> Self {
        self.description = Some(description.into());
        self
    }

    /// 设置输入端口。
    pub fn with_inputs(mut self, inputs: Vec<NodePortDefinition>) -> Self {
        self.inputs = inputs;
        self
    }

    /// 设置输出端口。
    pub fn with_outputs(mut self, outputs: Vec<NodePortDefinition>) -> Self {
        self.outputs = outputs;
        self
    }

    /// 设置表单字段。
    pub fn with_fields(mut self, fields: Vec<NodeLibraryFieldDefinition>) -> Self {
        self.fields = fields;
        self
    }

    /// 设置默认节点数据。
    pub fn with_default_data(mut self, default_data: Map<String, Value>) -> Self {
        self.default_data = Some(default_data);
        self
    }

    /// 设置外观定义。
    pub fn with_appearance(mut self, appearance: NodeAppearance) -> Self {
        self.appearance = Some(appearance);
        self
    }

    fn to_library_item(&self) -> NodeLibraryItem {
        NodeLibraryItem {
            node_type: self.node_type.clone(),
            display_name: self.display_name.clone(),
            description: self.description.clone(),
            category: self.category.clone(),
            inputs: (!self.inputs.is_empty()).then_some(self.inputs.clone()),
            outputs: (!self.outputs.is_empty()).then_some(self.outputs.clone()),
            fields: (!self.fields.is_empty()).then_some(self.fields.clone()),
            default_data: self.default_data.clone(),
            appearance: self.appearance.clone(),
        }
    }

    fn execute(&self, context: &mut NodeExecutionContext) -> NodeGraphResult<()> {
        (self.execute)(context)
    }
}

/// 图执行预算与断点选项。
#[derive(Debug, Clone, Default)]
pub struct NodeGraphExecutionOptions {
    /// 断点节点集合。
    pub breakpoints: Option<HashSet<String>>,
    /// 最大步数。
    pub max_steps: Option<usize>,
    /// 最大墙钟时间。
    pub max_wall_time: Option<Duration>,
}

/// 节点性能统计。
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct NodeGraphProfilerRecord {
    /// 平均耗时，毫秒。
    pub average_duration_ms: f64,
    /// 调用次数。
    pub call_count: usize,
    /// 最近一次耗时，毫秒。
    pub last_duration_ms: f64,
    /// 累计耗时，毫秒。
    pub total_duration_ms: f64,
}

/// 节点执行事件。
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct NodeGraphRuntimeEvent {
    /// 步数。
    pub step: usize,
    /// 事件类型。
    pub kind: String,
    /// 节点实例 ID。
    pub node_id: String,
    /// 节点模板类型。
    pub node_type: Option<String>,
    /// 耗时，毫秒。
    pub duration_ms: f64,
    /// 触发原因。
    pub reason: Option<String>,
    /// 触发端口。
    pub port_id: Option<String>,
}

/// 执行或调试快照。
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct NodeGraphExecutionSnapshot {
    /// 当前状态。
    pub status: String,
    /// 暂停原因。
    pub pause_reason: Option<String>,
    /// 待执行节点 ID。
    pub pending_node_id: Option<String>,
    /// 最近一次错误信息。
    pub last_error: Option<String>,
    /// 最近一次执行事件。
    pub last_event: Option<NodeGraphRuntimeEvent>,
    /// 节点性能数据。
    #[serde(default)]
    pub profiler: HashMap<String, NodeGraphProfilerRecord>,
    /// 宿主结果通道。
    #[serde(default)]
    pub results: HashMap<String, Vec<Value>>,
    /// 全量执行事件。
    #[serde(default)]
    pub events: Vec<NodeGraphRuntimeEvent>,
}

/// SDK 统一错误。
#[derive(Debug, Error)]
pub enum NodeGraphError {
    /// HTTP 或网络错误。
    #[error("request failed: {0}")]
    Request(#[from] reqwest::Error),
    /// JSON 序列化错误。
    #[error("json failed: {0}")]
    Json(#[from] serde_json::Error),
    /// NodeGraph 返回了非 2xx 状态。
    #[error("NodeGraph returned status {status}: {message}")]
    Http { status: u16, message: String },
    /// 运行时初始化配置错误。
    #[error("{0}")]
    Configuration(String),
    /// 节点图执行时错误。
    #[error("{0}")]
    Runtime(String),
}

/// 供运行时注册使用的最小客户端接口。
#[async_trait]
pub trait NodeGraphRuntimeClient {
    /// 向 NodeGraph 注册节点库与能力。
    async fn register_runtime(
        &self,
        request: &RuntimeRegistrationRequest,
    ) -> NodeGraphResult<RuntimeRegistrationResponse>;
}

/// NodeGraph HTTP 客户端。
#[derive(Clone, Debug)]
pub struct NodeGraphClient {
    base_url: String,
    http_client: Client,
}

impl NodeGraphClient {
    /// 使用服务基础地址创建客户端。
    pub fn new(base_url: impl Into<String>) -> Self {
        Self {
            base_url: base_url.into().trim_end_matches('/').to_string(),
            http_client: Client::new(),
        }
    }

    /// 注册运行时。
    pub async fn register_runtime(
        &self,
        request: &RuntimeRegistrationRequest,
    ) -> NodeGraphResult<RuntimeRegistrationResponse> {
        self.request_json(Method::POST, "/api/sdk/runtimes/register", Some(request))
            .await
    }

    /// 创建会话。
    pub async fn create_session(
        &self,
        request: &CreateSessionRequest,
    ) -> NodeGraphResult<CreateSessionResponse> {
        self.request_json(Method::POST, "/api/sdk/sessions", Some(request))
            .await
    }

    /// 获取原始会话详情。
    pub async fn get_session(&self, session_id: &str) -> NodeGraphResult<Value> {
        let path = format!("/api/sdk/sessions/{session_id}");
        self.request_json::<Value, Value>(Method::GET, &path, None)
            .await
    }

    async fn request_json<T, B>(
        &self,
        method: Method,
        path: &str,
        body: Option<&B>,
    ) -> NodeGraphResult<T>
    where
        T: DeserializeOwned,
        B: Serialize + ?Sized,
    {
        let url = format!("{}{}", self.base_url, path);
        let mut request = self.http_client.request(method, &url);

        if let Some(payload) = body {
            request = request.json(payload);
        }

        let response = request.send().await?;
        if response.status().is_success() {
            return Ok(response.json::<T>().await?);
        }

        Err(parse_http_error(response).await)
    }
}

#[async_trait]
impl NodeGraphRuntimeClient for NodeGraphClient {
    async fn register_runtime(
        &self,
        request: &RuntimeRegistrationRequest,
    ) -> NodeGraphResult<RuntimeRegistrationResponse> {
        NodeGraphClient::register_runtime(self, request).await
    }
}

/// SDK 侧运行时。
pub struct NodeGraphRuntime {
    domain: String,
    client_name: Option<String>,
    control_base_url: String,
    library_version: String,
    capabilities: RuntimeCapabilities,
    runtime_id: String,
    cache_ttl: Duration,
    now: Arc<dyn Fn() -> SystemTime + Send + Sync>,
    last_registered_at: Option<SystemTime>,
    node_definitions: IndexMap<String, NodeDefinition>,
    type_mappings: Vec<TypeMappingEntry>,
}

impl NodeGraphRuntime {
    /// 初始化运行时。
    pub fn new(options: NodeGraphRuntimeOptions) -> NodeGraphResult<Self> {
        if options.domain.trim().is_empty() {
            return Err(NodeGraphError::Configuration(
                "NodeGraphRuntime requires a domain.".to_string(),
            ));
        }

        if options.control_base_url.trim().is_empty() {
            return Err(NodeGraphError::Configuration(
                "NodeGraphRuntime requires a control_base_url.".to_string(),
            ));
        }

        if options.library_version.trim().is_empty() {
            return Err(NodeGraphError::Configuration(
                "NodeGraphRuntime requires a library_version.".to_string(),
            ));
        }

        Ok(Self {
            domain: options.domain,
            client_name: options.client_name,
            control_base_url: options.control_base_url.trim_end_matches('/').to_string(),
            library_version: options.library_version,
            capabilities: options.capabilities.unwrap_or_default(),
            runtime_id: options
                .runtime_id
                .filter(|value| !value.trim().is_empty())
                .unwrap_or_else(build_runtime_id),
            cache_ttl: options.cache_ttl.unwrap_or(DEFAULT_RUNTIME_CACHE_TTL),
            now: options.now.unwrap_or_else(|| Arc::new(SystemTime::now)),
            last_registered_at: None,
            node_definitions: IndexMap::new(),
            type_mappings: Vec::new(),
        })
    }

    /// 返回运行时 ID。
    pub fn runtime_id(&self) -> &str {
        &self.runtime_id
    }

    /// 返回运行时对应的业务域。
    pub fn domain(&self) -> &str {
        &self.domain
    }

    /// 返回当前节点库版本。
    pub fn library_version(&self) -> &str {
        &self.library_version
    }

    /// 返回宿主控制端点基础地址。
    pub fn control_base_url(&self) -> &str {
        &self.control_base_url
    }

    /// 返回能力声明。
    pub fn capabilities(&self) -> &RuntimeCapabilities {
        &self.capabilities
    }

    /// 注册节点定义。
    pub fn register_node(&mut self, definition: NodeDefinition) -> &mut Self {
        self.node_definitions
            .insert(definition.node_type.clone(), definition);
        self
    }

    /// 注册类型映射。
    pub fn register_type_mapping(&mut self, mapping: TypeMappingEntry) -> &mut Self {
        self.type_mappings.push(mapping);
        self
    }

    /// 导出节点库快照。
    pub fn get_library(&self) -> NodeLibraryEnvelope {
        NodeLibraryEnvelope {
            nodes: self
                .node_definitions
                .values()
                .map(NodeDefinition::to_library_item)
                .collect(),
            type_mappings: (!self.type_mappings.is_empty()).then_some(self.type_mappings.clone()),
        }
    }

    /// 创建运行时注册请求。
    pub fn create_registration_request(&self) -> RuntimeRegistrationRequest {
        RuntimeRegistrationRequest {
            runtime_id: self.runtime_id.clone(),
            domain: self.domain.clone(),
            client_name: self.client_name.clone(),
            control_base_url: self.control_base_url.clone(),
            library_version: self.library_version.clone(),
            capabilities: Some(self.capabilities.clone()),
            library: self.get_library(),
        }
    }

    /// 按 30 分钟本地缓存策略向 NodeGraph 注册运行时。
    pub async fn ensure_registered<C>(
        &mut self,
        client: &C,
        force: bool,
    ) -> NodeGraphResult<RuntimeRegistrationResponse>
    where
        C: NodeGraphRuntimeClient + Sync,
    {
        let now = (self.now)();
        let should_register = force
            || self
                .last_registered_at
                .and_then(|last| now.duration_since(last).ok())
                .map(|elapsed| elapsed >= self.cache_ttl)
                .unwrap_or(true);

        if !should_register {
            let last_registered_at = self
                .last_registered_at
                .expect("cached branch requires registration timestamp");
            return Ok(RuntimeRegistrationResponse {
                runtime_id: self.runtime_id.clone(),
                cached: true,
                expires_at: format_system_time(add_duration(last_registered_at, self.cache_ttl)),
                library_version: self.library_version.clone(),
            });
        }

        let request = self.create_registration_request();
        let response = client.register_runtime(&request).await?;
        self.last_registered_at = Some(now);
        Ok(response)
    }

    /// 创建断点调试会话。
    pub fn create_debugger(
        &self,
        graph: &NodeGraphDocument,
        options: Option<NodeGraphExecutionOptions>,
    ) -> NodeGraphRuntimeDebugSession {
        NodeGraphRuntimeDebugSession::new(self, graph, options)
    }

    /// 执行整张图并返回最终快照。
    pub fn execute_graph(
        &self,
        graph: &NodeGraphDocument,
        options: Option<NodeGraphExecutionOptions>,
    ) -> NodeGraphExecutionSnapshot {
        let mut debugger = self.create_debugger(graph, options);
        debugger.continue_execution()
    }
}

/// 节点图调试会话。
pub struct NodeGraphRuntimeDebugSession {
    graph: NodeGraphDocument,
    node_definitions: IndexMap<String, NodeDefinition>,
    breakpoints: HashSet<String>,
    max_steps: usize,
    max_wall_time: Duration,
    node_map: HashMap<String, NodeGraphNode>,
    node_state: HashMap<String, NodeExecutionState>,
    inbox: Arc<Mutex<HashMap<String, HashMap<String, Vec<Value>>>>>,
    results: Arc<Mutex<HashMap<String, Vec<Value>>>>,
    outgoing_edges: Arc<HashMap<String, Vec<NodeGraphEdge>>>,
    queue: Arc<Mutex<VecDeque<ExecutionQueueItem>>>,
    started_at: Instant,
    step_count: usize,
    status: String,
    pause_reason: Option<String>,
    pending_node_id: Option<String>,
    last_error: Option<String>,
    last_event: Option<NodeGraphRuntimeEvent>,
    profiler: HashMap<String, NodeGraphProfilerRecord>,
    events: Vec<NodeGraphRuntimeEvent>,
}

impl NodeGraphRuntimeDebugSession {
    fn new(
        runtime: &NodeGraphRuntime,
        graph: &NodeGraphDocument,
        options: Option<NodeGraphExecutionOptions>,
    ) -> Self {
        let options = options.unwrap_or_default();
        let graph = graph.clone();
        let node_map = graph
            .nodes
            .iter()
            .cloned()
            .map(|node| (node.id.clone(), node))
            .collect::<HashMap<_, _>>();
        let mut outgoing_edges = HashMap::<String, Vec<NodeGraphEdge>>::new();

        for edge in &graph.edges {
            let key = create_edge_key(&edge.source, edge.source_handle.as_deref());
            outgoing_edges.entry(key).or_default().push(edge.clone());
        }

        let mut queue = VecDeque::new();
        for node in &graph.nodes {
            if get_ports(node, "inputs").is_empty() {
                queue.push_back(ExecutionQueueItem {
                    node_id: node.id.clone(),
                    reason: REASON_INITIAL.to_string(),
                    port_id: None,
                    value: None,
                });
            }
        }

        Self {
            graph,
            node_definitions: runtime.node_definitions.clone(),
            breakpoints: options.breakpoints.unwrap_or_default(),
            max_steps: options.max_steps.unwrap_or(DEFAULT_MAX_STEPS),
            max_wall_time: options.max_wall_time.unwrap_or(DEFAULT_MAX_WALL_TIME),
            node_map,
            node_state: HashMap::new(),
            inbox: Arc::new(Mutex::new(HashMap::new())),
            results: Arc::new(Mutex::new(HashMap::new())),
            outgoing_edges: Arc::new(outgoing_edges),
            queue: Arc::new(Mutex::new(queue)),
            started_at: Instant::now(),
            step_count: 0,
            status: STATUS_IDLE.to_string(),
            pause_reason: None,
            pending_node_id: None,
            last_error: None,
            last_event: None,
            profiler: HashMap::new(),
            events: Vec::new(),
        }
    }

    /// 单步执行。
    pub fn step(&mut self) -> NodeGraphExecutionSnapshot {
        self.drain(true)
    }

    /// 继续执行直到完成、命中断点或预算耗尽。
    pub fn continue_execution(&mut self) -> NodeGraphExecutionSnapshot {
        self.drain(false)
    }

    fn drain(&mut self, single_step: bool) -> NodeGraphExecutionSnapshot {
        let mut ignore_breakpoint_for_node_id = None;
        if self.status == STATUS_PAUSED
            && self.pause_reason.as_deref() == Some(REASON_BREAKPOINT)
            && self.pending_node_id.is_some()
        {
            ignore_breakpoint_for_node_id = self.pending_node_id.clone();
        }

        self.status = STATUS_RUNNING.to_string();
        self.pause_reason = None;

        while !self.queue_is_empty() {
            if !self.ensure_budget() {
                return self.build_snapshot();
            }

            let next_item = self.peek_queue_item().expect("queue should not be empty");
            if self.breakpoints.contains(&next_item.node_id)
                && ignore_breakpoint_for_node_id.as_deref() != Some(next_item.node_id.as_str())
            {
                self.status = STATUS_PAUSED.to_string();
                self.pause_reason = Some(REASON_BREAKPOINT.to_string());
                self.pending_node_id = Some(next_item.node_id);
                return self.build_snapshot();
            }

            ignore_breakpoint_for_node_id = None;
            self.step_count += 1;
            let item = self
                .queue
                .lock()
                .expect("queue lock poisoned")
                .pop_front()
                .expect("queue item should exist");

            if let Err(error) = self.execute_queue_item(item.clone()) {
                self.status = STATUS_FAILED.to_string();
                self.pause_reason = Some(REASON_ERROR.to_string());
                self.pending_node_id = Some(item.node_id);
                self.last_error = Some(error.to_string());
                return self.build_snapshot();
            }

            if single_step {
                self.status = if self.queue_is_empty() {
                    STATUS_COMPLETED.to_string()
                } else {
                    STATUS_PAUSED.to_string()
                };
                self.pause_reason = if self.queue_is_empty() {
                    None
                } else {
                    Some(REASON_STEP.to_string())
                };
                self.pending_node_id = self.peek_queue_item().map(|queue_item| queue_item.node_id);
                return self.build_snapshot();
            }
        }

        self.status = STATUS_COMPLETED.to_string();
        self.pause_reason = None;
        self.pending_node_id = None;
        self.build_snapshot()
    }

    fn ensure_budget(&mut self) -> bool {
        if self.step_count >= self.max_steps {
            self.status = STATUS_BUDGET_EXCEEDED.to_string();
            self.pause_reason = Some("maxSteps".to_string());
            return false;
        }

        if self.started_at.elapsed() > self.max_wall_time {
            self.status = STATUS_BUDGET_EXCEEDED.to_string();
            self.pause_reason = Some("maxWallTime".to_string());
            return false;
        }

        true
    }

    fn execute_queue_item(&mut self, item: ExecutionQueueItem) -> NodeGraphResult<()> {
        let node = self.node_map.get(&item.node_id).cloned().ok_or_else(|| {
            NodeGraphError::Runtime(format!(
                "NodeGraphRuntime could not find node \"{}\" in the graph.",
                item.node_id
            ))
        })?;
        let node_type = read_runtime_node_type(&node)?;
        let definition = self
            .node_definitions
            .get(&node_type)
            .cloned()
            .ok_or_else(|| {
                NodeGraphError::Runtime(format!(
                    "NodeGraphRuntime could not find a node definition for \"{}\".",
                    node_type
                ))
            })?;
        let mut context = self.create_execution_context(node.clone(), item.clone());
        let started_at = Instant::now();
        definition.execute(&mut context)?;
        let duration_ms = started_at.elapsed().as_secs_f64() * 1000.0;
        let profiler = self.profiler.entry(node.id.clone()).or_default();

        profiler.call_count += 1;
        profiler.last_duration_ms = duration_ms;
        profiler.total_duration_ms += duration_ms;
        profiler.average_duration_ms = profiler.total_duration_ms / profiler.call_count as f64;

        let event = NodeGraphRuntimeEvent {
            step: self.step_count,
            kind: "nodeExecuted".to_string(),
            node_id: node.id,
            node_type: Some(node_type),
            duration_ms,
            reason: Some(item.reason),
            port_id: item.port_id,
        };
        self.last_event = Some(event.clone());
        self.events.push(event);
        self.last_error = None;

        Ok(())
    }

    fn create_execution_context(
        &mut self,
        node: NodeGraphNode,
        item: ExecutionQueueItem,
    ) -> NodeExecutionContext {
        NodeExecutionContext {
            graph: self.graph.clone(),
            node: node.clone(),
            state: self.get_node_state(&node.id),
            values: get_values(&node),
            trigger: NodeExecutionTrigger {
                reason: item.reason,
                port_id: item.port_id,
                value: item.value,
            },
            inbox: Arc::clone(&self.inbox),
            results: Arc::clone(&self.results),
            queue: Arc::clone(&self.queue),
            outgoing_edges: Arc::clone(&self.outgoing_edges),
        }
    }

    fn get_node_state(&mut self, node_id: &str) -> NodeExecutionState {
        self.node_state
            .entry(node_id.to_string())
            .or_default()
            .clone()
    }

    fn queue_is_empty(&self) -> bool {
        self.queue.lock().expect("queue lock poisoned").is_empty()
    }

    fn peek_queue_item(&self) -> Option<ExecutionQueueItem> {
        self.queue
            .lock()
            .expect("queue lock poisoned")
            .front()
            .cloned()
    }

    fn build_snapshot(&self) -> NodeGraphExecutionSnapshot {
        NodeGraphExecutionSnapshot {
            status: self.status.clone(),
            pause_reason: self.pause_reason.clone(),
            pending_node_id: self.pending_node_id.clone(),
            last_error: self.last_error.clone(),
            last_event: self.last_event.clone(),
            profiler: self.profiler.clone(),
            results: self.results.lock().expect("results lock poisoned").clone(),
            events: self.events.clone(),
        }
    }
}

#[derive(Debug, Clone)]
struct ExecutionQueueItem {
    node_id: String,
    reason: String,
    port_id: Option<String>,
    value: Option<Value>,
}

fn create_edge_key(node_id: &str, handle_id: Option<&str>) -> String {
    format!("{node_id}::{}", handle_id.unwrap_or_default())
}

fn get_ports(node: &NodeGraphNode, key: &str) -> Vec<NodePortDefinition> {
    node.data
        .get(key)
        .and_then(Value::as_array)
        .map(|ports| {
            ports
                .iter()
                .filter_map(|port| serde_json::from_value::<NodePortDefinition>(port.clone()).ok())
                .collect()
        })
        .unwrap_or_default()
}

fn get_values(node: &NodeGraphNode) -> Map<String, Value> {
    node.data
        .get("values")
        .and_then(Value::as_object)
        .cloned()
        .unwrap_or_default()
}

fn read_runtime_node_type(node: &NodeGraphNode) -> NodeGraphResult<String> {
    node.data
        .get("nodeType")
        .and_then(Value::as_str)
        .map(str::to_owned)
        .filter(|value| !value.trim().is_empty())
        .ok_or_else(|| {
            NodeGraphError::Runtime(format!(
                "NodeGraphRuntime could not read nodeType from node \"{}\".",
                node.id
            ))
        })
}

fn build_runtime_id() -> String {
    format!("rt_{}", Uuid::new_v4())
}

fn add_duration(time: SystemTime, duration: Duration) -> SystemTime {
    time.checked_add(duration).unwrap_or(time)
}

fn format_system_time(time: SystemTime) -> String {
    let datetime: DateTime<Utc> = time.into();
    datetime.to_rfc3339_opts(SecondsFormat::Millis, true)
}

async fn parse_http_error(response: reqwest::Response) -> NodeGraphError {
    let status = response.status().as_u16();
    let payload = response.json::<Value>().await.ok();
    let message = payload
        .and_then(|value| {
            value
                .get("error")
                .and_then(Value::as_str)
                .map(str::to_owned)
        })
        .unwrap_or_else(|| "NodeGraph request failed.".to_string());

    NodeGraphError::Http { status, message }
}
