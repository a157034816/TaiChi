using System.Text.Json;
using System.Text.Json.Serialization;

namespace NodeGraphSdk;

/// <summary>
/// 表示节点图视口状态。
/// </summary>
public sealed class NodeGraphViewport
{
    public double X { get; set; }

    public double Y { get; set; }

    public double Zoom { get; set; }
}

/// <summary>
/// 表示节点在画布中的坐标。
/// </summary>
public sealed class Position
{
    public double X { get; set; }

    public double Y { get; set; }
}

/// <summary>
/// 表示节点图中的节点实例。
/// </summary>
public sealed class NodeGraphNode
{
    public string Id { get; set; } = string.Empty;

    public string Type { get; set; } = "default";

    public Position Position { get; set; } = new();

    public Dictionary<string, object?> Data { get; set; } = new();

    public double? Width { get; set; }

    public double? Height { get; set; }

    public Dictionary<string, object?>? Style { get; set; }
}

/// <summary>
/// 表示节点图中的连线。
/// </summary>
public sealed class NodeGraphEdge
{
    public string Id { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string Target { get; set; } = string.Empty;

    public string? SourceHandle { get; set; }

    public string? TargetHandle { get; set; }

    public string? Label { get; set; }

    public string? Type { get; set; }

    public bool? Animated { get; set; }

    public string? InvalidReason { get; set; }
}

/// <summary>
/// 表示完整节点图文档。
/// </summary>
public sealed class NodeGraphDocument
{
    public string? GraphId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<NodeGraphNode> Nodes { get; set; } = [];

    public List<NodeGraphEdge> Edges { get; set; } = [];

    public NodeGraphViewport Viewport { get; set; } = new();
}

/// <summary>
/// NodeGraph 创建编辑会话时使用的请求体。
/// </summary>
public sealed class CreateSessionRequest
{
    public string RuntimeId { get; set; } = string.Empty;

    public string CompletionWebhook { get; set; } = string.Empty;

    public NodeGraphDocument? Graph { get; set; }

    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
/// NodeGraph 创建编辑会话后的响应体。
/// </summary>
public sealed class CreateSessionResponse
{
    public string SessionId { get; set; } = string.Empty;

    public string RuntimeId { get; set; } = string.Empty;

    public string EditorUrl { get; set; } = string.Empty;

    public string AccessType { get; set; } = string.Empty;
}

/// <summary>
/// 声明运行时具备的能力开关。
/// </summary>
public sealed class RuntimeCapabilities
{
    public bool CanExecute { get; set; } = true;

    public bool CanDebug { get; set; } = true;

    public bool CanProfile { get; set; } = true;
}

/// <summary>
/// 声明节点端口元数据。
/// </summary>
public sealed class NodePortDefinition
{
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string? DataType { get; set; }
}

/// <summary>
/// 声明节点字段元数据。
/// </summary>
public sealed class NodeLibraryFieldDefinition
{
    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string? Placeholder { get; set; }

    public string Kind { get; set; } = "text";

    public object? DefaultValue { get; set; }

    public string? OptionsEndpoint { get; set; }
}

/// <summary>
/// 声明节点卡片外观。
/// </summary>
public sealed class NodeAppearance
{
    public string? BgColor { get; set; }

    public string? BorderColor { get; set; }

    public string? TextColor { get; set; }
}

/// <summary>
/// 声明节点模板。
/// </summary>
public class NodeLibraryItem
{
    public string Type { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Category { get; set; } = string.Empty;

    public List<NodePortDefinition>? Inputs { get; set; }

    public List<NodePortDefinition>? Outputs { get; set; }

    public List<NodeLibraryFieldDefinition>? Fields { get; set; }

    public Dictionary<string, object?>? DefaultData { get; set; }

    public NodeAppearance? Appearance { get; set; }
}

/// <summary>
/// 声明 canonical id 到宿主运行时类型名的映射。
/// </summary>
public sealed class TypeMappingEntry
{
    public string CanonicalId { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string? Color { get; set; }
}

/// <summary>
/// 宿主一次性递交给 NodeGraph 的节点库载荷。
/// </summary>
public sealed class NodeLibraryEnvelope
{
    public List<NodeLibraryItem> Nodes { get; set; } = [];

    public List<TypeMappingEntry>? TypeMappings { get; set; }
}

/// <summary>
/// 运行时注册请求。
/// </summary>
public sealed class RuntimeRegistrationRequest
{
    public string RuntimeId { get; set; } = string.Empty;

    public string Domain { get; set; } = string.Empty;

    public string? ClientName { get; set; }

    public string ControlBaseUrl { get; set; } = string.Empty;

    public string LibraryVersion { get; set; } = string.Empty;

    public RuntimeCapabilities? Capabilities { get; set; }

    public NodeLibraryEnvelope Library { get; set; } = new();
}

/// <summary>
/// 运行时注册响应。
/// </summary>
public sealed class RuntimeRegistrationResponse
{
    public string RuntimeId { get; set; } = string.Empty;

    public bool Cached { get; set; }

    public string ExpiresAt { get; set; } = string.Empty;

    public string LibraryVersion { get; set; } = string.Empty;
}

/// <summary>
/// 运行时初始化配置。
/// </summary>
public sealed class NodeGraphRuntimeOptions
{
    public string Domain { get; set; } = string.Empty;

    public string? ClientName { get; set; }

    public string ControlBaseUrl { get; set; } = string.Empty;

    public string LibraryVersion { get; set; } = string.Empty;

    public RuntimeCapabilities? Capabilities { get; set; }

    public string? RuntimeId { get; set; }

    [JsonIgnore]
    public Func<DateTimeOffset>? NowProvider { get; set; }

    public TimeSpan? CacheTtl { get; set; }
}

/// <summary>
/// 调试/执行时的触发来源。
/// </summary>
public sealed class NodeExecutionTrigger
{
    public string Reason { get; set; } = "initial";

    public string? PortId { get; set; }

    public object? Value { get; set; }
}

/// <summary>
/// 节点执行上下文。
/// </summary>
public sealed class NodeExecutionContext
{
    private readonly Action<string, object?> _emit;
    private readonly Action<string, object?> _pushResult;
    private readonly Func<string, object?> _readInput;
    private readonly Func<Dictionary<string, List<object?>>> _getInputs;

    internal NodeExecutionContext(
        NodeGraphDocument graph,
        NodeGraphNode node,
        Dictionary<string, object?> state,
        Dictionary<string, object?> values,
        NodeExecutionTrigger trigger,
        Func<Dictionary<string, List<object?>>> getInputs,
        Func<string, object?> readInput,
        Action<string, object?> emit,
        Action<string, object?> pushResult)
    {
        Graph = graph;
        Node = node;
        State = state;
        Values = values;
        Trigger = trigger;
        _getInputs = getInputs;
        _readInput = readInput;
        _emit = emit;
        _pushResult = pushResult;
    }

    public NodeGraphDocument Graph { get; }

    public NodeGraphNode Node { get; }

    public Dictionary<string, object?> State { get; }

    public Dictionary<string, object?> Values { get; }

    public NodeExecutionTrigger Trigger { get; }

    public Dictionary<string, List<object?>> GetInputs() => _getInputs();

    public object? ReadInput(string portId) => _readInput(portId);

    public void Emit(string portId, object? value) => _emit(portId, value);

    public void PushResult(string channel, object? value) => _pushResult(channel, value);
}

/// <summary>
/// 节点定义，包含模板元数据与执行委托。
/// </summary>
public sealed class NodeDefinition : NodeLibraryItem
{
    public Func<NodeExecutionContext, Task> ExecuteAsync { get; set; } = _ => Task.CompletedTask;
}

/// <summary>
/// 执行/调试选项。
/// </summary>
public sealed class NodeGraphExecutionOptions
{
    public HashSet<string>? Breakpoints { get; set; }

    public int? MaxSteps { get; set; }

    public TimeSpan? MaxWallTime { get; set; }
}

/// <summary>
/// 节点性能统计。
/// </summary>
public sealed class NodeGraphProfilerRecord
{
    public double AverageDurationMs { get; set; }

    public int CallCount { get; set; }

    public double LastDurationMs { get; set; }

    public double TotalDurationMs { get; set; }
}

/// <summary>
/// 执行事件快照。
/// </summary>
public sealed class NodeGraphRuntimeEvent
{
    public int Step { get; set; }

    public string Kind { get; set; } = string.Empty;

    public string NodeId { get; set; } = string.Empty;

    public string? NodeType { get; set; }

    public double DurationMs { get; set; }

    public string? Reason { get; set; }

    public string? PortId { get; set; }
}

/// <summary>
/// 执行或调试后的快照。
/// </summary>
public sealed class NodeGraphExecutionSnapshot
{
    public string Status { get; set; } = "idle";

    public string? PauseReason { get; set; }

    public string? PendingNodeId { get; set; }

    public Exception? LastError { get; set; }

    public NodeGraphRuntimeEvent? LastEvent { get; set; }

    public Dictionary<string, NodeGraphProfilerRecord> Profiler { get; set; } = [];

    public Dictionary<string, List<object?>> Results { get; set; } = [];

    public List<NodeGraphRuntimeEvent> Events { get; set; } = [];
}

/// <summary>
/// NodeGraph SDK 调用失败时抛出的异常。
/// </summary>
public sealed class NodeGraphClientException : Exception
{
    public NodeGraphClientException(string message, int statusCode, JsonElement? payload = null) : base(message)
    {
        StatusCode = statusCode;
        Payload = payload;
    }

    public int StatusCode { get; }

    public JsonElement? Payload { get; }
}
