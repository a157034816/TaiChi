using System.Text.Json;

namespace NodeGraphSdk;

public sealed class NodeGraphViewport
{
    public double X { get; set; }

    public double Y { get; set; }

    public double Zoom { get; set; }
}

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

public sealed class Position
{
    public double X { get; set; }

    public double Y { get; set; }
}

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
}

public sealed class NodeGraphDocument
{
    public string? GraphId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<NodeGraphNode> Nodes { get; set; } = new();

    public List<NodeGraphEdge> Edges { get; set; } = new();

    public NodeGraphViewport Viewport { get; set; } = new();
}

public sealed class CreateSessionRequest
{
    public string Domain { get; set; } = string.Empty;

    public string? ClientName { get; set; }

    public string NodeLibraryEndpoint { get; set; } = string.Empty;

    public string CompletionWebhook { get; set; } = string.Empty;

    public NodeGraphDocument? Graph { get; set; }

    public Dictionary<string, string>? Metadata { get; set; }
}

public sealed class CreateSessionResponse
{
    public string SessionId { get; set; } = string.Empty;

    public string EditorUrl { get; set; } = string.Empty;

    public string AccessType { get; set; } = string.Empty;

    public bool DomainCached { get; set; }
}

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
