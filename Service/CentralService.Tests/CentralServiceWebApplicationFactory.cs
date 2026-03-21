using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CentralService.Tests;

/// <summary>
/// CentralService 集成测试专用的 <see cref="WebApplicationFactory{TEntryPoint}"/>。
/// 通过内存宿主启动 ASP.NET Core 管道，并使用临时 Sqlite 数据库与临时配置文件隔离测试运行。
/// </summary>
/// <remarks>
/// 该工厂会在构造时生成临时文件路径，并在 <see cref="Dispose(bool)"/> 中清理产物。
/// 如需复用已有数据库以便排查问题，可通过构造参数 <c>existingDbFilePath</c> 传入并禁用删除。
/// </remarks>
public sealed class CentralServiceWebApplicationFactory : WebApplicationFactory<CentralService.Program>
{
    private readonly string _dbFilePath;
    private readonly string _serviceCircuitTomlPath;
    private readonly string _serviceCircuitJsonPath;
    private readonly bool _useExistingDb;
    private readonly IReadOnlyDictionary<string, string?> _additionalSettings;
    private readonly Action<IServiceCollection>? _configureServices;

    /// <summary>
    /// 默认管理员用户名（由种子逻辑写入）。
    /// </summary>
    public string AdminUsername { get; } = "admin";

    /// <summary>
    /// 默认管理员密码（由种子逻辑写入）。
    /// </summary>
    public string AdminPassword { get; } = "admin123!";

    /// <summary>
    /// 服务熔断配置 TOML 文件路径（由测试工厂生成）。
    /// </summary>
    public string ServiceCircuitTomlPath => _serviceCircuitTomlPath;

    /// <summary>
    /// 服务熔断配置 JSON 文件路径（由测试工厂生成）。
    /// </summary>
    public string ServiceCircuitJsonPath => _serviceCircuitJsonPath;

    /// <summary>
    /// 创建集成测试宿主工厂。
    /// </summary>
    /// <param name="existingDbFilePath">
    /// 可选：指定已有的 Sqlite 数据库文件路径。
    /// 传入后测试将复用该数据库，并在 Dispose 时不删除该文件（便于本地调试）。
    /// </param>
    /// <param name="additionalSettings">可选：额外注入的配置键值对（覆盖默认测试配置）。</param>
    /// <param name="configureServices">可选：用于在测试宿主中替换/注入服务的回调。</param>
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

    /// <summary>
    /// 配置测试宿主的环境与配置源。
    /// </summary>
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

    /// <summary>
    /// 清理临时文件（数据库、熔断配置等）。
    /// </summary>
    /// <param name="disposing">是否正在显式释放。</param>
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
            // 忽略清理过程中的异常（例如文件被占用）。
        }
    }
}
