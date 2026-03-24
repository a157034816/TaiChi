package main

import (
	"context"
	"encoding/json"
	"fmt"
	"os"
	"strconv"
	"strings"
	"time"

	clientsdk "ensoai.local/centralservice-client"
	servicesdk "ensoai.local/centralservice-service"
)

type endpointConfig struct {
	BaseURL        string                 `json:"baseUrl"`
	Priority       int                    `json:"priority"`
	MaxAttempts    int                    `json:"maxAttempts"`
	CircuitBreaker map[string]interface{} `json:"circuitBreaker"`
}

func main() {
	if err := run(); err != nil {
		fmt.Fprintf(os.Stderr, "[go] scenario=%s failed: %v\n", scenarioName(), err)
		os.Exit(1)
	}
}

func run() error {
	timeout := timeoutFromEnv()
	endpoints, err := loadEndpoints()
	if err != nil {
		return err
	}
	if len(endpoints) == 0 {
		return fmt.Errorf("未提供中心服务端点")
	}
	scenario := scenarioName()

	fmt.Printf("[go] scenario=%s timeoutMs=%d endpoints=%d\n", scenario, timeout.Milliseconds(), len(endpoints))
	for index, endpoint := range endpoints {
		fmt.Printf("[go] endpoint[%d]=%s priority=%d maxAttempts=%d\n", index, endpoint.BaseURL, endpoint.Priority, endpoint.MaxAttempts)
	}

	switch scenario {
	case "smoke":
		return runSmoke(timeout, endpoints)
	case "service_fanout":
		return runServiceFanout(timeout, endpoints)
	case "business_no_failover":
		return runBusinessNoFailover(timeout, endpoints)
	case "transport_failover", "max_attempts", "circuit_open", "circuit_recovery", "half_open_reopen":
		return runTransportScenario(timeout, endpoints, scenario)
	default:
		return fmt.Errorf("不支持的场景: %s", scenario)
	}
}

func runSmoke(timeout time.Duration, endpoints []endpointConfig) error {
	serviceID := generatedServiceID("smoke")
	svc := servicesdk.NewServiceClient(serviceOptionsForSingleEndpoint(endpoints[0], timeout))
	client := clientsdk.NewDiscoveryClient(discoveryOptions(endpoints, timeout))
	ctx := context.Background()

	req := newRegistrationRequest(serviceID, "go-smoke")
	reg, err := svc.Register(ctx, req)
	if err != nil {
		return fmt.Errorf("smoke register 失败: %w", err)
	}
	fmt.Printf("[go] smoke registered id=%s\n", reg.Id)
	defer safeDeregister(ctx, svc, reg.Id)

	if err := svc.Heartbeat(ctx, reg.Id); err != nil {
		return fmt.Errorf("smoke heartbeat 失败: %w", err)
	}

	listed, err := client.List(ctx, req.Name)
	if err != nil {
		return fmt.Errorf("smoke list 失败: %w", err)
	}
	if len(listed.Services) == 0 {
		return fmt.Errorf("smoke list 未返回服务")
	}

	if _, err := client.DiscoverRoundRobin(ctx, req.Name); err != nil {
		return fmt.Errorf("smoke discover roundrobin 失败: %w", err)
	}
	if _, err := client.DiscoverWeighted(ctx, req.Name); err != nil {
		return fmt.Errorf("smoke discover weighted 失败: %w", err)
	}
	best, err := client.DiscoverBest(ctx, req.Name)
	if err != nil {
		return fmt.Errorf("smoke discover best 失败: %w", err)
	}
	if best.Id != reg.Id {
		return fmt.Errorf("smoke discover best 返回了意外服务: got=%s want=%s", best.Id, reg.Id)
	}

	if _, err := client.EvaluateNetwork(ctx, reg.Id); err != nil {
		return fmt.Errorf("smoke evaluate network 失败: %w", err)
	}
	if _, err := client.GetNetwork(ctx, reg.Id); err != nil {
		return fmt.Errorf("smoke get network 失败: %w", err)
	}
	if _, err := client.GetNetworkAll(ctx); err != nil {
		return fmt.Errorf("smoke get all network 失败: %w", err)
	}

	if err := svc.Deregister(ctx, reg.Id); err != nil {
		return fmt.Errorf("smoke deregister 失败: %w", err)
	}
	fmt.Printf("[go] smoke deregister ok id=%s\n", reg.Id)
	return nil
}

func runServiceFanout(timeout time.Duration, endpoints []endpointConfig) error {
	if err := requireEndpointCount(endpoints, 2, "service_fanout"); err != nil {
		return err
	}
	serviceID := generatedServiceID("fanout")
	ctx := context.Background()
	request := newRegistrationRequest(serviceID, "go-fanout")
	clients := make([]*servicesdk.ServiceClient, 0, len(endpoints))

	for _, endpoint := range endpoints {
		client := servicesdk.NewServiceClient(serviceOptionsForSingleEndpoint(endpoint, timeout))
		clients = append(clients, client)
		reg, err := client.Register(ctx, request)
		if err != nil {
			return fmt.Errorf("service_fanout register 失败 endpoint=%s: %w", endpoint.BaseURL, err)
		}
		if reg.Id != serviceID {
			return fmt.Errorf("service_fanout 注册返回了不同 serviceId endpoint=%s got=%s want=%s", endpoint.BaseURL, reg.Id, serviceID)
		}
		if err := client.Heartbeat(ctx, serviceID); err != nil {
			return fmt.Errorf("service_fanout heartbeat 失败 endpoint=%s: %w", endpoint.BaseURL, err)
		}
	}

	for _, endpoint := range endpoints {
		discovery := clientsdk.NewDiscoveryClient(discoveryOptions([]endpointConfig{endpoint}, timeout))
		listed, err := discovery.List(ctx, request.Name)
		if err != nil {
			return fmt.Errorf("service_fanout list 失败 endpoint=%s: %w", endpoint.BaseURL, err)
		}
		if len(listed.Services) == 0 || listed.Services[0].Id != serviceID {
			found := false
			for _, item := range listed.Services {
				if item.Id == serviceID {
					found = true
					break
				}
			}
			if !found {
				return fmt.Errorf("service_fanout list 未看到同一 serviceId endpoint=%s", endpoint.BaseURL)
			}
		}
		best, err := discovery.DiscoverBest(ctx, request.Name)
		if err != nil {
			return fmt.Errorf("service_fanout discoverBest 失败 endpoint=%s: %w", endpoint.BaseURL, err)
		}
		if best.Id != serviceID {
			return fmt.Errorf("service_fanout discoverBest 未返回同一 serviceId endpoint=%s got=%s want=%s", endpoint.BaseURL, best.Id, serviceID)
		}
	}

	for _, client := range clients {
		if err := client.Deregister(ctx, serviceID); err != nil {
			return fmt.Errorf("service_fanout deregister 失败: %w", err)
		}
	}

	fmt.Printf("[go] service_fanout ok serviceId=%s endpoints=%d\n", serviceID, len(endpoints))
	return nil
}

func runBusinessNoFailover(timeout time.Duration, endpoints []endpointConfig) error {
	if err := requireEndpointCount(endpoints, 2, "business_no_failover"); err != nil {
		return err
	}
	ctx := context.Background()
	healthyEndpoint := endpoints[len(endpoints)-1]
	primaryEndpoint := endpoints[0]
	serviceID := generatedServiceID("business-no-failover")
	service := servicesdk.NewServiceClient(serviceOptionsForSingleEndpoint(healthyEndpoint, timeout))
	request := newRegistrationRequest(serviceID, "go-business-no-failover")
	client := clientsdk.NewDiscoveryClient(discoveryOptions(endpoints, timeout))
	reg, err := service.Register(ctx, request)
	if err != nil {
		return fmt.Errorf("business_no_failover register 失败: %w", err)
	}
	defer safeDeregister(ctx, service, reg.Id)

	_, err = client.DiscoverBest(ctx, request.Name)
	if err == nil {
		return fmt.Errorf("business_no_failover 期望失败，但调用成功")
	}

	if centralErr, ok := err.(*clientsdk.CentralServiceError); ok {
		if centralErr.Kind == clientsdk.ErrorKindTransport {
			return fmt.Errorf("business_no_failover 不应被识别为 Transport 失败: %s", centralErr.Message)
		}
		if !strings.Contains(centralErr.URL, primaryEndpoint.BaseURL) {
			return fmt.Errorf("business_no_failover 未停留在首端点: got=%s want prefix=%s", centralErr.URL, primaryEndpoint.BaseURL)
		}
		fmt.Printf("[go] business_no_failover observed kind=%s url=%s\n", centralErr.Kind, centralErr.URL)
		return nil
	}

	fmt.Printf("[go] business_no_failover observed err=%v\n", err)
	return nil
}

func runTransportScenario(timeout time.Duration, endpoints []endpointConfig, scenario string) error {
	if err := requireEndpointCount(endpoints, 2, scenario); err != nil {
		return err
	}

	ctx := context.Background()
	healthyEndpoint := endpoints[len(endpoints)-1]
	serviceID := generatedServiceID(scenario)
	request := newRegistrationRequest(serviceID, "go-"+scenario)
	service := servicesdk.NewServiceClient(serviceOptionsForSingleEndpoint(healthyEndpoint, timeout))
	discovery := clientsdk.NewDiscoveryClient(discoveryOptions(endpoints, timeout))

	reg, err := service.Register(ctx, request)
	if err != nil {
		return fmt.Errorf("%s register 失败: %w", scenario, err)
	}
	defer safeDeregister(ctx, service, reg.Id)

	if scenario == "circuit_recovery" || scenario == "half_open_reopen" {
		if err := registerFanoutToHealthyEndpoints(ctx, endpoints, request, timeout); err != nil {
			return fmt.Errorf("%s fanout register 失败: %w", scenario, err)
		}
	}

	switch scenario {
	case "transport_failover", "max_attempts":
		first, err := discovery.DiscoverBest(ctx, request.Name)
		if err != nil {
			return fmt.Errorf("%s discover 失败: %w", scenario, err)
		}
		if err := assertServiceID("first", first.Id, reg.Id); err != nil {
			return err
		}
	case "circuit_open":
		first, err := discovery.DiscoverBest(ctx, request.Name)
		if err != nil {
			return fmt.Errorf("circuit_open 第一次调用失败: %w", err)
		}
		if err := assertServiceID("first", first.Id, reg.Id); err != nil {
			return err
		}
		second, err := discovery.DiscoverBest(ctx, request.Name)
		if err != nil {
			return fmt.Errorf("circuit_open 第二次调用失败: %w", err)
		}
		return assertServiceID("second", second.Id, reg.Id)
	case "circuit_recovery":
		first, err := discovery.DiscoverBest(ctx, request.Name)
		if err != nil {
			return fmt.Errorf("circuit_recovery 预热失败: %w", err)
		}
		if err := assertServiceID("first", first.Id, reg.Id); err != nil {
			return err
		}
		waitDuration := waitForHalfOpen(endpoints)
		fmt.Printf("[go] waiting for half-open: %s\n", waitDuration)
		time.Sleep(waitDuration)
		second, err := discovery.DiscoverBest(ctx, request.Name)
		if err != nil {
			return fmt.Errorf("circuit_recovery 半开第一次成功失败: %w", err)
		}
		if err := assertServiceID("second", second.Id, reg.Id); err != nil {
			return err
		}
		third, err := discovery.DiscoverBest(ctx, request.Name)
		if err != nil {
			return fmt.Errorf("circuit_recovery 恢复后调用失败: %w", err)
		}
		if err := assertServiceID("third", third.Id, reg.Id); err != nil {
			return err
		}
	case "half_open_reopen":
		first, err := discovery.DiscoverBest(ctx, request.Name)
		if err != nil {
			return fmt.Errorf("half_open_reopen 预热失败: %w", err)
		}
		if err := assertServiceID("first", first.Id, reg.Id); err != nil {
			return err
		}
		waitDuration := waitForHalfOpen(endpoints)
		fmt.Printf("[go] waiting for half-open: %s\n", waitDuration)
		time.Sleep(waitDuration)
		second, err := discovery.DiscoverBest(ctx, request.Name)
		if err != nil {
			return fmt.Errorf("half_open_reopen 半开探测失败: %w", err)
		}
		if err := assertServiceID("second", second.Id, reg.Id); err != nil {
			return err
		}
		third, err := discovery.DiscoverBest(ctx, request.Name)
		if err != nil {
			return fmt.Errorf("half_open_reopen 重新熔断后的备用调用失败: %w", err)
		}
		if err := assertServiceID("third", third.Id, reg.Id); err != nil {
			return err
		}
	default:
		return fmt.Errorf("不支持的 transport 场景: %s", scenario)
	}

	fmt.Printf("[go] %s exercised successfully serviceId=%s\n", scenario, reg.Id)
	return nil
}

func registerFanoutToHealthyEndpoints(ctx context.Context, endpoints []endpointConfig, request servicesdk.ServiceRegistrationRequest, timeout time.Duration) error {
	successCount := 0
	for index, endpoint := range endpoints {
		if index == 0 {
			continue
		}
		if endpoint.BaseURL == "" {
			continue
		}
		client := servicesdk.NewServiceClient(serviceOptionsForSingleEndpoint(endpoint, timeout))
		reg, err := client.Register(ctx, request)
		if err != nil {
			continue
		}
		if reg.Id == request.Id {
			successCount++
			_ = client.Heartbeat(ctx, reg.Id)
		}
	}
	if successCount == 0 {
		return fmt.Errorf("没有任何端点接受 fanout 注册")
	}
	return nil
}

func safeDeregister(ctx context.Context, client *servicesdk.ServiceClient, serviceID string) {
	if client == nil || serviceID == "" {
		return
	}
	if err := client.Deregister(ctx, serviceID); err == nil {
		fmt.Printf("[go] cleanup deregister ok id=%s\n", serviceID)
	}
}

func safeDeregisterAll(ctx context.Context, endpoints []endpointConfig, timeout time.Duration, serviceID string) {
	for index := len(endpoints) - 1; index >= 0; index-- {
		client := servicesdk.NewServiceClient(serviceOptionsForSingleEndpoint(endpoints[index], timeout))
		safeDeregister(ctx, client, serviceID)
	}
}

func loadEndpoints() ([]endpointConfig, error) {
	raw := strings.TrimSpace(os.Getenv("CENTRAL_SERVICE_ENDPOINTS_JSON"))
	if raw == "" {
		return []endpointConfig{{BaseURL: fallbackBaseURL(), Priority: 0, MaxAttempts: 2}}, nil
	}

	var endpoints []endpointConfig
	if err := json.Unmarshal([]byte(raw), &endpoints); err != nil {
		return nil, fmt.Errorf("解析 CENTRAL_SERVICE_ENDPOINTS_JSON 失败: %w", err)
	}
	for index := range endpoints {
		endpoints[index].BaseURL = strings.TrimRight(strings.TrimSpace(endpoints[index].BaseURL), "/")
		if endpoints[index].MaxAttempts < 1 {
			endpoints[index].MaxAttempts = 2
		}
	}
	if len(endpoints) == 0 {
		return nil, fmt.Errorf("CENTRAL_SERVICE_ENDPOINTS_JSON 不能为空数组")
	}
	return endpoints, nil
}

func timeoutFromEnv() time.Duration {
	raw := firstNonBlankEnv("CENTRAL_SERVICE_TIMEOUT_MS", "CENTRAL_SERVICE_E2E_TIMEOUT_MS")
	if raw == "" {
		return 5 * time.Second
	}
	value, err := strconv.Atoi(raw)
	if err != nil || value < 1 {
		return 5 * time.Second
	}
	return time.Duration(value) * time.Millisecond
}

func servicePortFromEnv() int {
	raw := firstNonBlankEnv("CENTRAL_SERVICE_E2E_SERVICE_PORT")
	if raw == "" {
		return 18084
	}
	value, err := strconv.Atoi(raw)
	if err != nil || value < 1 {
		return 18084
	}
	return value
}

func scenarioName() string {
	value := strings.TrimSpace(os.Getenv("CENTRAL_SERVICE_E2E_SCENARIO"))
	if value == "" {
		return "smoke"
	}
	return value
}

func fallbackBaseURL() string {
	value := strings.TrimSpace(os.Getenv("CENTRAL_SERVICE_BASEURL"))
	if value == "" {
		value = "http://127.0.0.1:5000"
	}
	return strings.TrimRight(value, "/")
}

func generatedServiceID(prefix string) string {
	return fmt.Sprintf("go-%s-%d", prefix, time.Now().UnixNano())
}

func newRegistrationRequest(serviceID string, sdkLabel string) servicesdk.ServiceRegistrationRequest {
	return servicesdk.ServiceRegistrationRequest{
		Id:              serviceID,
		Name:            "SdkE2E",
		Host:            "127.0.0.1",
		LocalIp:         "127.0.0.1",
		OperatorIp:      "127.0.0.1",
		PublicIp:        "127.0.0.1",
		Port:            servicePortFromEnv(),
		ServiceType:     "Web",
		HealthCheckUrl:  "/health",
		HealthCheckPort: 0,
		HealthCheckType: "Http",
		Weight:          1,
		Metadata:        map[string]string{"sdk": sdkLabel},
	}
}

func discoveryOptions(endpoints []endpointConfig, timeout time.Duration) clientsdk.Options {
	options := clientsdk.Options{
		Timeout:   timeout,
		UserAgent: "centralservice-go-client/0.1.0",
		Endpoints: make([]clientsdk.EndpointOptions, 0, len(endpoints)),
	}
	for _, endpoint := range endpoints {
		options.Endpoints = append(options.Endpoints, clientsdk.EndpointOptions{
			BaseURL:        endpoint.BaseURL,
			Priority:       endpoint.Priority,
			MaxAttempts:    endpoint.MaxAttempts,
			CircuitBreaker: toClientCircuitBreaker(endpoint.CircuitBreaker),
		})
	}
	return options
}

func serviceOptionsForSingleEndpoint(endpoint endpointConfig, timeout time.Duration) servicesdk.Options {
	return servicesdk.Options{
		Timeout:   timeout,
		UserAgent: "centralservice-go-service/0.1.0",
		Endpoints: []servicesdk.EndpointOptions{
			{
				BaseURL:        endpoint.BaseURL,
				Priority:       endpoint.Priority,
				MaxAttempts:    endpoint.MaxAttempts,
				CircuitBreaker: toServiceCircuitBreaker(endpoint.CircuitBreaker),
			},
		},
	}
}

func toClientCircuitBreaker(value map[string]interface{}) *clientsdk.CircuitBreakerOptions {
	if value == nil {
		return nil
	}
	return &clientsdk.CircuitBreakerOptions{
		FailureThreshold:     intValue(value["failureThreshold"], 1),
		BreakDurationMinutes: intValue(value["breakDurationMinutes"], 1),
		RecoveryThreshold:    intValue(value["recoveryThreshold"], 1),
	}
}

func toServiceCircuitBreaker(value map[string]interface{}) *servicesdk.CircuitBreakerOptions {
	if value == nil {
		return nil
	}
	return &servicesdk.CircuitBreakerOptions{
		FailureThreshold:     intValue(value["failureThreshold"], 1),
		BreakDurationMinutes: intValue(value["breakDurationMinutes"], 1),
		RecoveryThreshold:    intValue(value["recoveryThreshold"], 1),
	}
}

func intValue(value interface{}, fallback int) int {
	switch typed := value.(type) {
	case float64:
		if typed >= 1 {
			return int(typed)
		}
	case int:
		if typed >= 1 {
			return typed
		}
	}
	return fallback
}

func waitForHalfOpen(endpoints []endpointConfig) time.Duration {
	if raw := firstNonBlankEnv("CENTRAL_SERVICE_BREAK_WAIT_SECONDS", "CENTRAL_SERVICE_E2E_BREAK_WAIT_SECONDS"); raw != "" {
		value, err := strconv.Atoi(raw)
		if err == nil && value > 0 {
			return time.Duration(value) * time.Second
		}
	}
	maxDuration := time.Second
	for _, endpoint := range endpoints {
		if endpoint.CircuitBreaker == nil {
			continue
		}
		duration := time.Duration(intValue(endpoint.CircuitBreaker["breakDurationMinutes"], 1))*time.Minute + time.Second
		if duration > maxDuration {
			maxDuration = duration
		}
	}
	return maxDuration
}

func requireEndpointCount(endpoints []endpointConfig, minimumCount int, scenario string) error {
	if len(endpoints) < minimumCount {
		return fmt.Errorf("%s 至少需要 %d 个中心服务端点", scenario, minimumCount)
	}
	return nil
}

func assertServiceID(stepName, actualID, expectedID string) error {
	if actualID == "" || actualID != expectedID {
		return fmt.Errorf("%s 期望 id=%s，实际=%s", stepName, expectedID, actualID)
	}
	if expectedByEnv := firstNonBlankEnv("CENTRAL_SERVICE_E2E_EXPECTED_" + strings.ToUpper(stepName) + "_ID"); expectedByEnv != "" && expectedByEnv != actualID {
		return fmt.Errorf("%s 环境断言期望 id=%s，实际=%s", stepName, expectedByEnv, actualID)
	}
	return nil
}

func firstNonBlankEnv(keys ...string) string {
	for _, key := range keys {
		if value := strings.TrimSpace(os.Getenv(key)); value != "" {
			return value
		}
	}
	return ""
}
