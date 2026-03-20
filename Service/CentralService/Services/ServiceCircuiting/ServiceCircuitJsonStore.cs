using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using CentralService.Models;

namespace CentralService.Services.ServiceCircuiting;

public sealed class ServiceCircuitJsonStore
{
    private static readonly TimeSpan MinimumPersistTouchInterval = TimeSpan.FromMinutes(1);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger<ServiceCircuitJsonStore> _logger;
    private readonly string _filePath;

    private volatile IReadOnlyDictionary<string, PersistedServiceCircuitEntry> _servicesByKey =
        new Dictionary<string, PersistedServiceCircuitEntry>(StringComparer.OrdinalIgnoreCase);

    public ServiceCircuitJsonStore(
        IOptions<ServiceCircuitJsonOptions> options,
        ILogger<ServiceCircuitJsonStore> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var configured = options?.Value?.FilePath?.Trim();
        _filePath = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(AppContext.BaseDirectory, "data", "service-circuit.services.json")
            : Path.GetFullPath(configured);

        Initialize();
    }

    internal IReadOnlyDictionary<string, PersistedServiceCircuitEntry> ServicesByKey => _servicesByKey;

    internal PersistedServiceCircuitEntry? Find(ServiceInstanceKey key)
    {
        if (key.IsEmpty)
        {
            return null;
        }

        ServicesByKey.TryGetValue(key.PersistentKey, out var entry);
        return entry;
    }

    internal PersistedServiceCircuitEntry? Find(ServiceInfo? service)
    {
        return Find(ServiceInstanceKey.FromService(service));
    }

    internal async Task<PersistedServiceCircuitEntry?> EnsureServiceAsync(
        ServiceInfo? service,
        ServiceCircuitBreakerSettings defaults,
        CancellationToken cancellationToken = default)
    {
        return await TouchCoreAsync(service, DateTimeOffset.UtcNow, defaults, forceWrite: true, cancellationToken);
    }

    internal async Task<PersistedServiceCircuitEntry?> TouchServiceAsync(
        ServiceInfo? service,
        ServiceCircuitBreakerSettings defaults,
        CancellationToken cancellationToken = default)
    {
        return await TouchCoreAsync(service, DateTimeOffset.UtcNow, defaults, forceWrite: false, cancellationToken);
    }

    internal async Task<PersistedServiceCircuitEntry?> UpdateCircuitBreakerAsync(
        ServiceInfo? service,
        ServiceCircuitBreakerSettings? settings,
        ServiceCircuitBreakerSettings defaults,
        CancellationToken cancellationToken = default)
    {
        if (service == null || settings == null)
        {
            return null;
        }

        var serviceKey = ServiceInstanceKey.FromService(service);
        if (serviceKey.IsEmpty)
        {
            return null;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var normalized = settings.Normalize(defaults);
            var services = ServicesByKey.ToDictionary(
                x => x.Key,
                x => x.Value,
                StringComparer.OrdinalIgnoreCase);

            var nowUtc = DateTimeOffset.UtcNow;
            var entry = new PersistedServiceCircuitEntry
            {
                ServiceKey = serviceKey,
                LastSeenAtUtc = services.TryGetValue(serviceKey.PersistentKey, out var existing)
                    ? existing.LastSeenAtUtc
                    : nowUtc,
                CircuitBreaker = normalized,
            };

            services[serviceKey.PersistentKey] = entry;
            await PersistAsync(services, cancellationToken);
            return entry;
        }
        finally
        {
            _gate.Release();
        }
    }

    internal async Task<IReadOnlyList<ServiceInstanceKey>> CleanupStaleAsync(
        DateTimeOffset nowUtc,
        int staleDays,
        CancellationToken cancellationToken = default)
    {
        staleDays = staleDays < 1 ? 30 : staleDays;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var cutoffUtc = nowUtc.AddDays(-staleDays);
            var services = ServicesByKey.ToDictionary(
                x => x.Key,
                x => x.Value,
                StringComparer.OrdinalIgnoreCase);

            var removed = services.Values
                .Where(x => x.LastSeenAtUtc < cutoffUtc)
                .Select(x => x.ServiceKey)
                .ToArray();

            if (removed.Length == 0)
            {
                return Array.Empty<ServiceInstanceKey>();
            }

            foreach (var key in removed)
            {
                services.Remove(key.PersistentKey);
            }

            await PersistAsync(services, cancellationToken);
            return removed;
        }
        finally
        {
            _gate.Release();
        }
    }

    private void Initialize()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

        if (!File.Exists(_filePath))
        {
            File.WriteAllText(_filePath, "{}", new UTF8Encoding(false));
            _servicesByKey = new Dictionary<string, PersistedServiceCircuitEntry>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        var text = File.ReadAllText(_filePath, Encoding.UTF8);
        _servicesByKey = Parse(text);
    }

    private async Task<PersistedServiceCircuitEntry?> TouchCoreAsync(
        ServiceInfo? service,
        DateTimeOffset nowUtc,
        ServiceCircuitBreakerSettings defaults,
        bool forceWrite,
        CancellationToken cancellationToken)
    {
        if (service == null)
        {
            return null;
        }

        var serviceKey = ServiceInstanceKey.FromService(service);
        if (serviceKey.IsEmpty)
        {
            return null;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var normalizedDefaults = defaults?.Normalize() ?? ServiceCircuitBreakerSettings.Defaults();
            var services = ServicesByKey.ToDictionary(
                x => x.Key,
                x => x.Value,
                StringComparer.OrdinalIgnoreCase);

            if (services.TryGetValue(serviceKey.PersistentKey, out var existing))
            {
                var shouldPersist = forceWrite || nowUtc - existing.LastSeenAtUtc >= MinimumPersistTouchInterval;
                if (!shouldPersist)
                {
                    return existing;
                }

                var updatedExisting = new PersistedServiceCircuitEntry
                {
                    ServiceKey = serviceKey,
                    LastSeenAtUtc = nowUtc,
                    CircuitBreaker = existing.CircuitBreaker.Normalize(normalizedDefaults),
                };

                services[serviceKey.PersistentKey] = updatedExisting;
                await PersistAsync(services, cancellationToken);
                return updatedExisting;
            }

            var created = new PersistedServiceCircuitEntry
            {
                ServiceKey = serviceKey,
                LastSeenAtUtc = nowUtc,
                CircuitBreaker = normalizedDefaults.Clone(),
            };

            services[serviceKey.PersistentKey] = created;
            await PersistAsync(services, cancellationToken);
            return created;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task PersistAsync(
        IReadOnlyDictionary<string, PersistedServiceCircuitEntry> servicesByKey,
        CancellationToken cancellationToken)
    {
        var servicesJson = Serialize(servicesByKey);
        var tempFilePath = _filePath + ".tmp";
        await File.WriteAllTextAsync(tempFilePath, servicesJson, new UTF8Encoding(false), cancellationToken);
        File.Move(tempFilePath, _filePath, true);
        _servicesByKey = new Dictionary<string, PersistedServiceCircuitEntry>(servicesByKey, StringComparer.OrdinalIgnoreCase);
    }

    private IReadOnlyDictionary<string, PersistedServiceCircuitEntry> Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new Dictionary<string, PersistedServiceCircuitEntry>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            var services = new Dictionary<string, PersistedServiceCircuitEntry>(StringComparer.OrdinalIgnoreCase);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return services;
            }

            foreach (var serviceGroup in document.RootElement.EnumerateObject())
            {
                if (serviceGroup.Value.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var instanceElement in serviceGroup.Value.EnumerateArray())
                {
                    AddServiceInstance(services, serviceGroup.Name, instanceElement);
                }
            }

            return services;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析服务熔断 JSON 失败: {FilePath}", _filePath);
            throw;
        }
    }

    private static void AddServiceInstance(
        IDictionary<string, PersistedServiceCircuitEntry> services,
        string serviceName,
        JsonElement instanceElement)
    {
        var circuitBreaker = ServiceCircuitBreakerSettings.Defaults();
        if (TryGetJsonProperty(instanceElement, "circuitBreaker", out var circuitBreakerElement) &&
            circuitBreakerElement.ValueKind == JsonValueKind.Object)
        {
            var parsed = JsonSerializer.Deserialize<ServiceCircuitBreakerSettings>(circuitBreakerElement.GetRawText(), JsonOptions);
            if (parsed != null)
            {
                circuitBreaker = parsed.Normalize(circuitBreaker);
            }
        }

        var key = new ServiceInstanceKey(
            (serviceName ?? string.Empty).Trim(),
            GetJsonString(instanceElement, "localIp"),
            GetJsonString(instanceElement, "operatorIp"),
            GetJsonString(instanceElement, "publicIp"),
            GetJsonInt(instanceElement, "port") ?? 0);

        if (key.IsEmpty)
        {
            return;
        }

        var lastSeenAtUtc = GetJsonDateTimeOffset(instanceElement, "lastSeenAtUtc") ?? DateTimeOffset.UtcNow;
        services[key.PersistentKey] = new PersistedServiceCircuitEntry
        {
            ServiceKey = key,
            LastSeenAtUtc = lastSeenAtUtc,
            CircuitBreaker = circuitBreaker,
        };
    }

    private static string Serialize(IReadOnlyDictionary<string, PersistedServiceCircuitEntry> servicesByKey)
    {
        var grouped = new SortedDictionary<string, List<JsonServiceInstance>>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in servicesByKey.Values
                     .GroupBy(x => x.ServiceKey.ServiceName, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            grouped[group.Key] = group
                .OrderBy(x => x.ServiceKey.LocalIp, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.ServiceKey.OperatorIp, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.ServiceKey.PublicIp, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.ServiceKey.Port)
                .Select(entry => new JsonServiceInstance
                {
                    LocalIp = entry.ServiceKey.LocalIp,
                    OperatorIp = entry.ServiceKey.OperatorIp,
                    PublicIp = entry.ServiceKey.PublicIp,
                    Port = entry.ServiceKey.Port,
                    LastSeenAtUtc = entry.LastSeenAtUtc.ToUniversalTime().ToString("O"),
                    CircuitBreaker = entry.CircuitBreaker,
                })
                .ToList();
        }

        return JsonSerializer.Serialize(grouped, JsonOptions);
    }

    private static bool TryGetJsonProperty(JsonElement source, string propertyName, out JsonElement value)
    {
        if (source.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in source.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string GetJsonString(JsonElement source, string propertyName)
    {
        if (!TryGetJsonProperty(source, propertyName, out var value))
        {
            return string.Empty;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString()?.Trim() ?? string.Empty,
            JsonValueKind.Null => string.Empty,
            _ => value.ToString()?.Trim() ?? string.Empty,
        };
    }

    private static int? GetJsonInt(JsonElement source, string propertyName)
    {
        if (!TryGetJsonProperty(source, propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null,
        };
    }

    private static DateTimeOffset? GetJsonDateTimeOffset(JsonElement source, string propertyName)
    {
        if (!TryGetJsonProperty(source, propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String when DateTimeOffset.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null,
        };
    }

    private sealed class JsonServiceInstance
    {
        public string LocalIp { get; set; } = string.Empty;

        public string OperatorIp { get; set; } = string.Empty;

        public string PublicIp { get; set; } = string.Empty;

        public int Port { get; set; }

        public string LastSeenAtUtc { get; set; } = string.Empty;

        public ServiceCircuitBreakerSettings CircuitBreaker { get; set; } = ServiceCircuitBreakerSettings.Defaults();
    }
}

