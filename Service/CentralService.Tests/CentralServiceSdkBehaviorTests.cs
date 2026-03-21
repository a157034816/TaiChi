using System.Net;
using CentralService.Client;
using CentralService.Client.Errors;
using CentralService.Client.Internal;
using CentralService.Client.Models;
using CentralService.Shared.Internal;
using ServiceSdk = CentralService.Service;
using ServiceModels = CentralService.Service.Models;

namespace CentralService.Tests;

/// <summary>
/// CentralService SDK 行为测试：覆盖发现（Discovery）、服务端客户端（ServiceClient）、扇出注册（Fanout）与熔断器状态机。
/// </summary>
public sealed class CentralServiceSdkBehaviorTests
{
    /// <summary>
    /// 验证 Discovery SDK Options：端点按 Priority 升序排序，并补齐默认 MaxAttempts。
    /// </summary>
    [Fact]
    public void DiscoverySdkOptions_NormalizesPriorityOrder_AndDefaultMaxAttempts()
    {
        var options = new CentralServiceSdkOptions(
            new[]
            {
                new CentralServiceSdkOptions.CentralServiceEndpointOptions("http://127.0.0.1:5002/")
                {
                    Priority = 5,
                    MaxAttempts = 0,
                },
                new CentralServiceSdkOptions.CentralServiceEndpointOptions("http://127.0.0.1:5001/")
                {
                    Priority = 0,
                    MaxAttempts = null,
                },
            });

        Assert.Equal("http://127.0.0.1:5001", options.BaseUrl);
        Assert.Collection(
            options.Endpoints,
            endpoint =>
            {
                Assert.Equal("http://127.0.0.1:5001", endpoint.BaseUrl);
                Assert.Equal(0, endpoint.Priority);
                Assert.Equal(2, endpoint.MaxAttempts);
            },
            endpoint =>
            {
                Assert.Equal("http://127.0.0.1:5002", endpoint.BaseUrl);
                Assert.Equal(5, endpoint.Priority);
                Assert.Equal(2, endpoint.MaxAttempts);
            });
    }

    /// <summary>
    /// 验证发现客户端在传输层失败（连接失败等）时会自动切换到下一可用端点。
    /// </summary>
    [Fact]
    public void DiscoveryClient_FailsOver_OnTransportException()
    {
        using var backupServer = new LoopbackHttpServer((_, _) =>
            LoopbackHttpResponse.Json(HttpStatusCode.OK, SerializeClientModel(CreateDiscoveredService())));

        var primaryPort = LoopbackHttpServer.GetUnusedPort();
        var options = new CentralServiceSdkOptions(
            new[]
            {
                new CentralServiceSdkOptions.CentralServiceEndpointOptions($"http://127.0.0.1:{primaryPort}")
                {
                    Priority = 0,
                    MaxAttempts = 1,
                },
                new CentralServiceSdkOptions.CentralServiceEndpointOptions(backupServer.BaseUrl)
                {
                    Priority = 1,
                    MaxAttempts = 1,
                }
            });

        using var client = new CentralServiceDiscoveryClient(options);
        var serviceInfo = client.DiscoverBest("UpgradeService");

        Assert.Equal("UpgradeService", serviceInfo.Name);
        Assert.Equal("svc-upgrade", serviceInfo.Id);
        Assert.Equal(1, backupServer.RequestCount);
    }

    /// <summary>
    /// 验证单端点重试：在同一端点达到 MaxAttempts 次数前不应切换到备份端点。
    /// </summary>
    [Fact]
    public void DiscoveryClient_RetriesSingleEndpoint_MaxAttemptsBeforeFallback()
    {
        using var primaryServer = new LoopbackHttpServer((_, _) => LoopbackHttpResponse.HangConnection(300));
        using var backupServer = new LoopbackHttpServer((_, _) =>
            LoopbackHttpResponse.Json(HttpStatusCode.OK, SerializeClientModel(CreateDiscoveredService())));

        var options = new CentralServiceSdkOptions(
            new[]
            {
                new CentralServiceSdkOptions.CentralServiceEndpointOptions(primaryServer.BaseUrl)
                {
                    Priority = 0,
                    MaxAttempts = 3,
                },
                new CentralServiceSdkOptions.CentralServiceEndpointOptions(backupServer.BaseUrl)
                {
                    Priority = 1,
                    MaxAttempts = 1,
                }
            });
        options.Timeout = TimeSpan.FromMilliseconds(100);

        using var client = new CentralServiceDiscoveryClient(options);
        var serviceInfo = client.DiscoverBest("UpgradeService");

        Assert.Equal("svc-upgrade", serviceInfo.Id);
        Assert.Equal(3, primaryServer.RequestCount);
        Assert.Equal(1, backupServer.RequestCount);
    }

    /// <summary>
    /// 验证业务失败不触发故障转移：例如服务返回 404/业务错误时，应直接失败并保留原始错误信息。
    /// </summary>
    [Fact]
    public void DiscoveryClient_DoesNotFailOver_OnBusinessFailure()
    {
        using var primaryServer = new LoopbackHttpServer((_, _) =>
            LoopbackHttpResponse.PlainText(HttpStatusCode.NotFound, "service not found"));
        using var backupServer = new LoopbackHttpServer((_, _) =>
            LoopbackHttpResponse.Json(HttpStatusCode.OK, SerializeClientModel(CreateDiscoveredService())));

        var options = new CentralServiceSdkOptions(
            new[]
            {
                new CentralServiceSdkOptions.CentralServiceEndpointOptions(primaryServer.BaseUrl)
                {
                    Priority = 0,
                    MaxAttempts = 1,
                },
                new CentralServiceSdkOptions.CentralServiceEndpointOptions(backupServer.BaseUrl)
                {
                    Priority = 1,
                    MaxAttempts = 1,
                }
            });

        using var client = new CentralServiceDiscoveryClient(options);
        var exception = Assert.Throws<CentralServiceException>(() => client.DiscoverBest("UpgradeService"));

        Assert.Equal(CentralServiceErrorKind.PlainText, exception.Error.Kind);
        Assert.Contains(primaryServer.BaseUrl, exception.Error.Message);
        Assert.Equal(1, primaryServer.RequestCount);
        Assert.Equal(0, backupServer.RequestCount);
    }

    /// <summary>
    /// 验证熔断打开后会跳过对应端点：在后续请求中应直接选择下一个端点，避免重复打到不可用实例。
    /// </summary>
    [Fact]
    public void DiscoveryClient_SkipsOpenCircuitEndpoint_OnSubsequentRequests()
    {
        using var primaryServer = new LoopbackHttpServer((_, _) => LoopbackHttpResponse.HangConnection(300));
        using var backupServer = new LoopbackHttpServer((_, _) =>
            LoopbackHttpResponse.Json(HttpStatusCode.OK, SerializeClientModel(CreateDiscoveredService())));

        var options = new CentralServiceSdkOptions(
            new[]
            {
                new CentralServiceSdkOptions.CentralServiceEndpointOptions(primaryServer.BaseUrl)
                {
                    Priority = 0,
                    MaxAttempts = 1,
                    CircuitBreaker = new CentralServiceSdkOptions.CentralServiceCircuitBreakerOptions
                    {
                        FailureThreshold = 1,
                        BreakDurationMinutes = 1,
                        RecoveryThreshold = 1,
                    },
                },
                new CentralServiceSdkOptions.CentralServiceEndpointOptions(backupServer.BaseUrl)
                {
                    Priority = 1,
                    MaxAttempts = 1,
                }
            });
        options.Timeout = TimeSpan.FromMilliseconds(100);

        using var client = new CentralServiceDiscoveryClient(options);

        var first = client.DiscoverBest("UpgradeService");
        var second = client.DiscoverBest("UpgradeService");

        Assert.Equal("svc-upgrade", first.Id);
        Assert.Equal("svc-upgrade", second.Id);
        Assert.Equal(1, primaryServer.RequestCount);
        Assert.Equal(2, backupServer.RequestCount);
    }

    /// <summary>
    /// 验证 ServiceClient 在传输层失败时会自动切换端点（与 DiscoveryClient 行为一致）。
    /// </summary>
    [Fact]
    public void ServiceClient_FailsOver_OnTransportException()
    {
        using var backupServer = new LoopbackHttpServer((request, _) =>
        {
            Assert.Equal("POST", request.Method);
            Assert.Equal("/api/Service/register", request.PathAndQuery);
            return LoopbackHttpResponse.Json(
                HttpStatusCode.OK,
                SerializeServiceModel(new ServiceModels.ApiResponse<ServiceModels.ServiceRegistrationResponse>
                {
                    Success = true,
                    Data = new ServiceModels.ServiceRegistrationResponse
                    {
                        Id = "register-1",
                        RegisterTimestamp = 123L,
                    },
                }));
        });

        var primaryPort = LoopbackHttpServer.GetUnusedPort();
        var options = new ServiceSdk.CentralServiceSdkOptions(
            new[]
            {
                new ServiceSdk.CentralServiceSdkOptions.CentralServiceEndpointOptions($"http://127.0.0.1:{primaryPort}")
                {
                    Priority = 0,
                    MaxAttempts = 1,
                },
                new ServiceSdk.CentralServiceSdkOptions.CentralServiceEndpointOptions(backupServer.BaseUrl)
                {
                    Priority = 1,
                    MaxAttempts = 1,
                }
            });

        using var client = new ServiceSdk.CentralServiceServiceClient(options);
        var response = client.Register(new ServiceModels.ServiceRegistrationRequest
        {
            Name = "UpgradeService",
            Host = "127.0.0.1",
            LocalIp = "127.0.0.1",
            OperatorIp = string.Empty,
            PublicIp = string.Empty,
            Port = 5288,
            ServiceType = "Web",
            HealthCheckType = "Http",
            HealthCheckUrl = "/health",
            Weight = 1,
            Metadata = new Dictionary<string, string>(),
        });

        Assert.Equal("register-1", response.Id);
        Assert.Equal(1, backupServer.RequestCount);
    }

    /// <summary>
    /// 验证服务扇出注册：同一个稳定的 ServiceId 能跨多个端点复用，便于客户端侧一致性追踪。
    /// </summary>
    [Fact]
    public void ServiceFanout_CanReuseSingleStableServiceIdAcrossEndpoints()
    {
        const string sharedServiceId = "shared-service-id";
        var registerBodies = new List<string>();
        var heartbeatBodies = new List<string>();
        var deregisterPaths = new List<string>();

        using var primaryServer = CreateRegistrationServer(sharedServiceId, registerBodies, heartbeatBodies, deregisterPaths);
        using var backupServer = CreateRegistrationServer(sharedServiceId, registerBodies, heartbeatBodies, deregisterPaths);

        var clients = new[]
        {
            CreateServiceClient(primaryServer.BaseUrl),
            CreateServiceClient(backupServer.BaseUrl),
        };

        try
        {
            var request = new ServiceModels.ServiceRegistrationRequest
            {
                Id = sharedServiceId,
                Name = "UpgradeService",
                Host = "127.0.0.1",
                LocalIp = "127.0.0.1",
                OperatorIp = string.Empty,
                PublicIp = string.Empty,
                Port = 5288,
                ServiceType = "Web",
                HealthCheckType = "Http",
                HealthCheckUrl = "/health",
                Weight = 1,
                Metadata = new Dictionary<string, string>(),
            };

            foreach (var client in clients)
            {
                var response = client.Register(request);
                Assert.Equal(sharedServiceId, response.Id);
            }

            foreach (var client in clients)
            {
                client.Heartbeat(sharedServiceId);
                client.Deregister(sharedServiceId);
            }
        }
        finally
        {
            foreach (var client in clients)
            {
                client.Dispose();
            }
        }

        Assert.Equal(2, registerBodies.Count);
        Assert.All(registerBodies, body => Assert.Contains(sharedServiceId, body));
        Assert.Equal(2, heartbeatBodies.Count);
        Assert.All(heartbeatBodies, body => Assert.Contains(sharedServiceId, body));
        Assert.Equal(2, deregisterPaths.Count);
        Assert.All(deregisterPaths, path => Assert.EndsWith("/" + sharedServiceId, path));
    }

    /// <summary>
    /// 验证熔断器状态机：达到失败阈值后应进入 Open，并在 BreakDuration 后允许 Half-Open 探测请求。
    /// </summary>
    [Fact]
    public void CircuitBreaker_Opens_AndAllowsHalfOpenAfterDuration()
    {
        var state = CreateCircuitBreakerState(1, TimeSpan.FromMinutes(1), 2);
        var start = new DateTimeOffset(2026, 3, 13, 0, 0, 0, TimeSpan.Zero);

        Assert.True(TryAllowRequest(state, start, out _));
        ReportFailure(state, start);

        Assert.False(TryAllowRequest(state, start.AddSeconds(30), out var skipReason));
        Assert.Contains("熔断开启", skipReason);

        Assert.True(TryAllowRequest(state, start.AddMinutes(1).AddSeconds(1), out _));
    }

    /// <summary>
    /// 验证熔断器在 Half-Open 阶段满足连续成功次数后应关闭（恢复到 Closed）。
    /// </summary>
    [Fact]
    public void CircuitBreaker_ClosesAfterRecoveryThresholdSuccesses()
    {
        var state = CreateCircuitBreakerState(2, TimeSpan.FromMinutes(1), 2);
        var start = new DateTimeOffset(2026, 3, 13, 0, 0, 0, TimeSpan.Zero);

        ReportFailure(state, start);
        Assert.True(TryAllowRequest(state, start.AddSeconds(1), out _));

        ReportFailure(state, start.AddSeconds(1));
        Assert.False(TryAllowRequest(state, start.AddSeconds(2), out _));

        var reopenAt = start.AddMinutes(1).AddSeconds(1);
        Assert.True(TryAllowRequest(state, reopenAt, out _));
        ReportSuccess(state);

        Assert.True(TryAllowRequest(state, reopenAt.AddSeconds(1), out _));
        ReportSuccess(state);

        ReportFailure(state, reopenAt.AddSeconds(2));
        Assert.True(TryAllowRequest(state, reopenAt.AddSeconds(2), out _));
    }

    /// <summary>
    /// 验证 Half-Open 期间若再次失败，应重新打开熔断并延长不可用窗口。
    /// </summary>
    [Fact]
    public void CircuitBreaker_Reopens_WhenHalfOpenFails()
    {
        var state = CreateCircuitBreakerState(1, TimeSpan.FromMinutes(1), 1);
        var start = new DateTimeOffset(2026, 3, 13, 0, 0, 0, TimeSpan.Zero);

        ReportFailure(state, start);
        Assert.True(TryAllowRequest(state, start.AddMinutes(1).AddSeconds(1), out _));

        ReportFailure(state, start.AddMinutes(1).AddSeconds(1));
        Assert.False(TryAllowRequest(state, start.AddMinutes(1).AddSeconds(30), out _));
    }

    /// <summary>
    /// 使用 Client SDK 的序列化器生成 JSON（用于构造模拟发现接口返回的 payload）。
    /// </summary>
    /// <typeparam name="T">模型类型。</typeparam>
    /// <param name="value">待序列化对象。</param>
    /// <returns>JSON 字符串。</returns>
    private static string SerializeClientModel<T>(T value)
    {
        return CentralServiceJson.Serialize(value);
    }

    /// <summary>
    /// 使用 Service SDK 的序列化器生成 JSON（用于构造模拟服务端返回的 payload）。
    /// </summary>
    /// <typeparam name="T">模型类型。</typeparam>
    /// <param name="value">待序列化对象。</param>
    /// <returns>JSON 字符串。</returns>
    private static string SerializeServiceModel<T>(T value)
    {
        return ServiceSdk.Internal.CentralServiceJson.Serialize(value);
    }

    /// <summary>
    /// 创建 Service SDK 客户端，并仅配置一个端点（用于测试扇出/故障转移行为）。
    /// </summary>
    /// <param name="baseUrl">中心服务地址。</param>
    /// <returns>服务端 SDK 客户端。</returns>
    private static ServiceSdk.CentralServiceServiceClient CreateServiceClient(string baseUrl)
    {
        return new ServiceSdk.CentralServiceServiceClient(
            new ServiceSdk.CentralServiceSdkOptions(
                new[]
                {
                    new ServiceSdk.CentralServiceSdkOptions.CentralServiceEndpointOptions(baseUrl)
                    {
                        Priority = 0,
                        MaxAttempts = 1,
                    }
                }));
    }

    /// <summary>
    /// 创建一个模拟中心服务的回环服务器：支持 register/heartbeat/deregister 三个接口。
    /// </summary>
    /// <param name="sharedServiceId">固定返回的服务实例 Id（用于跨端点一致性断言）。</param>
    /// <param name="registerBodies">收集注册请求 body 的容器。</param>
    /// <param name="heartbeatBodies">收集心跳请求 body 的容器。</param>
    /// <param name="deregisterPaths">收集注销请求路径的容器。</param>
    /// <returns>回环 HTTP 服务器。</returns>
    private static LoopbackHttpServer CreateRegistrationServer(
        string sharedServiceId,
        ICollection<string> registerBodies,
        ICollection<string> heartbeatBodies,
        ICollection<string> deregisterPaths)
    {
        return new LoopbackHttpServer((request, _) =>
        {
            if (request.PathAndQuery == "/api/Service/register")
            {
                registerBodies.Add(request.Body);
                return LoopbackHttpResponse.Json(
                    HttpStatusCode.OK,
                    SerializeServiceModel(new ServiceModels.ApiResponse<ServiceModels.ServiceRegistrationResponse>
                    {
                        Success = true,
                        Data = new ServiceModels.ServiceRegistrationResponse
                        {
                            Id = sharedServiceId,
                            RegisterTimestamp = 123L,
                        },
                    }));
            }

            if (request.PathAndQuery == "/api/Service/heartbeat")
            {
                heartbeatBodies.Add(request.Body);
                return LoopbackHttpResponse.Json(
                    HttpStatusCode.OK,
                    SerializeServiceModel(new ServiceModels.ApiResponse<object>
                    {
                        Success = true,
                        Data = null,
                    }));
            }

            if (request.PathAndQuery.EndsWith("/" + sharedServiceId, StringComparison.Ordinal))
            {
                deregisterPaths.Add(request.PathAndQuery);
                return LoopbackHttpResponse.Json(
                    HttpStatusCode.OK,
                    SerializeServiceModel(new ServiceModels.ApiResponse<object>
                    {
                        Success = true,
                        Data = null,
                    }));
            }

            return LoopbackHttpResponse.PlainText(HttpStatusCode.NotFound, "unexpected path");
        });
    }

    /// <summary>
    /// 通过反射创建熔断器状态对象（内部类型），用于验证状态机逻辑。
    /// </summary>
    /// <param name="failureThreshold">失败阈值。</param>
    /// <param name="breakDuration">打开熔断后的持续时间。</param>
    /// <param name="recoveryThreshold">Half-Open 恢复阈值（连续成功次数）。</param>
    /// <returns>内部熔断器状态实例。</returns>
    private static object CreateCircuitBreakerState(int failureThreshold, TimeSpan breakDuration, int recoveryThreshold)
    {
        var stateType = typeof(CentralServiceDiscoveryClient).Assembly
            .GetType("CentralService.Shared.Internal.CentralServiceCircuitBreakerState", throwOnError: true)!;
        return Activator.CreateInstance(stateType, failureThreshold, breakDuration, recoveryThreshold)!;
    }

    /// <summary>
    /// 调用内部状态对象的 TryAllowRequest（反射），用于判断当前时刻是否允许请求并返回跳过原因。
    /// </summary>
    /// <param name="state">内部熔断器状态实例。</param>
    /// <param name="now">当前时间。</param>
    /// <param name="skipReason">当不允许请求时的跳过原因。</param>
    /// <returns>是否允许请求。</returns>
    private static bool TryAllowRequest(object state, DateTimeOffset now, out string? skipReason)
    {
        var args = new object?[] { now, null };
        var allowed = (bool)state.GetType().GetMethod("TryAllowRequest")!.Invoke(state, args)!;
        skipReason = args[1] as string;
        return allowed;
    }

    /// <summary>
    /// 报告一次失败（反射调用内部状态对象）。
    /// </summary>
    /// <param name="state">内部熔断器状态实例。</param>
    /// <param name="now">失败发生时间。</param>
    private static void ReportFailure(object state, DateTimeOffset now)
    {
        state.GetType().GetMethod("ReportFailure")!.Invoke(state, new object?[] { now });
    }

    /// <summary>
    /// 报告一次成功（反射调用内部状态对象）。
    /// </summary>
    /// <param name="state">内部熔断器状态实例。</param>
    private static void ReportSuccess(object state)
    {
        state.GetType().GetMethod("ReportSuccess")!.Invoke(state, Array.Empty<object>());
    }

    /// <summary>
    /// 构造一个用于发现接口返回的示例服务实例信息。
    /// </summary>
    /// <returns>服务实例信息。</returns>
    private static ServiceInfo CreateDiscoveredService()
    {
        return new ServiceInfo
        {
            Id = "svc-upgrade",
            Name = "UpgradeService",
            Host = "127.0.0.1",
            Port = 5288,
            ServiceType = "Web",
            HealthCheckType = "Http",
            HealthCheckUrl = "/health",
            Url = "http://127.0.0.1:5288",
            RegisterTime = DateTime.UtcNow.ToString("O"),
            LastHeartbeatTime = DateTime.UtcNow.ToString("O"),
            Metadata = new Dictionary<string, string>(),
        };
    }
}
