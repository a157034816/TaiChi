package centralservice

import (
	"context"
	"encoding/json"
	"net/url"
)

// ServiceClient 封装中心服务注册与续约相关接口。
type ServiceClient struct {
	http *httpClient
}

// NewServiceClient 创建一个服务注册客户端。
func NewServiceClient(opts Options) *ServiceClient {
	return &ServiceClient{http: newHTTPClient(opts)}
}

// Register 向中心服务注册一个服务实例。
func (c *ServiceClient) Register(ctx context.Context, req ServiceRegistrationRequest) (ServiceRegistrationResponse, error) {
	result, err := c.http.send(ctx, "POST", "/api/Service/register", req)
	if err != nil {
		return ServiceRegistrationResponse{}, newTransportError("POST", "/api/Service/register", err)
	}
	if result.StatusCode < 200 || result.StatusCode > 299 {
		parsed := parseError("POST", result.URL, result.StatusCode, result.Body)
		parsed.Message = appendTransportContext(parsed.Message, result)
		return ServiceRegistrationResponse{}, parsed
	}

	var api ApiResponse[ServiceRegistrationResponse]
	if err := json.Unmarshal([]byte(result.Body), &api); err != nil {
		return ServiceRegistrationResponse{}, err
	}
	if !api.Success {
		parsed := parseError("POST", result.URL, result.StatusCode, result.Body)
		parsed.Message = appendTransportContext(parsed.Message, result)
		return ServiceRegistrationResponse{}, parsed
	}
	return api.Data, nil
}

// Heartbeat 为指定服务实例发送一次心跳续约。
func (c *ServiceClient) Heartbeat(ctx context.Context, serviceId string) error {
	result, err := c.http.send(ctx, "POST", "/api/Service/heartbeat", ServiceHeartbeatRequest{Id: serviceId})
	if err != nil {
		return newTransportError("POST", "/api/Service/heartbeat", err)
	}
	if result.StatusCode < 200 || result.StatusCode > 299 {
		parsed := parseError("POST", result.URL, result.StatusCode, result.Body)
		parsed.Message = appendTransportContext(parsed.Message, result)
		return parsed
	}
	return nil
}

// Deregister 从中心服务注销指定服务实例。
func (c *ServiceClient) Deregister(ctx context.Context, serviceId string) error {
	path := "/api/Service/deregister/" + url.PathEscape(serviceId)
	result, err := c.http.send(ctx, "DELETE", path, nil)
	if err != nil {
		return newTransportError("DELETE", path, err)
	}
	if result.StatusCode < 200 || result.StatusCode > 299 {
		parsed := parseError("DELETE", result.URL, result.StatusCode, result.Body)
		parsed.Message = appendTransportContext(parsed.Message, result)
		return parsed
	}
	return nil
}
