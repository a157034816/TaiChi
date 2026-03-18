# .NET NodeGraph SDK

```csharp
using NodeGraphSdk;

var httpClient = new HttpClient();
var client = new NodeGraphClient(httpClient, "http://localhost:3000");

var session = await client.CreateSessionAsync(new CreateSessionRequest
{
    Domain = "erp-workflow",
    ClientName = "TaiChi ERP",
    NodeLibraryEndpoint = "https://client.example.com/nodegraph/library",
    CompletionWebhook = "https://client.example.com/nodegraph/completed",
    Graph = new NodeGraphDocument
    {
        Name = "审批流程",
        Viewport = new NodeGraphViewport { X = 0, Y = 0, Zoom = 1 }
    }
});

Console.WriteLine(session.EditorUrl);
```

说明：

- SDK 通过 `HttpClient` 调用 NodeGraph HTTP API。
- 编辑完成后的结果回传仍由业务方自己的 webhook 地址接收。
