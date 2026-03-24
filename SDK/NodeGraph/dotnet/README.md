# .NET NodeGraph SDK

## 快速开始

```csharp
using NodeGraphSdk;

var client = new NodeGraphClient(new HttpClient(), "http://localhost:3000");

var runtime = new NodeGraphRuntime(new NodeGraphRuntimeOptions
{
    Domain = "hello-world",
    ClientName = "Hello World Host",
    ControlBaseUrl = "http://localhost:3200/api/runtime",
    LibraryVersion = "hello-world@1",
});

runtime
    .RegisterTypeMapping(new TypeMappingEntry
    {
        CanonicalId = "hello/text",
        Type = typeof(string).FullName ?? nameof(String),
        Color = "#2563eb",
    })
    .RegisterNode(new NodeDefinition
    {
        Type = "greeting_source",
        DisplayName = "Greeting Source",
        Description = "Create the greeting text.",
        Category = "Hello World",
        Outputs =
        [
            new NodePortDefinition
            {
                Id = "text",
                Label = "Text",
                DataType = "hello/text",
            },
        ],
        ExecuteAsync = context =>
        {
            var name = context.Values.TryGetValue("name", out var value)
                ? Convert.ToString(value)
                : "World";
            context.Emit("text", $"Hello, {name}!");
            return Task.CompletedTask;
        },
    })
    .RegisterNode(new NodeDefinition
    {
        Type = "console_output",
        DisplayName = "Console Output",
        Description = "Collect the greeting into the result buffer.",
        Category = "Hello World",
        Inputs =
        [
            new NodePortDefinition
            {
                Id = "text",
                Label = "Text",
                DataType = "hello/text",
            },
        ],
        ExecuteAsync = context =>
        {
            context.PushResult("console", context.ReadInput("text") ?? "Hello, World!");
            return Task.CompletedTask;
        },
    });

await runtime.EnsureRegisteredAsync(client);

var session = await client.CreateSessionAsync(new CreateSessionRequest
{
    RuntimeId = runtime.RuntimeId,
    CompletionWebhook = "http://localhost:3200/api/completed",
});

Console.WriteLine(session.EditorUrl);
```

## 执行、调试与性能分析

```csharp
var snapshot = await runtime.ExecuteGraphAsync(graph);

var debugger = runtime.CreateDebugger(graph, new NodeGraphExecutionOptions
{
    Breakpoints = ["node_output"],
});

await debugger.StepAsync();
debugger.SetBreakpoints([]);
await debugger.ContinueAsync();
```

`SetBreakpoints(...)` 会在当前调试会话内整体替换断点列表，适合配合 NodeGraph 可视化调试页动态设断点/取消断点。

返回快照中包含：

- `Status`
- `PauseReason`
- `PendingNodeId`
- `Results`
- `Events`
- `Profiler`

## 宿主侧还需要做什么

- 提供 `CompletionWebhook`
- 在 `ControlBaseUrl` 下实现 `GET /library`
- 当节点库变化时，可调用 `EnsureRegisteredAsync(client, force: true)`

## 说明

- SDK 通过 `HttpClient` 调用 NodeGraph
- 节点库文本不参与 i18n
- `DataType` 推荐使用 canonical id，再用 `TypeMappingEntry` 映射回宿主类型名
