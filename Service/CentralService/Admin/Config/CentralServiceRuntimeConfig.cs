namespace CentralService.Admin.Config;

public sealed class CentralServiceRuntimeConfig
{
    public List<CentralServiceServicePolicyConfig> Services { get; set; } = new();

    public List<CentralServiceServiceInstanceOverrideConfig> Instances { get; set; } = new();
}

public sealed class CentralServiceServicePolicyConfig
{
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// 优先选择与中心服务同一局域网的实例（若存在）。
    /// </summary>
    public bool PreferLocalNetwork { get; set; }

    /// <summary>
    /// 最小可用实例数（用于告警/监控阈值；不直接阻断发现接口）。
    /// </summary>
    public int? MinHealthyInstances { get; set; }
}

public sealed class CentralServiceServiceInstanceOverrideConfig
{
    public string ServiceId { get; set; } = string.Empty;

    public string? ServiceName { get; set; }

    public string? Host { get; set; }

    public int? Port { get; set; }

    public bool Disabled { get; set; }

    public int? Weight { get; set; }
}
