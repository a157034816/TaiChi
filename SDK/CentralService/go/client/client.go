package centralservice

import (
	"context"
	"encoding/json"
	"net/url"
)

// DiscoveryClient 封装中心服务发现与网络评估相关接口。
type DiscoveryClient struct {
	http *httpClient
}

// NewDiscoveryClient 创建一个发现客户端。
func NewDiscoveryClient(opts Options) *DiscoveryClient {
	return &DiscoveryClient{http: newHTTPClient(opts)}
}

// List 列出已注册服务；name 非空时按服务名过滤。
func (c *DiscoveryClient) List(ctx context.Context, name string) (ServiceListResponse, error) {
	p := "/api/Service/list"
	if name != "" {
		p += "?name=" + url.QueryEscape(name)
	}
	result, err := c.http.send(ctx, "GET", p, nil)
	if err != nil {
		return ServiceListResponse{}, newTransportError("GET", p, err)
	}
	if result.StatusCode < 200 || result.StatusCode > 299 {
		parsed := parseError("GET", result.URL, result.StatusCode, result.Body)
		parsed.Message = appendTransportContext(parsed.Message, result)
		return ServiceListResponse{}, parsed
	}

	var api ApiResponse[ServiceListResponse]
	if err := json.Unmarshal([]byte(result.Body), &api); err != nil {
		return ServiceListResponse{}, err
	}
	if !api.Success {
		parsed := parseError("GET", result.URL, result.StatusCode, result.Body)
		parsed.Message = appendTransportContext(parsed.Message, result)
		return ServiceListResponse{}, parsed
	}
	return api.Data, nil
}

// DiscoverRoundRobin 使用轮询策略发现服务实例。
func (c *DiscoveryClient) DiscoverRoundRobin(ctx context.Context, serviceName string) (ServiceInfo, error) {
	return c.getService(ctx, "/api/ServiceDiscovery/discover/roundrobin/"+url.PathEscape(serviceName))
}

// DiscoverWeighted 使用权重策略发现服务实例。
func (c *DiscoveryClient) DiscoverWeighted(ctx context.Context, serviceName string) (ServiceInfo, error) {
	return c.getService(ctx, "/api/ServiceDiscovery/discover/weighted/"+url.PathEscape(serviceName))
}

// DiscoverBest 使用综合评分策略发现服务实例。
func (c *DiscoveryClient) DiscoverBest(ctx context.Context, serviceName string) (ServiceInfo, error) {
	return c.getService(ctx, "/api/ServiceDiscovery/discover/best/"+url.PathEscape(serviceName))
}

// GetNetworkAll 获取所有服务的网络评估结果。
func (c *DiscoveryClient) GetNetworkAll(ctx context.Context) ([]ServiceNetworkStatus, error) {
	result, err := c.http.send(ctx, "GET", "/api/ServiceDiscovery/network/all", nil)
	if err != nil {
		return nil, newTransportError("GET", "/api/ServiceDiscovery/network/all", err)
	}
	if result.StatusCode < 200 || result.StatusCode > 299 {
		parsed := parseError("GET", result.URL, result.StatusCode, result.Body)
		parsed.Message = appendTransportContext(parsed.Message, result)
		return nil, parsed
	}
	var arr []ServiceNetworkStatus
	if err := json.Unmarshal([]byte(result.Body), &arr); err != nil {
		return nil, err
	}
	return arr, nil
}

// GetNetwork 获取指定服务的网络评估结果。
func (c *DiscoveryClient) GetNetwork(ctx context.Context, serviceId string) (ServiceNetworkStatus, error) {
	path := "/api/ServiceDiscovery/network/" + url.PathEscape(serviceId)
	result, err := c.http.send(ctx, "GET", path, nil)
	if err != nil {
		return ServiceNetworkStatus{}, newTransportError("GET", path, err)
	}
	if result.StatusCode < 200 || result.StatusCode > 299 {
		parsed := parseError("GET", result.URL, result.StatusCode, result.Body)
		parsed.Message = appendTransportContext(parsed.Message, result)
		return ServiceNetworkStatus{}, parsed
	}
	var s ServiceNetworkStatus
	if err := json.Unmarshal([]byte(result.Body), &s); err != nil {
		return ServiceNetworkStatus{}, err
	}
	return s, nil
}

// EvaluateNetwork 触发一次网络评估并返回最新结果。
func (c *DiscoveryClient) EvaluateNetwork(ctx context.Context, serviceId string) (ServiceNetworkStatus, error) {
	path := "/api/ServiceDiscovery/network/evaluate/" + url.PathEscape(serviceId)
	result, err := c.http.send(ctx, "POST", path, nil)
	if err != nil {
		return ServiceNetworkStatus{}, newTransportError("POST", path, err)
	}
	if result.StatusCode < 200 || result.StatusCode > 299 {
		parsed := parseError("POST", result.URL, result.StatusCode, result.Body)
		parsed.Message = appendTransportContext(parsed.Message, result)
		return ServiceNetworkStatus{}, parsed
	}
	var s ServiceNetworkStatus
	if err := json.Unmarshal([]byte(result.Body), &s); err != nil {
		return ServiceNetworkStatus{}, err
	}
	return s, nil
}

// getService 请求直接返回 ServiceInfo 的发现接口。
func (c *DiscoveryClient) getService(ctx context.Context, path string) (ServiceInfo, error) {
	result, err := c.http.send(ctx, "GET", path, nil)
	if err != nil {
		return ServiceInfo{}, newTransportError("GET", path, err)
	}
	if result.StatusCode < 200 || result.StatusCode > 299 {
		parsed := parseError("GET", result.URL, result.StatusCode, result.Body)
		parsed.Message = appendTransportContext(parsed.Message, result)
		return ServiceInfo{}, parsed
	}
	var s ServiceInfo
	if err := json.Unmarshal([]byte(result.Body), &s); err != nil {
		return ServiceInfo{}, err
	}
	return s, nil
}
