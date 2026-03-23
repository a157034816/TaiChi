# Rust NodeGraph SDK

## 快速开始

```rust
use nodegraph_sdk::{
    CreateSessionRequest, NodeDefinition, NodeGraphClient, NodeGraphRuntime,
    NodeGraphRuntimeOptions, NodeLibraryFieldDefinition, NodePortDefinition, TypeMappingEntry,
};
use serde_json::json;

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    let client = NodeGraphClient::new("http://localhost:3000");

    let mut runtime = NodeGraphRuntime::new(NodeGraphRuntimeOptions {
        domain: "hello-world".to_string(),
        client_name: Some("Hello World Host".to_string()),
        control_base_url: "http://localhost:3300/api/runtime".to_string(),
        library_version: "hello-world@1".to_string(),
        capabilities: None,
        runtime_id: None,
        cache_ttl: None,
        now: None,
    })?;

    runtime.register_type_mapping(TypeMappingEntry {
        canonical_id: "hello/text".to_string(),
        node_type: "String".to_string(),
        color: Some("#2563eb".to_string()),
    });

    runtime.register_node(
        NodeDefinition::new("greeting_source", "Greeting Source", "Hello World", |context| {
            let name = context
                .values
                .get("name")
                .and_then(|value| value.as_str())
                .filter(|value| !value.trim().is_empty())
                .unwrap_or("World");
            context.emit("text", json!(format!("Hello, {name}!")));
            Ok(())
        })
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
        .with_inputs(vec![NodePortDefinition {
            id: "text".to_string(),
            label: "Text".to_string(),
            data_type: Some("hello/text".to_string()),
        }]),
    );

    runtime.ensure_registered(&client, false).await?;

    let session = client
        .create_session(&CreateSessionRequest {
            runtime_id: runtime.runtime_id().to_string(),
            completion_webhook: "http://localhost:3300/api/completed".to_string(),
            graph: None,
            metadata: None,
        })
        .await?;

    println!("{}", session.editor_url);
    Ok(())
}
```

## 执行、调试与性能分析

```rust
let snapshot = runtime.execute_graph(&graph, None);

let mut debugger = runtime.create_debugger(
    &graph,
    Some(nodegraph_sdk::NodeGraphExecutionOptions {
        breakpoints: Some(std::collections::HashSet::from(["node_output".to_string()])),
        max_steps: None,
        max_wall_time: None,
    }),
);

let first_step = debugger.step();
let paused = debugger.continue_execution();
let completed = debugger.continue_execution();
```

## 宿主侧还需要做什么

- 提供 `completion_webhook`
- 在 `control_base_url` 下实现 `GET /library`
- 当节点库变化时，可再次调用 `ensure_registered(&client, true)`

## 说明

- Rust SDK 使用 `reqwest`
- 节点库文本不参与 i18n
- `data_type` 推荐使用 canonical id，再通过 `TypeMappingEntry` 映射回宿主真实类型名
