# Rust NodeGraph SDK

```rust
use nodegraph_sdk::{CreateSessionRequest, NodeGraphClient, NodeGraphDocument, NodeGraphViewport};

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    let client = NodeGraphClient::new("http://localhost:3000");

    let response = client
        .create_session(&CreateSessionRequest {
            domain: "erp-workflow".into(),
            client_name: Some("TaiChi ERP".into()),
            node_library_endpoint: "https://client.example.com/nodegraph/library".into(),
            completion_webhook: "https://client.example.com/nodegraph/completed".into(),
            graph: Some(NodeGraphDocument {
                graph_id: None,
                name: "审批流程".into(),
                description: None,
                nodes: vec![],
                edges: vec![],
                viewport: NodeGraphViewport { x: 0.0, y: 0.0, zoom: 1.0 },
            }),
            metadata: None,
        })
        .await?;

    println!("{}", response.editor_url);
    Ok(())
}
```

说明：

- Rust SDK 使用 `reqwest` 发送 HTTP 请求。
- 若要接收编辑完成 webhook，请在业务侧自行提供对应 HTTP 服务。
