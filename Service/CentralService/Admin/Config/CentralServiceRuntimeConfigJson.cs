using System.Text.Json;
using System.Text.Json.Serialization;

namespace CentralService.Admin.Config;

public static class CentralServiceRuntimeConfigJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    public static string DefaultJson => JsonSerializer.Serialize(
        new CentralServiceRuntimeConfig(),
        Options);
}

