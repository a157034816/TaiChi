use std::collections::HashSet;
use std::sync::{Arc, Mutex};
use std::time::{Duration, SystemTime, UNIX_EPOCH};

use async_trait::async_trait;
use httpmock::Method::POST;
use httpmock::MockServer;
use nodegraph_sdk::{
    CreateSessionRequest, NodeDefinition, NodeGraphClient, NodeGraphDocument, NodeGraphEdge,
    NodeGraphExecutionOptions, NodeGraphNode, NodeGraphRuntime, NodeGraphRuntimeClient,
    NodeGraphRuntimeOptions, NodeGraphViewport, NodeLibraryEnvelope, NodeLibraryFieldDefinition,
    NodePortDefinition, Position, RuntimeRegistrationRequest, RuntimeRegistrationResponse,
};
use serde_json::{Map, Value, json};

fn json_map(value: Value) -> Map<String, Value> {
    value.as_object().cloned().expect("expected json object")
}

fn create_hello_runtime(
    now: Option<Arc<dyn Fn() -> SystemTime + Send + Sync>>,
) -> NodeGraphRuntime {
    let mut runtime = NodeGraphRuntime::new(NodeGraphRuntimeOptions {
        domain: "hello-world".into(),
        client_name: Some("Hello Runtime Host".into()),
        control_base_url: "http://127.0.0.1:4310/runtime".into(),
        library_version: "hello-world@1".into(),
        capabilities: None,
        runtime_id: Some("rt_demo_001".into()),
        cache_ttl: Some(Duration::from_secs(30 * 60)),
        now,
    })
    .expect("runtime should be created");

    runtime.register_node(
        NodeDefinition::new(
            "greeting_source",
            "Greeting Source",
            "Hello World",
            |context| {
                let name = context
                    .values
                    .get("name")
                    .and_then(Value::as_str)
                    .unwrap_or("World");
                context.emit("text", json!(format!("Hello, {name}!")));
                Ok(())
            },
        )
        .with_description("Create the base greeting text.")
        .with_outputs(vec![NodePortDefinition {
            id: "text".into(),
            label: "Text".into(),
            data_type: Some("hello/text".into()),
        }])
        .with_fields(vec![NodeLibraryFieldDefinition {
            key: "name".into(),
            label: "Name".into(),
            placeholder: None,
            kind: "text".into(),
            default_value: Some(json!("World")),
            options_endpoint: None,
        }]),
    );

    runtime.register_node(
        NodeDefinition::new(
            "console_output",
            "Console Output",
            "Hello World",
            |context| {
                if let Some(value) = context.read_input("text") {
                    context.push_result("console", value);
                }
                Ok(())
            },
        )
        .with_description("Write the greeting to the host result buffer.")
        .with_inputs(vec![NodePortDefinition {
            id: "text".into(),
            label: "Text".into(),
            data_type: Some("hello/text".into()),
        }]),
    );

    runtime
}

fn create_hello_graph() -> NodeGraphDocument {
    NodeGraphDocument {
        graph_id: None,
        name: "Hello World".into(),
        description: None,
        nodes: vec![
            NodeGraphNode {
                id: "node_source".into(),
                node_type: "default".into(),
                position: Position { x: 0.0, y: 0.0 },
                data: json_map(json!({
                    "label": "Greeting Source",
                    "nodeType": "greeting_source",
                    "outputs": [
                        {
                            "id": "text",
                            "label": "Text",
                            "dataType": "hello/text"
                        }
                    ],
                    "values": {
                        "name": "Codex"
                    }
                })),
                width: None,
                height: None,
                style: None,
            },
            NodeGraphNode {
                id: "node_output".into(),
                node_type: "default".into(),
                position: Position { x: 280.0, y: 0.0 },
                data: json_map(json!({
                    "label": "Console Output",
                    "nodeType": "console_output",
                    "inputs": [
                        {
                            "id": "text",
                            "label": "Text",
                            "dataType": "hello/text"
                        }
                    ],
                    "values": {}
                })),
                width: None,
                height: None,
                style: None,
            },
        ],
        edges: vec![NodeGraphEdge {
            id: "edge_source_output".into(),
            source: "node_source".into(),
            target: "node_output".into(),
            source_handle: Some("text".into()),
            target_handle: Some("text".into()),
            label: None,
            edge_type: None,
            animated: None,
            invalid_reason: None,
        }],
        viewport: NodeGraphViewport {
            x: 0.0,
            y: 0.0,
            zoom: 1.0,
        },
    }
}

struct RecordingRuntimeClient {
    calls: Arc<Mutex<Vec<RuntimeRegistrationRequest>>>,
}

#[async_trait]
impl NodeGraphRuntimeClient for RecordingRuntimeClient {
    async fn register_runtime(
        &self,
        request: &RuntimeRegistrationRequest,
    ) -> Result<RuntimeRegistrationResponse, nodegraph_sdk::NodeGraphError> {
        self.calls.lock().expect("lock").push(request.clone());
        Ok(RuntimeRegistrationResponse {
            runtime_id: request.runtime_id.clone(),
            cached: false,
            expires_at: "2026-03-21T00:30:00.000Z".into(),
            library_version: request.library_version.clone(),
        })
    }
}

#[tokio::test]
async fn client_registers_runtime_and_creates_session_with_runtime_id() {
    let server = MockServer::start_async().await;
    let register_mock = server
        .mock_async(|when, then| {
            when.method(POST).path("/api/sdk/runtimes/register");
            then.status(201).json_body(json!({
                "runtimeId": "rt_demo_001",
                "cached": false,
                "expiresAt": "2026-03-21T00:30:00.000Z",
                "libraryVersion": "hello-world@1"
            }));
        })
        .await;
    let session_mock = server
        .mock_async(|when, then| {
            when.method(POST)
                .path("/api/sdk/sessions")
                .json_body(json!({
                    "runtimeId": "rt_demo_001",
                    "completionWebhook": "http://127.0.0.1:4310/api/completed"
                }));
            then.status(201).json_body(json!({
                "sessionId": "ngs_demo",
                "runtimeId": "rt_demo_001",
                "editorUrl": format!("{}/editor/ngs_demo", server.base_url()),
                "accessType": "private"
            }));
        })
        .await;
    let client = NodeGraphClient::new(server.base_url());

    client
        .register_runtime(&RuntimeRegistrationRequest {
            runtime_id: "rt_demo_001".into(),
            domain: "hello-world".into(),
            client_name: None,
            control_base_url: "http://127.0.0.1:4310/runtime".into(),
            library_version: "hello-world@1".into(),
            capabilities: None,
            library: NodeLibraryEnvelope::default(),
        })
        .await
        .expect("register runtime should succeed");

    client
        .create_session(&CreateSessionRequest {
            runtime_id: "rt_demo_001".into(),
            completion_webhook: "http://127.0.0.1:4310/api/completed".into(),
            graph: None,
            metadata: None,
        })
        .await
        .expect("create session should succeed");

    register_mock.assert_async().await;
    session_mock.assert_async().await;
}

#[tokio::test]
async fn runtime_skips_redundant_registration_within_cache_ttl() {
    let current_time = Arc::new(Mutex::new(UNIX_EPOCH + Duration::from_secs(1_742_516_800)));
    let now = {
        let current_time = Arc::clone(&current_time);
        Arc::new(move || *current_time.lock().expect("lock"))
    };
    let mut runtime = create_hello_runtime(Some(now));
    let calls = Arc::new(Mutex::new(Vec::<RuntimeRegistrationRequest>::new()));
    let client = RecordingRuntimeClient {
        calls: Arc::clone(&calls),
    };

    runtime
        .ensure_registered(&client, false)
        .await
        .expect("first registration should succeed");
    runtime
        .ensure_registered(&client, false)
        .await
        .expect("cached registration should succeed");
    *current_time.lock().expect("lock") += Duration::from_secs(31 * 60);
    runtime
        .ensure_registered(&client, false)
        .await
        .expect("expired registration should succeed");

    let calls = calls.lock().expect("lock");
    assert_eq!(calls.len(), 2);
    assert_eq!(calls[0].runtime_id, runtime.runtime_id());
    assert_eq!(calls[1].runtime_id, runtime.runtime_id());
    assert_eq!(
        runtime
            .get_library()
            .nodes
            .iter()
            .map(|node| node.node_type.clone())
            .collect::<Vec<_>>(),
        vec!["greeting_source".to_string(), "console_output".to_string()]
    );
}

#[test]
fn runtime_executes_hello_graph_and_captures_profiling() {
    let runtime = create_hello_runtime(None);

    let result = runtime.execute_graph(&create_hello_graph(), None);

    assert_eq!(result.status, "completed");
    assert_eq!(result.results["console"], vec![json!("Hello, Codex!")]);
    assert_eq!(result.profiler["node_source"].call_count, 1);
    assert_eq!(result.profiler["node_output"].call_count, 1);
    assert!(result.profiler["node_output"].total_duration_ms >= 0.0);
}

#[test]
fn runtime_debugger_pauses_on_breakpoint_and_can_continue() {
    let runtime = create_hello_runtime(None);
    let mut debug_session = runtime.create_debugger(
        &create_hello_graph(),
        Some(NodeGraphExecutionOptions {
            breakpoints: Some(HashSet::from(["node_output".to_string()])),
            max_steps: None,
            max_wall_time: None,
        }),
    );

    let first_step = debug_session.step();
    assert_eq!(first_step.status, "paused");
    assert_eq!(
        first_step
            .last_event
            .expect("last event should exist")
            .node_id,
        "node_source"
    );

    let paused = debug_session.continue_execution();
    assert_eq!(paused.status, "paused");
    assert_eq!(paused.pause_reason.as_deref(), Some("breakpoint"));
    assert_eq!(paused.pending_node_id.as_deref(), Some("node_output"));

    let completed = debug_session.continue_execution();
    assert_eq!(completed.status, "completed");
    assert_eq!(completed.results["console"], vec![json!("Hello, Codex!")]);
    assert_eq!(completed.profiler["node_output"].call_count, 1);
}
