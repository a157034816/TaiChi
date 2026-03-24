package centralservice

import (
	"encoding/json"
	"fmt"
	"strings"
)

// ErrorKind 标识中心服务返回错误的分类。
type ErrorKind string

const (
	// ErrorKindTransport 表示所有候选端点都因传输层失败或熔断跳过而不可用。
	ErrorKindTransport ErrorKind = "Transport"
	// ErrorKindApiResponse 表示错误体符合 ApiResponse 结构。
	ErrorKindApiResponse ErrorKind = "ApiResponse"
	// ErrorKindProblemDetails 表示错误体符合 RFC 7807 问题详情结构。
	ErrorKindProblemDetails ErrorKind = "ProblemDetails"
	// ErrorKindValidationProblemDetails 表示错误体包含字段级校验信息。
	ErrorKindValidationProblemDetails ErrorKind = "ValidationProblemDetails"
	// ErrorKindPlainText 表示错误体仅包含纯文本。
	ErrorKindPlainText ErrorKind = "PlainText"
	// ErrorKindUnknown 表示错误体无法归类到已知格式。
	ErrorKindUnknown ErrorKind = "Unknown"
)

// CentralServiceError 封装 HTTP 请求失败时的上下文信息。
type CentralServiceError struct {
	// HTTPStatus 是响应状态码。
	HTTPStatus int
	// Method 是发起请求时使用的 HTTP 方法。
	Method string
	// URL 是请求目标地址。
	URL string
	// Kind 表示错误体被解析出的分类。
	Kind ErrorKind
	// Message 是适合直接展示的错误摘要。
	Message string
	// ErrorCode 是业务错误码，适用于 ApiResponse 错误体。
	ErrorCode *int
	// RawBody 保留原始响应体，便于诊断。
	RawBody string
}

// Error 返回适合日志记录和上层包装的错误描述。
func (e *CentralServiceError) Error() string {
	return fmt.Sprintf("%s HTTP %d %s %s: %s", e.Kind, e.HTTPStatus, e.Method, e.URL, e.Message)
}

// looksLikeJSON 仅做快速前缀判断，避免对明显的纯文本错误体重复反序列化。
func looksLikeJSON(text string) bool {
	t := strings.TrimLeft(text, " \r\n\t")
	return strings.HasPrefix(t, "{") || strings.HasPrefix(t, "[")
}

// parseError 按已知响应结构解析错误体，并尽量保留原始上下文。
func parseError(method, url string, status int, body string) *CentralServiceError {
	raw := body
	trimmed := strings.TrimSpace(raw)
	if !looksLikeJSON(trimmed) {
		msg := trimmed
		if msg == "" {
			msg = fmt.Sprintf("HTTP %d", status)
		}
		return &CentralServiceError{HTTPStatus: status, Method: method, URL: url, Kind: ErrorKindPlainText, Message: msg, RawBody: raw}
	}

	var obj map[string]any
	if err := json.Unmarshal([]byte(trimmed), &obj); err != nil {
		// 无法解析成 JSON 时退回纯文本错误，避免吞掉原始响应。
		msg := trimmed
		if msg == "" {
			msg = fmt.Sprintf("HTTP %d", status)
		}
		return &CentralServiceError{HTTPStatus: status, Method: method, URL: url, Kind: ErrorKindPlainText, Message: msg, RawBody: raw}
	}

	if _, ok := obj["errors"]; ok {
		title, _ := obj["title"].(string)
		if title == "" {
			title = "Validation error"
		}
		return &CentralServiceError{HTTPStatus: status, Method: method, URL: url, Kind: ErrorKindValidationProblemDetails, Message: title, RawBody: raw}
	}

	if obj["title"] != nil && obj["status"] != nil {
		title, _ := obj["title"].(string)
		if title == "" {
			title = "ProblemDetails"
		}
		return &CentralServiceError{HTTPStatus: status, Method: method, URL: url, Kind: ErrorKindProblemDetails, Message: title, RawBody: raw}
	}

	if _, ok := obj["success"]; ok {
		msg, _ := obj["errorMessage"].(string)
		var codePtr *int
		if v, ok2 := obj["errorCode"].(float64); ok2 {
			c := int(v)
			codePtr = &c
		}
		if msg == "" {
			msg = "ApiResponse error"
		}
		return &CentralServiceError{HTTPStatus: status, Method: method, URL: url, Kind: ErrorKindApiResponse, Message: msg, ErrorCode: codePtr, RawBody: raw}
	}

	return &CentralServiceError{HTTPStatus: status, Method: method, URL: url, Kind: ErrorKindUnknown, Message: "Unknown error", RawBody: raw}
}
