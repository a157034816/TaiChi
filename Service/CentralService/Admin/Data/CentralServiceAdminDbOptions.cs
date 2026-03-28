namespace CentralService.Admin.Data;

/// <summary>
/// 中心服务后台管理数据库配置。
/// </summary>
public sealed class CentralServiceAdminDbOptions
{
    /// <summary>
    /// 数据库连接字符串。
    /// <para>
    /// 默认值使用 SQLite 文件：<c>data/central-service-admin.db</c>。
    /// </para>
    /// <para>
    /// 注意：若使用相对路径，将以进程工作目录为基准（例如通过启动器从项目目录启动时即落在项目目录下的 <c>data/</c>）。
    /// </para>
    /// </summary>
    public string ConnectionString { get; set; } = "DataSource=data/central-service-admin.db";
}

