# NodeGraph

`Common/NodeGraph` 是一个面向 SDK 使用者的全栈节点图编辑服务，基于 `Next.js + TypeScript + Tailwind CSS + React Flow + shadcn/ui` 实现。

## 核心能力

- SDK client 按 `domain` 发起“新建节点图”或“编辑已有节点图”请求。
- NodeGraph 首次见到某个 `domain` 时，会向 client 提供的节点库接口拉取“支持的节点库”，并把结果缓存到内存里。
- NodeGraph 会根据请求来源 IP 选择公网或内网可访问的编辑 URL。
- 用户在编辑器完成提交后，NodeGraph 会主动调用 client webhook，并回传最终节点图数据。
- 编辑器画布支持右键自定义菜单：空白处可快速添加节点，选中节点后可复制、剪切、粘贴和删除。

## 目录结构

- `src/app/api`：对 SDK 与编辑器暴露的 HTTP 路由
- `src/app/editor/[sessionId]`：节点图编辑页
- `src/components/editor`：React Flow 编辑器与左右侧面板
- `src/components/ui`：shadcn 风格 UI 组件
- `src/lib/nodegraph`：共享数据模型、schema 与节点工厂
- `src/lib/server`：domain/session 内存态服务、网络判断与 webhook 调度

## 运行

```bash
npm install
npm run dev
```

默认访问地址：

- 主页：`http://localhost:3000`
- 健康检查：`http://localhost:3000/api/health`

如果你要直接体验完整联调链路，可以在 [Examples/NodeGraph.DemoClient/README.md](../../Examples/NodeGraph.DemoClient/README.md) 使用 `npm run demo:interactive`。该脚本会启动当前服务、demo client，并自动生成一个可手动访问的 `editorUrl`。

## 环境变量

参见 `.env.example`：

- `NODEGRAPH_PUBLIC_BASE_URL`：返回给公网 caller 的编辑 URL 基地址
- `NODEGRAPH_PRIVATE_BASE_URL`：返回给内网 caller 的编辑 URL 基地址
- `NODEGRAPH_LIBRARY_TIMEOUT_MS`：拉取 client 节点库的超时时间
- `NODEGRAPH_WEBHOOK_TIMEOUT_MS`：回调 client webhook 的超时时间

## API 概览

### `POST /api/sdk/sessions`

创建编辑会话。请求体至少包含：

```json
{
  "domain": "erp-workflow",
  "nodeLibraryEndpoint": "https://client.example.com/nodegraph/library",
  "completionWebhook": "https://client.example.com/nodegraph/completed",
  "graph": {
    "name": "审批流程",
    "nodes": [],
    "edges": [],
    "viewport": { "x": 0, "y": 0, "zoom": 1 }
  }
}
```

多端口图可在边对象中额外携带 `sourceHandle` / `targetHandle`，用于记录具体连接到哪个输入或输出端口。

### `GET /api/sdk/sessions/{sessionId}`

获取会话当前状态与节点图数据。

### `GET /api/editor/sessions/{sessionId}`

供编辑页读取初始会话数据与对应 domain 节点库。

### `POST /api/editor/sessions/{sessionId}/complete`

提交最终节点图，并触发 completion webhook。

## client 节点库返回格式

client 的 `nodeLibraryEndpoint` 支持以下两种返回：

```json
[
  {
    "type": "approval",
    "label": "Approval",
    "description": "A manual approval step",
    "category": "workflow",
    "inputs": [{ "id": "request", "label": "Request" }],
    "outputs": [
      { "id": "approved", "label": "Approved" },
      { "id": "rejected", "label": "Rejected" }
    ]
  }
]
```

或：

```json
{
  "nodes": [
    {
      "type": "approval",
      "label": "Approval",
      "description": "A manual approval step",
      "category": "workflow",
      "inputs": [{ "id": "request", "label": "Request" }],
      "outputs": [
        { "id": "approved", "label": "Approved" },
        { "id": "rejected", "label": "Rejected" }
      ]
    }
  ]
}
```

说明：

- `inputs` / `outputs` 省略时，NodeGraph 会兼容回退为单输入和单输出。
- `inputs: []` 或 `outputs: []` 表示该方向没有可连接端口。

## 验证

```bash
npm run lint
npm run typecheck
npm run test
npm run build
```
