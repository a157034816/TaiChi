using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using Client = CentralService.Client;
using ClientErrors = CentralService.Client.Errors;
using Service = CentralService.Service;
using ServiceModels = CentralService.Service.Models;

namespace CentralService.DotNetE2eShared
{
    internal static class CentralServiceE2ERunner
    {
        public static int Run(string label, int servicePort)
        {
            var scenario = (Environment.GetEnvironmentVariable("CENTRAL_SERVICE_E2E_SCENARIO") ?? "smoke").Trim();
            try
            {
                RunCore(label, servicePort, scenario);
                Console.WriteLine("[{0}] scenario={1} ok", label, scenario);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[{0}] scenario={1} failed: {2}", label, scenario, ex.Message);
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static void RunCore(string label, int servicePort, string scenario)
        {
            var endpoints = LoadEndpoints();
            switch ((scenario ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "smoke":
                    RunSmoke(label, servicePort, endpoints);
                    return;
                case "service_fanout":
                    RunServiceFanout(label, servicePort, endpoints);
                    return;
                case "transport_failover":
                    RunTransportFailover(label, servicePort, endpoints);
                    return;
                case "business_no_failover":
                    RunBusinessNoFailover(label, servicePort, endpoints);
                    return;
                case "max_attempts":
                    RunMaxAttempts(label, servicePort, endpoints);
                    return;
                case "circuit_open":
                    RunCircuitOpen(label, servicePort, endpoints);
                    return;
                case "circuit_recovery":
                    RunCircuitRecovery(label, servicePort, endpoints);
                    return;
                case "half_open_reopen":
                    RunHalfOpenReopen(label, servicePort, endpoints);
                    return;
                default:
                    throw new InvalidOperationException("未知场景: " + scenario);
            }
        }

        private static void RunSmoke(string label, int servicePort, List<EndpointConfig> endpoints)
        {
            EnsureEndpointCount(endpoints, 1, "smoke");
            var endpoint = endpoints[0];
            var serviceName = CreateServiceName(label, "smoke");
            var serviceId = RegisterSingleEndpoint(endpoint, CreateRequest(label, servicePort, serviceName, null));
            var serviceClient = CreateServiceClient(endpoint);
            var discoveryClient = CreateDiscoveryClient(endpoints);
            try
            {
                Console.WriteLine("[{0}] baseUrl={1}", label, endpoint.BaseUrl);
                serviceClient.Heartbeat(serviceId);
                Console.WriteLine("[{0}] heartbeat ok", label);

                var listed = discoveryClient.List(serviceName);
                var listedCount = listed == null || listed.Services == null ? 0 : listed.Services.Length;
                Assert(listedCount > 0, "smoke list 未返回任何服务");
                Console.WriteLine("[{0}] list count={1}", label, listedCount);

                var rr = discoveryClient.DiscoverRoundRobin(serviceName);
                Assert(rr != null && rr.Id == serviceId, "smoke roundrobin 结果不匹配");
                Console.WriteLine("[{0}] discover roundrobin id={1}", label, rr.Id);

                var weighted = discoveryClient.DiscoverWeighted(serviceName);
                Assert(weighted != null && weighted.Id == serviceId, "smoke weighted 结果不匹配");
                Console.WriteLine("[{0}] discover weighted id={1}", label, weighted.Id);

                var best = discoveryClient.DiscoverBest(serviceName);
                Assert(best != null && best.Id == serviceId, "smoke best 结果不匹配");
                Console.WriteLine("[{0}] discover best id={1}", label, best.Id);

                var evaluated = discoveryClient.EvaluateNetwork(serviceId);
                Console.WriteLine("[{0}] network evaluated score={1}", label, evaluated.CalculateScore());

                var network = discoveryClient.GetNetwork(serviceId);
                Console.WriteLine("[{0}] network get score={1}", label, network.CalculateScore());

                var all = discoveryClient.GetNetworkAll();
                Console.WriteLine("[{0}] network all count={1}", label, all == null ? 0 : all.Length);
            }
            finally
            {
                TryDeregister(serviceClient, serviceId);
                discoveryClient.Dispose();
                serviceClient.Dispose();
            }
        }

        private static void RunServiceFanout(string label, int servicePort, List<EndpointConfig> endpoints)
        {
            EnsureEndpointCount(endpoints, 2, "service_fanout");
            var serviceName = CreateServiceName(label, "fanout");
            var sharedServiceId = CreateStableServiceId(label, "fanout");
            var request = CreateRequest(label, servicePort, serviceName, sharedServiceId);
            var serviceClients = endpoints.Select(CreateServiceClient).ToList();
            var discoveryClients = endpoints.Select(CreateSingleEndpointDiscoveryClient).ToList();
            try
            {
                foreach (var serviceClient in serviceClients)
                {
                    var response = serviceClient.Register(CloneRequest(request));
                    Assert(string.Equals(response.Id, sharedServiceId, StringComparison.Ordinal), "service_fanout 注册返回了不同的 serviceId");
                    Console.WriteLine("[{0}] register endpoint serviceId={1}", label, response.Id);
                }

                foreach (var serviceClient in serviceClients)
                {
                    serviceClient.Heartbeat(sharedServiceId);
                }
                Console.WriteLine("[{0}] fanout heartbeat ok", label);

                foreach (var discoveryClient in discoveryClients)
                {
                    var listed = discoveryClient.List(serviceName);
                    var services = listed == null || listed.Services == null
                        ? Enumerable.Empty<Client.Models.ServiceInfo>()
                        : listed.Services;
                    Assert(services.Any(x => string.Equals(x.Id, sharedServiceId, StringComparison.Ordinal)), "service_fanout 某中心未查到共享 serviceId");
                }
                Console.WriteLine("[{0}] service_fanout list verification ok", label);
            }
            finally
            {
                foreach (var serviceClient in serviceClients)
                {
                    TryDeregister(serviceClient, sharedServiceId);
                    serviceClient.Dispose();
                }

                foreach (var discoveryClient in discoveryClients)
                {
                    discoveryClient.Dispose();
                }
            }
        }

        private static void RunTransportFailover(string label, int servicePort, List<EndpointConfig> endpoints)
        {
            EnsureEndpointCount(endpoints, 2, "transport_failover");
            var healthyEndpoint = endpoints[endpoints.Count - 1];
            var serviceName = CreateServiceName(label, "transport");
            var serviceId = RegisterSingleEndpoint(healthyEndpoint, CreateRequest(label, servicePort, serviceName, null));
            var serviceClient = CreateServiceClient(healthyEndpoint);
            var discoveryClient = CreateDiscoveryClient(endpoints);
            try
            {
                var discovered = discoveryClient.DiscoverBest(serviceName);
                Assert(discovered != null && discovered.Id == serviceId, "transport_failover 未通过备用中心发现服务");
                Console.WriteLine("[{0}] transport_failover discovered id={1}", label, discovered.Id);
            }
            finally
            {
                TryDeregister(serviceClient, serviceId);
                discoveryClient.Dispose();
                serviceClient.Dispose();
            }
        }

        private static void RunBusinessNoFailover(string label, int servicePort, List<EndpointConfig> endpoints)
        {
            EnsureEndpointCount(endpoints, 2, "business_no_failover");
            var healthyEndpoint = endpoints[endpoints.Count - 1];
            var primaryEndpoint = endpoints[0];
            var serviceName = CreateServiceName(label, "business");
            var serviceId = RegisterSingleEndpoint(healthyEndpoint, CreateRequest(label, servicePort, serviceName, null));
            var serviceClient = CreateServiceClient(healthyEndpoint);
            var discoveryClient = CreateDiscoveryClient(endpoints);
            try
            {
                try
                {
                    discoveryClient.DiscoverBest(serviceName);
                    throw new InvalidOperationException("business_no_failover 期望主中心返回业务失败，但调用成功了。");
                }
                catch (ClientErrors.CentralServiceException ex)
                {
                    Assert(ex.Error.Message.IndexOf(primaryEndpoint.BaseUrl, StringComparison.OrdinalIgnoreCase) >= 0, "business_no_failover 错误上下文未指向主中心");
                    Console.WriteLine("[{0}] business_no_failover primary error={1}", label, ex.Error.Message);
                }
            }
            finally
            {
                TryDeregister(serviceClient, serviceId);
                discoveryClient.Dispose();
                serviceClient.Dispose();
            }
        }

        private static void RunMaxAttempts(string label, int servicePort, List<EndpointConfig> endpoints)
        {
            EnsureEndpointCount(endpoints, 2, "max_attempts");
            var healthyEndpoint = endpoints[endpoints.Count - 1];
            var serviceName = CreateServiceName(label, "attempts");
            var serviceId = RegisterSingleEndpoint(healthyEndpoint, CreateRequest(label, servicePort, serviceName, null));
            var serviceClient = CreateServiceClient(healthyEndpoint);
            var discoveryClient = CreateDiscoveryClient(endpoints);
            try
            {
                var discovered = discoveryClient.DiscoverBest(serviceName);
                Assert(discovered != null && discovered.Id == serviceId, "max_attempts 未通过备用中心发现服务");
                Console.WriteLine("[{0}] max_attempts discovered id={1}", label, discovered.Id);
            }
            finally
            {
                TryDeregister(serviceClient, serviceId);
                discoveryClient.Dispose();
                serviceClient.Dispose();
            }
        }

        private static void RunCircuitOpen(string label, int servicePort, List<EndpointConfig> endpoints)
        {
            EnsureEndpointCount(endpoints, 2, "circuit_open");
            var healthyEndpoint = endpoints[endpoints.Count - 1];
            var serviceName = CreateServiceName(label, "circuit-open");
            var serviceId = RegisterSingleEndpoint(healthyEndpoint, CreateRequest(label, servicePort, serviceName, null));
            var serviceClient = CreateServiceClient(healthyEndpoint);
            var discoveryClient = CreateDiscoveryClient(endpoints);
            try
            {
                var first = discoveryClient.DiscoverBest(serviceName);
                Assert(first != null && first.Id == serviceId, "circuit_open 第一次发现失败");
                var second = discoveryClient.DiscoverBest(serviceName);
                Assert(second != null && second.Id == serviceId, "circuit_open 第二次发现失败");
                Console.WriteLine("[{0}] circuit_open fallback ok", label);
            }
            finally
            {
                TryDeregister(serviceClient, serviceId);
                discoveryClient.Dispose();
                serviceClient.Dispose();
            }
        }

        private static void RunCircuitRecovery(string label, int servicePort, List<EndpointConfig> endpoints)
        {
            EnsureEndpointCount(endpoints, 2, "circuit_recovery");
            var healthyEndpoint = endpoints[endpoints.Count - 1];
            var serviceName = CreateServiceName(label, "circuit-recovery");
            var serviceId = RegisterSingleEndpoint(healthyEndpoint, CreateRequest(label, servicePort, serviceName, null));
            var serviceClient = CreateServiceClient(healthyEndpoint);
            var discoveryClient = CreateDiscoveryClient(endpoints);
            try
            {
                var first = discoveryClient.DiscoverBest(serviceName);
                Assert(first != null && first.Id == serviceId, "circuit_recovery 第一次发现失败");
                Thread.Sleep(GetBreakWaitMilliseconds());

                var second = discoveryClient.DiscoverBest(serviceName);
                Assert(second != null && second.Id == serviceId, "circuit_recovery 半开第一次成功失败");

                var third = discoveryClient.DiscoverBest(serviceName);
                Assert(third != null && third.Id == serviceId, "circuit_recovery 半开恢复成功失败");
                Console.WriteLine("[{0}] circuit_recovery recovered via primary", label);
            }
            finally
            {
                TryDeregister(serviceClient, serviceId);
                discoveryClient.Dispose();
                serviceClient.Dispose();
            }
        }

        private static void RunHalfOpenReopen(string label, int servicePort, List<EndpointConfig> endpoints)
        {
            EnsureEndpointCount(endpoints, 2, "half_open_reopen");
            var healthyEndpoint = endpoints[endpoints.Count - 1];
            var serviceName = CreateServiceName(label, "half-open");
            var serviceId = RegisterSingleEndpoint(healthyEndpoint, CreateRequest(label, servicePort, serviceName, null));
            var serviceClient = CreateServiceClient(healthyEndpoint);
            var discoveryClient = CreateDiscoveryClient(endpoints);
            try
            {
                var first = discoveryClient.DiscoverBest(serviceName);
                Assert(first != null && first.Id == serviceId, "half_open_reopen 第一次发现失败");
                Thread.Sleep(GetBreakWaitMilliseconds());

                var second = discoveryClient.DiscoverBest(serviceName);
                Assert(second != null && second.Id == serviceId, "half_open_reopen 半开失败后的备用发现失败");

                var third = discoveryClient.DiscoverBest(serviceName);
                Assert(third != null && third.Id == serviceId, "half_open_reopen 重新熔断后的备用发现失败");
                Console.WriteLine("[{0}] half_open_reopen fallback ok", label);
            }
            finally
            {
                TryDeregister(serviceClient, serviceId);
                discoveryClient.Dispose();
                serviceClient.Dispose();
            }
        }

        private static string RegisterSingleEndpoint(EndpointConfig endpoint, ServiceModels.ServiceRegistrationRequest request)
        {
            var serviceClient = CreateServiceClient(endpoint);
            try
            {
                var response = serviceClient.Register(request);
                return response.Id;
            }
            finally
            {
                serviceClient.Dispose();
            }
        }

        private static ServiceModels.ServiceRegistrationRequest CreateRequest(string label, int servicePort, string serviceName, string serviceId)
        {
            return new ServiceModels.ServiceRegistrationRequest
            {
                Id = serviceId ?? string.Empty,
                Name = serviceName,
                Host = "127.0.0.1",
                LocalIp = "127.0.0.1",
                OperatorIp = "127.0.0.1",
                PublicIp = "127.0.0.1",
                Port = servicePort,
                ServiceType = "Web",
                HealthCheckType = "Http",
                HealthCheckUrl = "/health",
                Weight = 1,
                Metadata = new Dictionary<string, string>
                {
                    ["sdk"] = label,
                }
            };
        }

        private static ServiceModels.ServiceRegistrationRequest CloneRequest(ServiceModels.ServiceRegistrationRequest source)
        {
            return new ServiceModels.ServiceRegistrationRequest
            {
                Id = source.Id,
                Name = source.Name,
                Host = source.Host,
                LocalIp = source.LocalIp,
                OperatorIp = source.OperatorIp,
                PublicIp = source.PublicIp,
                Port = source.Port,
                ServiceType = source.ServiceType,
                HealthCheckType = source.HealthCheckType,
                HealthCheckUrl = source.HealthCheckUrl,
                HealthCheckPort = source.HealthCheckPort,
                Weight = source.Weight,
                Metadata = source.Metadata == null
                    ? new Dictionary<string, string>()
                    : new Dictionary<string, string>(source.Metadata),
            };
        }

        private static Service.CentralServiceServiceClient CreateServiceClient(EndpointConfig endpoint)
        {
            var options = new Service.CentralServiceSdkOptions(
                new[]
                {
                    endpoint.ToServiceEndpointOptions()
                });
            options.Timeout = GetTimeout();
            return new Service.CentralServiceServiceClient(options);
        }

        private static Client.CentralServiceDiscoveryClient CreateSingleEndpointDiscoveryClient(EndpointConfig endpoint)
        {
            var options = new Client.CentralServiceSdkOptions(
                new[]
                {
                    endpoint.ToClientEndpointOptions()
                });
            options.Timeout = GetTimeout();
            return new Client.CentralServiceDiscoveryClient(options);
        }

        private static Client.CentralServiceDiscoveryClient CreateDiscoveryClient(List<EndpointConfig> endpoints)
        {
            var options = new Client.CentralServiceSdkOptions(endpoints.Select(x => x.ToClientEndpointOptions()).ToArray());
            options.Timeout = GetTimeout();
            return new Client.CentralServiceDiscoveryClient(options);
        }

        private static List<EndpointConfig> LoadEndpoints()
        {
            var json = Environment.GetEnvironmentVariable("CENTRAL_SERVICE_ENDPOINTS_JSON");
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<EndpointConfig>
                {
                    new EndpointConfig
                    {
                        BaseUrl = GetBaseUrl(),
                        Priority = 0,
                        MaxAttempts = 2,
                    }
                };
            }

            var serializer = new DataContractJsonSerializer(typeof(List<EndpointConfigJsonModel>));
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var models = serializer.ReadObject(stream) as List<EndpointConfigJsonModel>;
                if (models == null)
                {
                    throw new InvalidOperationException("CENTRAL_SERVICE_ENDPOINTS_JSON 无法解析。");
                }

                return models
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.BaseUrl))
                    .Select((x, index) => EndpointConfig.FromJsonModel(x, index))
                    .OrderBy(x => x.Priority)
                    .ThenBy(x => x.Order)
                    .ToList();
            }
        }

        private static int GetBreakWaitMilliseconds()
        {
            var env = Environment.GetEnvironmentVariable("CENTRAL_SERVICE_BREAK_WAIT_SECONDS");
            int seconds;
            if (!int.TryParse(env, out seconds) || seconds < 1)
            {
                seconds = 65;
            }

            return seconds * 1000;
        }

        private static TimeSpan GetTimeout()
        {
            var env = Environment.GetEnvironmentVariable("CENTRAL_SERVICE_TIMEOUT_MS");
            int milliseconds;
            if (!int.TryParse(env, out milliseconds) || milliseconds < 1)
            {
                milliseconds = 5000;
            }

            return TimeSpan.FromMilliseconds(milliseconds);
        }

        private static string GetBaseUrl()
        {
            var env = Environment.GetEnvironmentVariable("CENTRAL_SERVICE_BASEURL");
            if (!string.IsNullOrWhiteSpace(env))
            {
                return env.Trim().TrimEnd('/');
            }

            return "http://127.0.0.1:5000";
        }

        private static string CreateServiceName(string label, string suffix)
        {
            return string.Format("SdkE2E-{0}-{1}-{2}", label, suffix, Guid.NewGuid().ToString("N")).Replace('_', '-');
        }

        private static string CreateStableServiceId(string label, string suffix)
        {
            return string.Format("sdk-e2e-{0}-{1}-{2}", label, suffix, Guid.NewGuid().ToString("N"));
        }

        private static void TryDeregister(Service.CentralServiceServiceClient client, string serviceId)
        {
            if (client == null || string.IsNullOrWhiteSpace(serviceId))
            {
                return;
            }

            try
            {
                client.Deregister(serviceId);
            }
            catch
            {
            }
        }

        private static void EnsureEndpointCount(List<EndpointConfig> endpoints, int count, string scenario)
        {
            Assert(endpoints != null && endpoints.Count >= count, string.Format("{0} 需要至少 {1} 个中心服务端点。", scenario, count));
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        [DataContract]
        private sealed class EndpointConfigJsonModel
        {
            [DataMember(Name = "baseUrl")]
            public string BaseUrl { get; set; }

            [DataMember(Name = "priority")]
            public int Priority { get; set; }

            [DataMember(Name = "maxAttempts")]
            public int? MaxAttempts { get; set; }

            [DataMember(Name = "circuitBreaker")]
            public CircuitBreakerJsonModel CircuitBreaker { get; set; }
        }

        [DataContract]
        private sealed class CircuitBreakerJsonModel
        {
            [DataMember(Name = "failureThreshold")]
            public int FailureThreshold { get; set; }

            [DataMember(Name = "breakDurationMinutes")]
            public int BreakDurationMinutes { get; set; }

            [DataMember(Name = "recoveryThreshold")]
            public int RecoveryThreshold { get; set; }
        }

        private sealed class EndpointConfig
        {
            public string BaseUrl { get; set; }

            public int Priority { get; set; }

            public int MaxAttempts { get; set; }

            public CircuitBreakerConfig CircuitBreaker { get; set; }

            public int Order { get; set; }

            public static EndpointConfig FromJsonModel(EndpointConfigJsonModel model, int order)
            {
                return new EndpointConfig
                {
                    BaseUrl = (model.BaseUrl ?? string.Empty).Trim().TrimEnd('/'),
                    Priority = model.Priority,
                    MaxAttempts = NormalizeMaxAttempts(model.MaxAttempts),
                    CircuitBreaker = model.CircuitBreaker == null
                        ? null
                        : new CircuitBreakerConfig
                        {
                            FailureThreshold = Math.Max(1, model.CircuitBreaker.FailureThreshold),
                            BreakDurationMinutes = Math.Max(1, model.CircuitBreaker.BreakDurationMinutes),
                            RecoveryThreshold = Math.Max(1, model.CircuitBreaker.RecoveryThreshold),
                        },
                    Order = order,
                };
            }

            public Client.CentralServiceSdkOptions.CentralServiceEndpointOptions ToClientEndpointOptions()
            {
                return new Client.CentralServiceSdkOptions.CentralServiceEndpointOptions(BaseUrl)
                {
                    Priority = Priority,
                    MaxAttempts = MaxAttempts,
                    CircuitBreaker = CircuitBreaker == null
                        ? null
                        : new Client.CentralServiceSdkOptions.CentralServiceCircuitBreakerOptions
                        {
                            FailureThreshold = CircuitBreaker.FailureThreshold,
                            BreakDurationMinutes = CircuitBreaker.BreakDurationMinutes,
                            RecoveryThreshold = CircuitBreaker.RecoveryThreshold,
                        },
                };
            }

            public Service.CentralServiceSdkOptions.CentralServiceEndpointOptions ToServiceEndpointOptions()
            {
                return new Service.CentralServiceSdkOptions.CentralServiceEndpointOptions(BaseUrl)
                {
                    Priority = Priority,
                    MaxAttempts = MaxAttempts,
                    CircuitBreaker = CircuitBreaker == null
                        ? null
                        : new Service.CentralServiceSdkOptions.CentralServiceCircuitBreakerOptions
                        {
                            FailureThreshold = CircuitBreaker.FailureThreshold,
                            BreakDurationMinutes = CircuitBreaker.BreakDurationMinutes,
                            RecoveryThreshold = CircuitBreaker.RecoveryThreshold,
                        },
                };
            }

            private static int NormalizeMaxAttempts(int? value)
            {
                var normalized = value.GetValueOrDefault(2);
                return normalized < 1 ? 2 : normalized;
            }
        }

        private sealed class CircuitBreakerConfig
        {
            public int FailureThreshold { get; set; }

            public int BreakDurationMinutes { get; set; }

            public int RecoveryThreshold { get; set; }
        }
    }
}
