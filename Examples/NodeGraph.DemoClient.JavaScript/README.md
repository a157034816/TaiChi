# NodeGraph Demo Client（JavaScript）

`Examples/NodeGraph.DemoClient.JavaScript` 是一个基于 JavaScript SDK 的 Demo Showcase 宿主示例，用来完整演示：

- 运行时初始化与 `runtimeId` 生成
- 节点库内嵌输出
- 注册运行时缓存
- 创建编辑会话
- 多类型字段（`text/textarea/boolean/select/int/double/date/color/decimal`）
- 远端字段选项（`/api/runtime/field-options`，用于下拉框动态选项）
- 宿主侧执行节点图
- 断点调试与性能统计
- 同一调试会话内动态设置/取消节点级断点
- 接收编辑完成回调

## 提供的接口

- `GET /`：Demo 首页
- `GET /api/health`：健康检查
- `GET /api/runtime/library`：输出当前运行时节点库
- `GET /api/node-library`：兼容别名，返回与 `/api/runtime/library` 相同的数据
- `GET /api/runtime/field-options`：远端字段选项（当前仅 `demo_source.punctuation` 返回 4 个标点选项）
- `POST /api/runtime/register`：触发运行时注册
- `POST /api/runtime/execute`：执行一张 Demo Showcase 图
- `POST /api/runtime/debug/sessions`：创建宿主内调试会话
- `GET /api/runtime/debug/sessions/{debugSessionId}`：读取调试会话
- `POST /api/runtime/debug/sessions/{debugSessionId}/step`：单步推进调试会话
- `POST /api/runtime/debug/sessions/{debugSessionId}/continue`：继续运行调试会话
- `PUT /api/runtime/debug/sessions/{debugSessionId}/breakpoints`：动态替换断点集合
- `DELETE /api/runtime/debug/sessions/{debugSessionId}`：关闭调试会话
- `POST /api/runtime/debug/sample`：返回一组断点调试快照
- `POST /api/create-session`：先注册运行时，再向 NodeGraph 创建编辑会话
- `POST /api/completed`：接收 NodeGraph 编辑完成回调
- `GET /api/results/latest`：查看最近一次注册、执行、调试、会话和回调状态

## 示例节点库

当前 Demo 节点库版本为 `demo-showcase@1`，包含 13 个可执行节点，用于展示节点编辑器的组合能力：

- `greeting_source`：读取 `name` 字段并输出 `Hello, {name}!`
- `console_output`：读取输入端口 `text`，把最终结果写入 `console` 输出通道
- `demo_source`：发射一组带 canonical 类型的值（文本/数字/布尔/日期/颜色/小数）
- `greeting_builder`：等待 `name/punctuation` 同时就绪后拼接问候语
- `math_add`：将 `a/b` 相加得到 `sum`
- `if_text`：根据布尔条件选择文本分支
- `text_interpolate`：把多种类型输入渲染成多行模板文本
- `const_text/const_number/const_boolean/const_date/const_color/const_decimal`：各类型常量输入节点

默认已有图（`Demo Showcase Pipeline`）执行结果为：

```json
{
  "console": [
    "Greeting: Hello, Codex!\nLucky: 12\nDate: 2026-03-21\nTheme: #2563eb\nAmount: 123.45"
  ]
}
```

节点库文本不做 i18n；Demo 返回什么字符串，NodeGraph 就展示什么字符串。

## 启动前提

先启动 `Service/NodeGraph`：

```bash
cd Service/NodeGraph
npm install
npm run dev
```

默认假定 NodeGraph 运行在 `http://localhost:3000`。

## 启动 Demo

```bash
cd Examples/NodeGraph.DemoClient.JavaScript
npm start
```

默认访问地址：`http://localhost:3100`

## 一键联调

推荐直接从仓库根目录启动：

```powershell
.tools/start-nodegraph-demo.ps1
```

或：

```bat
.tools\start-nodegraph-demo.cmd
```

它们会自动：

1. 启动 `Service/NodeGraph`
2. 启动当前 JavaScript Demo
3. 自动创建一个 Demo Showcase 编辑会话
4. 在终端打印 `editorUrl`

如果你希望从示例目录内直接启动交互联调，也可以运行：

```bash
cd Examples/NodeGraph.DemoClient.JavaScript
npm run demo:interactive
```

默认会优先尝试：

- NodeGraph：`3300`
- Demo：`3101`

若端口占用，脚本会自动顺延到可用端口。

## 演示步骤

1. 打开 Demo 首页
2. 点击 `Create editor session`
3. 打开返回的 `editorUrl`
4. 在 NodeGraph 页面里编辑 Demo Showcase 图
5. 如需刷新节点库，可在编辑页触发强制刷新
6. 点击 `Complete editing`
7. 回到 Demo 首页或访问 `GET /api/results/latest` 查看最新回调

## 可选环境变量

参见 `.env.example`：

- `NODEGRAPH_BASE_URL`：NodeGraph 服务地址，默认 `http://localhost:3000`
- `DEMO_CLIENT_PORT`：Demo 监听端口，默认 `3100`
- `DEMO_CLIENT_HOST`：监听主机，默认 `127.0.0.1`
- `DEMO_CLIENT_BASE_URL`：Demo 对外地址，默认 `http://localhost:3100`
- `DEMO_CLIENT_DOMAIN`：运行时业务域，默认 `demo-visual-playground`
- `DEMO_CLIENT_NAME`：客户端名称

## 验证

```bash
npm test
npm run check
```
