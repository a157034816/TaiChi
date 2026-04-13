import erp.centralservice.client.CentralServiceDiscoveryClient;
import erp.centralservice.client.CentralServiceSdkOptions;
import erp.centralservice.client.errors.CentralServiceException;
import erp.centralservice.client.internal.CentralServiceJson;
import erp.centralservice.client.models.ServiceInfo;
import erp.centralservice.client.models.ServiceListResponse;
import erp.centralservice.client.models.ServiceNetworkStatus;
import erp.centralservice.service.CentralServiceServiceClient;
import erp.centralservice.service.models.ServiceRegistrationRequest;
import erp.centralservice.service.models.ServiceRegistrationResponse;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;

public class E2EMain {
    public static void main(String[] args) {
        try {
            new E2EMain().run();
        } catch (Exception ex) {
            System.err.println("[java] scenario=" + scenarioName() + " failed: " + ex.getMessage());
            ex.printStackTrace(System.err);
            System.exit(1);
        }
    }

    private void run() {
        List<EndpointConfig> endpoints = loadEndpoints();
        int timeoutMs = loadTimeoutMs();
        String scenario = scenarioName();
        System.out.println("[java] scenario=" + scenario + " timeoutMs=" + timeoutMs + " endpoints=" + endpoints.size());

        for (int i = 0; i < endpoints.size(); i++) {
            EndpointConfig endpoint = endpoints.get(i);
            System.out.println("[java] endpoint[" + i + "]=" + endpoint.baseUrl + " priority=" + endpoint.priority + " maxAttempts=" + endpoint.maxAttempts);
        }

        if ("smoke".equals(scenario)) {
            runSmoke(endpoints, timeoutMs);
            return;
        }
        if ("service_fanout".equals(scenario)) {
            runServiceFanout(endpoints, timeoutMs);
            return;
        }
        if ("business_no_failover".equals(scenario)) {
            runBusinessNoFailover(endpoints, timeoutMs);
            return;
        }
        if ("transport_failover".equals(scenario)
            || "max_attempts".equals(scenario)
            || "circuit_open".equals(scenario)
            || "circuit_recovery".equals(scenario)
            || "half_open_reopen".equals(scenario)) {
            runTransportScenario(endpoints, timeoutMs, scenario);
            return;
        }

        throw new IllegalArgumentException("不支持的场景: " + scenario);
    }

    private void runSmoke(List<EndpointConfig> endpoints, int timeoutMs) {
        String serviceId = generatedServiceId("smoke");
        ServiceRegistrationRequest request = newRegistrationRequest(serviceId, "java-smoke");
        CentralServiceServiceClient service = new CentralServiceServiceClient(singleServiceOptions(endpoints.get(0), timeoutMs));
        CentralServiceDiscoveryClient discovery = new CentralServiceDiscoveryClient(discoveryOptions(endpoints, timeoutMs));

        ServiceRegistrationResponse reg = service.register(request);
        System.out.println("[java] smoke registered id=" + reg.id);
        try {
            ServiceListResponse listed = discovery.list(request.name);
            if (listed == null || listed.services == null || listed.services.length == 0) {
                throw new IllegalStateException("smoke list 未返回服务");
            }
            ServiceInfo best = discovery.discoverBest(request.name);
            if (best == null || !reg.id.equals(best.id)) {
                throw new IllegalStateException("smoke discover best 返回了意外服务");
            }
            discovery.discoverRoundRobin(request.name);
            discovery.discoverWeighted(request.name);
            discovery.evaluateNetwork(reg.id);
            discovery.getNetwork(reg.id);
            discovery.getNetworkAll();
        } finally {
            safeDeregister(service, reg.id);
        }
    }

    private void runServiceFanout(List<EndpointConfig> endpoints, int timeoutMs) {
        requireEndpointCount(endpoints, 2, "service_fanout");
        String serviceId = generatedServiceId("fanout");
        ServiceRegistrationRequest request = newRegistrationRequest(serviceId, "java-fanout");
        List<CentralServiceServiceClient> clients = new ArrayList<CentralServiceServiceClient>();

        for (int i = 0; i < endpoints.size(); i++) {
            EndpointConfig endpoint = endpoints.get(i);
            CentralServiceServiceClient client = new CentralServiceServiceClient(singleServiceOptions(endpoint, timeoutMs));
            clients.add(client);
            ServiceRegistrationResponse reg = client.register(request);
            if (!serviceId.equals(reg.id)) {
                throw new IllegalStateException("service_fanout 注册返回了不同 serviceId endpoint=" + endpoint.baseUrl);
            }
        }

        for (int i = 0; i < endpoints.size(); i++) {
            EndpointConfig endpoint = endpoints.get(i);
            CentralServiceDiscoveryClient discovery = new CentralServiceDiscoveryClient(discoveryOptions(singleton(endpoint), timeoutMs));
            ServiceListResponse listed = discovery.list(request.name);
            if (listed == null || listed.services == null || listed.services.length == 0 || !containsServiceId(listed, serviceId)) {
                throw new IllegalStateException("service_fanout list 未看到同一 serviceId endpoint=" + endpoint.baseUrl);
            }
            ServiceInfo best = discovery.discoverBest(request.name);
            if (best == null || !serviceId.equals(best.id)) {
                throw new IllegalStateException("service_fanout discoverBest 未返回同一 serviceId endpoint=" + endpoint.baseUrl);
            }
        }

        for (int i = 0; i < clients.size(); i++) {
            safeDeregister(clients.get(i), serviceId);
        }
        System.out.println("[java] service_fanout ok serviceId=" + serviceId + " endpoints=" + endpoints.size());
    }

    private void runBusinessNoFailover(List<EndpointConfig> endpoints, int timeoutMs) {
        requireEndpointCount(endpoints, 2, "business_no_failover");
        EndpointConfig primaryEndpoint = endpoints.get(0);
        EndpointConfig healthyEndpoint = endpoints.get(endpoints.size() - 1);
        String serviceId = generatedServiceId("business-no-failover");
        ServiceRegistrationRequest request = newRegistrationRequest(serviceId, "java-business-no-failover");
        CentralServiceServiceClient service = new CentralServiceServiceClient(singleServiceOptions(healthyEndpoint, timeoutMs));
        CentralServiceDiscoveryClient discovery = new CentralServiceDiscoveryClient(discoveryOptions(endpoints, timeoutMs));
        ServiceRegistrationResponse reg = service.register(request);
        try {
            discovery.discoverBest(request.name);
            throw new IllegalStateException("business_no_failover 期望主中心返回业务失败，但调用成功");
        } catch (CentralServiceException ex) {
            if (ex.getError() != null && ex.getError().kind != null && "Transport".equals(String.valueOf(ex.getError().kind))) {
                throw new IllegalStateException("business_no_failover 不应被识别为 Transport 失败: " + ex.getError().message);
            }
            if (ex.getError() == null || ex.getError().url == null || !ex.getError().url.startsWith(primaryEndpoint.baseUrl)) {
                String actualUrl = ex.getError() == null ? "<null>" : ex.getError().url;
                throw new IllegalStateException("business_no_failover 未停留在首端点: " + actualUrl);
            }
            System.out.println("[java] business_no_failover observed=" + ex.getMessage());
        } finally {
            safeDeregister(service, reg.id);
        }
    }

    private void runTransportScenario(List<EndpointConfig> endpoints, int timeoutMs, String scenario) {
        requireEndpointCount(endpoints, 2, scenario);
        EndpointConfig healthyEndpoint = endpoints.get(endpoints.size() - 1);
        String serviceId = generatedServiceId(scenario);
        ServiceRegistrationRequest request = newRegistrationRequest(serviceId, "java-" + scenario);
        CentralServiceServiceClient service = new CentralServiceServiceClient(singleServiceOptions(healthyEndpoint, timeoutMs));
        CentralServiceDiscoveryClient discovery = new CentralServiceDiscoveryClient(discoveryOptions(endpoints, timeoutMs));

        ServiceRegistrationResponse reg = service.register(request);
        try {
            if ("circuit_recovery".equals(scenario) || "half_open_reopen".equals(scenario)) {
                fanoutRegisterBestEffort(endpoints, timeoutMs, request);
            }

            if ("transport_failover".equals(scenario) || "max_attempts".equals(scenario)) {
                ServiceInfo first = discovery.discoverBest(request.name);
                assertServiceId("first", first, reg.id, scenario + " 未返回已注册服务");
                System.out.println("[java] " + scenario + " exercised successfully serviceId=" + reg.id);
                return;
            }

            if ("circuit_open".equals(scenario)) {
                ServiceInfo first = discovery.discoverBest(request.name);
                ServiceInfo second = discovery.discoverBest(request.name);
                assertServiceId("first", first, reg.id, "circuit_open 第一次发现失败");
                assertServiceId("second", second, reg.id, "circuit_open 第二次发现失败");
                System.out.println("[java] circuit_open exercised successfully serviceId=" + reg.id);
                return;
            }

            if ("circuit_recovery".equals(scenario)) {
                ServiceInfo first = discovery.discoverBest(request.name);
                assertServiceId("first", first, reg.id, "circuit_recovery 预热失败");
                waitForHalfOpen(endpoints);
                ServiceInfo second = discovery.discoverBest(request.name);
                ServiceInfo third = discovery.discoverBest(request.name);
                assertServiceId("second", second, reg.id, "circuit_recovery 半开第一次成功失败");
                assertServiceId("third", third, reg.id, "circuit_recovery 恢复后调用失败");
                System.out.println("[java] circuit_recovery exercised successfully serviceId=" + reg.id);
                return;
            }

            if ("half_open_reopen".equals(scenario)) {
                ServiceInfo first = discovery.discoverBest(request.name);
                assertServiceId("first", first, reg.id, "half_open_reopen 预热失败");
                waitForHalfOpen(endpoints);
                ServiceInfo second = discovery.discoverBest(request.name);
                ServiceInfo third = discovery.discoverBest(request.name);
                assertServiceId("second", second, reg.id, "half_open_reopen 半开探测失败");
                assertServiceId("third", third, reg.id, "half_open_reopen 重新熔断后的备用调用失败");
                System.out.println("[java] half_open_reopen exercised successfully serviceId=" + reg.id);
                return;
            }

            throw new IllegalArgumentException("不支持的 transport 场景: " + scenario);
        } finally {
            safeDeregister(service, reg.id);
        }
    }

    private static boolean containsServiceId(ServiceListResponse listed, String serviceId) {
        if (listed == null || listed.services == null || serviceId == null) return false;
        for (int i = 0; i < listed.services.length; i++) {
            ServiceInfo item = listed.services[i];
            if (item != null && serviceId.equals(item.id)) {
                return true;
            }
        }
        return false;
    }

    private void fanoutRegisterBestEffort(List<EndpointConfig> endpoints, int timeoutMs, ServiceRegistrationRequest request) {
        for (int i = 0; i < endpoints.size(); i++) {
            if (i == 0) continue;
            try {
                CentralServiceServiceClient client = new CentralServiceServiceClient(singleServiceOptions(endpoints.get(i), timeoutMs));
                ServiceRegistrationResponse reg = client.register(request);
            } catch (Exception ignored) {
            }
        }
    }

    private static void safeDeregister(CentralServiceServiceClient client, String serviceId) {
        if (client == null || serviceId == null || serviceId.trim().isEmpty()) return;
        try {
            client.deregister(serviceId);
            System.out.println("[java] cleanup deregister ok id=" + serviceId);
        } catch (Exception ignored) {
        }
    }

    private static void safeDeregisterAll(List<EndpointConfig> endpoints, int timeoutMs, String serviceId) {
        if (endpoints == null) return;
        for (int i = endpoints.size() - 1; i >= 0; i--) {
            safeDeregister(new CentralServiceServiceClient(singleServiceOptions(endpoints.get(i), timeoutMs)), serviceId);
        }
    }

    private static List<EndpointConfig> loadEndpoints() {
        String raw = trim(System.getenv("CENTRAL_SERVICE_ENDPOINTS_JSON"));
        ArrayList<EndpointConfig> endpoints = new ArrayList<EndpointConfig>();
        if (raw.isEmpty()) {
            endpoints.add(new EndpointConfig(fallbackBaseUrl(), 0, 2, null));
            return endpoints;
        }

        List<Object> array = CentralServiceJson.asArray(CentralServiceJson.parse(raw));
        if (array == null) throw new IllegalArgumentException("CENTRAL_SERVICE_ENDPOINTS_JSON 不是数组");
        for (int i = 0; i < array.size(); i++) {
            Map<String, Object> item = CentralServiceJson.asObject(array.get(i));
            if (item == null) continue;
            String baseUrl = trim(CentralServiceJson.asString(item.get("baseUrl")));
            if (baseUrl.isEmpty()) continue;
            Integer priority = CentralServiceJson.asIntNullable(item.get("priority"));
            Integer maxAttempts = CentralServiceJson.asIntNullable(item.get("maxAttempts"));
            Map<String, Object> circuitBreaker = CentralServiceJson.asObject(item.get("circuitBreaker"));
            endpoints.add(new EndpointConfig(stripTrailingSlash(baseUrl), priority == null ? 0 : priority.intValue(), maxAttempts == null ? 2 : Math.max(1, maxAttempts.intValue()), circuitBreaker));
        }
        if (endpoints.isEmpty()) throw new IllegalArgumentException("CENTRAL_SERVICE_ENDPOINTS_JSON 不能为空数组");
        return endpoints;
    }

    private static int loadTimeoutMs() {
        String raw = firstNonBlank(
            System.getenv("CENTRAL_SERVICE_TIMEOUT_MS"),
            System.getenv("CENTRAL_SERVICE_E2E_TIMEOUT_MS"));
        if (raw.isEmpty()) return 5000;
        try {
            int value = Integer.parseInt(raw);
            return value > 0 ? value : 5000;
        } catch (NumberFormatException ex) {
            return 5000;
        }
    }

    private static int loadServicePort() {
        String raw = trim(System.getenv("CENTRAL_SERVICE_E2E_SERVICE_PORT"));
        if (raw.isEmpty()) return 18083;
        try {
            int value = Integer.parseInt(raw);
            return value > 0 ? value : 18083;
        } catch (NumberFormatException ex) {
            return 18083;
        }
    }

    private static String scenarioName() {
        String value = trim(System.getenv("CENTRAL_SERVICE_E2E_SCENARIO"));
        return value.isEmpty() ? "smoke" : value;
    }

    private static String fallbackBaseUrl() {
        String value = trim(System.getenv("CENTRAL_SERVICE_BASEURL"));
        return value.isEmpty() ? "http://127.0.0.1:5000" : stripTrailingSlash(value);
    }

    private static String generatedServiceId(String prefix) {
        return "java-" + prefix + "-" + UUID.randomUUID().toString();
    }

    private static ServiceRegistrationRequest newRegistrationRequest(String serviceId, String sdkLabel) {
        ServiceRegistrationRequest request = new ServiceRegistrationRequest();
        request.id = serviceId;
        request.name = "SdkE2E";
        request.host = "127.0.0.1";
        request.localIp = "127.0.0.1";
        request.operatorIp = "127.0.0.1";
        request.publicIp = "127.0.0.1";
        request.port = loadServicePort();
        request.serviceType = "Web";
        request.healthCheckUrl = "/health";
        request.healthCheckPort = 0;
        request.weight = 1;
        request.metadata = new HashMap<String, String>();
        request.metadata.put("sdk", sdkLabel);
        return request;
    }

    private static CentralServiceSdkOptions discoveryOptions(List<EndpointConfig> endpoints, int timeoutMs) {
        ArrayList<CentralServiceSdkOptions.CentralServiceEndpointOptions> items = new ArrayList<CentralServiceSdkOptions.CentralServiceEndpointOptions>();
        for (int i = 0; i < endpoints.size(); i++) {
            EndpointConfig endpoint = endpoints.get(i);
            CentralServiceSdkOptions.CentralServiceEndpointOptions option = new CentralServiceSdkOptions.CentralServiceEndpointOptions(endpoint.baseUrl);
            option.priority = endpoint.priority;
            option.maxAttempts = Integer.valueOf(endpoint.maxAttempts);
            option.circuitBreaker = toClientCircuitBreaker(endpoint.circuitBreaker);
            items.add(option);
        }
        CentralServiceSdkOptions options = new CentralServiceSdkOptions(items);
        options.timeoutMs = timeoutMs;
        return options;
    }

    private static erp.centralservice.service.CentralServiceSdkOptions singleServiceOptions(EndpointConfig endpoint, int timeoutMs) {
        ArrayList<erp.centralservice.service.CentralServiceSdkOptions.CentralServiceEndpointOptions> items =
            new ArrayList<erp.centralservice.service.CentralServiceSdkOptions.CentralServiceEndpointOptions>();
        erp.centralservice.service.CentralServiceSdkOptions.CentralServiceEndpointOptions option =
            new erp.centralservice.service.CentralServiceSdkOptions.CentralServiceEndpointOptions(endpoint.baseUrl);
        option.priority = endpoint.priority;
        option.maxAttempts = Integer.valueOf(endpoint.maxAttempts);
        option.circuitBreaker = toServiceCircuitBreaker(endpoint.circuitBreaker);
        items.add(option);

        erp.centralservice.service.CentralServiceSdkOptions options = new erp.centralservice.service.CentralServiceSdkOptions(items);
        options.timeoutMs = timeoutMs;
        return options;
    }

    private static CentralServiceSdkOptions.CentralServiceCircuitBreakerOptions toClientCircuitBreaker(Map<String, Object> value) {
        if (value == null) return null;
        CentralServiceSdkOptions.CentralServiceCircuitBreakerOptions options = new CentralServiceSdkOptions.CentralServiceCircuitBreakerOptions();
        options.failureThreshold = intValue(value.get("failureThreshold"), 1);
        options.breakDurationMinutes = intValue(value.get("breakDurationMinutes"), 1);
        options.recoveryThreshold = intValue(value.get("recoveryThreshold"), 1);
        return options;
    }

    private static erp.centralservice.service.CentralServiceSdkOptions.CentralServiceCircuitBreakerOptions toServiceCircuitBreaker(Map<String, Object> value) {
        if (value == null) return null;
        erp.centralservice.service.CentralServiceSdkOptions.CentralServiceCircuitBreakerOptions options =
            new erp.centralservice.service.CentralServiceSdkOptions.CentralServiceCircuitBreakerOptions();
        options.failureThreshold = intValue(value.get("failureThreshold"), 1);
        options.breakDurationMinutes = intValue(value.get("breakDurationMinutes"), 1);
        options.recoveryThreshold = intValue(value.get("recoveryThreshold"), 1);
        return options;
    }

    private static int intValue(Object value, int fallback) {
        Integer parsed = CentralServiceJson.asIntNullable(value);
        if (parsed == null || parsed.intValue() < 1) return fallback;
        return parsed.intValue();
    }

    private static void waitForHalfOpen(List<EndpointConfig> endpoints) {
        long waitMs = waitForHalfOpenMs(endpoints);
        System.out.println("[java] waiting for half-open: " + waitMs + "ms");
        sleep(waitMs);
    }

    private static long waitForHalfOpenMs(List<EndpointConfig> endpoints) {
        String overrideSeconds = firstNonBlank(
            System.getenv("CENTRAL_SERVICE_BREAK_WAIT_SECONDS"),
            System.getenv("CENTRAL_SERVICE_E2E_BREAK_WAIT_SECONDS"));
        if (!overrideSeconds.isEmpty()) {
            try {
                long seconds = Long.parseLong(overrideSeconds);
                if (seconds > 0L) {
                    return seconds * 1000L;
                }
            } catch (NumberFormatException ignored) {
            }
        }
        long maxValue = 1000L;
        for (int i = 0; i < endpoints.size(); i++) {
            EndpointConfig endpoint = endpoints.get(i);
            if (endpoint.circuitBreaker == null) continue;
            long value = (long) intValue(endpoint.circuitBreaker.get("breakDurationMinutes"), 1) * 60_000L + 1000L;
            if (value > maxValue) maxValue = value;
        }
        return maxValue;
    }

    private static void requireEndpointCount(List<EndpointConfig> endpoints, int minimumCount, String scenario) {
        if (endpoints == null || endpoints.size() < minimumCount) {
            throw new IllegalArgumentException(scenario + " 至少需要 " + minimumCount + " 个中心服务端点");
        }
    }

    private static void assertServiceId(String stepName, ServiceInfo info, String expectedServiceId, String message) {
        if (info == null || expectedServiceId == null || !expectedServiceId.equals(info.id)) {
            throw new IllegalStateException(message + " actual=" + (info == null ? "<null>" : info.id) + " expected=" + expectedServiceId);
        }
        assertOptionalExpectedId(stepName, info.id);
    }

    private static void assertOptionalExpectedId(String stepName, String actualId) {
        String envName = "CENTRAL_SERVICE_E2E_EXPECTED_" + stepName.toUpperCase() + "_ID";
        String expectedId = trim(System.getenv(envName));
        if (!expectedId.isEmpty() && !expectedId.equals(actualId)) {
            throw new IllegalStateException(stepName + " 期望 id=" + expectedId + "，实际=" + actualId);
        }
    }

    private static void sleep(long millis) {
        try {
            Thread.sleep(millis);
        } catch (InterruptedException ex) {
            Thread.currentThread().interrupt();
            throw new IllegalStateException("等待半开状态时被中断", ex);
        }
    }

    private static List<EndpointConfig> singleton(EndpointConfig endpoint) {
        ArrayList<EndpointConfig> items = new ArrayList<EndpointConfig>();
        items.add(endpoint);
        return items;
    }

    private static String trim(String value) {
        return value == null ? "" : value.trim();
    }

    private static String firstNonBlank(String first, String second) {
        String value = trim(first);
        if (!value.isEmpty()) return value;
        return trim(second);
    }

    private static String stripTrailingSlash(String value) {
        String result = trim(value);
        while (result.endsWith("/")) {
            result = result.substring(0, result.length() - 1);
        }
        return result;
    }

    private static final class EndpointConfig {
        final String baseUrl;
        final int priority;
        final int maxAttempts;
        final Map<String, Object> circuitBreaker;

        EndpointConfig(String baseUrl, int priority, int maxAttempts, Map<String, Object> circuitBreaker) {
            this.baseUrl = baseUrl;
            this.priority = priority;
            this.maxAttempts = maxAttempts;
            this.circuitBreaker = circuitBreaker;
        }
    }
}
