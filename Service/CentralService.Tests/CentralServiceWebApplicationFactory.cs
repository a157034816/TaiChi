using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CentralService.Tests;

public sealed class CentralServiceWebApplicationFactory : WebApplicationFactory<CentralService.Program>
{
    private readonly string _dbFilePath;
    private readonly string _serviceCircuitTomlPath;
    private readonly string _serviceCircuitJsonPath;
    private readonly bool _useExistingDb;
    private readonly IReadOnlyDictionary<string, string?> _additionalSettings;
    private readonly Action<IServiceCollection>? _configureServices;

    public string AdminUsername { get; } = "admin";
    public string AdminPassword { get; } = "admin123!";
    public string ServiceCircuitTomlPath => _serviceCircuitTomlPath;
    public string ServiceCircuitJsonPath => _serviceCircuitJsonPath;

    public CentralServiceWebApplicationFactory(
        string? existingDbFilePath = null,
        IReadOnlyDictionary<string, string?>? additionalSettings = null,
        Action<IServiceCollection>? configureServices = null)
    {
        _additionalSettings = additionalSettings ?? new Dictionary<string, string?>();
        _configureServices = configureServices;
        var tempDirectory = Path.Combine(Path.GetTempPath(), "central-service-admin-tests");
        Directory.CreateDirectory(tempDirectory);

        if (!string.IsNullOrWhiteSpace(existingDbFilePath))
        {
            _dbFilePath = existingDbFilePath;
            _serviceCircuitTomlPath = Path.Combine(tempDirectory, $"central-service-circuit-{Guid.NewGuid():N}.toml");
            _serviceCircuitJsonPath = Path.Combine(tempDirectory, $"central-service-circuit-{Guid.NewGuid():N}.services.json");
            _useExistingDb = true;
            return;
        }

        _dbFilePath = Path.Combine(tempDirectory, $"central-service-admin-{Guid.NewGuid():N}.db");
        _serviceCircuitTomlPath = Path.Combine(tempDirectory, $"central-service-circuit-{Guid.NewGuid():N}.toml");
        _serviceCircuitJsonPath = Path.Combine(tempDirectory, $"central-service-circuit-{Guid.NewGuid():N}.services.json");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureAppConfiguration((context, configBuilder) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["CentralServiceAdminDb:DatabaseType"] = "Sqlite",
                ["CentralServiceAdminDb:ConnectionString"] = $"Data Source={_dbFilePath}",
                ["CentralServiceAdminSeed:Enabled"] = "true",
                ["CentralServiceAdminSeed:AdminUsername"] = AdminUsername,
                ["CentralServiceAdminSeed:AdminPassword"] = AdminPassword,
                ["ServiceCircuitToml:FilePath"] = _serviceCircuitTomlPath,
                ["ServiceCircuitToml:CleanupIntervalMinutes"] = "1",
                ["ServiceCircuitJson:FilePath"] = _serviceCircuitJsonPath,
                ["ManagedWebApps:Definitions:0:Enabled"] = "false",
            };

            foreach (var pair in _additionalSettings)
            {
                settings[pair.Key] = pair.Value;
            }

            configBuilder.AddInMemoryCollection(settings);
        });

        if (_configureServices != null)
        {
            builder.ConfigureServices(_configureServices);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        try
        {
            if (!_useExistingDb && File.Exists(_dbFilePath))
            {
                File.Delete(_dbFilePath);
            }

            if (File.Exists(_serviceCircuitTomlPath))
            {
                File.Delete(_serviceCircuitTomlPath);
            }

            if (File.Exists(_serviceCircuitJsonPath))
            {
                File.Delete(_serviceCircuitJsonPath);
            }
        }
        catch
        {
            // ignore
        }
    }
}
