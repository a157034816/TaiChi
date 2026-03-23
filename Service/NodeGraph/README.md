# NodeGraph

`Service/NodeGraph` 是一个面向 SDK 宿主的节点图编辑服务，基于 `Next.js + TypeScript + Tailwind CSS + React Flow + shadcn/ui` 实现。

## 核心能力

- SDK 宿主在运行时初始化时生成唯一 `runtimeId`，并把节点库快照通过 `POST /api/sdk/runtimes/register` 显式递交给 NodeGraph。
- NodeGraph 以 `runtimeId` 为键缓存运行时与节点库，默认缓存 `30` 分钟；同一 `runtimeId` 在缓存有效期内可以直接复用。
- 创建编辑会话时只需要提交 `runtimeId + completionWebhook + graph?`，不再使用 `domain + nodeLibraryEndpoint` 模式。
- 编辑器内可以通过 `POST /api/editor/sessions/{sessionId}/library/refresh` 强制刷新缓存；刷新后页面节点库会立刻切到最新版本。
- NodeGraph 自身 UI 支持 i18n；节点库本身不做 i18n，宿主返回什么字符串，编辑器就展示什么字符串。
- 节点执行、断点调试、性能分析由各语言 SDK 在宿主进程内完成，NodeGraph 服务本身专注于“编辑、缓存、回调”。

## 目录结构

- `src/app/api`：对 SDK 与编辑器暴露的 HTTP 路由
- `src/app/editor/[sessionId]`：节点图编辑页
- `src/components/editor`：React Flow 编辑器与左右侧面板
- `src/components/ui`：shadcn 风格 UI 组件
- `src/lib/nodegraph`：共享数据模型、schema 与节点工厂
- `src/lib/server`：运行时缓存、会话缓存、网络判断与 webhook 调度

## 运行

```bash
npm install
npm run dev
```

默认访问地址：

- 主页：`http://localhost:3000`
- 健康检查：`http://localhost:3000/api/health`

如果你想直接体验完整联调链路，可参考：

- JavaScript Demo：`Examples/NodeGraph.DemoClient.JavaScript/README.md`
- .NET Demo：`Examples/NodeGraph.DemoClient.DotNet/README.md`
- Rust Demo：`Examples/NodeGraph.DemoClient.Rust/README.md`

## 环境变量

参见 `.env.example`：

- `NODEGRAPH_PUBLIC_BASE_URL`：返回给公网 caller 的编辑 URL 基地址
- `NODEGRAPH_PRIVATE_BASE_URL`：返回给内网 caller 的编辑 URL 基地址
- `NODEGRAPH_LIBRARY_TIMEOUT_MS`：刷新节点库与代理远端字段选项时的超时时间
- `NODEGRAPH_WEBHOOK_TIMEOUT_MS`：回调 `completionWebhook` 的超时时间
- `NODEGRAPH_RUNTIME_CACHE_TTL_MS`：运行时缓存 TTL，默认 `30 * 60 * 1000`

## 生命周期

### 1. SDK 宿主初始化运行时

宿主在内存中创建运行时对象：

- 生成唯一 `runtimeId`
- 注册节点定义与 `typeMappings`
- 暴露 `controlBaseUrl`
- 声明能力：`canExecute / canDebug / canProfile`

### 2. 递交运行时与节点库

```http
POST /api/sdk/runtimes/register
```

```json
{
  "runtimeId": "rt_hello_world_001",
  "domain": "hello-world",
  "clientName": "TaiChi Hello World Host",
  "controlBaseUrl": "https://client.example.com/nodegraph/runtime",
  "libraryVersion": "hello-world@1",
  "capabilities": {
    "canExecute": true,
    "canDebug": true,
    "canProfile": true
  },
  "library": {
    "nodes": [
      {
        "type": "greeting_source",
        "displayName": "Greeting Source",
        "description": "Create the greeting text that will be sent to the output node.",
        "category": "Hello World",
        "outputs": [
          {
            "id": "text",
            "label": "Text",
            "dataType": "hello/text"
          }
        ],
        "fields": [
          {
            "key": "name",
            "label": "Name",
            "placeholder": "Who should be greeted?",
            "kind": "text",
            "defaultValue": "World"
          }
        ]
      },
      {
        "type": "console_output",
        "displayName": "Console Output",
        "description": "Collect the final greeting into the runtime result buffer.",
        "category": "Hello World",
        "inputs": [
          {
            "id": "text",
            "label": "Text",
            "dataType": "hello/text"
          }
        ]
      }
    ],
    "typeMappings": [
      {
        "canonicalId": "hello/text",
        "type": "String",
        "color": "#2563eb"
      }
    ]
  }
}
```

响应示例：

```json
{
  "runtimeId": "rt_hello_world_001",
  "cached": false,
  "expiresAt": "2026-03-21T08:30:00.000Z",
  "libraryVersion": "hello-world@1"
}
```

说明：

- `cached: true` 表示 NodeGraph 已经有同一个 `runtimeId` 的同版本缓存。
- 即使重复注册，NodeGraph 也会刷新缓存有效期；而 SDK 侧通常会在本地先按 30 分钟 TTL 跳过重复注册。
- 节点库必须非空。

### 3. 创建编辑会话

```http
POST /api/sdk/sessions
```

```json
{
  "runtimeId": "rt_hello_world_001",
  "completionWebhook": "https://client.example.com/nodegraph/completed",
  "graph": {
    "name": "Hello World Pipeline",
    "nodes": [],
    "edges": [],
    "viewport": { "x": 0, "y": 0, "zoom": 1 }
  },
  "metadata": {
    "ticketId": "HW-1001"
  }
}
```

响应示例：

```json
{
  "sessionId": "ngs_123",
  "runtimeId": "rt_hello_world_001",
  "editorUrl": "http://localhost:3000/editor/ngs_123",
  "accessType": "private"
}
```

### 4. 编辑器读取、刷新与提交

- `GET /api/sdk/sessions/{sessionId}`：获取会话当前状态
- `GET /api/editor/sessions/{sessionId}`：编辑器首次加载会话与节点库
- `GET /api/editor/sessions/{sessionId}/field-options`：代理 `select` 字段远端选项
- `POST /api/editor/sessions/{sessionId}/library/refresh`：强制刷新当前会话所依赖的运行时节点库
- `POST /api/editor/sessions/{sessionId}/complete`：提交最终节点图并触发 `completionWebhook`

强制刷新示例：

```http
POST /api/editor/sessions/{sessionId}/library/refresh
```

```json
{
  "graph": {
    "name": "Hello World Pipeline",
    "nodes": [],
    "edges": [],
    "viewport": { "x": 0, "y": 0, "zoom": 1 }
  }
}
```

返回值会包含：

- 最新 `runtime`
- 最新 `nodeLibrary`
- 最新 `typeMappings`
- `migratedGraph`：基于最新节点库迁移后的图数据

### 5. 宿主控制端点

NodeGraph 在“强制刷新节点库”时，会向宿主的 `controlBaseUrl` 发起：

```http
GET {controlBaseUrl}/library
```

宿主应返回：

```json
{
  "libraryVersion": "hello-world@2",
  "library": {
    "nodes": [],
    "typeMappings": []
  }
}
```

说明：

- `libraryVersion` 可选；不返回时会继续沿用当前缓存版本号。
- 当前 NodeGraph 服务只会用这个控制端点刷新节点库；执行、调试、性能分析仍由宿主 SDK 自己完成。

## 节点库格式

NodeGraph 推荐新的节点库全部直接输出“可显示文本”，例如：

- `displayName`
- `description`
- `category`
- 端口 `label`
- 字段 `label`
- 字段 `placeholder`

### 字段类型

`fields.kind` 支持：

- `text`
- `textarea`
- `boolean`
- `select`
- `date`
- `color`
- `int`
- `float`
- `double`
- `decimal`

补充约束：

- `select` 必须提供 `optionsEndpoint`
- `date` 使用 `YYYY-MM-DD`
- `color` 使用 `#RRGGBB`
- `decimal` 以字符串保存，避免精度丢失
- `int / float / double` 以 JSON number 保存

### 端口类型与映射

- `dataType` 推荐使用跨语言 canonical id，例如 `hello/text`
- `typeMappings` 可选，用于把 canonical id 映射回宿主运行时真实类型名
- `typeMappings.color` 可选，用于编辑器中为端口和连线着色

## 编辑器行为

- 编辑器支持浏览器本地语言切换，影响的是 NodeGraph 自身 UI 文案
- 节点库文本永远按宿主返回的原始字符串展示
- 边对象会保留 `sourceHandle / targetHandle`，用于多端口图恢复
- 本地 UI 偏好使用浏览器 `localStorage` 保存，不写入最终 `NodeGraphDocument`
- 刷新节点库后，编辑器会标记已失效的节点模板或连线，帮助用户立即发现兼容性问题

## 验证

```bash
npm run test
npm run typecheck
npm run build
```
