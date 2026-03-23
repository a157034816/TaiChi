using System.Text.Json;
using NodeGraphSdk;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.WriteIndented = true;
});

builder.Services.AddSingleton(_ =>
{
    var config = DemoConfig.FromEnvironment();
    var runtime = HelloWorldFactory.CreateRuntime(config);
    var client = new NodeGraphClient(new HttpClient(), config.NodeGraphBaseUrl);
    return new DemoHost(config, runtime, client);
});

var app = builder.Build();
var host = app.Services.GetRequiredService<DemoHost>();

app.Urls.Clear();
app.Urls.Add(host.Config.ListenUrl);

app.MapGet("/", (DemoHost demoHost) => Results.Json(demoHost.GetOverview()));
app.MapGet("/api/health", (DemoHost demoHost) => Results.Json(demoHost.GetHealth()));
app.MapGet("/api/runtime/library", (DemoHost demoHost) => Results.Json(demoHost.GetLibraryPayload()));
app.MapGet("/api/results/latest", async (DemoHost demoHost) => Results.Json(await demoHost.GetLatestAsync()));

app.MapPost("/api/runtime/register", async (RegisterRequest? request, DemoHost demoHost) =>
{
    return Results.Json(await demoHost.RegisterRuntimeAsync(request?.Force == true));
});

app.MapPost("/api/runtime/execute", async (GraphRequest? request, DemoHost demoHost) =>
{
    return Results.Json(await demoHost.ExecuteAsync(request?.GraphMode, request?.GraphName));
});

app.MapPost("/api/runtime/debug/sample", async (GraphRequest? request, DemoHost demoHost) =>
{
    return Results.Json(await demoHost.DebugSampleAsync(request?.GraphMode, request?.GraphName));
});

app.MapPost("/api/create-session", async (CreateSessionPayload? request, DemoHost demoHost) =>
{
    return Results.Json(await demoHost.CreateSessionAsync(
        request?.GraphMode,
        request?.GraphName,
        request?.ForceRefresh == true));
});

app.MapPost("/api/completed", async (JsonElement payload, DemoHost demoHost) =>
{
    return Results.Json(await demoHost.StoreCompletionAsync(payload.Clone()));
});

app.Run();

/// <summary>
/// Demo 宿主服务的基础配置。
/// </summary>
internal sealed class DemoConfig
{
    /// <summary>
    /// NodeGraph 服务地址。
    /// </summary>
    public required string NodeGraphBaseUrl { get; init; }

    /// <summary>
    /// Demo 宿主对外地址。
    /// </summary>
    public required string DemoClientBaseUrl { get; init; }

    /// <summary>
    /// 监听地址。
    /// </summary>
    public required string ListenUrl { get; init; }

    /// <summary>
    /// Demo 使用的业务域。
    /// </summary>
    public required string DemoDomain { get; init; }

    /// <summary>
    /// Demo 客户端名称。
    /// </summary>
    public required string ClientName { get; init; }

    /// <summary>
    /// 从环境变量中构造默认配置。
    /// </summary>
    public static DemoConfig FromEnvironment()
    {
        var port = int.TryParse(Environment.GetEnvironmentVariable("DEMO_CLIENT_PORT"), out var parsedPort)
            ? parsedPort
            : 3200;
        var host = Environment.GetEnvironmentVariable("DEMO_CLIENT_HOST") ?? "127.0.0.1";
        var demoClientBaseUrl = Environment.GetEnvironmentVariable("DEMO_CLIENT_BASE_URL") ?? $"http://localhost:{port}";

        return new DemoConfig
        {
            NodeGraphBaseUrl = Environment.GetEnvironmentVariable("NODEGRAPH_BASE_URL") ?? "http://localhost:3000",
            DemoClientBaseUrl = demoClientBaseUrl.TrimEnd('/'),
            ListenUrl = $"http://{host}:{port}",
            DemoDomain = Environment.GetEnvironmentVariable("DEMO_CLIENT_DOMAIN") ?? "demo-hello-world",
            ClientName = Environment.GetEnvironmentVariable("DEMO_CLIENT_NAME") ?? "NodeGraph Demo Client (.NET)",
        };
    }
}

/// <summary>
/// Hello World 节点图与运行时工厂。
/// </summary>
internal static class HelloWorldFactory
{
    private const string LibraryVersion = "hello-world@1";
    private const string HelloTextType = "hello/text";

    /// <summary>
    /// 创建带有两个可执行节点的运行时。
    /// </summary>
    public static NodeGraphRuntime CreateRuntime(DemoConfig config)
    {
        var runtime = new NodeGraphRuntime(new NodeGraphRuntimeOptions
        {
            Domain = config.DemoDomain,
            ClientName = config.ClientName,
            ControlBaseUrl = $"{config.DemoClientBaseUrl}/api/runtime",
            LibraryVersion = LibraryVersion,
        });

        runtime.RegisterTypeMapping(new TypeMappingEntry
        {
            CanonicalId = HelloTextType,
            Type = typeof(string).FullName ?? nameof(String),
            Color = "#2563eb",
        });

        runtime.RegisterNode(new NodeDefinition
        {
            Type = "greeting_source",
            DisplayName = "Greeting Source",
            Description = "Create the greeting text that will be sent to the output node.",
            Category = "Hello World",
            Outputs =
            [
                new NodePortDefinition
                {
                    Id = "text",
                    Label = "Text",
                    DataType = HelloTextType,
                },
            ],
            Fields =
            [
                new NodeLibraryFieldDefinition
                {
                    Key = "name",
                    Label = "Name",
                    Kind = "text",
                    DefaultValue = "World",
                    Placeholder = "Who should be greeted?",
                },
            ],
            Appearance = new NodeAppearance
            {
                BgColor = "#eff6ff",
                BorderColor = "#2563eb",
                TextColor = "#1e3a8a",
            },
            ExecuteAsync = context =>
            {
                var name = context.Values.TryGetValue("name", out var value)
                    ? Convert.ToString(value)
                    : "World";
                context.Emit("text", $"Hello, {(string.IsNullOrWhiteSpace(name) ? "World" : name!.Trim())}!");
                return Task.CompletedTask;
            },
        });

        runtime.RegisterNode(new NodeDefinition
        {
            Type = "console_output",
            DisplayName = "Console Output",
            Description = "Collect the final greeting into the runtime result buffer.",
            Category = "Hello World",
            Inputs =
            [
                new NodePortDefinition
                {
                    Id = "text",
                    Label = "Text",
                    DataType = HelloTextType,
                },
            ],
            Appearance = new NodeAppearance
            {
                BgColor = "#f0fdf4",
                BorderColor = "#16a34a",
                TextColor = "#14532d",
            },
            ExecuteAsync = context =>
            {
                context.PushResult("console", context.ReadInput("text") ?? "Hello, World!");
                return Task.CompletedTask;
            },
        });

        return runtime;
    }

    /// <summary>
    /// 创建默认图或空白图。
    /// </summary>
    public static NodeGraphDocument CreateGraph(string? graphName, string? graphMode)
    {
        var mode = string.Equals(graphMode, "new", StringComparison.OrdinalIgnoreCase) ? "new" : "existing";
        var resolvedName = string.IsNullOrWhiteSpace(graphName)
            ? mode == "new" ? "Blank Hello World Graph" : "Hello World Pipeline"
            : graphName.Trim();

        if (mode == "new")
        {
            return new NodeGraphDocument
            {
                Name = resolvedName,
                Description = "Start from a blank Hello World graph.",
                Nodes = [],
                Edges = [],
                Viewport = new NodeGraphViewport { X = 0, Y = 0, Zoom = 1 },
            };
        }

        return new NodeGraphDocument
        {
            GraphId = "hello-world-demo-graph",
            Name = resolvedName,
            Description = "A runnable Hello World graph hosted by the .NET SDK demo.",
            Nodes =
            [
                new NodeGraphNode
                {
                    Id = "node_source",
                    Type = "default",
                    Position = new Position { X = 80, Y = 160 },
                    Data = new Dictionary<string, object?>
                    {
                        ["label"] = "Greeting Source",
                        ["nodeType"] = "greeting_source",
                        ["outputs"] = new object?[]
                        {
                            new Dictionary<string, object?>
                            {
                                ["id"] = "text",
                                ["label"] = "Text",
                                ["dataType"] = HelloTextType,
                            },
                        },
                        ["values"] = new Dictionary<string, object?>
                        {
                            ["name"] = "Codex",
                        },
                    },
                },
                new NodeGraphNode
                {
                    Id = "node_output",
                    Type = "default",
                    Position = new Position { X = 380, Y = 160 },
                    Data = new Dictionary<string, object?>
                    {
                        ["label"] = "Console Output",
                        ["nodeType"] = "console_output",
                        ["inputs"] = new object?[]
                        {
                            new Dictionary<string, object?>
                            {
                                ["id"] = "text",
                                ["label"] = "Text",
                                ["dataType"] = HelloTextType,
                            },
                        },
                        ["values"] = new Dictionary<string, object?>(),
                    },
                },
            ],
            Edges =
            [
                new NodeGraphEdge
                {
                    Id = "edge_source_output",
                    Source = "node_source",
                    SourceHandle = "text",
                    Target = "node_output",
                    TargetHandle = "text",
                },
            ],
            Viewport = new NodeGraphViewport { X = 40, Y = 20, Zoom = 0.95 },
        };
    }
}

/// <summary>
/// Demo 宿主的运行与状态管理器。
/// </summary>
internal sealed class DemoHost
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly NodeGraphRuntime _runtime;
    private readonly NodeGraphClient _client;
    private DemoRegistrationRecord? _lastRegistration;
    private DemoSessionRecord? _lastSession;
    private DemoExecutionRecord? _lastExecution;
    private DemoDebugRecord? _lastDebug;
    private DemoCompletionRecord? _latestCompletion;
    private readonly List<DemoCompletionRecord> _callbackHistory = [];

    public DemoHost(DemoConfig config, NodeGraphRuntime runtime, NodeGraphClient client)
    {
        Config = config;
        _runtime = runtime;
        _client = client;
    }

    /// <summary>
    /// 当前宿主配置。
    /// </summary>
    public DemoConfig Config { get; }

    /// <summary>
    /// 返回 Demo 首页所需的概览数据。
    /// </summary>
    public object GetOverview()
    {
        return new
        {
            message = "NodeGraph .NET Hello World demo host",
            runtime = BuildRuntimeInfo(),
            library = _runtime.GetLibrary(),
            sampleGraph = HelloWorldFactory.CreateGraph(null, "existing"),
            endpoints = new[]
            {
                "/api/health",
                "/api/runtime/library",
                "/api/runtime/register",
                "/api/runtime/execute",
                "/api/runtime/debug/sample",
                "/api/create-session",
                "/api/completed",
                "/api/results/latest",
            },
        };
    }

    /// <summary>
    /// 返回健康检查响应。
    /// </summary>
    public object GetHealth()
    {
        return new
        {
            status = "ok",
            service = "NodeGraph Demo Client (.NET)",
            demoClientBaseUrl = Config.DemoClientBaseUrl,
            nodeGraphBaseUrl = Config.NodeGraphBaseUrl,
            demoDomain = Config.DemoDomain,
            runtime = BuildRuntimeInfo(),
        };
    }

    /// <summary>
    /// 返回当前运行时节点库。
    /// </summary>
    public object GetLibraryPayload()
    {
        return new
        {
            runtime = BuildRuntimeInfo(),
            library = _runtime.GetLibrary(),
        };
    }

    /// <summary>
    /// 注册当前运行时并保存最近一次结果。
    /// </summary>
    public async Task<RuntimeRegistrationResponse> RegisterRuntimeAsync(bool force)
    {
        await _gate.WaitAsync();
        try
        {
            var response = await _runtime.EnsureRegisteredAsync(_client, force);
            _lastRegistration = new DemoRegistrationRecord(
                DateTimeOffset.UtcNow.ToString("O"),
                force,
                _runtime.CreateRegistrationRequest(),
                response);
            return response;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 创建编辑会话，并确保注册信息在会话前完成。
    /// </summary>
    public async Task<CreateSessionResponse> CreateSessionAsync(string? graphMode, string? graphName, bool forceRefresh)
    {
        await _gate.WaitAsync();
        try
        {
            var registration = await _runtime.EnsureRegisteredAsync(_client, forceRefresh);
            _lastRegistration = new DemoRegistrationRecord(
                DateTimeOffset.UtcNow.ToString("O"),
                forceRefresh,
                _runtime.CreateRegistrationRequest(),
                registration);

            var mode = string.Equals(graphMode, "new", StringComparison.OrdinalIgnoreCase) ? "new" : "existing";
            var resolvedName = string.IsNullOrWhiteSpace(graphName)
                ? mode == "new" ? "Blank Hello World Graph" : "Hello World Pipeline"
                : graphName.Trim();
            var response = await _client.CreateSessionAsync(new CreateSessionRequest
            {
                RuntimeId = _runtime.RuntimeId,
                CompletionWebhook = $"{Config.DemoClientBaseUrl}/api/completed",
                Graph = HelloWorldFactory.CreateGraph(resolvedName, mode),
                Metadata = new Dictionary<string, string>
                {
                    ["graphMode"] = mode,
                    ["source"] = "NodeGraph.DemoClient.DotNet.HelloWorld",
                },
            });

            _lastSession = new DemoSessionRecord(
                DateTimeOffset.UtcNow.ToString("O"),
                new DemoSessionRequest(mode, resolvedName, forceRefresh),
                registration,
                response);

            return response;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 执行 Hello World 图并返回 profiler 快照。
    /// </summary>
    public async Task<DemoExecutionRecord> ExecuteAsync(string? graphMode, string? graphName)
    {
        await _gate.WaitAsync();
        try
        {
            var graph = HelloWorldFactory.CreateGraph(graphName, graphMode);
            var snapshot = await _runtime.ExecuteGraphAsync(graph);
            _lastExecution = new DemoExecutionRecord(
                DateTimeOffset.UtcNow.ToString("O"),
                graph,
                snapshot);
            return _lastExecution;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 运行一次带断点的调试样例。
    /// </summary>
    public async Task<DemoDebugRecord> DebugSampleAsync(string? graphMode, string? graphName)
    {
        await _gate.WaitAsync();
        try
        {
            var graph = HelloWorldFactory.CreateGraph(graphName, graphMode);
            var debugger = _runtime.CreateDebugger(graph, new NodeGraphExecutionOptions
            {
                Breakpoints = ["node_output"],
            });

            var firstStep = await debugger.StepAsync();
            var paused = await debugger.ContinueAsync();
            var completed = await debugger.ContinueAsync();

            _lastDebug = new DemoDebugRecord(
                DateTimeOffset.UtcNow.ToString("O"),
                graph,
                new[] { "node_output" },
                firstStep,
                paused,
                completed);
            return _lastDebug;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 存储最近一次编辑完成回调。
    /// </summary>
    public async Task<object> StoreCompletionAsync(JsonElement payload)
    {
        await _gate.WaitAsync();
        try
        {
            var record = new DemoCompletionRecord(DateTimeOffset.UtcNow.ToString("O"), payload);
            _latestCompletion = record;
            _callbackHistory.Add(record);

            while (_callbackHistory.Count > 10)
            {
                _callbackHistory.RemoveAt(0);
            }

            return new
            {
                success = true,
                receivedAt = record.ReceivedAt,
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 返回最近一次注册、执行、调试与回调状态。
    /// </summary>
    public async Task<object> GetLatestAsync()
    {
        await _gate.WaitAsync();
        try
        {
            return new
            {
                runtime = BuildRuntimeInfo(),
                lastRegistration = _lastRegistration,
                lastSession = _lastSession,
                lastExecution = _lastExecution,
                lastDebug = _lastDebug,
                latestCompletion = _latestCompletion,
                callbackCount = _callbackHistory.Count,
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    private object BuildRuntimeInfo()
    {
        return new
        {
            runtimeId = _runtime.RuntimeId,
            domain = _runtime.Domain,
            libraryVersion = _runtime.LibraryVersion,
            controlBaseUrl = _runtime.ControlBaseUrl,
            capabilities = _runtime.Capabilities,
        };
    }
}

/// <summary>
/// 运行时注册接口的请求体。
/// </summary>
internal sealed record RegisterRequest(bool Force);

/// <summary>
/// 基于图模式和图名称的请求体。
/// </summary>
internal sealed record GraphRequest(string? GraphMode, string? GraphName);

/// <summary>
/// 创建会话时的扩展请求体。
/// </summary>
internal sealed record CreateSessionPayload(string? GraphMode, string? GraphName, bool ForceRefresh);

/// <summary>
/// 最近一次注册记录。
/// </summary>
internal sealed record DemoRegistrationRecord(
    string RegisteredAt,
    bool Force,
    RuntimeRegistrationRequest Request,
    RuntimeRegistrationResponse Response);

/// <summary>
/// 最近一次会话创建请求。
/// </summary>
internal sealed record DemoSessionRequest(
    string GraphMode,
    string GraphName,
    bool ForceRefresh);

/// <summary>
/// 最近一次会话创建记录。
/// </summary>
internal sealed record DemoSessionRecord(
    string CreatedAt,
    DemoSessionRequest Request,
    RuntimeRegistrationResponse Registration,
    CreateSessionResponse Response);

/// <summary>
/// 最近一次执行记录。
/// </summary>
internal sealed record DemoExecutionRecord(
    string ExecutedAt,
    NodeGraphDocument Graph,
    NodeGraphExecutionSnapshot Snapshot);

/// <summary>
/// 最近一次断点调试记录。
/// </summary>
internal sealed record DemoDebugRecord(
    string DebuggedAt,
    NodeGraphDocument Graph,
    IReadOnlyList<string> Breakpoints,
    NodeGraphExecutionSnapshot FirstStep,
    NodeGraphExecutionSnapshot Paused,
    NodeGraphExecutionSnapshot Completed);

/// <summary>
/// 最近一次编辑完成回调记录。
/// </summary>
internal sealed record DemoCompletionRecord(
    string ReceivedAt,
    JsonElement Payload);
