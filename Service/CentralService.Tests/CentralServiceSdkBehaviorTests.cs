using System.Net;
using CentralService.Client;
using CentralService.Client.Errors;
using CentralService.Client.Internal;
using CentralService.Client.Models;
using CentralService.Shared.Internal;
using ServiceSdk = CentralService.Service;
using ServiceModels = CentralService.Service.Models;

namespace CentralService.Tests;

public sealed class CentralServiceSdkBehaviorTests
{
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

    private static string SerializeClientModel<T>(T value)
    {
        return CentralServiceJson.Serialize(value);
    }

    private static string SerializeServiceModel<T>(T value)
    {
        return ServiceSdk.Internal.CentralServiceJson.Serialize(value);
    }

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

    private static object CreateCircuitBreakerState(int failureThreshold, TimeSpan breakDuration, int recoveryThreshold)
    {
        var stateType = typeof(CentralServiceDiscoveryClient).Assembly
            .GetType("CentralService.Shared.Internal.CentralServiceCircuitBreakerState", throwOnError: true)!;
        return Activator.CreateInstance(stateType, failureThreshold, breakDuration, recoveryThreshold)!;
    }

    private static bool TryAllowRequest(object state, DateTimeOffset now, out string? skipReason)
    {
        var args = new object?[] { now, null };
        var allowed = (bool)state.GetType().GetMethod("TryAllowRequest")!.Invoke(state, args)!;
        skipReason = args[1] as string;
        return allowed;
    }

    private static void ReportFailure(object state, DateTimeOffset now)
    {
        state.GetType().GetMethod("ReportFailure")!.Invoke(state, new object?[] { now });
    }

    private static void ReportSuccess(object state)
    {
        state.GetType().GetMethod("ReportSuccess")!.Invoke(state, Array.Empty<object>());
    }

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
