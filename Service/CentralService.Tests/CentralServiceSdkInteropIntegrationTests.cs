using System.Net;
using System.Net.Http.Json;
using CentralService.Client;
using CentralService.Client.Errors;
using CentralService.Client.Models;
using CentralService.Admin;
using CentralService.Admin.Models;
using CentralService.Service.Models;
using CentralService.Services.ServiceCircuiting;
using Microsoft.AspNetCore.Mvc.Testing;
using ServiceSdk = CentralService.Service;

namespace CentralService.Tests;

public sealed class CentralServiceSdkInteropIntegrationTests
{
    private static WebApplicationFactoryClientOptions ClientOptions()
    {
        return new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        };
    }

    private static async Task LoginAsync(HttpClient client, string username, string password)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password));
        resp.EnsureSuccessStatusCode();
    }

    private static CentralServiceDiscoveryClient CreateSdk(CentralServiceWebApplicationFactory factory, string clientName)
    {
        var options = new CentralServiceSdkOptions("http://localhost")
        {
            Timeout = TimeSpan.FromSeconds(5),
            HttpMessageHandler = factory.Server.CreateHandler(),
            ClientIdentity = new CentralServiceClientIdentity
            {
                ClientName = clientName,
                LocalIp = "127.0.0.11",
                OperatorIp = "10.0.0.11",
                PublicIp = "110.0.0.11",
            }
        };

        return new CentralServiceDiscoveryClient(options);
    }

    private static ServiceSdk.CentralServiceServiceClient CreateServiceSdk(CentralServiceWebApplicationFactory factory)
    {
        var options = new ServiceSdk.CentralServiceSdkOptions("http://localhost")
        {
            Timeout = TimeSpan.FromSeconds(5),
            HttpMessageHandler = factory.Server.CreateHandler(),
        };

        return new ServiceSdk.CentralServiceServiceClient(options);
    }

    private static async Task<CentralService.Service.Models.ServiceInfo[]> ListServiceInstancesAsync(
        HttpClient client,
        string serviceName)
    {
        var resp = await client.GetFromJsonAsync<CentralService.Service.Models.ApiResponse<CentralService.Service.Models.ServiceListResponse>>(
            "/api/Service/list?name=" + Uri.EscapeDataString(serviceName));
        Assert.NotNull(resp);
        Assert.True(resp.Success, resp.ErrorMessage ?? string.Empty);

        return resp.Data?.Services ?? Array.Empty<CentralService.Service.Models.ServiceInfo>();
    }

    [Fact]
    public async Task SdkAccess_Succeeds_AndReports()
    {
        using var factory = new CentralServiceWebApplicationFactory();
        using var serviceSdk = CreateServiceSdk(factory);
        _ = serviceSdk.Register(new CentralService.Service.Models.ServiceRegistrationRequest
        {
            Name = "SdkAccessService",
            Host = "127.0.0.1",
            LocalIp = "127.0.0.1",
            OperatorIp = "10.20.30.40",
            PublicIp = "127.0.0.1",
            Port = 18101,
            ServiceType = "Web",
            HealthCheckType = "Http",
            HealthCheckUrl = "/health",
            Weight = 1,
        });

        using var sdk = CreateSdk(factory, "sdk-client-ok");
        var value = await sdk.AccessAsync(
            "SdkAccessService",
            _ => Task.FromResult(ServiceAccessCallbackResult<string>.FromSuccess("ok")));

        Assert.Equal("ok", value);
    }

    [Fact]
    public async Task ServiceSdk_Heartbeat_UpdatesLastHeartbeatTime_AndDeregisterRemovesInstance()
    {
        using var factory = new CentralServiceWebApplicationFactory();
        using var serviceSdk = CreateServiceSdk(factory);

        var register = serviceSdk.Register(new CentralService.Service.Models.ServiceRegistrationRequest
        {
            Name = "SdkHeartbeatService",
            Host = "127.0.0.1",
            LocalIp = "127.0.0.1",
            OperatorIp = "10.20.30.40",
            PublicIp = "127.0.0.1",
            Port = 18301,
            ServiceType = "Web",
            HealthCheckType = "Http",
            HealthCheckUrl = "/health",
            Weight = 1,
        });

        var serviceId = register.Id;
        Assert.False(string.IsNullOrWhiteSpace(serviceId));

        var apiClient = factory.CreateClient(ClientOptions());

        var beforeList = await ListServiceInstancesAsync(apiClient, "SdkHeartbeatService");
        var before = Assert.Single(beforeList);
        Assert.False(string.IsNullOrWhiteSpace(before.LastHeartbeatTime));
        var beforeHeartbeat = DateTimeOffset.Parse(before.LastHeartbeatTime);

        await Task.Delay(100);
        serviceSdk.Heartbeat(serviceId);

        var afterList = await ListServiceInstancesAsync(apiClient, "SdkHeartbeatService");
        var after = Assert.Single(afterList);
        Assert.False(string.IsNullOrWhiteSpace(after.LastHeartbeatTime));
        var afterHeartbeat = DateTimeOffset.Parse(after.LastHeartbeatTime);

        Assert.True(afterHeartbeat > beforeHeartbeat);

        serviceSdk.Deregister(serviceId);

        var emptyList = await ListServiceInstancesAsync(apiClient, "SdkHeartbeatService");
        Assert.Empty(emptyList);
    }

    [Fact]
    public async Task SdkAccess_OpensCircuit_ThenTriesNextInstance()
    {
        using var factory = new CentralServiceWebApplicationFactory();
        using var serviceSdk = CreateServiceSdk(factory);

        var registerA = serviceSdk.Register(new CentralService.Service.Models.ServiceRegistrationRequest
        {
            Name = "SdkFailoverService",
            Host = "127.0.0.1",
            LocalIp = "127.0.0.1",
            OperatorIp = "10.20.30.40",
            PublicIp = "127.0.0.1",
            Port = 18111,
            ServiceType = "Web",
            HealthCheckType = "Http",
            HealthCheckUrl = "/health",
            Weight = 10,
        });

        _ = serviceSdk.Register(new CentralService.Service.Models.ServiceRegistrationRequest
        {
            Name = "SdkFailoverService",
            Host = "127.0.0.1",
            LocalIp = "127.0.0.1",
            OperatorIp = "10.20.30.40",
            PublicIp = "127.0.0.1",
            Port = 18112,
            ServiceType = "Web",
            HealthCheckType = "Http",
            HealthCheckUrl = "/health",
            Weight = 1,
        });

        var serviceIdA = registerA.Id;

        var adminClient = factory.CreateClient(ClientOptions());
        await LoginAsync(adminClient, factory.AdminUsername, factory.AdminPassword);

        // 让一次失败就熔断，并给足 MaxAttempts 让 SDK 有机会尝试下一个实例。
        var updateResp = await adminClient.PutAsJsonAsync(
            $"/api/admin/service-circuits/instances/{serviceIdA}/config",
            new UpdateServiceCircuitConfigRequest(
                MaxAttempts: 5,
                FailureThreshold: 1,
                BreakDurationMinutes: 1,
                RecoveryThreshold: 1));
        updateResp.EnsureSuccessStatusCode();

        using var sdk = CreateSdk(factory, "sdk-client-failover");
        var value = await sdk.AccessAsync(
            "SdkFailoverService",
            context =>
            {
                if (context.Service.Port == 18111)
                {
                    return Task.FromResult(ServiceAccessCallbackResult<string>.FromFailure(
                        ServiceAccessFailureKind.Transport,
                        "connect failed"));
                }

                return Task.FromResult(ServiceAccessCallbackResult<string>.FromSuccess("ok-b"));
            });

        Assert.Equal("ok-b", value);
    }

    [Fact]
    public async Task SdkAccess_CircuitIsPerClient_OneClientOpenDoesNotBlockAnother()
    {
        using var factory = new CentralServiceWebApplicationFactory();
        using var serviceSdk = CreateServiceSdk(factory);
        var register = serviceSdk.Register(new CentralService.Service.Models.ServiceRegistrationRequest
        {
            Name = "SdkPerClientCircuitService",
            Host = "127.0.0.1",
            LocalIp = "127.0.0.1",
            OperatorIp = "10.20.30.40",
            PublicIp = "127.0.0.1",
            Port = 18401,
            ServiceType = "Web",
            HealthCheckType = "Http",
            HealthCheckUrl = "/health",
            Weight = 1,
        });

        var serviceId = register.Id;

        var adminClient = factory.CreateClient(ClientOptions());
        await LoginAsync(adminClient, factory.AdminUsername, factory.AdminPassword);

        var updateResp = await adminClient.PutAsJsonAsync(
            $"/api/admin/service-circuits/instances/{serviceId}/config",
            new UpdateServiceCircuitConfigRequest(
                MaxAttempts: 5,
                FailureThreshold: 1,
                BreakDurationMinutes: 1,
                RecoveryThreshold: 1));
        updateResp.EnsureSuccessStatusCode();

        using var sdkA = CreateSdk(factory, "sdk-client-a");
        await Assert.ThrowsAsync<CentralServiceAccessException>(() => sdkA.AccessAsync(
            "SdkPerClientCircuitService",
            _ => Task.FromResult(ServiceAccessCallbackResult<string>.FromFailure(
                ServiceAccessFailureKind.Transport,
                "connect failed"))));

        using var sdkB = CreateSdk(factory, "sdk-client-b");
        var value = await sdkB.AccessAsync(
            "SdkPerClientCircuitService",
            _ => Task.FromResult(ServiceAccessCallbackResult<string>.FromSuccess("ok")));

        Assert.Equal("ok", value);
    }

    [Fact]
    public async Task SdkAccess_WhenAllCircuitsOpen_ThrowsCentralServiceAccessException()
    {
        using var factory = new CentralServiceWebApplicationFactory();
        using var serviceSdk = CreateServiceSdk(factory);
        var register = serviceSdk.Register(new CentralService.Service.Models.ServiceRegistrationRequest
        {
            Name = "SdkCircuitOpenService",
            Host = "127.0.0.1",
            LocalIp = "127.0.0.1",
            OperatorIp = "10.20.30.40",
            PublicIp = "127.0.0.1",
            Port = 18201,
            ServiceType = "Web",
            HealthCheckType = "Http",
            HealthCheckUrl = "/health",
            Weight = 1,
        });

        var serviceId = register.Id;

        var adminClient = factory.CreateClient(ClientOptions());
        await LoginAsync(adminClient, factory.AdminUsername, factory.AdminPassword);

        var updateResp = await adminClient.PutAsJsonAsync(
            $"/api/admin/service-circuits/instances/{serviceId}/config",
            new UpdateServiceCircuitConfigRequest(
                MaxAttempts: 5,
                FailureThreshold: 1,
                BreakDurationMinutes: 1,
                RecoveryThreshold: 1));
        updateResp.EnsureSuccessStatusCode();

        using var sdk = CreateSdk(factory, "sdk-client-open");

        // 第一次回调失败后熔断立即打开，下一轮 resolve 会被中心服务拒绝（ACCESS_CIRCUIT_OPEN）。
        var ex = await Assert.ThrowsAsync<CentralServiceAccessException>(() => sdk.AccessAsync(
            "SdkCircuitOpenService",
            _ => Task.FromResult(ServiceAccessCallbackResult<string>.FromFailure(
                ServiceAccessFailureKind.Transport,
                "connect failed"))));

        Assert.Equal("SdkCircuitOpenService", ex.ServiceName);
        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }

    [Fact]
    public async Task AdminClear_RemovesOpenClients_AndAllowsRetryForAllClients()
    {
        using var factory = new CentralServiceWebApplicationFactory();
        using var serviceSdk = CreateServiceSdk(factory);
        var register = serviceSdk.Register(new CentralService.Service.Models.ServiceRegistrationRequest
        {
            Name = "SdkClearCircuitService",
            Host = "127.0.0.1",
            LocalIp = "127.0.0.1",
            OperatorIp = "10.20.30.40",
            PublicIp = "127.0.0.1",
            Port = 18501,
            ServiceType = "Web",
            HealthCheckType = "Http",
            HealthCheckUrl = "/health",
            Weight = 1,
        });
        var serviceId = register.Id;

        var adminClient = factory.CreateClient(ClientOptions());
        await LoginAsync(adminClient, factory.AdminUsername, factory.AdminPassword);

        var updateResp = await adminClient.PutAsJsonAsync(
            $"/api/admin/service-circuits/instances/{serviceId}/config",
            new UpdateServiceCircuitConfigRequest(
                MaxAttempts: 5,
                FailureThreshold: 1,
                BreakDurationMinutes: 1,
                RecoveryThreshold: 1));
        updateResp.EnsureSuccessStatusCode();

        using var sdkA = CreateSdk(factory, "sdk-clear-a");
        using var sdkB = CreateSdk(factory, "sdk-clear-b");

        Task<string> AccessFail(CentralServiceDiscoveryClient sdk)
        {
            return sdk.AccessAsync(
                "SdkClearCircuitService",
                _ => Task.FromResult(ServiceAccessCallbackResult<string>.FromFailure(
                    ServiceAccessFailureKind.Transport,
                    "connect failed")));
        }

        await Assert.ThrowsAsync<CentralServiceAccessException>(() => AccessFail(sdkA));
        await Assert.ThrowsAsync<CentralServiceAccessException>(() => AccessFail(sdkB));

        var detail = await adminClient.GetFromJsonAsync<CentralService.Service.Models.ApiResponse<ServiceCircuitServiceDetailResponse>>(
            "/api/admin/service-circuits/services/SdkClearCircuitService");
        Assert.NotNull(detail);
        Assert.True(detail.Success, detail.ErrorMessage ?? string.Empty);

        var instance = Assert.Single(detail.Data!.Instances);
        Assert.Equal(serviceId, instance.ServiceId);
        Assert.Contains(instance.OpenClients, x => x.ClientName == "sdk-clear-a");
        Assert.Contains(instance.OpenClients, x => x.ClientName == "sdk-clear-b");

        var clearResp = await adminClient.PostAsync($"/api/admin/service-circuits/instances/{serviceId}/clear", null);
        clearResp.EnsureSuccessStatusCode();

        var detailAfter = await adminClient.GetFromJsonAsync<CentralService.Service.Models.ApiResponse<ServiceCircuitServiceDetailResponse>>(
            "/api/admin/service-circuits/services/SdkClearCircuitService");
        Assert.NotNull(detailAfter);
        Assert.True(detailAfter.Success, detailAfter.ErrorMessage ?? string.Empty);

        var instanceAfter = Assert.Single(detailAfter.Data!.Instances);
        Assert.Empty(instanceAfter.OpenClients);

        Task<string> AccessOk(CentralServiceDiscoveryClient sdk)
        {
            return sdk.AccessAsync(
                "SdkClearCircuitService",
                _ => Task.FromResult(ServiceAccessCallbackResult<string>.FromSuccess("ok")));
        }

        Assert.Equal("ok", await AccessOk(sdkA));
        Assert.Equal("ok", await AccessOk(sdkB));
    }
}
