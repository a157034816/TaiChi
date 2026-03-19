# NodeGraph SDK

`SDK/NodeGraph` 提供对 `Common/NodeGraph` 服务的多语言客户端封装，目标是让业务系统可以快速发起节点图编辑流程。

## 统一契约

所有语言 SDK 都围绕以下能力展开：

- 创建编辑会话：`POST /api/sdk/sessions`
- 获取会话状态：`GET /api/sdk/sessions/{sessionId}`

多端口图在统一契约里通过边对象上的 `sourceHandle` / `targetHandle` 标识具体连接到哪个端口。

如果业务系统希望把 canonical id 映射回当前 SDK 的类型名，建议 `nodeLibraryEndpoint` 返回以下结构：

- 端口上的 `dataType` 只放跨语言共享的 canonical id，例如 `workflow/request`
- 节点库对象可额外返回 `typeMappings`，每项为 `{ canonicalId, type }`
- 每个 `canonicalId` 在单次响应里只需要提供一个当前 SDK 可识别的 `type`
- 同一个 `type` 不能映射到多个 `canonicalId`

创建会话时需要提供：

- `domain`：client 的业务域标识
- `nodeLibraryEndpoint`：NodeGraph 首次见到该 `domain` 时拉取节点库的地址
- `completionWebhook`：用户完成编辑后，NodeGraph 回调最终节点图数据的地址
- `graph`：可选；为空时表示创建新节点图，存在时表示编辑已有节点图
- `metadata`：可选；业务自定义元数据

## 目录

- `javascript`：ESM JavaScript SDK
- `dotnet`：`NodeGraphSdk` C# SDK
- `rust`：Rust crate

## 推荐接入流程

1. 业务系统准备可访问的 `nodeLibraryEndpoint` 与 `completionWebhook`。
2. 通过对应语言 SDK 调用 `createSession`。
3. 将返回的 `editorUrl` 提供给最终用户打开。
4. 等待 NodeGraph 回调 `completionWebhook`，再按业务逻辑持久化或继续处理节点图数据。
