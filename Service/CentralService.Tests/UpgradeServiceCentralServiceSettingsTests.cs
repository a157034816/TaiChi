using Microsoft.Extensions.Configuration;
using Upgrade.Service;

namespace CentralService.Tests;

/// <summary>
/// <see cref="UpgradeServiceCentralServiceSettings"/> 的单元测试：验证配置读取优先级与端点排序规则。
/// </summary>
public sealed class UpgradeServiceCentralServiceSettingsTests
{
    /// <summary>
    /// 验证：当同时配置 legacy 单地址与端点列表时，应优先使用端点列表，并按 Priority 升序排序。
    /// </summary>
    [Fact]
    public void Settings_PreferEndpointList_AndSortByPriority()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CentralServiceSettings:CentralServiceUrl"] = "http://legacy:15700",
                ["CentralServiceSettings:Endpoints:0:BaseUrl"] = "http://backup:15700",
                ["CentralServiceSettings:Endpoints:0:Priority"] = "5",
                ["CentralServiceSettings:Endpoints:1:BaseUrl"] = "http://primary:15700",
                ["CentralServiceSettings:Endpoints:1:Priority"] = "1",
                ["CentralServiceSettings:Endpoints:1:MaxAttempts"] = "3",
                ["CentralServiceSettings:Endpoints:1:CircuitBreaker:FailureThreshold"] = "2",
                ["CentralServiceSettings:Endpoints:1:CircuitBreaker:BreakDurationMinutes"] = "1",
                ["CentralServiceSettings:Endpoints:1:CircuitBreaker:RecoveryThreshold"] = "2",
            })
            .Build();

        var settings = UpgradeServiceCentralServiceSettings.FromConfiguration(configuration, null);
        var endpoints = settings.GetConfiguredEndpoints();

        Assert.Equal(2, endpoints.Count);
        Assert.Equal("http://primary:15700", endpoints[0].BaseUrl);
        Assert.Equal("http://backup:15700", endpoints[1].BaseUrl);
        Assert.Equal(3, endpoints[0].MaxAttempts);
        Assert.NotNull(endpoints[0].CircuitBreaker);
    }
}
