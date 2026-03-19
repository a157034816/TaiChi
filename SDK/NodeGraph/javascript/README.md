# JavaScript NodeGraph SDK

## 用法

```js
import { NodeGraphClient } from "@taichi/nodegraph-sdk";

const client = new NodeGraphClient({
  baseUrl: "http://localhost:3000",
});

const session = await client.createSession({
  domain: "erp-workflow",
  clientName: "TaiChi ERP",
  nodeLibraryEndpoint: "https://client.example.com/nodegraph/library",
  completionWebhook: "https://client.example.com/nodegraph/completed",
  graph: {
    name: "审批流程",
    nodes: [],
    edges: [],
    viewport: { x: 0, y: 0, zoom: 1 },
  },
});

console.log(session.editorUrl);
```

## 说明

- SDK 只封装 NodeGraph 的 HTTP 调用，不代替业务方实现节点库接口与完成回调接口。
- 如果运行环境没有全局 `fetch`，请在构造函数里显式传入 `fetch` 实现。
- `nodeLibraryEndpoint` 返回的端口 `dataType` 建议使用 canonical id，例如 `workflow/request`；如需把 canonical id 映射到当前 SDK 使用的类型名，可在节点库对象里额外提供 `typeMappings: [{ canonicalId, type }]`。
