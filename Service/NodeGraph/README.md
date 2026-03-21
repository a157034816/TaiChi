# NodeGraph

`Service/NodeGraph` 是一个面向 SDK 使用者的全栈节点图编辑服务，基于 `Next.js + TypeScript + Tailwind CSS + React Flow + shadcn/ui` 实现。

## 核心能力

- SDK client 按 `domain` 发起“新建节点图”或“编辑已有节点图”请求。
- NodeGraph 首次见到某个 `domain` 时，会向 client 提供的节点库接口拉取“支持的节点库”，并把结果缓存到内存里。
- NodeGraph 会根据请求来源 IP 选择公网或内网可访问的编辑 URL。
- 用户在编辑器完成提交后，NodeGraph 会主动调用 client webhook，并回传最终节点图数据。
- 编辑器画布支持右键自定义菜单：空白处可快速添加节点，选中节点后可复制、剪切、粘贴和删除。
- 从 input 或 output 把连接线拖到空白处时，会弹出兼容节点创建菜单，并在创建后自动补齐这条连接。
- 编辑器支持浏览器本地全局偏好：可切换连接线风格（如贝塞尔曲线、平滑折线、直线、阶梯折线）与界面语言。
- 编辑器内置简体中文 / English 双语切换，默认简体中文；节点库标签、描述、分类、端口与字段模板展示也会随语言切换。

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

如果你要直接体验完整联调链路，可以在 [Examples/NodeGraph.DemoClient.JavaScript/README.md](../../Examples/NodeGraph.DemoClient.JavaScript/README.md) 使用 `npm run demo:interactive`。该脚本会启动当前服务、demo client，并自动生成一个可手动访问的 `editorUrl`。

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

### `GET /api/editor/sessions/{sessionId}/field-options`

供编辑器内部为 `select` 字段代理远端选项数据。请求参数：

- `nodeType`：当前节点类型
- `fieldKey`：字段 key
- `locale`：当前编辑器语言

### `POST /api/editor/sessions/{sessionId}/complete`

提交最终节点图，并触发 completion webhook。

## client 节点库返回格式

client 的 `nodeLibraryEndpoint` 必须返回如下 envelope：

```json
{
  "nodes": [
    {
      "type": "approval",
      "labelKey": "nodes.approval.label",
      "descriptionKey": "nodes.approval.description",
      "categoryKey": "categories.workflow",
      "inputs": [
        {
          "id": "request",
          "labelKey": "ports.request",
          "dataType": "workflow/request"
        }
      ],
      "outputs": [
        {
          "id": "approved",
          "labelKey": "ports.approved",
          "dataType": "workflow/approval-decision"
        },
        {
          "id": "rejected",
          "labelKey": "ports.rejected",
          "dataType": "workflow/approval-decision"
        }
      ],
      "fields": [
        {
          "key": "priority",
          "labelKey": "fields.priority.label",
          "placeholderKey": "fields.priority.placeholder",
          "kind": "select",
          "optionsEndpoint": "https://client.example.com/nodegraph/options/priorities"
        },
        {
          "key": "dueDate",
          "labelKey": "fields.dueDate.label",
          "kind": "date"
        },
        {
          "key": "budget",
          "labelKey": "fields.budget.label",
          "kind": "decimal",
          "defaultValue": "99.90"
        }
      ]
    }
  ],
  "i18n": {
    "defaultLocale": "zh-CN",
    "locales": {
      "zh-CN": {
        "categories.workflow": "流程",
        "fields.budget.label": "预算",
        "fields.dueDate.label": "截止日期",
        "fields.priority.label": "优先级",
        "fields.priority.placeholder": "请选择优先级",
        "nodes.approval.description": "一个人工审批步骤",
        "nodes.approval.label": "审批",
        "ports.approved": "通过",
        "ports.rejected": "驳回",
        "ports.request": "请求"
      },
      "en": {
        "categories.workflow": "Workflow",
        "fields.budget.label": "Budget",
        "fields.dueDate.label": "Due date",
        "fields.priority.label": "Priority",
        "fields.priority.placeholder": "Select a priority",
        "nodes.approval.description": "A manual approval step",
        "nodes.approval.label": "Approval",
        "ports.approved": "Approved",
        "ports.rejected": "Rejected",
        "ports.request": "Request"
      }
    }
  },
  "typeMappings": [
    {
      "canonicalId": "workflow/request",
      "type": "WorkflowRequest",
      "color": "#0ea5e9"
    },
    {
      "canonicalId": "workflow/approval-decision",
      "type": "ApprovalDecision",
      "color": "#f97316"
    }
  ]
}
```

说明：

- 节点库必须返回 `nodes + i18n`，不再接受旧的数组根节点格式。
- `labelKey` / `descriptionKey` / `categoryKey` / 端口 `labelKey` / 字段 `labelKey` 与 `placeholderKey` 统一引用 `i18n.locales` 中的扁平 key。
- `fields.kind` 支持 `text`、`textarea`、`boolean`、`select`、`date`、`color`、`int`、`float`、`double`、`decimal`。
- 旧的 `kind: "number"` 已移除，节点库需要迁移到 `int / float / double / decimal` 之一。
- `select` 字段必须提供 `optionsEndpoint`，NodeGraph 会通过 `GET /api/editor/sessions/{sessionId}/field-options` 代理请求，并自动附带 `domain`、`nodeType`、`fieldKey`、`locale` 查询参数。
- `select` 远端接口需要返回 `{ "options": [{ "value": "high", "label": "High" }] }`，其中 `label` 已经是当前语言下可直接显示的文本。
- `date` 字段的值使用 `YYYY-MM-DD`；`color` 使用 `#RRGGBB`；`decimal` 在图谱中按字符串保存以避免精度漂移；`int / float / double` 按 JSON number 保存。
- `inputs` / `outputs` 省略时，NodeGraph 会兼容回退为单输入和单输出。
- `inputs: []` 或 `outputs: []` 表示该方向没有可连接端口。
- `dataType` 为可选字段，但语义上只表示跨语言共享的 canonical id，例如 `workflow/request`、`workflow/review-task`、`workflow/approval-decision`。
- `typeMappings` 为可选字段，使用扁平数组声明 `canonicalId -> type` 的映射；这里的 `type` 只需要符合当前 SDK 或当前节点库提供方所使用的语言类型名。
- `typeMappings.color` 为可选字段，用于声明该 canonical id 在编辑器里展示端口与连线时使用的颜色，格式为 `#RRGGBB`；缺省时 NodeGraph 会按灰色展示。
- 同一个 `canonicalId` 在单次节点库响应里只需要声明一个 `type`，同一个 `type` 也不能映射到多个 `canonicalId`。
- 只要返回了 `typeMappings`，节点库中所有端口上出现的 `dataType` 都必须能在 `typeMappings.canonicalId` 中找到对应项；旧节点库如果不返回 `typeMappings`，仍然可以继续使用。
- 编辑器会在拖线到空白处时使用 `dataType` 过滤兼容节点，并在创建后自动选择匹配的对侧端口；NodeGraph 不会解析 `.NET List<T>`、Rust `Vec<T>` 这类语言类型字符串。
- 若端口未声明 `dataType`，编辑器会回退使用端口 `id` 和端口顺序进行匹配，以兼容旧节点库。
- 编辑器本地偏好使用浏览器 `localStorage` 保存，不会写入 `NodeGraphDocument` 或 completion webhook 结果。

## 验证

```bash
npm run lint
npm run typecheck
npm run test
npm run build
```
