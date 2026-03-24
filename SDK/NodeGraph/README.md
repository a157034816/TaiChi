# NodeGraph SDK

`SDK/NodeGraph` 提供 JavaScript、.NET、Rust 三套宿主 SDK，用来把业务程序接入 `Service/NodeGraph`。

## 统一接入模型

所有语言 SDK 都围绕同一条链路工作：

1. 宿主程序在运行时初始化 `NodeGraphRuntime`
2. SDK 自动生成或接收一个唯一 `runtimeId`
3. 宿主注册节点定义、类型映射与运行能力
4. 宿主把节点库快照通过 `registerRuntime` 递交给 NodeGraph
5. 创建编辑会话时只提交 `runtimeId`
6. 用户完成编辑后，NodeGraph 通过 `completionWebhook` 回传最终图
7. 图执行、断点调试、性能分析继续由宿主 SDK 在本地完成

## 统一契约

### 注册运行时

```http
POST /api/sdk/runtimes/register
```

请求体核心字段：

- `runtimeId`：运行时唯一标识
- `domain`：宿主业务域
- `clientName`：宿主名称，可选
- `controlBaseUrl`：宿主控制端点基地址，用于刷新节点库
- `libraryVersion`：节点库版本号
- `capabilities`：`canExecute / canDebug / canProfile`
- `library`：节点库快照，格式为 `{ nodes, typeMappings? }`

### 创建编辑会话

```http
POST /api/sdk/sessions
```

请求体核心字段：

- `runtimeId`
- `completionWebhook`
- `graph?`
- `metadata?`

### 查询会话

```http
GET /api/sdk/sessions/{sessionId}
```

## 统一约定

### 节点库字符串

节点库不参与 i18n。宿主输出什么字符串，NodeGraph 就展示什么字符串：

- `displayName`
- `description`
- `category`
- 端口 `label`
- 字段 `label / placeholder`

### 类型映射

- 端口 `dataType` 推荐使用跨语言 canonical id，例如 `hello/text`
- 如需映射回宿主真实类型名，可额外提供 `typeMappings: [{ canonicalId, type, color? }]`

### 运行时缓存

- NodeGraph 默认按 `runtimeId` 缓存 30 分钟
- SDK 侧也会在本地维护同样的注册 TTL，避免重复递交
- 当节点库发生变化时，可强制重新注册，或在编辑器侧触发刷新缓存

### 宿主控制端点

为支持“编辑器内强制刷新节点库”，宿主应暴露：

```http
GET {controlBaseUrl}/library
```

响应格式：

```json
{
  "libraryVersion": "hello-world@2",
  "library": {
    "nodes": [],
    "typeMappings": []
  }
}
```

## SDK 能力

三套 SDK 都提供：

- `NodeGraphClient`：调用 NodeGraph HTTP API
- `NodeGraphRuntime`：管理 `runtimeId`、节点库与运行时缓存
- 图执行：在宿主进程内执行节点图
- 断点调试：单步、继续、断点暂停
- 动态断点替换：在同一个调试会话内直接替换整组断点，无需重建调试器
- 性能统计：每个节点的调用次数、耗时、平均耗时

## 目录

- `javascript`：ESM JavaScript SDK
- `dotnet`：`NodeGraphSdk` C# SDK
- `rust`：Rust crate `nodegraph-sdk`

## 示例项目

- `Examples/NodeGraph.DemoClient.JavaScript`
- `Examples/NodeGraph.DemoClient.DotNet`
- `Examples/NodeGraph.DemoClient.Rust`

这三个 Demo 都已经改造成真实可运行的 Hello World 宿主，可作为接入参考。
