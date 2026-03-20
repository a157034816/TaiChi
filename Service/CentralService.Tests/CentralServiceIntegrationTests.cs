using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CentralService.Admin;
using CentralService.Admin.Config;
using CentralService.Admin.Models;
using CentralService.Service.Models;
using CentralService.Services.ServiceCircuiting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CentralService.Tests;

public sealed class CentralServiceIntegrationTests
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

        var body = await resp.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
    }

    [Fact]
    public async Task ServiceContract_RegisterHeartbeatDeregister_Works()
    {
        using var factory = new CentralServiceWebApplicationFactory();
        var client = factory.CreateClient(ClientOptions());

        var registerRequest = new ServiceRegistrationRequest
        {
            Id = string.Empty,
            Name = "TestService",
            Host = "127.0.0.1",
            LocalIp = "192.168.3.249",
            OperatorIp = "218.71.4.73",
            PublicIp = "95.40.60.113",
            Port = 12345,
            ServiceType = "Web",
            HealthCheckType = "Http",
            HealthCheckUrl = "/health",
            Weight = 1,
        };

        var registerResp = await client.PostAsJsonAsync("/api/Service/register", registerRequest);
        registerResp.EnsureSuccessStatusCode();

        var registerBody = await registerResp.Content.ReadFromJsonAsync<ApiResponse<ServiceRegistrationResponse>>();
        Assert.NotNull(registerBody);
        Assert.True(registerBody!.Success);
        Assert.NotNull(registerBody.Data);
        Assert.False(string.IsNullOrWhiteSpace(registerBody.Data.Id));

        var serviceId = registerBody.Data.Id;

        var listResp = await client.GetAsync("/api/Service/list");
        listResp.EnsureSuccessStatusCode();

        var listBody = await listResp.Content.ReadFromJsonAsync<ApiResponse<ServiceListResponse>>();
        Assert.NotNull(listBody);
        Assert.True(listBody!.Success);
        var registered = Assert.Single(listBody.Data.Services, x => x.Id == serviceId);
        Assert.Equal("192.168.3.249", registered.LocalIp);
        Assert.Equal("218.71.4.73", registered.OperatorIp);
        Assert.Equal("95.40.60.113", registered.PublicIp);

        var heartbeatResp = await client.PostAsJsonAsync(
            "/api/Service/heartbeat",
            new ServiceHeartbeatRequest { Id = serviceId });
        Assert.Equal(HttpStatusCode.OK, heartbeatResp.StatusCode);

        var deregisterResp = await client.DeleteAsync($"/api/Service/deregister/{serviceId}");
        Assert.Equal(HttpStatusCode.OK, deregisterResp.StatusCode);

        var heartbeatAfterDeregisterResp = await client.PostAsJsonAsync(
            "/api/Service/heartbeat",
            new ServiceHeartbeatRequest { Id = serviceId });
        Assert.Equal(HttpStatusCode.NotFound, heartbeatAfterDeregisterResp.StatusCode);
    }

    [Fact]
    public async Task Auth_And_Rbac_Permissions_Are_Enforced()
    {
        using var factory = new CentralServiceWebApplicationFactory();

        var adminClient = factory.CreateClient(ClientOptions());
        var unauthResp = await adminClient.GetAsync("/api/admin/config/current");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthResp.StatusCode);

        await LoginAsync(adminClient, factory.AdminUsername, factory.AdminPassword);

        var roleName = $"ConfigReader_{Guid.NewGuid():N}";
        var roleResp = await adminClient.PostAsJsonAsync(
            "/api/admin/rbac/roles",
            new CreateRoleRequest(roleName, "test role"));
        roleResp.EnsureSuccessStatusCode();

        var roleBody = await roleResp.Content.ReadFromJsonAsync<ApiResponse<RoleDto>>();
        Assert.NotNull(roleBody);
        Assert.True(roleBody!.Success);
        var roleId = roleBody.Data.Id;

        var setPermResp = await adminClient.PutAsJsonAsync(
            $"/api/admin/rbac/roles/{roleId}/permissions",
            new SetRolePermissionsRequest(new[] { CentralServicePermissions.Config.Read }));
        setPermResp.EnsureSuccessStatusCode();

        var username = $"user_{Guid.NewGuid():N}";
        var password = "user123!";

        var createUserResp = await adminClient.PostAsJsonAsync(
            "/api/admin/rbac/users",
            new CreateUserRequest(username, password, new[] { roleId }));
        createUserResp.EnsureSuccessStatusCode();

        var limitedClient = factory.CreateClient(ClientOptions());
        await LoginAsync(limitedClient, username, password);

        var configCurrentResp = await limitedClient.GetAsync("/api/admin/config/current");
        if (configCurrentResp.StatusCode != HttpStatusCode.OK)
        {
            var body = await configCurrentResp.Content.ReadAsStringAsync();
            Assert.Fail($"Expected 200 OK, got {(int)configCurrentResp.StatusCode} {configCurrentResp.StatusCode}. Body: {body}");
        }

        var createDraftResp = await limitedClient.PostAsJsonAsync(
            "/api/admin/config/versions",
            new CreateConfigDraftRequest("draft", null));
        Assert.Equal(HttpStatusCode.Forbidden, createDraftResp.StatusCode);

        var monitoringResp = await limitedClient.GetAsync("/api/admin/monitoring/summary");
        Assert.Equal(HttpStatusCode.Forbidden, monitoringResp.StatusCode);
    }

    [Fact]
    public async Task Bootstrap_Status_Should_Report_Disabled_When_Config_Turns_It_Off()
    {
        using var factory = new CentralServiceWebApplicationFactory(
            additionalSettings: new Dictionary<string, string?>
            {
                ["CentralServiceAuth:Bootstrap:Enabled"] = "false",
                ["CentralServiceAdminSeed:Enabled"] = "false",
            });
        var client = factory.CreateClient(ClientOptions());

        var response = await client.GetFromJsonAsync<ApiResponse<BootstrapStatusResponse>>("/api/auth/bootstrap/status");

        Assert.NotNull(response);
        Assert.True(response!.Success);
        Assert.False(response.Data.Enabled);
        Assert.False(response.Data.CanBootstrap);
        Assert.Contains("CentralServiceAuth:Bootstrap:Enabled", response.Data.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Config_Publish_And_Rollback_Works()
    {
        using var factory = new CentralServiceWebApplicationFactory();
        var client = factory.CreateClient(ClientOptions());

        await LoginAsync(client, factory.AdminUsername, factory.AdminPassword);

        var draft1Resp = await client.PostAsJsonAsync(
            "/api/admin/config/versions",
            new CreateConfigDraftRequest("draft1", null));
        if (!draft1Resp.IsSuccessStatusCode)
        {
            var body = await draft1Resp.Content.ReadAsStringAsync();
            Assert.Fail($"Create draft failed: {(int)draft1Resp.StatusCode} {draft1Resp.StatusCode}. Body: {body}");
        }

        var draft1Body = await draft1Resp.Content.ReadFromJsonAsync<ApiResponse<ConfigVersionDetail>>();
        Assert.NotNull(draft1Body);
        Assert.True(draft1Body!.Success);
        var draft1Id = draft1Body.Data.Id;

        var update1Resp = await client.PutAsJsonAsync(
            $"/api/admin/config/versions/{draft1Id}",
            new UpdateConfigDraftRequest(CentralServiceRuntimeConfigJson.DefaultJson, "draft1"));
        update1Resp.EnsureSuccessStatusCode();

        var publish1Resp = await client.PostAsJsonAsync(
            $"/api/admin/config/versions/{draft1Id}/publish",
            new PublishConfigRequest("publish1"));
        publish1Resp.EnsureSuccessStatusCode();

        var currentAfterPublish1Resp = await client.GetFromJsonAsync<ApiResponse<CurrentConfigResponse>>(
            "/api/admin/config/current");
        Assert.NotNull(currentAfterPublish1Resp);
        Assert.True(currentAfterPublish1Resp!.Success);
        Assert.Equal(draft1Id, currentAfterPublish1Resp.Data.CurrentVersionId);

        var draft2Resp = await client.PostAsJsonAsync(
            "/api/admin/config/versions",
            new CreateConfigDraftRequest("draft2", currentAfterPublish1Resp.Data.CurrentVersionId));
        draft2Resp.EnsureSuccessStatusCode();

        var draft2Body = await draft2Resp.Content.ReadFromJsonAsync<ApiResponse<ConfigVersionDetail>>();
        Assert.NotNull(draft2Body);
        Assert.True(draft2Body!.Success);
        var draft2Id = draft2Body.Data.Id;

        var update2Resp = await client.PutAsJsonAsync(
            $"/api/admin/config/versions/{draft2Id}",
            new UpdateConfigDraftRequest(CentralServiceRuntimeConfigJson.DefaultJson, "draft2"));
        update2Resp.EnsureSuccessStatusCode();

        var publish2Resp = await client.PostAsJsonAsync(
            $"/api/admin/config/versions/{draft2Id}/publish",
            new PublishConfigRequest("publish2"));
        publish2Resp.EnsureSuccessStatusCode();

        var currentAfterPublish2Resp = await client.GetFromJsonAsync<ApiResponse<CurrentConfigResponse>>(
            "/api/admin/config/current");
        Assert.NotNull(currentAfterPublish2Resp);
        Assert.True(currentAfterPublish2Resp!.Success);
        Assert.Equal(draft2Id, currentAfterPublish2Resp.Data.CurrentVersionId);

        var rollbackResp = await client.PostAsJsonAsync(
            $"/api/admin/config/versions/{draft1Id}/rollback",
            new RollbackConfigRequest("rollback"));
        rollbackResp.EnsureSuccessStatusCode();

        var currentAfterRollbackResp = await client.GetFromJsonAsync<ApiResponse<CurrentConfigResponse>>(
            "/api/admin/config/current");
        Assert.NotNull(currentAfterRollbackResp);
        Assert.True(currentAfterRollbackResp!.Success);
        Assert.Equal(draft1Id, currentAfterRollbackResp.Data.CurrentVersionId);
    }

    [Fact]
    public async Task Audit_List_Works()
    {
        using var factory = new CentralServiceWebApplicationFactory();
        var client = factory.CreateClient(ClientOptions());

        await LoginAsync(client, factory.AdminUsername, factory.AdminPassword);

        var resp = await client.GetAsync("/api/admin/audit?page=1&pageSize=8");
        if (!resp.IsSuccessStatusCode)
        {
            var bodyText = await resp.Content.ReadAsStringAsync();
            Assert.Fail(
                $"Expected 200 OK, got {(int)resp.StatusCode} {resp.StatusCode}. Body: {bodyText}");
        }

        var body = await resp.Content.ReadFromJsonAsync<ApiResponse<PagedResult<AuditLogListItem>>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.NotNull(body.Data);
        Assert.Equal(1, body.Data.Page);
        Assert.Equal(8, body.Data.PageSize);
        Assert.NotNull(body.Data.Items);
    }

    [Fact]
    public async Task ServiceRegister_ShouldPersistCircuitConfig_AsJsonGroupedByServiceName()
    {
        using var factory = new CentralServiceWebApplicationFactory();
        var client = factory.CreateClient(ClientOptions());

        var registerRequestA = new ServiceRegistrationRequest
        {
            Name = "JsonService",
            Host = "127.0.0.1",
            LocalIp = "127.0.0.1",
            OperatorIp = "10.20.30.40",
            PublicIp = "127.0.0.1",
            Port = 18001,
            ServiceType = "Web",
            HealthCheckType = "Http",
            HealthCheckUrl = "/health",
            Weight = 1,
        };

        var registerRequestB = new ServiceRegistrationRequest
        {
            Name = "JsonService",
            Host = "127.0.0.1",
            LocalIp = "127.0.0.1",
            OperatorIp = "10.20.30.40",
            PublicIp = "127.0.0.1",
            Port = 18002,
            ServiceType = "Web",
            HealthCheckType = "Http",
            HealthCheckUrl = "/health",
            Weight = 1,
        };

        var registerRespA = await client.PostAsJsonAsync("/api/Service/register", registerRequestA);
        registerRespA.EnsureSuccessStatusCode();

        var registerRespB = await client.PostAsJsonAsync("/api/Service/register", registerRequestB);
        registerRespB.EnsureSuccessStatusCode();

        var servicesJson = await File.ReadAllTextAsync(factory.ServiceCircuitJsonPath);
        using var servicesDocument = JsonDocument.Parse(servicesJson);
        Assert.True(servicesDocument.RootElement.TryGetProperty("JsonService", out var instancesElement));
        Assert.Equal(JsonValueKind.Array, instancesElement.ValueKind);
        var instances = instancesElement.EnumerateArray().ToArray();
        Assert.Equal(2, instances.Length);
        Assert.Contains(instances, item => item.GetProperty("port").GetInt32() == 18001);
        Assert.Contains(instances, item => item.GetProperty("port").GetInt32() == 18002);

        var tomlText = await File.ReadAllTextAsync(factory.ServiceCircuitTomlPath);
        Assert.Contains("[defaults]", tomlText, StringComparison.Ordinal);
        Assert.Contains("[defaults.circuitBreaker]", tomlText, StringComparison.Ordinal);
        Assert.Contains("staleDays = 30", tomlText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ServiceAccess_ShouldTrackCircuitPerClient_AndAdminCanClear()
    {
        using var factory = new CentralServiceWebApplicationFactory();
        var runtimeClient = factory.CreateClient(ClientOptions());

        var registerResp = await runtimeClient.PostAsJsonAsync(
            "/api/Service/register",
            new ServiceRegistrationRequest
            {
                Name = "AccessService",
                Host = "127.0.0.1",
                LocalIp = "127.0.0.1",
                OperatorIp = "10.20.30.40",
                PublicIp = "127.0.0.1",
                Port = 18002,
                ServiceType = "Web",
                HealthCheckType = "Http",
                HealthCheckUrl = "/health",
                Weight = 1,
            });
        registerResp.EnsureSuccessStatusCode();

        var registerBody = await registerResp.Content.ReadFromJsonAsync<ApiResponse<ServiceRegistrationResponse>>();
        Assert.NotNull(registerBody);
        var serviceId = registerBody!.Data.Id;

        for (var index = 0; index < 3; index++)
        {
            var resolveResp = await runtimeClient.PostAsJsonAsync(
                "/api/ServiceAccess/resolve",
                new ServiceAccessResolveRequest
                {
                    ServiceName = "AccessService",
                    ClientName = "client-a",
                    ClientLocalIp = "127.0.0.11",
                    ClientOperatorIp = "10.0.0.11",
                    ClientPublicIp = "110.1.1.11",
                });
            resolveResp.EnsureSuccessStatusCode();

            var resolveBody = await resolveResp.Content.ReadFromJsonAsync<ApiResponse<ServiceAccessResolveResponse>>();
            Assert.NotNull(resolveBody);
            Assert.True(resolveBody!.Success);

            var reportResp = await runtimeClient.PostAsJsonAsync(
                "/api/ServiceAccess/report",
                new ServiceAccessReportRequest
                {
                    AccessTicket = resolveBody.Data.AccessTicket,
                    ClientName = "client-a",
                    ClientLocalIp = "127.0.0.11",
                    ClientOperatorIp = "10.0.0.11",
                    ClientPublicIp = "110.1.1.11",
                    Success = false,
                    FailureKind = "Transport",
                    FailureMessage = "connect failed",
                });
            reportResp.EnsureSuccessStatusCode();
        }

        var blockedResolve = await runtimeClient.PostAsJsonAsync(
            "/api/ServiceAccess/resolve",
            new ServiceAccessResolveRequest
            {
                ServiceName = "AccessService",
                ClientName = "client-a",
                ClientLocalIp = "127.0.0.11",
                ClientOperatorIp = "10.0.0.11",
                ClientPublicIp = "110.1.1.11",
            });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, blockedResolve.StatusCode);

        var blockedBody = await blockedResolve.Content.ReadFromJsonAsync<ApiResponse<ServiceAccessResolveResponse>>();
        Assert.NotNull(blockedBody);
        Assert.False(blockedBody!.Success);
        Assert.Equal(ServiceAccessErrorKeys.CircuitOpen, blockedBody.ErrorKey);

        var otherClientResolve = await runtimeClient.PostAsJsonAsync(
            "/api/ServiceAccess/resolve",
            new ServiceAccessResolveRequest
            {
                ServiceName = "AccessService",
                ClientName = "client-b",
                ClientLocalIp = "127.0.0.12",
                ClientOperatorIp = "10.0.0.12",
                ClientPublicIp = "110.1.1.12",
            });
        otherClientResolve.EnsureSuccessStatusCode();

        var adminClient = factory.CreateClient(ClientOptions());
        await LoginAsync(adminClient, factory.AdminUsername, factory.AdminPassword);

        var detailResp = await adminClient.GetFromJsonAsync<ApiResponse<ServiceCircuitServiceDetailResponse>>(
            "/api/admin/service-circuits/services/AccessService");
        Assert.NotNull(detailResp);
        Assert.True(detailResp!.Success);
        var instance = Assert.Single(detailResp.Data.Instances, x => x.ServiceId == serviceId);
        var openClient = Assert.Single(instance.OpenClients);
        Assert.Equal("client-a", openClient.ClientName);

        var clearResp = await adminClient.PostAsJsonAsync(
            $"/api/admin/service-circuits/instances/{serviceId}/clear",
            new { });
        clearResp.EnsureSuccessStatusCode();

        var clearedDetail = await adminClient.GetFromJsonAsync<ApiResponse<ServiceCircuitServiceDetailResponse>>(
            "/api/admin/service-circuits/services/AccessService");
        Assert.NotNull(clearedDetail);
        Assert.True(clearedDetail!.Success);
        var clearedInstance = Assert.Single(clearedDetail.Data.Instances, x => x.ServiceId == serviceId);
        Assert.Empty(clearedInstance.OpenClients);
    }

    [Fact]
    public async Task ServiceAccess_AdminCanUpdateCircuitConfig()
    {
        using var factory = new CentralServiceWebApplicationFactory();
        var runtimeClient = factory.CreateClient(ClientOptions());

        var registerResp = await runtimeClient.PostAsJsonAsync(
            "/api/Service/register",
            new ServiceRegistrationRequest
            {
                Name = "ConfigurableService",
                Host = "127.0.0.1",
                LocalIp = "127.0.0.1",
                OperatorIp = "10.20.30.41",
                PublicIp = "127.0.0.1",
                Port = 18003,
                ServiceType = "Web",
                HealthCheckType = "Http",
                HealthCheckUrl = "/health",
                Weight = 1,
            });
        registerResp.EnsureSuccessStatusCode();

        var registerBody = await registerResp.Content.ReadFromJsonAsync<ApiResponse<ServiceRegistrationResponse>>();
        Assert.NotNull(registerBody);
        var serviceId = registerBody!.Data.Id;

        var adminClient = factory.CreateClient(ClientOptions());
        await LoginAsync(adminClient, factory.AdminUsername, factory.AdminPassword);

        var updateResp = await adminClient.PutAsJsonAsync(
            $"/api/admin/service-circuits/instances/{serviceId}/config",
            new UpdateServiceCircuitConfigRequest(
                MaxAttempts: 4,
                FailureThreshold: 5,
                BreakDurationMinutes: 2,
                RecoveryThreshold: 3));
        updateResp.EnsureSuccessStatusCode();

        var detailResp = await adminClient.GetFromJsonAsync<ApiResponse<ServiceCircuitServiceDetailResponse>>(
            "/api/admin/service-circuits/services/ConfigurableService");
        Assert.NotNull(detailResp);
        Assert.True(detailResp!.Success);
        var instance = Assert.Single(detailResp.Data.Instances, x => x.ServiceId == serviceId);
        Assert.Equal(4, instance.MaxAttempts);
        Assert.Equal(5, instance.FailureThreshold);
        Assert.Equal(2, instance.BreakDurationMinutes);
        Assert.Equal(3, instance.RecoveryThreshold);
    }
}
