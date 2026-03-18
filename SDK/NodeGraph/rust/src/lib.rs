use reqwest::Client;
use serde::{Deserialize, Serialize};
use serde_json::Value;
use thiserror::Error;

#[derive(Debug, Serialize, Deserialize, Clone)]
pub struct NodeGraphViewport {
    pub x: f64,
    pub y: f64,
    pub zoom: f64,
}

#[derive(Debug, Serialize, Deserialize, Clone)]
pub struct Position {
    pub x: f64,
    pub y: f64,
}

#[derive(Debug, Serialize, Deserialize, Clone)]
pub struct NodeGraphNode {
    pub id: String,
    #[serde(rename = "type")]
    pub node_type: String,
    pub position: Position,
    pub data: Value,
    pub width: Option<f64>,
    pub height: Option<f64>,
    pub style: Option<Value>,
}

#[derive(Debug, Serialize, Deserialize, Clone)]
pub struct NodeGraphEdge {
    pub id: String,
    pub source: String,
    pub target: String,
    #[serde(rename = "sourceHandle")]
    pub source_handle: Option<String>,
    #[serde(rename = "targetHandle")]
    pub target_handle: Option<String>,
    pub label: Option<String>,
    #[serde(rename = "type")]
    pub edge_type: Option<String>,
    pub animated: Option<bool>,
}

#[derive(Debug, Serialize, Deserialize, Clone)]
pub struct NodeGraphDocument {
    #[serde(rename = "graphId")]
    pub graph_id: Option<String>,
    pub name: String,
    pub description: Option<String>,
    pub nodes: Vec<NodeGraphNode>,
    pub edges: Vec<NodeGraphEdge>,
    pub viewport: NodeGraphViewport,
}

#[derive(Debug, Serialize, Deserialize, Clone)]
pub struct CreateSessionRequest {
    pub domain: String,
    #[serde(rename = "clientName")]
    pub client_name: Option<String>,
    #[serde(rename = "nodeLibraryEndpoint")]
    pub node_library_endpoint: String,
    #[serde(rename = "completionWebhook")]
    pub completion_webhook: String,
    pub graph: Option<NodeGraphDocument>,
    pub metadata: Option<std::collections::HashMap<String, String>>,
}

#[derive(Debug, Serialize, Deserialize, Clone)]
pub struct CreateSessionResponse {
    #[serde(rename = "sessionId")]
    pub session_id: String,
    #[serde(rename = "editorUrl")]
    pub editor_url: String,
    #[serde(rename = "accessType")]
    pub access_type: String,
    #[serde(rename = "domainCached")]
    pub domain_cached: bool,
}

#[derive(Debug, Error)]
pub enum NodeGraphError {
    #[error("request failed: {0}")]
    Request(#[from] reqwest::Error),
    #[error("NodeGraph returned status {status}: {message}")]
    Http { status: u16, message: String },
}

#[derive(Clone)]
pub struct NodeGraphClient {
    base_url: String,
    http_client: Client,
}

impl NodeGraphClient {
    pub fn new(base_url: impl Into<String>) -> Self {
        Self {
            base_url: base_url.into().trim_end_matches('/').to_string(),
            http_client: Client::new(),
        }
    }

    pub async fn create_session(
        &self,
        request: &CreateSessionRequest,
    ) -> Result<CreateSessionResponse, NodeGraphError> {
        let response = self
            .http_client
            .post(format!("{}/api/sdk/sessions", self.base_url))
            .json(request)
            .send()
            .await?;

        if response.status().is_success() {
            return Ok(response.json::<CreateSessionResponse>().await?);
        }

        let status = response.status().as_u16();
        let message = response
            .json::<Value>()
            .await
            .ok()
            .and_then(|value| value.get("error").and_then(|value| value.as_str()).map(str::to_owned))
            .unwrap_or_else(|| "NodeGraph request failed.".to_string());

        Err(NodeGraphError::Http { status, message })
    }

    pub async fn get_session(&self, session_id: &str) -> Result<Value, NodeGraphError> {
        let response = self
            .http_client
            .get(format!("{}/api/sdk/sessions/{}", self.base_url, session_id))
            .send()
            .await?;

        if response.status().is_success() {
            return Ok(response.json::<Value>().await?);
        }

        let status = response.status().as_u16();
        let message = response
            .json::<Value>()
            .await
            .ok()
            .and_then(|value| value.get("error").and_then(|value| value.as_str()).map(str::to_owned))
            .unwrap_or_else(|| "NodeGraph request failed.".to_string());

        Err(NodeGraphError::Http { status, message })
    }
}
