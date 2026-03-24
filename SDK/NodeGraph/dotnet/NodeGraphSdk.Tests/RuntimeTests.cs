using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using NodeGraphSdk;
using Xunit;

namespace NodeGraphSdk.Tests;

public sealed class RuntimeTests
{
    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }

    private static NodeGraphRuntime CreateHelloRuntime(Func<DateTimeOffset>? nowProvider = null)
    {
        var runtime = new NodeGraphRuntime(new NodeGraphRuntimeOptions
        {
            Domain = "hello-world",
            ClientName = "Hello Runtime Host",
            ControlBaseUrl = "http://127.0.0.1:4310/runtime",
            LibraryVersion = "hello-world@1",
            NowProvider = nowProvider,
        });

        runtime.RegisterNode(new NodeDefinition
        {
            Type = "greeting_source",
            DisplayName = "Greeting Source",
            Description = "Create the base greeting text.",
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
            Fields =
            [
                new NodeLibraryFieldDefinition
                {
                    Key = "name",
                    Label = "Name",
                    Kind = "text",
                    DefaultValue = "World",
                },
            ],
            ExecuteAsync = context =>
            {
                context.Emit("text", $"Hello, {context.Values["name"]}!");
                return Task.CompletedTask;
            },
        });

        runtime.RegisterNode(new NodeDefinition
        {
            Type = "console_output",
            DisplayName = "Console Output",
            Description = "Write the greeting to the host result buffer.",
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
                context.PushResult("console", context.ReadInput("text"));
                return Task.CompletedTask;
            },
        });

        return runtime;
    }

    private static NodeGraphDocument CreateHelloGraph()
    {
        return new NodeGraphDocument
        {
            Name = "Hello World",
            Nodes =
            [
                new NodeGraphNode
                {
                    Id = "node_source",
                    Type = "default",
                    Position = new Position(),
                    Data = new Dictionary<string, object?>
                    {
                        ["label"] = "Greeting Source",
                        ["nodeType"] = "greeting_source",
                        ["outputs"] = new[]
                        {
                            new Dictionary<string, object?>
                            {
                                ["id"] = "text",
                                ["label"] = "Text",
                                ["dataType"] = "hello/text",
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
                    Position = new Position(),
                    Data = new Dictionary<string, object?>
                    {
                        ["label"] = "Console Output",
                        ["nodeType"] = "console_output",
                        ["inputs"] = new[]
                        {
                            new Dictionary<string, object?>
                            {
                                ["id"] = "text",
                                ["label"] = "Text",
                                ["dataType"] = "hello/text",
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
            Viewport = new NodeGraphViewport(),
        };
    }

    [Fact]
    public async Task ClientRegistersRuntimeAndCreatesSessionWithRuntimeIdAsync()
    {
        var requests = new List<HttpRequestMessage>();
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            requests.Add(request);

            if (request.RequestUri!.AbsolutePath.EndsWith("/api/sdk/runtimes/register", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent(
                        "{\"runtimeId\":\"rt_demo_001\",\"cached\":false,\"expiresAt\":\"2026-03-21T00:30:00.000Z\",\"libraryVersion\":\"hello-world@1\"}",
                        Encoding.UTF8,
                        "application/json"),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(
                    "{\"sessionId\":\"ngs_demo\",\"runtimeId\":\"rt_demo_001\",\"editorUrl\":\"http://localhost:3000/editor/ngs_demo\",\"accessType\":\"private\"}",
                    Encoding.UTF8,
                    "application/json"),
            };
        }));
        var client = new NodeGraphClient(httpClient, "http://localhost:3000");

        await client.RegisterRuntimeAsync(new RuntimeRegistrationRequest
        {
            RuntimeId = "rt_demo_001",
            Domain = "hello-world",
            ControlBaseUrl = "http://127.0.0.1:4310/runtime",
            LibraryVersion = "hello-world@1",
            Library = new NodeLibraryEnvelope(),
        });

        await client.CreateSessionAsync(new CreateSessionRequest
        {
            RuntimeId = "rt_demo_001",
            CompletionWebhook = "http://127.0.0.1:4310/api/completed",
        });

        Assert.Equal(2, requests.Count);
        Assert.EndsWith("/api/sdk/runtimes/register", requests[0].RequestUri!.AbsolutePath, StringComparison.Ordinal);
        Assert.EndsWith("/api/sdk/sessions", requests[1].RequestUri!.AbsolutePath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RuntimeSkipsRedundantRegistrationWithinCacheTtlAsync()
    {
        var currentTime = new DateTimeOffset(2026, 3, 21, 0, 0, 0, TimeSpan.Zero);
        var runtime = CreateHelloRuntime(() => currentTime);
        var calls = new List<RuntimeRegistrationRequest>();
        var client = new RecordingRuntimeClient(request =>
        {
            calls.Add(request);
            return new RuntimeRegistrationResponse
            {
                RuntimeId = request.RuntimeId,
                Cached = calls.Count > 1,
                ExpiresAt = "2026-03-21T00:30:00.000Z",
                LibraryVersion = request.LibraryVersion,
            };
        });

        await runtime.EnsureRegisteredAsync(client);
        await runtime.EnsureRegisteredAsync(client);
        currentTime = currentTime.AddMinutes(31);
        await runtime.EnsureRegisteredAsync(client);

        Assert.Equal(2, calls.Count);
        Assert.Equal(runtime.RuntimeId, calls[0].RuntimeId);
        Assert.Equal(runtime.RuntimeId, calls[1].RuntimeId);
        Assert.Equal(["greeting_source", "console_output"], runtime.GetLibrary().Nodes.Select(node => node.Type).ToArray());
    }

    [Fact]
    public async Task RuntimeExecutesHelloGraphAndCapturesProfilingAsync()
    {
        var runtime = CreateHelloRuntime();

        var result = await runtime.ExecuteGraphAsync(CreateHelloGraph());

        Assert.Equal("completed", result.Status);
        Assert.Equal(["Hello, Codex!"], result.Results["console"].Cast<string>().ToArray());
        Assert.Equal(1, result.Profiler["node_source"].CallCount);
        Assert.Equal(1, result.Profiler["node_output"].CallCount);
        Assert.True(result.Profiler["node_output"].TotalDurationMs >= 0);
    }

    [Fact]
    public async Task RuntimeDebuggerPausesOnBreakpointAndCanContinueAsync()
    {
        var runtime = CreateHelloRuntime();
        var debugSession = runtime.CreateDebugger(CreateHelloGraph(), new NodeGraphExecutionOptions
        {
            Breakpoints = ["node_output"],
        });

        var firstStep = await debugSession.StepAsync();
        Assert.Equal("paused", firstStep.Status);
        Assert.Equal("node_source", firstStep.LastEvent?.NodeId);

        var paused = await debugSession.ContinueAsync();
        Assert.Equal("paused", paused.Status);
        Assert.Equal("breakpoint", paused.PauseReason);
        Assert.Equal("node_output", paused.PendingNodeId);

        var completed = await debugSession.ContinueAsync();
        Assert.Equal("completed", completed.Status);
        Assert.Equal(["Hello, Codex!"], completed.Results["console"].Cast<string>().ToArray());
        Assert.Equal(1, completed.Profiler["node_output"].CallCount);
    }

    [Fact]
    public async Task RuntimeDebuggerCanReplaceBreakpointsWhileReusingTheSameSessionAsync()
    {
        var runtime = CreateHelloRuntime();
        var debugSession = runtime.CreateDebugger(CreateHelloGraph());

        var firstStep = await debugSession.StepAsync();
        Assert.Equal("paused", firstStep.Status);
        Assert.Equal("node_output", firstStep.PendingNodeId);

        debugSession.SetBreakpoints(["node_output"]);

        var paused = await debugSession.ContinueAsync();
        Assert.Equal("paused", paused.Status);
        Assert.Equal("breakpoint", paused.PauseReason);
        Assert.Equal("node_output", paused.PendingNodeId);

        debugSession.SetBreakpoints([]);

        var completed = await debugSession.ContinueAsync();
        Assert.Equal("completed", completed.Status);
        Assert.Equal(["Hello, Codex!"], completed.Results["console"].Cast<string>().ToArray());
    }

    private sealed class RecordingRuntimeClient(Func<RuntimeRegistrationRequest, RuntimeRegistrationResponse> handler) : INodeGraphRuntimeClient
    {
        public Task<RuntimeRegistrationResponse> RegisterRuntimeAsync(RuntimeRegistrationRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(handler(request));
        }
    }
}
