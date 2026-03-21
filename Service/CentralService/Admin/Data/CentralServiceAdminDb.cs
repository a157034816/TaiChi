using CentralService.Admin.Entities;
using Microsoft.Extensions.Options;
using SqlSugar;

namespace CentralService.Admin.Data;

/// <summary>
/// 中心服务后台管理数据库（SQLite + SqlSugar）。
/// </summary>
public sealed class CentralServiceAdminDb
{
    private static readonly Type[] EntityTypes =
    {
        typeof(CentralServiceUser),
        typeof(CentralServiceRole),
        typeof(CentralServiceUserRole),
        typeof(CentralServiceRolePermission),
        typeof(CentralServiceAuditLog),
        typeof(CentralServiceConfigState),
        typeof(CentralServiceConfigVersion),
        typeof(CentralServiceConfigPublish),
    };

    private readonly ILogger<CentralServiceAdminDb> _logger;
    private readonly object _initLock = new();
    private volatile bool _initialized;

    /// <summary>
    /// 创建后台管理数据库访问对象。
    /// </summary>
    public CentralServiceAdminDb(IOptions<CentralServiceAdminDbOptions> options, ILogger<CentralServiceAdminDb> logger)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var connectionString = (options.Value.ConnectionString ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = new CentralServiceAdminDbOptions().ConnectionString;
        }

        Db = new SqlSugarScope(new ConnectionConfig
        {
            ConnectionString = connectionString,
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
        });
    }

    /// <summary>
    /// SqlSugar 数据库对象。
    /// </summary>
    public SqlSugarScope Db { get; }

    /// <summary>
    /// 确保数据库文件与表结构已创建。
    /// </summary>
    public void EnsureCreated()
    {
        if (_initialized)
        {
            return;
        }

        lock (_initLock)
        {
            if (_initialized)
            {
                return;
            }

            EnsureSqliteDirectory(Db.CurrentConnectionConfig.ConnectionString);
            Db.CodeFirst.InitTables(EntityTypes);
            _initialized = true;
        }
    }

    private void EnsureSqliteDirectory(string connectionString)
    {
        var dataSource = TryParseSqliteDataSource(connectionString);
        if (string.IsNullOrWhiteSpace(dataSource))
        {
            return;
        }

        // SQLite 内存模式不需要创建目录
        if (string.Equals(dataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(dataSource);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析 CentralServiceAdminDb 的 DataSource 路径失败: {DataSource}", dataSource);
            return;
        }

        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
    }

    private static string? TryParseSqliteDataSource(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        foreach (var segment in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = segment.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                continue;
            }

            var key = parts[0];
            var value = parts[1].Trim().Trim('"');
            if (key.Equals("DataSource", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("Data Source", StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }
}

