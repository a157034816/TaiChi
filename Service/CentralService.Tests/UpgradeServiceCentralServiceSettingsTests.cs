using Microsoft.Extensions.Configuration;
using Upgrade.Service;

namespace CentralService.Tests;

public sealed class UpgradeServiceCentralServiceSettingsTests
{
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
