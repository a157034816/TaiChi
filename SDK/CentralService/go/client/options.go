package centralservice

import (
	"sort"
	"strings"
	"time"
)

const defaultMaxAttempts = 2

// CircuitBreakerOptions 描述单个中心服务端点的熔断器配置。
type CircuitBreakerOptions struct {
	// FailureThreshold 表示连续失败多少次后进入熔断。
	FailureThreshold int `json:"failureThreshold"`
	// BreakDurationMinutes 表示熔断持续时长，单位为分钟。
	BreakDurationMinutes int `json:"breakDurationMinutes"`
	// RecoveryThreshold 表示半开状态下连续成功多少次后恢复。
	RecoveryThreshold int `json:"recoveryThreshold"`
}

// EndpointOptions 描述单个中心服务端点的连接配置。
type EndpointOptions struct {
	// BaseURL 是中心服务 API 根地址。
	BaseURL string `json:"baseUrl"`
	// Priority 是端点优先级，数值越小越优先。
	Priority int `json:"priority"`
	// MaxAttempts 是单个端点允许的最大尝试次数，含首次调用。
	MaxAttempts int `json:"maxAttempts"`
	// CircuitBreaker 是该端点的熔断配置；为空表示禁用熔断。
	CircuitBreaker *CircuitBreakerOptions `json:"circuitBreaker"`
	order          int
}

// Options 定义 SDK HTTP 客户端的基础配置。
type Options struct {
	// BaseURL 是中心服务 API 根地址。
	BaseURL string
	// Endpoints 描述多个中心服务端点；非空时优先生效。
	Endpoints []EndpointOptions
	// Timeout 是单次请求超时时间。
	Timeout time.Duration
	// UserAgent 是请求头中发送的 User-Agent。
	UserAgent string
}

// NewOptions 基于 baseURL 返回带默认超时和 User-Agent 的配置。
func NewOptions(baseURL string) Options {
	return Options{
		BaseURL:   baseURL,
		Timeout:   5 * time.Second,
		UserAgent: "centralservice-go-client/0.1.0",
	}
}

// NormalizeBaseURL 去掉基地址尾部斜杠，便于稳定拼接相对路径。
func NormalizeBaseURL(baseURL string) string {
	return strings.TrimRight(strings.TrimSpace(baseURL), "/")
}

func (o Options) normalizedEndpoints() []EndpointOptions {
	if len(o.Endpoints) == 0 {
		baseURL := NormalizeBaseURL(o.BaseURL)
		if baseURL == "" {
			panic("baseURL is required")
		}
		return []EndpointOptions{
			{
				BaseURL: baseURL,
				order:   0,
			},
		}
	}

	endpoints := make([]EndpointOptions, 0, len(o.Endpoints))
	for index, endpoint := range o.Endpoints {
		baseURL := NormalizeBaseURL(endpoint.BaseURL)
		if baseURL == "" {
			continue
		}

		normalized := endpoint
		normalized.BaseURL = baseURL
		if normalized.MaxAttempts < 1 {
			normalized.MaxAttempts = defaultMaxAttempts
		}
		if normalized.CircuitBreaker != nil {
			normalized.CircuitBreaker = &CircuitBreakerOptions{
				FailureThreshold:     max(1, normalized.CircuitBreaker.FailureThreshold),
				BreakDurationMinutes: max(1, normalized.CircuitBreaker.BreakDurationMinutes),
				RecoveryThreshold:    max(1, normalized.CircuitBreaker.RecoveryThreshold),
			}
		}
		normalized.order = index
		endpoints = append(endpoints, normalized)
	}

	if len(endpoints) == 0 {
		panic("at least one endpoint is required")
	}

	sort.SliceStable(endpoints, func(i, j int) bool {
		if endpoints[i].Priority == endpoints[j].Priority {
			return endpoints[i].order < endpoints[j].order
		}
		return endpoints[i].Priority < endpoints[j].Priority
	})
	return endpoints
}

func max(left, right int) int {
	if left > right {
		return left
	}
	return right
}
