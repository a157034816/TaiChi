"""CentralService Python service SDK 的多端点传输实现。"""

from __future__ import annotations

import json
import os
import time
import urllib.error
import urllib.request
from dataclasses import dataclass
from typing import Any, Dict, Iterable, List, Optional

DEFAULT_BASE_URL = "http://127.0.0.1:5000"
DEFAULT_TIMEOUT_SEC = 5.0
DEFAULT_MAX_ATTEMPTS = 2


@dataclass
class CentralServiceError(Exception):
    """用于 HTTP 与业务层失败的标准化 SDK 异常。"""

    http_status: int
    method: str
    url: str
    kind: str
    message: str
    error_code: Optional[int] = None
    raw_body: str = ""

    def __str__(self) -> str:  # pragma: no cover
        return f"{self.kind} HTTP {self.http_status} {self.method} {self.url}: {self.message}"


@dataclass
class CentralServiceCircuitBreakerOptions:
    """单端点熔断器配置。"""

    failure_threshold: int
    break_duration_minutes: int
    recovery_threshold: int


@dataclass
class CentralServiceEndpointOptions:
    """单端点配置。"""

    base_url: str
    priority: int = 0
    max_attempts: Optional[int] = None
    circuit_breaker: Optional[CentralServiceCircuitBreakerOptions] = None
    order: int = 0


class CircuitBreakerState:
    """在进程内维护单个端点的熔断状态。"""

    def __init__(self, options: CentralServiceCircuitBreakerOptions) -> None:
        self.failure_threshold = max(1, int(options.failure_threshold))
        self.break_duration_sec = max(1, int(options.break_duration_minutes)) * 60
        self.recovery_threshold = max(1, int(options.recovery_threshold))
        self.mode = "closed"
        self.failure_count = 0
        self.half_open_success_count = 0
        self.open_until = 0.0

    def try_allow_request(self, now: float) -> tuple[bool, Optional[str]]:
        if self.mode == "open":
            if now >= self.open_until:
                self.mode = "half-open"
                self.failure_count = 0
                self.half_open_success_count = 0
                return True, None

            remaining = max(1, int(self.open_until - now + 0.999))
            return False, f"熔断开启，剩余约 {remaining} 秒"

        return True, None

    def report_success(self) -> None:
        if self.mode == "half-open":
            self.half_open_success_count += 1
            if self.half_open_success_count >= self.recovery_threshold:
                self.mode = "closed"
                self.failure_count = 0
                self.half_open_success_count = 0
                self.open_until = 0.0
            return

        self.failure_count = 0

    def report_failure(self, now: float) -> bool:
        if self.mode == "half-open":
            self._open(now)
            return False

        self.failure_count += 1
        if self.failure_count >= self.failure_threshold:
            self._open(now)
            return False
        return True

    def _open(self, now: float) -> None:
        self.mode = "open"
        self.failure_count = 0
        self.half_open_success_count = 0
        self.open_until = now + self.break_duration_sec


@dataclass
class TransportEndpoint:
    """运行时归一化后的端点定义。"""

    base_url: str
    priority: int
    max_attempts: int
    order: int
    circuit_breaker: Optional[CircuitBreakerState]


@dataclass
class TransportResult:
    """一次传输成功后的原始 HTTP 结果。"""

    base_url: str
    url: str
    attempt: int
    max_attempts: int
    status_code: int
    body_text: str
    skipped_endpoints: List[str]


class TransportExhaustedError(Exception):
    """所有候选端点都因传输异常或熔断跳过而无法完成请求。"""

    def __init__(
        self,
        method: str,
        path: str,
        last_url: Optional[str],
        skipped_endpoints: List[str],
        failure_summaries: List[str],
    ) -> None:
        segments: List[str] = []
        if skipped_endpoints:
            segments.append("跳过端点: " + "; ".join(skipped_endpoints))
        if failure_summaries:
            segments.append("失败详情: " + "; ".join(failure_summaries))
        if not segments:
            segments.append("未找到可用的中心服务端点。")

        self.method = method
        self.path = path
        self.last_url = last_url or ""
        self.raw_detail = " | ".join(segments)
        super().__init__("中心服务调用失败，所有可用端点均已耗尽。 " + self.raw_detail)


def normalize_base_url(base_url: str) -> str:
    """去掉 BaseUrl 尾部斜杠。"""

    if not base_url:
        raise ValueError("base_url is required")
    return base_url.rstrip("/")


def normalize_max_attempts(value: Optional[int]) -> int:
    """归一化单端点最大尝试次数。"""

    try:
        normalized = int(value if value is not None and value != "" else DEFAULT_MAX_ATTEMPTS)
    except (TypeError, ValueError):
        normalized = DEFAULT_MAX_ATTEMPTS
    return normalized if normalized >= 1 else DEFAULT_MAX_ATTEMPTS


def coerce_int(value: Any, default: int) -> int:
    """将任意值转换为整数。"""

    try:
        return int(value if value is not None and value != "" else default)
    except (TypeError, ValueError):
        return default


def normalize_circuit_breaker(raw: Optional[Dict[str, Any]]) -> Optional[CentralServiceCircuitBreakerOptions]:
    """归一化熔断配置。"""

    if raw is None:
        return None

    return CentralServiceCircuitBreakerOptions(
        failure_threshold=max(1, coerce_int(raw.get("failureThreshold", raw.get("failure_threshold", 1)), 1)),
        break_duration_minutes=max(1, coerce_int(raw.get("breakDurationMinutes", raw.get("break_duration_minutes", 1)), 1)),
        recovery_threshold=max(1, coerce_int(raw.get("recoveryThreshold", raw.get("recovery_threshold", 1)), 1)),
    )


def normalize_endpoints(base_url: Optional[str], endpoints: Optional[Iterable[Dict[str, Any]]]) -> List[TransportEndpoint]:
    """将原始输入转换为排序后的端点数组。"""

    raw_endpoints = list(endpoints or [])
    if not raw_endpoints:
        raw_endpoints = [{"baseUrl": base_url}]

    normalized: List[TransportEndpoint] = []
    for order, raw_endpoint in enumerate(raw_endpoints):
        if not raw_endpoint:
            continue
        endpoint_base_url = raw_endpoint.get("baseUrl") or raw_endpoint.get("base_url")
        if not endpoint_base_url:
            continue
        breaker_options = normalize_circuit_breaker(raw_endpoint.get("circuitBreaker") or raw_endpoint.get("circuit_breaker"))
        normalized.append(
            TransportEndpoint(
                base_url=normalize_base_url(str(endpoint_base_url)),
                priority=coerce_int(raw_endpoint.get("priority", 0), 0),
                max_attempts=normalize_max_attempts(raw_endpoint.get("maxAttempts", raw_endpoint.get("max_attempts"))),
                order=order,
                circuit_breaker=CircuitBreakerState(breaker_options) if breaker_options else None,
            )
        )

    normalized.sort(key=lambda item: (item.priority, item.order))
    if not normalized:
        raise ValueError("at least one central service endpoint is required")
    return normalized


def load_sdk_options_from_env(timeout_sec: Optional[float] = None) -> Dict[str, Any]:
    """从环境变量读取 SDK 选项。"""

    resolved_timeout_sec = timeout_sec
    if resolved_timeout_sec is None:
        raw_timeout_ms = os.environ.get("CENTRAL_SERVICE_TIMEOUT_MS")
        if raw_timeout_ms:
            try:
                resolved_timeout_sec = max(1, int(raw_timeout_ms)) / 1000.0
            except ValueError:
                resolved_timeout_sec = DEFAULT_TIMEOUT_SEC
        else:
            resolved_timeout_sec = DEFAULT_TIMEOUT_SEC

    raw_endpoints = os.environ.get("CENTRAL_SERVICE_ENDPOINTS_JSON")
    if raw_endpoints:
        return {
            "endpoints": json.loads(raw_endpoints),
            "timeout_sec": resolved_timeout_sec,
        }

    return {
        "base_url": os.environ.get("CENTRAL_SERVICE_BASEURL", DEFAULT_BASE_URL).rstrip("/"),
        "timeout_sec": resolved_timeout_sec,
    }


def build_url(base_url: str, path: str) -> str:
    """拼接 BaseUrl 与路径。"""

    base = normalize_base_url(base_url)
    normalized_path = path if path.startswith("/") else f"/{path}"
    return f"{base}{normalized_path}"


def looks_like_json(text: str) -> bool:
    """判断响应体是否看起来像 JSON。"""

    if not text:
        return False
    trimmed = text.lstrip()
    return trimmed.startswith("{") or trimmed.startswith("[")


def parse_error(method: str, url: str, status: int, body_text: str) -> CentralServiceError:
    """将错误响应转换为标准化异常。"""

    raw_body = body_text or ""
    trimmed = raw_body.strip()
    if not looks_like_json(trimmed):
        return CentralServiceError(status, method, url, "PlainText", trimmed or f"HTTP {status}", raw_body=raw_body)

    try:
        parsed = json.loads(trimmed)
    except Exception:
        return CentralServiceError(status, method, url, "PlainText", trimmed or f"HTTP {status}", raw_body=raw_body)

    if isinstance(parsed, dict) and "errors" in parsed:
        return CentralServiceError(status, method, url, "ValidationProblemDetails", str(parsed.get("title") or "Validation error"), raw_body=raw_body)
    if isinstance(parsed, dict) and parsed.get("title") and parsed.get("status") is not None:
        return CentralServiceError(status, method, url, "ProblemDetails", str(parsed.get("title") or "ProblemDetails"), raw_body=raw_body)
    if isinstance(parsed, dict) and "success" in parsed:
        return CentralServiceError(
            status,
            method,
            url,
            "ApiResponse",
            str(parsed.get("errorMessage") or "ApiResponse error"),
            error_code=parsed.get("errorCode"),
            raw_body=raw_body,
        )

    return CentralServiceError(status, method, url, "Unknown", "Unknown error", raw_body=raw_body)


def append_transport_context(message: str, transport: TransportResult) -> str:
    """附加多端点上下文。"""

    segments = [
        f"端点={transport.base_url}",
        f"尝试={transport.attempt}/{transport.max_attempts}",
    ]
    if transport.skipped_endpoints:
        segments.append("已跳过=" + "、".join(transport.skipped_endpoints))
    return f"{message or ''} ({'; '.join(segments)})"


def create_parsed_error(method: str, transport: TransportResult, error: CentralServiceError) -> CentralServiceError:
    """基于传输结果补全错误上下文。"""

    return CentralServiceError(
        http_status=error.http_status,
        method=method,
        url=transport.url,
        kind=error.kind,
        message=append_transport_context(error.message, transport),
        error_code=error.error_code,
        raw_body=error.raw_body,
    )


def create_transport_error(method: str, error: TransportExhaustedError) -> CentralServiceError:
    """将端点耗尽转换为对外异常。"""

    return CentralServiceError(
        http_status=503,
        method=method,
        url=error.last_url or error.path,
        kind="Transport",
        message=str(error),
        raw_body=error.raw_detail,
    )


def http_json(method: str, url: str, body: Optional[Dict[str, Any]], timeout_sec: float) -> tuple[int, str]:
    """发送 JSON 请求并返回原始响应。"""

    data = None if body is None else json.dumps(body).encode("utf-8")
    req = urllib.request.Request(url=url, method=method)
    req.add_header("Accept", "application/json")
    if data is not None:
        req.add_header("Content-Type", "application/json; charset=utf-8")
        req.data = data

    try:
        with urllib.request.urlopen(req, timeout=timeout_sec) as resp:
            status = int(getattr(resp, "status", 0) or 0)
            text = resp.read().decode("utf-8")
            return status, text
    except urllib.error.HTTPError as exc:
        try:
            text = exc.read().decode("utf-8")
        except Exception:
            text = ""
        return int(exc.code), text


class MultiEndpointTransport:
    """按优先级处理多端点请求。"""

    def __init__(self, endpoints: List[TransportEndpoint], timeout_sec: float) -> None:
        self.endpoints = endpoints
        self.timeout_sec = timeout_sec

    def send(self, method: str, path: str, body: Optional[Dict[str, Any]]) -> TransportResult:
        skipped_endpoints: List[str] = []
        failure_summaries: List[str] = []
        last_url: Optional[str] = None

        for endpoint in self.endpoints:
            now = time.time()
            if endpoint.circuit_breaker is not None:
                allowed, reason = endpoint.circuit_breaker.try_allow_request(now)
                if not allowed:
                    skipped_endpoints.append(f"{endpoint.base_url}（{reason}）")
                    continue

            for attempt in range(1, endpoint.max_attempts + 1):
                url = build_url(endpoint.base_url, path)
                last_url = url
                try:
                    status_code, body_text = http_json(method, url, body, self.timeout_sec)
                    if endpoint.circuit_breaker is not None:
                        endpoint.circuit_breaker.report_success()
                    return TransportResult(
                        base_url=endpoint.base_url,
                        url=url,
                        attempt=attempt,
                        max_attempts=endpoint.max_attempts,
                        status_code=status_code,
                        body_text=body_text,
                        skipped_endpoints=list(skipped_endpoints),
                    )
                except (urllib.error.URLError, TimeoutError, OSError, ValueError) as exc:
                    should_retry = True
                    if endpoint.circuit_breaker is not None:
                        should_retry = endpoint.circuit_breaker.report_failure(time.time())
                    failure_summaries.append(
                        f"{endpoint.base_url} 第 {attempt}/{endpoint.max_attempts} 次失败：{type(exc).__name__}: {exc}"
                    )
                    if not should_retry:
                        break

        raise TransportExhaustedError(method, path, last_url, skipped_endpoints, failure_summaries)
