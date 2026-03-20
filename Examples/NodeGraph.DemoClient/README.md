# NodeGraph Demo Client

`Examples/NodeGraph.DemoClient` 是一个“业务侧 client”演示项目，用来完整模拟 NodeGraph 的接入方。

它同时扮演 3 个角色：

- 提供节点库接口：`/api/node-library`
- 接收编辑完成回调：`/api/completed`
- 通过 `SDK/NodeGraph/javascript` 创建编辑会话：`/api/create-session`

## 为什么它有用

启动这个 demo client 后，你可以不自己再造一套业务系统，就直接体验完整链路：

1. demo client 调用 NodeGraph 创建会话
2. NodeGraph 首次见到当前 `domain` 时，从 demo client 拉取节点库
3. 用户打开 NodeGraph 编辑器进行编辑
4. 用户提交完成后，NodeGraph 回调 demo client
5. demo client 页面实时显示最新回调结果

当前示例节点库已经内置 Blueprint 风格的多输入/多输出节点，包括分流、汇聚、审批通过/拒绝双出口，以及成功/失败双入口通知节点。

示例节点库还会返回 `typeMappings`，用于把端口 `dataType` 的 canonical id 映射回当前 JS client 的真实契约类型名。对应的示例契约类型在 `Examples/NodeGraph.DemoClient/src/contracts.mjs` 中定义。

## 启动前提

先启动 `Service/NodeGraph`：

```bash
cd Service/NodeGraph
npm install
npm run dev
```

默认假定 NodeGraph 运行在 `http://localhost:3000`。

## 启动 demo client

```bash
cd Examples/NodeGraph.DemoClient
npm start
```

默认访问地址：`http://localhost:3100`

## 一键启动联调

如果你希望直接从仓库根目录拉起 NodeGraph 与 demo client，并在启动后马上拿到一个可访问的 `editorUrl`，优先使用 `.tools` 入口：

```powershell
.tools/start-nodegraph-demo.ps1
```

如果你更习惯双击或 `cmd.exe`，也可以使用：

```bat
.tools\start-nodegraph-demo.cmd
```

这两个入口都会转调 `Examples/NodeGraph.DemoClient/scripts/interactive-demo.py`，因此会继承现有联调脚本的行为，包括：

1. 自动启动 `Service/NodeGraph` 与 demo client
2. 默认端口被占用时自动让步到新的可用端口
3. 把实际启动出来的 `NODEGRAPH_BASE_URL` 传给 demo client，保证它始终连到正确的 NodeGraph 地址
4. 自动创建一个演示 session，并在终端打印 `editorUrl`

如果你仍然希望从示例目录内启动，也可以继续运行：

```bash
cd Examples/NodeGraph.DemoClient
npm run demo:interactive
```

这个命令现在默认走 Python 版联调脚本。它会做 4 件事：

1. 启动 `Service/NodeGraph`，默认端口 `3300`
2. 启动 demo client，默认端口 `3101`
3. 自动创建一个演示 session
4. 在终端打印 `editorUrl`，等你手动打开编辑页面试玩并保存

默认流程是：

1. 运行 `npm run demo:interactive`
2. 从终端复制 `Editor URL`
3. 手动访问节点编辑页面，调整节点后点击 `Complete editing`
4. 回到 demo client 首页 `http://localhost:3101` 查看最新完成回调

脚本会持续保活，并在收到当前 session 的完成回调后在终端提示。结束时按 `Ctrl+C` 即可。

### 可选参数

你也可以覆盖默认图模式、图名称和端口：

```bash
.tools/start-nodegraph-demo.ps1 --graph-mode new --graph-name "Blank Demo Flow" --nodegraph-port 3400 --demo-port 3201
```

## 可选环境变量

参见 `.env.example`：

- `NODEGRAPH_BASE_URL`：NodeGraph 服务地址，默认 `http://localhost:3000`
- `DEMO_CLIENT_PORT`：demo client 监听端口，默认 `3100`
- `DEMO_CLIENT_HOST`：监听主机，默认 `127.0.0.1`
- `DEMO_CLIENT_BASE_URL`：demo client 对外可访问地址，默认 `http://localhost:3100`
- `DEMO_CLIENT_DOMAIN`：示例 domain，默认 `demo-workflow`

## 演示步骤

1. 打开 `http://localhost:3100`
2. 选择“新建节点图”或“编辑已有示例图”
3. 点击 `Create editor session`
4. 从页面里最新 session 区域拿到 `editorUrl`，或点击 `Open editor page`
5. 在 NodeGraph 编辑页完成编辑并点击 `Complete editing`
6. 回到 demo client 页面查看“最新完成回调”

## 验证

```bash
npm run check
npm run test
```
