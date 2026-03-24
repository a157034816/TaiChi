# NodeGraph Demo Client（Rust）

`Examples/NodeGraph.DemoClient.Rust` 是一个基于 `nodegraph-sdk` 的 Demo Showcase 宿主示例。

## 提供的接口

- `GET /`：JSON 概览
- `GET /api/health`：健康检查
- `GET /api/runtime/library`：输出当前运行时节点库
- `GET /api/runtime/field-options`：远端字段选项（当前仅 `demo_source.punctuation` 返回 4 个标点选项）
- `POST /api/runtime/register`：注册运行时
- `POST /api/runtime/execute`：执行 Demo Showcase 图
- `POST /api/runtime/debug/sessions`：创建宿主内调试会话
- `GET /api/runtime/debug/sessions/{debugSessionId}`：读取调试会话
- `POST /api/runtime/debug/sessions/{debugSessionId}/step`：单步推进调试会话
- `POST /api/runtime/debug/sessions/{debugSessionId}/continue`：继续运行调试会话
- `PUT /api/runtime/debug/sessions/{debugSessionId}/breakpoints`：动态替换断点集合
- `DELETE /api/runtime/debug/sessions/{debugSessionId}`：关闭调试会话
- `POST /api/runtime/debug/sample`：返回断点调试样例
- `POST /api/create-session`：创建 NodeGraph 编辑会话
- `POST /api/completed`：接收编辑完成回调
- `GET /api/results/latest`：查看最近一次状态

## 运行

先启动 `Service/NodeGraph`，然后执行：

```bash
cargo run --manifest-path Examples/NodeGraph.DemoClient.Rust/Cargo.toml
```

默认地址：`http://localhost:3300`

## 示例能力

该 Demo 内置版本为 `demo-showcase@1` 的可执行节点库（13 个节点），并自带一张可运行的 Showcase 图（`Demo Showcase Pipeline`）。

节点类型列表：

- `greeting_source`
- `console_output`
- `demo_source`
- `greeting_builder`
- `math_add`
- `if_text`
- `text_interpolate`
- `const_text`
- `const_number`
- `const_boolean`
- `const_date`
- `const_color`
- `const_decimal`

默认图执行结果为：

```json
{
  "console": [
    "Greeting: Hello, Codex!\nLucky: 12\nDate: 2026-03-21\nTheme: #2563eb\nAmount: 123.45"
  ]
}
```

同时演示：

- `runtimeId` 初始化
- 节点库递交与缓存
- 会话创建前自动注册
- 宿主内执行
- 断点调试
- 同一调试会话内动态设置/取消节点级断点
- 性能统计
- 远端字段选项（下拉框动态选项）

## 可选环境变量

- `NODEGRAPH_BASE_URL`：默认 `http://localhost:3000`
- `DEMO_CLIENT_PORT`：默认 `3300`
- `DEMO_CLIENT_HOST`：默认 `127.0.0.1`
- `DEMO_CLIENT_BASE_URL`：默认 `http://localhost:3300`
- `DEMO_CLIENT_DOMAIN`：默认 `demo-hello-world-rust`
- `DEMO_CLIENT_NAME`：默认 `NodeGraph Demo Client (Rust)`

## 验证

```bash
cargo test --manifest-path Examples/NodeGraph.DemoClient.Rust/Cargo.toml tests::runtime_debug_sessions_support_dynamic_breakpoint_updates -- --exact
cargo check --manifest-path Examples/NodeGraph.DemoClient.Rust/Cargo.toml
```
