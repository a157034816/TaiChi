# JavaScript NodeGraph SDK

## 快速开始

```js
import { NodeGraphClient, NodeGraphRuntime } from "./index.js";

const runtime = new NodeGraphRuntime({
  domain: "hello-world",
  clientName: "Hello World Host",
  controlBaseUrl: "http://localhost:3100/api/runtime",
  libraryVersion: "hello-world@1",
});

runtime
  .registerTypeMapping({
    canonicalId: "hello/text",
    type: "String",
    color: "#2563eb",
  })
  .registerNode({
    type: "greeting_source",
    displayName: "Greeting Source",
    description: "Create the greeting text.",
    category: "Hello World",
    outputs: [{ id: "text", label: "Text", dataType: "hello/text" }],
    fields: [
      {
        key: "name",
        label: "Name",
        placeholder: "Who should be greeted?",
        kind: "text",
        defaultValue: "World",
      },
    ],
    execute(context) {
      const name = typeof context.values.name === "string" && context.values.name.trim()
        ? context.values.name.trim()
        : "World";
      context.emit("text", `Hello, ${name}!`);
    },
  })
  .registerNode({
    type: "console_output",
    displayName: "Console Output",
    description: "Collect the greeting into the result buffer.",
    category: "Hello World",
    inputs: [{ id: "text", label: "Text", dataType: "hello/text" }],
    execute(context) {
      context.pushResult("console", context.readInput("text") ?? "Hello, World!");
    },
  });

const client = new NodeGraphClient({
  baseUrl: "http://localhost:3000",
});

await runtime.ensureRegistered(client);

const session = await client.createSession({
  runtimeId: runtime.runtimeId,
  completionWebhook: "http://localhost:3100/api/completed",
});

console.log(session.editorUrl);
```

## 执行、调试与性能分析

```js
const snapshot = await runtime.executeGraph(graph);
const debuggerSession = runtime.createDebugger(graph, {
  breakpoints: ["node_output"],
});

await debuggerSession.step();
debuggerSession.setBreakpoints([]);
await debuggerSession.continue();
```

`setBreakpoints([...])` 可以在不重建 `debuggerSession` 的前提下直接替换当前断点集合，适合给 NodeGraph 可视化调试页做“设断点 / 取消断点”交互。

`snapshot` 中会包含：

- `status`
- `pauseReason`
- `pendingNodeId`
- `results`
- `events`
- `profiler`

## 宿主侧还需要做什么

- 提供 `completionWebhook`
- 在 `controlBaseUrl` 下实现 `GET /library`，供 NodeGraph 强制刷新节点库时读取最新快照
- 如运行环境没有全局 `fetch`，在 `NodeGraphClient` 构造函数中传入 `fetch`

## 说明

- 节点库文本不会由 NodeGraph 翻译，传什么就显示什么
- `dataType` 推荐使用 canonical id，例如 `hello/text`
- 如需映射回 JS 宿主的真实类型名，可用 `registerTypeMapping`
- `runtime.ensureRegistered(client, { force: true })` 可强制重新递交节点库
