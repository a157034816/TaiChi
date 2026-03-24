using System.Text;
using Microsoft.Extensions.Options;
using Tomlyn;
using Tomlyn.Model;

namespace CentralService.Services.ServiceCircuiting;

public sealed class ServiceCircuitTomlStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger<ServiceCircuitTomlStore> _logger;
    private readonly string _filePath;

    private volatile DefaultsSnapshot _snapshot = DefaultsSnapshot.CreateDefault();

    public ServiceCircuitTomlStore(
        IOptions<ServiceCircuitTomlOptions> options,
        ILogger<ServiceCircuitTomlStore> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var configured = options?.Value?.FilePath?.Trim();
        _filePath = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(AppContext.BaseDirectory, "data", "service-circuit.toml")
            : Path.GetFullPath(configured);

        Initialize();
    }

    internal int StaleDays => _snapshot.StaleDays;

    internal ServiceCircuitBreakerSettings Defaults => _snapshot.CircuitBreaker;

    internal async Task TouchAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var text = Serialize(_snapshot);
            var tempFilePath = _filePath + ".tmp";
            await File.WriteAllTextAsync(tempFilePath, text, new UTF8Encoding(false), cancellationToken);
            File.Move(tempFilePath, _filePath, true);
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
            var snapshot = DefaultsSnapshot.CreateDefault();
            File.WriteAllText(_filePath, Serialize(snapshot), new UTF8Encoding(false));
            _snapshot = snapshot;
            return;
        }

        var text = File.ReadAllText(_filePath, Encoding.UTF8);
        _snapshot = Parse(text);
    }

    private DefaultsSnapshot Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return DefaultsSnapshot.CreateDefault();
        }

        try
        {
            var model = Toml.ToModel(text) as TomlTable;
            if (model == null)
            {
                return DefaultsSnapshot.CreateDefault();
            }

            var defaultsTable = GetTable(model, "defaults");
            var staleDays = GetInt(defaultsTable, "staleDays") ?? 30;

            var circuitBreaker = ParseCircuitBreaker(
                GetTable(defaultsTable, "circuitBreaker"),
                ServiceCircuitBreakerSettings.Defaults());

            return new DefaultsSnapshot(staleDays, circuitBreaker);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析服务熔断 TOML defaults 失败: {FilePath}", _filePath);
            throw;
        }
    }

    private static ServiceCircuitBreakerSettings ParseCircuitBreaker(
        TomlTable? table,
        ServiceCircuitBreakerSettings fallback)
    {
        if (table == null)
        {
            return fallback.Clone();
        }

        return new ServiceCircuitBreakerSettings
        {
            MaxAttempts = GetInt(table, "maxAttempts") ?? fallback.MaxAttempts,
            FailureThreshold = GetInt(table, "failureThreshold") ?? fallback.FailureThreshold,
            BreakDurationMinutes = GetInt(table, "breakDurationMinutes") ?? fallback.BreakDurationMinutes,
            RecoveryThreshold = GetInt(table, "recoveryThreshold") ?? fallback.RecoveryThreshold,
        }.Normalize(fallback);
    }

    private static string Serialize(DefaultsSnapshot snapshot)
    {
        var builder = new StringBuilder();
        builder.AppendLine("[defaults]");
        builder.Append("staleDays = ").AppendLine(snapshot.StaleDays.ToString());
        builder.AppendLine();
        builder.AppendLine("[defaults.circuitBreaker]");
        builder.Append("maxAttempts = ").AppendLine(snapshot.CircuitBreaker.MaxAttempts.ToString());
        builder.Append("failureThreshold = ").AppendLine(snapshot.CircuitBreaker.FailureThreshold.ToString());
        builder.Append("breakDurationMinutes = ").AppendLine(snapshot.CircuitBreaker.BreakDurationMinutes.ToString());
        builder.Append("recoveryThreshold = ").AppendLine(snapshot.CircuitBreaker.RecoveryThreshold.ToString());
        return builder.ToString();
    }

    private static TomlTable? GetTable(TomlTable? source, string key)
    {
        if (source == null)
        {
            return null;
        }

        return source.TryGetValue(key, out var value) ? value as TomlTable : null;
    }

    private static int? GetInt(TomlTable? source, string key)
    {
        if (source == null || !source.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        return value switch
        {
            int i => i,
            long l when l <= int.MaxValue && l >= int.MinValue => (int)l,
            _ when int.TryParse(value.ToString(), out var parsed) => parsed,
            _ => null,
        };
    }

    private sealed record DefaultsSnapshot(int StaleDays, ServiceCircuitBreakerSettings CircuitBreaker)
    {
        public static DefaultsSnapshot CreateDefault()
        {
            return new DefaultsSnapshot(30, ServiceCircuitBreakerSettings.Defaults());
        }
    }
}

