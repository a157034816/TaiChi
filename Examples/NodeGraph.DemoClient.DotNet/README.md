# NodeGraph Demo Client（.NET）

`Examples/NodeGraph.DemoClient.DotNet` 是一个基于 `NodeGraphSdk` 的最小 Hello World 宿主。

## 提供的接口

- `GET /`：JSON 概览
- `GET /api/health`：健康检查
- `GET /api/runtime/library`：输出当前运行时节点库
- `POST /api/runtime/register`：注册运行时
- `POST /api/runtime/execute`：执行 Hello World 图
- `POST /api/runtime/debug/sample`：返回断点调试样例
- `POST /api/create-session`：创建 NodeGraph 编辑会话
- `POST /api/completed`：接收编辑完成回调
- `GET /api/results/latest`：查看最近一次状态

## 运行

先启动 `Service/NodeGraph`，然后执行：

```bash
dotnet run --project Examples/NodeGraph.DemoClient.DotNet/NodeGraph.DemoClient.DotNet.csproj
```

默认地址：`http://localhost:3200`

## 示例能力

该 Demo 内置一个真实可运行的 Hello World 节点库：

- `greeting_source`
- `console_output`

同时演示：

- `runtimeId` 初始化
- 节点库递交与缓存
- 会话创建前自动注册
- 宿主内执行
- 断点调试
- 性能统计

## 可选环境变量

- `NODEGRAPH_BASE_URL`：默认 `http://localhost:3000`
- `DEMO_CLIENT_PORT`：默认 `3200`
- `DEMO_CLIENT_HOST`：默认 `127.0.0.1`
- `DEMO_CLIENT_BASE_URL`：默认 `http://localhost:3200`
- `DEMO_CLIENT_DOMAIN`：默认 `demo-hello-world`
- `DEMO_CLIENT_NAME`：默认 `NodeGraph Demo Client (.NET)`

## 验证

```bash
dotnet build Examples/NodeGraph.DemoClient.DotNet/NodeGraph.DemoClient.DotNet.csproj
```
