namespace CentralService.Admin.Data;

/// <summary>
/// 后台管理端的“种子管理员”配置。
/// </summary>
public sealed class CentralServiceAdminSeedOptions
{
    /// <summary>
    /// 是否启用种子管理员初始化。
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 初始管理员用户名。
    /// <para>当 <see cref="Enabled"/> 为 <c>true</c> 时必须提供。</para>
    /// </summary>
    public string? AdminUsername { get; set; }

    /// <summary>
    /// 初始管理员密码（明文，仅用于首次写入数据库）。
    /// <para>当 <see cref="Enabled"/> 为 <c>true</c> 时必须提供。</para>
    /// </summary>
    public string? AdminPassword { get; set; }
}

