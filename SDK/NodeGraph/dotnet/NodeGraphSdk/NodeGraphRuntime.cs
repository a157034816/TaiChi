using System.Collections;
using System.Diagnostics;
using System.Text.Json;

namespace NodeGraphSdk;

/// <summary>
/// SDK 侧运行时，负责生成运行时标识、节点库注册以及图执行/调试。
/// </summary>
public sealed class NodeGraphRuntime
{
    private static readonly TimeSpan DefaultRuntimeCacheTtl = TimeSpan.FromMinutes(30);
    private readonly Dictionary<string, NodeDefinition> _nodeDefinitions = [];
    private readonly List<TypeMappingEntry> _typeMappings = [];
    private DateTimeOffset? _lastRegisteredAt;

    public NodeGraphRuntime(NodeGraphRuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.Domain))
        {
            throw new ArgumentException("NodeGraphRuntime requires a domain.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.ControlBaseUrl))
        {
            throw new ArgumentException("NodeGraphRuntime requires a controlBaseUrl.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.LibraryVersion))
        {
            throw new ArgumentException("NodeGraphRuntime requires a libraryVersion.", nameof(options));
        }

        Domain = options.Domain;
        ClientName = options.ClientName;
        ControlBaseUrl = options.ControlBaseUrl.TrimEnd('/');
        LibraryVersion = options.LibraryVersion;
        Capabilities = options.Capabilities ?? new RuntimeCapabilities();
        RuntimeId = string.IsNullOrWhiteSpace(options.RuntimeId) ? $"rt_{Guid.NewGuid():N}" : options.RuntimeId;
        CacheTtl = options.CacheTtl ?? DefaultRuntimeCacheTtl;
        NowProvider = options.NowProvider ?? (() => DateTimeOffset.UtcNow);
    }

    public string Domain { get; }

    public string? ClientName { get; }

    public string ControlBaseUrl { get; }

    public string LibraryVersion { get; }

    public RuntimeCapabilities Capabilities { get; }

    public string RuntimeId { get; }

    public TimeSpan CacheTtl { get; }

    private Func<DateTimeOffset> NowProvider { get; }

    public NodeGraphRuntime RegisterNode(NodeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (string.IsNullOrWhiteSpace(definition.Type))
        {
            throw new ArgumentException("Node definitions must provide a type.", nameof(definition));
        }

        _nodeDefinitions[definition.Type] = definition;
        return this;
    }

    public NodeGraphRuntime RegisterTypeMapping(TypeMappingEntry mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        _typeMappings.Add(mapping);
        return this;
    }

    public NodeLibraryEnvelope GetLibrary()
    {
        return new NodeLibraryEnvelope
        {
            Nodes = _nodeDefinitions.Values.Select(definition => new NodeLibraryItem
            {
                Type = definition.Type,
                DisplayName = definition.DisplayName,
                Description = definition.Description,
                Category = definition.Category,
                Inputs = definition.Inputs?.Select(NodeGraphRuntimeDebugSession.ClonePort).ToList(),
                Outputs = definition.Outputs?.Select(NodeGraphRuntimeDebugSession.ClonePort).ToList(),
                Fields = definition.Fields?.Select(NodeGraphRuntimeDebugSession.CloneField).ToList(),
                DefaultData = NodeGraphRuntimeDebugSession.CloneDictionary(definition.DefaultData),
                Appearance = definition.Appearance is null
                    ? null
                    : new NodeAppearance
                    {
                        BgColor = definition.Appearance.BgColor,
                        BorderColor = definition.Appearance.BorderColor,
                        TextColor = definition.Appearance.TextColor,
                    },
            }).ToList(),
            TypeMappings = _typeMappings.Count == 0
                ? null
                : _typeMappings.Select(mapping => new TypeMappingEntry
                {
                    CanonicalId = mapping.CanonicalId,
                    Type = mapping.Type,
                    Color = mapping.Color,
                }).ToList(),
        };
    }

    public RuntimeRegistrationRequest CreateRegistrationRequest()
    {
        return new RuntimeRegistrationRequest
        {
            RuntimeId = RuntimeId,
            Domain = Domain,
            ClientName = ClientName,
            ControlBaseUrl = ControlBaseUrl,
            LibraryVersion = LibraryVersion,
            Capabilities = new RuntimeCapabilities
            {
                CanDebug = Capabilities.CanDebug,
                CanExecute = Capabilities.CanExecute,
                CanProfile = Capabilities.CanProfile,
            },
            Library = GetLibrary(),
        };
    }

    public async Task<RuntimeRegistrationResponse> EnsureRegisteredAsync(
        INodeGraphRuntimeClient client,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);

        var now = NowProvider();
        var shouldRegister = force || _lastRegisteredAt is null || now - _lastRegisteredAt >= CacheTtl;

        if (!shouldRegister)
        {
            return new RuntimeRegistrationResponse
            {
                RuntimeId = RuntimeId,
                Cached = true,
                ExpiresAt = _lastRegisteredAt!.Value.Add(CacheTtl).ToString("O"),
                LibraryVersion = LibraryVersion,
            };
        }

        var response = await client.RegisterRuntimeAsync(CreateRegistrationRequest(), cancellationToken);
        _lastRegisteredAt = now;
        return response;
    }

    public NodeGraphRuntimeDebugSession CreateDebugger(NodeGraphDocument graph, NodeGraphExecutionOptions? options = null)
    {
        return new NodeGraphRuntimeDebugSession(this, graph, options);
    }

    public Task<NodeGraphExecutionSnapshot> ExecuteGraphAsync(NodeGraphDocument graph, NodeGraphExecutionOptions? options = null)
    {
        return CreateDebugger(graph, options).ContinueAsync();
    }

    internal NodeDefinition GetDefinition(string nodeType)
    {
        if (!_nodeDefinitions.TryGetValue(nodeType, out var definition))
        {
            throw new InvalidOperationException($"NodeGraphRuntime could not find a node definition for \"{nodeType}\".");
        }

        return definition;
    }
}

/// <summary>
/// 节点图调试会话，支持单步与断点继续。
/// </summary>
public sealed class NodeGraphRuntimeDebugSession
{
    private const int DefaultMaxSteps = 1000;
    private static readonly TimeSpan DefaultMaxWallTime = TimeSpan.FromSeconds(5);
    private readonly NodeGraphRuntime _runtime;
    private readonly HashSet<string> _breakpoints;
    private readonly Dictionary<string, NodeGraphNode> _nodeMap;
    private readonly Dictionary<string, Dictionary<string, List<object?>>> _inbox = [];
    private readonly Dictionary<string, Dictionary<string, object?>> _nodeState = [];
    private readonly Dictionary<string, List<NodeGraphEdge>> _outgoingEdges = [];
    private readonly Queue<ExecutionQueueItem> _queue = new();
    private readonly Stopwatch _wallClock = Stopwatch.StartNew();
    private int _stepCount;
    private string _status = "idle";
    private string? _pauseReason;
    private string? _pendingNodeId;
    private Exception? _lastError;
    private NodeGraphRuntimeEvent? _lastEvent;

    public NodeGraphRuntimeDebugSession(NodeGraphRuntime runtime, NodeGraphDocument graph, NodeGraphExecutionOptions? options = null)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        Graph = CloneDocument(graph ?? throw new ArgumentNullException(nameof(graph)));
        _breakpoints = options?.Breakpoints ?? [];
        MaxSteps = options?.MaxSteps ?? DefaultMaxSteps;
        MaxWallTime = options?.MaxWallTime ?? DefaultMaxWallTime;
        _nodeMap = Graph.Nodes.ToDictionary(node => node.Id, node => node);

        foreach (var edge in Graph.Edges)
        {
            var key = CreateEdgeKey(edge.Source, edge.SourceHandle);
            if (!_outgoingEdges.TryGetValue(key, out var edges))
            {
                edges = [];
                _outgoingEdges[key] = edges;
            }

            edges.Add(edge);
        }

        foreach (var node in Graph.Nodes)
        {
            if (GetPorts(node, "inputs").Count == 0)
            {
                _queue.Enqueue(new ExecutionQueueItem(node.Id, "initial"));
            }
        }
    }

    public NodeGraphDocument Graph { get; }

    public int MaxSteps { get; }

    public TimeSpan MaxWallTime { get; }

    public Dictionary<string, NodeGraphProfilerRecord> Profiler { get; } = [];

    public Dictionary<string, List<object?>> Results { get; } = [];

    public List<NodeGraphRuntimeEvent> Events { get; } = [];

    public Task<NodeGraphExecutionSnapshot> StepAsync()
    {
        return DrainAsync(singleStep: true);
    }

    public Task<NodeGraphExecutionSnapshot> ContinueAsync()
    {
        return DrainAsync(singleStep: false);
    }

    /// <summary>
    /// 使用新的节点 ID 集合替换当前调试会话的全部断点。
    /// </summary>
    /// <param name="breakpoints">新的断点节点 ID 列表；传入 <see langword="null" /> 等价于清空。</param>
    public void SetBreakpoints(IEnumerable<string>? breakpoints)
    {
        _breakpoints.Clear();

        if (breakpoints is null)
        {
            return;
        }

        foreach (var breakpoint in breakpoints)
        {
            if (!string.IsNullOrWhiteSpace(breakpoint))
            {
                _breakpoints.Add(breakpoint);
            }
        }
    }

    internal static NodePortDefinition ClonePort(NodePortDefinition port)
    {
        return new NodePortDefinition
        {
            Id = port.Id,
            Label = port.Label,
            DataType = port.DataType,
        };
    }

    internal static NodeLibraryFieldDefinition CloneField(NodeLibraryFieldDefinition field)
    {
        return new NodeLibraryFieldDefinition
        {
            Key = field.Key,
            Label = field.Label,
            Placeholder = field.Placeholder,
            Kind = field.Kind,
            DefaultValue = CloneValue(field.DefaultValue),
            OptionsEndpoint = field.OptionsEndpoint,
        };
    }

    internal static Dictionary<string, object?>? CloneDictionary(Dictionary<string, object?>? values)
    {
        if (values is null)
        {
            return null;
        }

        return values.ToDictionary(entry => entry.Key, entry => CloneValue(entry.Value));
    }

    private async Task<NodeGraphExecutionSnapshot> DrainAsync(bool singleStep)
    {
        string? ignoreBreakpointForNodeId = null;
        if (_status == "paused" && _pauseReason == "breakpoint" && !string.IsNullOrWhiteSpace(_pendingNodeId))
        {
            ignoreBreakpointForNodeId = _pendingNodeId;
        }

        _status = "running";
        _pauseReason = null;

        while (_queue.Count > 0)
        {
            if (!EnsureBudget())
            {
                return BuildSnapshot();
            }

            var nextItem = _queue.Peek();
            if (_breakpoints.Contains(nextItem.NodeId) && !string.Equals(ignoreBreakpointForNodeId, nextItem.NodeId, StringComparison.Ordinal))
            {
                _status = "paused";
                _pauseReason = "breakpoint";
                _pendingNodeId = nextItem.NodeId;
                return BuildSnapshot();
            }

            ignoreBreakpointForNodeId = null;
            _stepCount += 1;
            var item = _queue.Dequeue();

            try
            {
                await ExecuteQueueItemAsync(item);
            }
            catch (Exception error)
            {
                _status = "failed";
                _pauseReason = "error";
                _lastError = error;
                return BuildSnapshot();
            }

            if (singleStep)
            {
                _status = _queue.Count > 0 ? "paused" : "completed";
                _pauseReason = _queue.Count > 0 ? "step" : null;
                _pendingNodeId = _queue.Count > 0 ? _queue.Peek().NodeId : null;
                return BuildSnapshot();
            }
        }

        _status = "completed";
        _pauseReason = null;
        _pendingNodeId = null;
        return BuildSnapshot();
    }

    private bool EnsureBudget()
    {
        if (_stepCount >= MaxSteps)
        {
            _status = "budget_exceeded";
            _pauseReason = "maxSteps";
            return false;
        }

        if (_wallClock.Elapsed > MaxWallTime)
        {
            _status = "budget_exceeded";
            _pauseReason = "maxWallTime";
            return false;
        }

        return true;
    }

    private async Task ExecuteQueueItemAsync(ExecutionQueueItem item)
    {
        if (!_nodeMap.TryGetValue(item.NodeId, out var node))
        {
            throw new InvalidOperationException($"NodeGraphRuntime could not find node \"{item.NodeId}\" in the graph.");
        }

        var definition = _runtime.GetDefinition(GetNodeType(node));
        var stopwatch = Stopwatch.StartNew();
        await definition.ExecuteAsync(CreateExecutionContext(node, item));
        stopwatch.Stop();

        if (!Profiler.TryGetValue(node.Id, out var profiler))
        {
            profiler = new NodeGraphProfilerRecord();
            Profiler[node.Id] = profiler;
        }

        profiler.CallCount += 1;
        profiler.LastDurationMs = stopwatch.Elapsed.TotalMilliseconds;
        profiler.TotalDurationMs += profiler.LastDurationMs;
        profiler.AverageDurationMs = profiler.TotalDurationMs / profiler.CallCount;

        _lastEvent = new NodeGraphRuntimeEvent
        {
            Step = _stepCount,
            Kind = "nodeExecuted",
            NodeId = node.Id,
            NodeType = GetNodeType(node),
            DurationMs = profiler.LastDurationMs,
            Reason = item.Reason,
            PortId = item.PortId,
        };
        Events.Add(_lastEvent);
    }

    private NodeExecutionContext CreateExecutionContext(NodeGraphNode node, ExecutionQueueItem item)
    {
        return new NodeExecutionContext(
            Graph,
            node,
            GetNodeState(node.Id),
            GetValues(node),
            new NodeExecutionTrigger
            {
                Reason = item.Reason,
                PortId = item.PortId,
                Value = CloneValue(item.Value),
            },
            () => GetInputsSnapshot(node.Id),
            portId => ReadInput(node.Id, portId),
            (portId, value) => Emit(node, portId, value),
            (channel, value) => PushResult(channel, value));
    }

    private Dictionary<string, object?> GetNodeState(string nodeId)
    {
        if (_nodeState.TryGetValue(nodeId, out var state))
        {
            return state;
        }

        state = [];
        _nodeState[nodeId] = state;
        return state;
    }

    private Dictionary<string, List<object?>> GetInputsSnapshot(string nodeId)
    {
        if (!_inbox.TryGetValue(nodeId, out var inputs))
        {
            return [];
        }

        return inputs.ToDictionary(entry => entry.Key, entry => entry.Value.Select(CloneValue).ToList());
    }

    private object? ReadInput(string nodeId, string portId)
    {
        if (!_inbox.TryGetValue(nodeId, out var inputs))
        {
            return null;
        }

        if (inputs.TryGetValue(portId, out var values) && values.Count > 0)
        {
            return CloneValue(values[^1]);
        }

        if (inputs.TryGetValue("__default__", out var defaultValues) && defaultValues.Count > 0)
        {
            return CloneValue(defaultValues[^1]);
        }

        return null;
    }

    private void Emit(NodeGraphNode sourceNode, string portId, object? value)
    {
        var directKey = CreateEdgeKey(sourceNode.Id, portId);
        var fallbackKey = CreateEdgeKey(sourceNode.Id, null);
        var outgoingEdges = new List<NodeGraphEdge>();

        if (_outgoingEdges.TryGetValue(directKey, out var directEdges))
        {
            outgoingEdges.AddRange(directEdges);
        }

        if (_outgoingEdges.TryGetValue(fallbackKey, out var fallbackEdges))
        {
            outgoingEdges.AddRange(fallbackEdges);
        }

        foreach (var edge in outgoingEdges)
        {
            var inbox = GetInbox(edge.Target);
            var targetPort = edge.TargetHandle ?? "__default__";
            if (!inbox.TryGetValue(targetPort, out var values))
            {
                values = [];
                inbox[targetPort] = values;
            }

            values.Add(CloneValue(value));
            _queue.Enqueue(new ExecutionQueueItem(edge.Target, "message", edge.TargetHandle, CloneValue(value)));
        }
    }

    private void PushResult(string channel, object? value)
    {
        if (!Results.TryGetValue(channel, out var values))
        {
            values = [];
            Results[channel] = values;
        }

        values.Add(CloneValue(value));
    }

    private Dictionary<string, List<object?>> GetInbox(string nodeId)
    {
        if (_inbox.TryGetValue(nodeId, out var inbox))
        {
            return inbox;
        }

        inbox = [];
        _inbox[nodeId] = inbox;
        return inbox;
    }

    private NodeGraphExecutionSnapshot BuildSnapshot()
    {
        return new NodeGraphExecutionSnapshot
        {
            Status = _status,
            PauseReason = _pauseReason,
            PendingNodeId = _pendingNodeId,
            LastError = _lastError,
            LastEvent = _lastEvent,
            Profiler = Profiler.ToDictionary(
                entry => entry.Key,
                entry => new NodeGraphProfilerRecord
                {
                    AverageDurationMs = entry.Value.AverageDurationMs,
                    CallCount = entry.Value.CallCount,
                    LastDurationMs = entry.Value.LastDurationMs,
                    TotalDurationMs = entry.Value.TotalDurationMs,
                }),
            Results = Results.ToDictionary(entry => entry.Key, entry => entry.Value.Select(CloneValue).ToList()),
            Events = Events.Select(eventItem => new NodeGraphRuntimeEvent
            {
                Step = eventItem.Step,
                Kind = eventItem.Kind,
                NodeId = eventItem.NodeId,
                NodeType = eventItem.NodeType,
                DurationMs = eventItem.DurationMs,
                Reason = eventItem.Reason,
                PortId = eventItem.PortId,
            }).ToList(),
        };
    }

    private static string CreateEdgeKey(string nodeId, string? handleId)
    {
        return $"{nodeId}::{handleId ?? string.Empty}";
    }

    private static string GetNodeType(NodeGraphNode node)
    {
        if (TryGetDictionaryValue(node.Data, "nodeType", out var nodeType)
            && TryReadString(nodeType, out var value)
            && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new InvalidOperationException($"NodeGraphRuntime could not read nodeType from node \"{node.Id}\".");
    }

    private static Dictionary<string, object?> GetValues(NodeGraphNode node)
    {
        if (TryGetDictionaryValue(node.Data, "values", out var rawValues) && ConvertToDictionary(rawValues) is { } values)
        {
            return values;
        }

        return [];
    }

    private static List<NodePortDefinition> GetPorts(NodeGraphNode node, string key)
    {
        if (!TryGetDictionaryValue(node.Data, key, out var rawPorts))
        {
            return [];
        }

        if (rawPorts is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<NodePortDefinition>>(jsonElement.GetRawText()) ?? [];
        }

        if (rawPorts is IEnumerable<NodePortDefinition> typedPorts)
        {
            return typedPorts.Select(ClonePort).ToList();
        }

        if (rawPorts is IEnumerable enumerable)
        {
            var ports = new List<NodePortDefinition>();
            foreach (var item in enumerable)
            {
                if (item is NodePortDefinition port)
                {
                    ports.Add(ClonePort(port));
                    continue;
                }

                if (ConvertToDictionary(item) is { } dictionary)
                {
                    ports.Add(new NodePortDefinition
                    {
                        Id = dictionary.TryGetValue("id", out var id) ? Convert.ToString(id) ?? string.Empty : string.Empty,
                        Label = dictionary.TryGetValue("label", out var label) ? Convert.ToString(label) ?? string.Empty : string.Empty,
                        DataType = dictionary.TryGetValue("dataType", out var dataType) ? Convert.ToString(dataType) : null,
                    });
                }
            }

            return ports;
        }

        return [];
    }

    private static bool TryGetDictionaryValue(Dictionary<string, object?> values, string key, out object? value)
    {
        if (values.TryGetValue(key, out value))
        {
            return true;
        }

        foreach (var entry in values)
        {
            if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = entry.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static Dictionary<string, object?>? ConvertToDictionary(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case Dictionary<string, object?> dictionary:
                return dictionary.ToDictionary(entry => entry.Key, entry => CloneValue(entry.Value));
            case JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Object:
                return jsonElement.EnumerateObject()
                    .ToDictionary(property => property.Name, property => MaterializeJsonElement(property.Value));
            default:
                try
                {
                    return CloneValue(value) as Dictionary<string, object?>;
                }
                catch
                {
                    return null;
                }
        }
    }

    private static bool TryReadString(object? value, out string? result)
    {
        switch (value)
        {
            case null:
                result = null;
                return false;
            case string text:
                result = text;
                return true;
            case JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.String:
                result = jsonElement.GetString();
                return !string.IsNullOrWhiteSpace(result);
            default:
                result = Convert.ToString(value);
                return !string.IsNullOrWhiteSpace(result);
        }
    }

    private static object? CloneValue(object? value)
    {
        return value switch
        {
            null => null,
            JsonElement jsonElement => MaterializeJsonElement(jsonElement),
            _ => MaterializeSerializedValue(value),
        };
    }

    private static object? MaterializeSerializedValue(object value)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value));
        return MaterializeJsonElement(document.RootElement);
    }

    private static object? MaterializeJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => MaterializeJsonNumber(element),
            JsonValueKind.True or JsonValueKind.False => element.GetBoolean(),
            JsonValueKind.Array => element.EnumerateArray().Select(MaterializeJsonElement).ToList(),
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(property => property.Name, property => MaterializeJsonElement(property.Value)),
            _ => element.GetRawText(),
        };
    }

    private static object MaterializeJsonNumber(JsonElement element)
    {
        if (element.TryGetInt32(out var intValue))
        {
            return intValue;
        }

        if (element.TryGetInt64(out var longValue))
        {
            return longValue;
        }

        if (element.TryGetDecimal(out var decimalValue))
        {
            return decimalValue;
        }

        if (element.TryGetDouble(out var doubleValue))
        {
            return doubleValue;
        }

        return element.GetRawText();
    }

    private static NodeGraphDocument CloneDocument(NodeGraphDocument document)
    {
        return JsonSerializer.Deserialize<NodeGraphDocument>(JsonSerializer.Serialize(document))
            ?? throw new InvalidOperationException("NodeGraphRuntime could not clone the graph document.");
    }

    private sealed record ExecutionQueueItem(string NodeId, string Reason, string? PortId = null, object? Value = null);
}
