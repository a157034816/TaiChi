using System.Net.Http.Json;
using System.Text.Json;

namespace NodeGraphSdk;

public sealed class NodeGraphClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;

    public NodeGraphClient(HttpClient httpClient, string baseUrl)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    }

    public async Task<CreateSessionResponse> CreateSessionAsync(
        CreateSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/sdk/sessions", request, JsonOptions, cancellationToken);
        return await ReadResponseAsync<CreateSessionResponse>(response, cancellationToken);
    }

    public async Task<JsonElement> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync($"api/sdk/sessions/{Uri.EscapeDataString(sessionId)}", cancellationToken);
        return await ReadResponseAsync<JsonElement>(response, cancellationToken);
    }

    private static async Task<T> ReadResponseAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
            return result ?? throw new NodeGraphClientException("NodeGraph returned an empty payload.", (int)response.StatusCode);
        }

        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        JsonElement? payload = null;

        if (!string.IsNullOrWhiteSpace(raw))
        {
            payload = JsonSerializer.Deserialize<JsonElement>(raw, JsonOptions);
        }

        var message = payload.HasValue && payload.Value.TryGetProperty("error", out var errorProperty)
            ? errorProperty.GetString() ?? "NodeGraph request failed."
            : "NodeGraph request failed.";

        throw new NodeGraphClientException(message, (int)response.StatusCode, payload);
    }
}
