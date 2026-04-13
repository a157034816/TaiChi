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

/// <summary>
/// CentralService 与 SDK 的互操作集成测试：
/// 在进程内宿主上同时验证 Service SDK（注册/心跳/注销）与 Client SDK（Access/熔断/上报）行为一致性。
/// </summary>
/// <remarks>
/// 测试通过 <see cref="CentralServiceWebApplicationFactory"/> 提供的 Server Handler 在进程内发起请求，避免端口依赖。
/// </remarks>
public sealed class CentralServiceSdkInteropIntegrationTests
{
    /// <summary>
    /// 创建集成测试 <see cref="HttpClient"/> 的默认选项：禁用自动重定向并启用 Cookie（覆盖登录态场景）。
    /// </summary>
    private static WebApplicationFactoryClientOptions ClientOptions()
    {
        return new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        };
    }

    /// <summary>
    /// 调用登录接口，建立管理员 Cookie 会话（用于后续访问管理端 API）。
    /// </summary>
    /// <param name="client">测试用 HttpClient。</param>
    /// <param name="username">用户名。</param>
    /// <param name="password">密码。</param>
    private static async Task LoginAsync(HttpClient client, string username, string password)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password));
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            Assert.Fail($"Login failed: {(int)resp.StatusCode} {resp.StatusCode}. Body: {body}");
        }
    }

    /// <summary>
    /// 创建 Client SDK，并注入测试宿主的 <see cref="HttpMessageHandler"/> 与客户端身份信息。
    /// </summary>
    /// <param name="factory">测试宿主工厂。</param>
    /// <param name="clientName">客户端名称（用于“按客户端熔断”维度）。</param>
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

    /// <summary>
    /// 创建 Service SDK，并注入测试宿主的 <see cref="HttpMessageHandler"/>。
    /// </summary>
    /// <param name="factory">测试宿主工厂。</param>
    private static ServiceSdk.CentralServiceServiceClient CreateServiceSdk(CentralServiceWebApplicationFactory factory)
    {
        var options = new ServiceSdk.CentralServiceSdkOptions("http://localhost")
        {
            Timeout = TimeSpan.FromSeconds(5),
            HttpMessageHandler = factory.Server.CreateHandler(),
        };

        return new ServiceSdk.CentralServiceServiceClient(options);
    }

    /// <summary>
    /// 调用服务列表接口并返回指定服务名的实例列表。
    /// </summary>
    /// <param name="client">测试用 HttpClient。</param>
    /// <param name="serviceName">服务名称。</param>
    /// <returns>实例数组；若为空表示当前未注册实例。</returns>
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

    /// <summary>
    /// 验证 Client SDK Access：回调返回成功时应返回业务值，并完成访问上报流程（隐式）。
    /// </summary>
    [Fact]
    public async Task SdkAccess_Succeeds_AndReports()
    {
        using var factory = new CentralServiceWebApplicationFactory();
        using var serviceSdk = CreateServiceSdk(factory);
        var register = serviceSdk.Register(new CentralService.Service.Models.ServiceRegistrationRequest
        {
            Name = "SdkAccessService",
            Host = "127.0.0.1",
            LocalIp = "127.0.0.1",
            OperatorIp = "10.20.30.40",
            PublicIp = "127.0.0.1",
            Port = 18101,
            ServiceType = "Web",
            HealthCheckUrl = "/health",
            HeartbeatIntervalSeconds = 1,
            Weight = 1,
        });

        await using var heartbeatWs = await ServiceHeartbeatWebSocketTestClient.StartAsync(factory, register.Id);
        _ = await heartbeatWs.FirstHeartbeatHandled.WaitAsync(TimeSpan.FromSeconds(2));

        using var sdk = CreateSdk(factory, "sdk-client-ok");
        var value = await sdk.AccessAsync(
            "SdkAccessService",
            _ => Task.FromResult(ServiceAccessCallbackResult<string>.FromSuccess("ok")));

        Assert.Equal("ok", value);
    }

    /// <summary>
    /// 验证 Service SDK：心跳应更新 LastHeartbeatTime，注销后列表中应移除对应实例。
    /// </summary>
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
            HealthCheckUrl = "/health",
            HeartbeatIntervalSeconds = 1,
            Weight = 1,
        });

        var serviceId = register.Id;
        Assert.False(string.IsNullOrWhiteSpace(serviceId));

        await using var heartbeatWs = await ServiceHeartbeatWebSocketTestClient.StartAsync(factory, serviceId);
        _ = await heartbeatWs.FirstHeartbeatHandled.WaitAsync(TimeSpan.FromSeconds(2));

        var apiClient = factory.CreateClient(ClientOptions());

        var beforeList = await ListServiceInstancesAsync(apiClient, "SdkHeartbeatService");
        var before = Assert.Single(beforeList);
        Assert.False(string.IsNullOrWhiteSpace(before.LastHeartbeatTime));
        var beforeHeartbeat = DateTimeOffset.Parse(before.LastHeartbeatTime);

        await Task.Delay(TimeSpan.FromSeconds(1));

        var afterList = await ListServiceInstancesAsync(apiClient, "SdkHeartbeatService");
        var after = Assert.Single(afterList);
        Assert.False(string.IsNullOrWhiteSpace(after.LastHeartbeatTime));
        var afterHeartbeat = DateTimeOffset.Parse(after.LastHeartbeatTime);

        Assert.True(afterHeartbeat > beforeHeartbeat);

        serviceSdk.Deregister(serviceId);

        var emptyList = await ListServiceInstancesAsync(apiClient, "SdkHeartbeatService");
        Assert.Empty(emptyList);
    }

    /// <summary>
    /// 验证 Client SDK Access：当某实例连续失败触发熔断后，应自动切换到下一实例并成功返回。
    /// </summary>
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
            HealthCheckUrl = "/health",
            HeartbeatIntervalSeconds = 1,
            Weight = 10,
        });

        var registerB = serviceSdk.Register(new CentralService.Service.Models.ServiceRegistrationRequest
        {
            Name = "SdkFailoverService",
            Host = "127.0.0.1",
            LocalIp = "127.0.0.1",
            OperatorIp = "10.20.30.40",
            PublicIp = "127.0.0.1",
            Port = 18112,
            ServiceType = "Web",
            HealthCheckUrl = "/health",
            HeartbeatIntervalSeconds = 1,
            Weight = 1,
        });

        var serviceIdA = registerA.Id;

        await using var heartbeatWsA = await ServiceHeartbeatWebSocketTestClient.StartAsync(factory, registerA.Id);
        await using var heartbeatWsB = await ServiceHeartbeatWebSocketTestClient.StartAsync(factory, registerB.Id);
        _ = await heartbeatWsA.FirstHeartbeatHandled.WaitAsync(TimeSpan.FromSeconds(2));
        _ = await heartbeatWsB.FirstHeartbeatHandled.WaitAsync(TimeSpan.FromSeconds(2));

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

    /// <summary>
    /// 验证熔断按客户端维度隔离：一个客户端熔断打开不应影响另一个客户端的访问尝试。
    /// </summary>
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
            HealthCheckUrl = "/health",
            HeartbeatIntervalSeconds = 1,
            Weight = 1,
        });

        var serviceId = register.Id;

        await using var heartbeatWs = await ServiceHeartbeatWebSocketTestClient.StartAsync(factory, serviceId);
        _ = await heartbeatWs.FirstHeartbeatHandled.WaitAsync(TimeSpan.FromSeconds(2));

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

    /// <summary>
    /// 验证当所有候选实例的熔断均处于打开状态时，Access 应抛出 <see cref="CentralServiceAccessException"/>。
    /// </summary>
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
            HealthCheckUrl = "/health",
            HeartbeatIntervalSeconds = 1,
            Weight = 1,
        });

        var serviceId = register.Id;

        await using var heartbeatWs = await ServiceHeartbeatWebSocketTestClient.StartAsync(factory, serviceId);
        _ = await heartbeatWs.FirstHeartbeatHandled.WaitAsync(TimeSpan.FromSeconds(2));

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

    /// <summary>
    /// 验证管理员清除熔断状态后，所有客户端应可重新尝试访问并成功返回。
    /// </summary>
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
            HealthCheckUrl = "/health",
            HeartbeatIntervalSeconds = 1,
            Weight = 1,
        });
        var serviceId = register.Id;

        await using var heartbeatWs = await ServiceHeartbeatWebSocketTestClient.StartAsync(factory, serviceId);
        _ = await heartbeatWs.FirstHeartbeatHandled.WaitAsync(TimeSpan.FromSeconds(2));

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
