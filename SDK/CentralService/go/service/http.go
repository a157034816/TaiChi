package centralservice

import (
	"bytes"
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"net"
	"net/http"
	"strings"
	"sync"
	"time"
)

// httpClient 封装 SDK 内部的多端点 HTTP 传输配置。
type httpClient struct {
	endpoints []transportEndpoint
	timeout   time.Duration
	ua        string
	now       func() time.Time
}

type transportEndpoint struct {
	BaseURL        string
	Priority       int
	MaxAttempts    int
	Order          int
	CircuitBreaker *circuitBreakerState
}

type transportResult struct {
	BaseURL          string
	URL              string
	Attempt          int
	MaxAttempts      int
	StatusCode       int
	Body             string
	SkippedEndpoints []string
}

type transportExhaustedError struct {
	Method    string
	Path      string
	LastURL   string
	RawDetail string
}

func (e *transportExhaustedError) Error() string {
	return "中心服务调用失败，所有可用端点均已耗尽。 " + e.RawDetail
}

type circuitBreakerState struct {
	mu                   sync.Mutex
	failureThreshold     int
	breakDuration        time.Duration
	recoveryThreshold    int
	mode                 circuitBreakerMode
	failureCount         int
	halfOpenSuccessCount int
	openUntil            time.Time
}

type circuitBreakerMode int

const (
	circuitBreakerClosed circuitBreakerMode = iota
	circuitBreakerOpen
	circuitBreakerHalfOpen
)

// newHTTPClient 归一化 Options，并补齐默认超时与 User-Agent。
func newHTTPClient(opts Options) *httpClient {
	timeout := opts.Timeout
	if timeout <= 0 {
		timeout = 5 * time.Second
	}

	ua := opts.UserAgent
	if ua == "" {
		ua = "centralservice-go-service"
	}

	endpoints := opts.normalizedEndpoints()
	runtimeEndpoints := make([]transportEndpoint, 0, len(endpoints))
	for _, endpoint := range endpoints {
		runtimeEndpoint := transportEndpoint{
			BaseURL:     endpoint.BaseURL,
			Priority:    endpoint.Priority,
			MaxAttempts: endpoint.MaxAttempts,
			Order:       endpoint.order,
		}
		if runtimeEndpoint.MaxAttempts < 1 {
			runtimeEndpoint.MaxAttempts = defaultMaxAttempts
		}
		if endpoint.CircuitBreaker != nil {
			runtimeEndpoint.CircuitBreaker = newCircuitBreakerState(endpoint.CircuitBreaker)
		}
		runtimeEndpoints = append(runtimeEndpoints, runtimeEndpoint)
	}

	return &httpClient{
		endpoints: runtimeEndpoints,
		timeout:   timeout,
		ua:        ua,
		now:       time.Now,
	}
}

// buildURL 统一补齐路径前导斜杠，避免与 baseURL 拼接出错。
func (c *httpClient) buildURL(baseURL, path string) string {
	if path == "" {
		return baseURL
	}
	if !strings.HasPrefix(path, "/") {
		path = "/" + path
	}
	return baseURL + path
}

// send 统一处理多端点重试、熔断与响应体读取逻辑。
func (c *httpClient) send(ctx context.Context, method, path string, body any) (transportResult, error) {
	skippedEndpoints := make([]string, 0)
	failureSummaries := make([]string, 0)
	var lastURL string

	for _, endpoint := range c.endpoints {
		now := c.now()
		if endpoint.CircuitBreaker != nil {
			allowed, skipReason := endpoint.CircuitBreaker.TryAllowRequest(now)
			if !allowed {
				skippedEndpoints = append(skippedEndpoints, fmt.Sprintf("%s（%s）", endpoint.BaseURL, skipReason))
				continue
			}
		}

		for attempt := 1; attempt <= endpoint.MaxAttempts; attempt++ {
			requestURL := c.buildURL(endpoint.BaseURL, path)
			lastURL = requestURL

			statusCode, responseBody, err := c.sendCore(ctx, method, requestURL, body)
			if err == nil {
				if endpoint.CircuitBreaker != nil {
					endpoint.CircuitBreaker.ReportSuccess()
				}
				return transportResult{
					BaseURL:          endpoint.BaseURL,
					URL:              requestURL,
					Attempt:          attempt,
					MaxAttempts:      endpoint.MaxAttempts,
					StatusCode:       statusCode,
					Body:             responseBody,
					SkippedEndpoints: append([]string(nil), skippedEndpoints...),
				}, nil
			}

			if !isTransportError(err) {
				return transportResult{}, err
			}

			if endpoint.CircuitBreaker != nil {
				endpoint.CircuitBreaker.ReportFailure(c.now())
			}
			failureSummaries = append(
				failureSummaries,
				fmt.Sprintf("%s 第 %d/%d 次失败：%T: %s", endpoint.BaseURL, attempt, endpoint.MaxAttempts, err, err.Error()),
			)
		}
	}

	segments := make([]string, 0, 2)
	if len(skippedEndpoints) > 0 {
		segments = append(segments, "跳过端点: "+strings.Join(skippedEndpoints, "; "))
	}
	if len(failureSummaries) > 0 {
		segments = append(segments, "失败详情: "+strings.Join(failureSummaries, "; "))
	}
	if len(segments) == 0 {
		segments = append(segments, "未找到可用的中心服务端点。")
	}

	return transportResult{}, &transportExhaustedError{
		Method:    method,
		Path:      path,
		LastURL:   lastURL,
		RawDetail: strings.Join(segments, " | "),
	}
}

func (c *httpClient) sendCore(ctx context.Context, method, url string, body any) (int, string, error) {
	var reader io.Reader
	if body != nil {
		payload, err := json.Marshal(body)
		if err != nil {
			return 0, "", err
		}
		reader = bytes.NewReader(payload)
	}

	req, err := http.NewRequestWithContext(ctx, method, url, reader)
	if err != nil {
		return 0, "", err
	}
	req.Header.Set("Accept", "application/json")
	req.Header.Set("User-Agent", c.ua)
	if body != nil {
		req.Header.Set("Content-Type", "application/json; charset=utf-8")
	}

	client := &http.Client{Timeout: c.timeout}
	resp, err := client.Do(req)
	if err != nil {
		return 0, "", err
	}
	defer resp.Body.Close()

	data, readErr := io.ReadAll(resp.Body)
	if readErr != nil {
		return 0, "", readErr
	}
	return resp.StatusCode, string(data), nil
}

func isTransportError(err error) bool {
	var netErr net.Error
	if errors.As(err, &netErr) {
		return true
	}
	return errors.Is(err, io.EOF) || errors.Is(err, io.ErrUnexpectedEOF)
}

func appendTransportContext(message string, result transportResult) string {
	segments := []string{
		fmt.Sprintf("端点=%s", result.BaseURL),
		fmt.Sprintf("尝试=%d/%d", result.Attempt, result.MaxAttempts),
	}
	if len(result.SkippedEndpoints) > 0 {
		segments = append(segments, "已跳过="+strings.Join(result.SkippedEndpoints, "、"))
	}
	return fmt.Sprintf("%s (%s)", message, strings.Join(segments, "; "))
}

func newTransportError(method, path string, err error) *CentralServiceError {
	exhausted := &transportExhaustedError{}
	if errors.As(err, &exhausted) {
		targetURL := exhausted.LastURL
		if targetURL == "" {
			targetURL = path
		}
		return &CentralServiceError{
			HTTPStatus: 503,
			Method:     method,
			URL:        targetURL,
			Kind:       ErrorKindTransport,
			Message:    exhausted.Error(),
			RawBody:    exhausted.RawDetail,
		}
	}

	return &CentralServiceError{
		HTTPStatus: 503,
		Method:     method,
		URL:        path,
		Kind:       ErrorKindTransport,
		Message:    err.Error(),
	}
}

func newCircuitBreakerState(options *CircuitBreakerOptions) *circuitBreakerState {
	breakDuration := time.Duration(max(1, options.BreakDurationMinutes)) * time.Minute
	return &circuitBreakerState{
		failureThreshold:  max(1, options.FailureThreshold),
		breakDuration:     breakDuration,
		recoveryThreshold: max(1, options.RecoveryThreshold),
		mode:              circuitBreakerClosed,
	}
}

func (s *circuitBreakerState) TryAllowRequest(now time.Time) (bool, string) {
	s.mu.Lock()
	defer s.mu.Unlock()

	if s.mode == circuitBreakerOpen {
		if !now.Before(s.openUntil) {
			s.mode = circuitBreakerHalfOpen
			s.failureCount = 0
			s.halfOpenSuccessCount = 0
			return true, ""
		}

		remainingSeconds := int(s.openUntil.Sub(now).Seconds())
		if remainingSeconds < 1 {
			remainingSeconds = 1
		}
		return false, fmt.Sprintf("熔断开启，剩余约 %d 秒", remainingSeconds)
	}

	return true, ""
}

func (s *circuitBreakerState) ReportSuccess() {
	s.mu.Lock()
	defer s.mu.Unlock()

	if s.mode == circuitBreakerHalfOpen {
		s.halfOpenSuccessCount++
		if s.halfOpenSuccessCount >= s.recoveryThreshold {
			s.mode = circuitBreakerClosed
			s.failureCount = 0
			s.halfOpenSuccessCount = 0
			s.openUntil = time.Time{}
		}
		return
	}

	s.failureCount = 0
}

func (s *circuitBreakerState) ReportFailure(now time.Time) {
	s.mu.Lock()
	defer s.mu.Unlock()

	if s.mode == circuitBreakerHalfOpen {
		s.open(now)
		return
	}

	s.failureCount++
	if s.failureCount >= s.failureThreshold {
		s.open(now)
	}
}

func (s *circuitBreakerState) open(now time.Time) {
	s.mode = circuitBreakerOpen
	s.failureCount = 0
	s.halfOpenSuccessCount = 0
	s.openUntil = now.Add(s.breakDuration)
}
