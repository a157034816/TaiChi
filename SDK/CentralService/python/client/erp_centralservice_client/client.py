"""CentralService 服务发现 SDK。"""

from __future__ import annotations

import json
import urllib.parse
from typing import Any, Dict, Optional

from .transport import (
    DEFAULT_BASE_URL,
    DEFAULT_TIMEOUT_SEC,
    CentralServiceEndpointOptions,
    CentralServiceError,
    create_parsed_error,
    create_transport_error,
    load_sdk_options_from_env,
    normalize_endpoints,
    parse_error,
    MultiEndpointTransport,
)


def calculate_network_score(status: Dict[str, Any]) -> int:
    """在客户端复现服务端的网络评分契约。"""

    if not status or not status.get("isAvailable"):
        return 0

    response_time = float(status.get("responseTime") or 0)
    packet_loss = float(status.get("packetLoss") or 0)
    response_time_score = 0 if response_time >= 1000 else 50 if response_time <= 50 else int(50 * (1 - (response_time - 50) / 950.0))
    packet_loss_score = 0 if packet_loss >= 50 else 50 if packet_loss <= 0 else int(50 * (1 - packet_loss / 50.0))
    return int(response_time_score + packet_loss_score)


class CentralServiceDiscoveryClient:
    """面向注册中心列表、服务发现与网络评估的客户端。"""

    def __init__(
        self,
        base_url_or_options: str | Dict[str, Any],
        timeout_sec: float = DEFAULT_TIMEOUT_SEC,
    ) -> None:
        options: Dict[str, Any]
        if isinstance(base_url_or_options, dict):
            options = dict(base_url_or_options)
        else:
            options = {"base_url": base_url_or_options, "timeout_sec": timeout_sec}

        self.timeout_sec = float(options.get("timeout_sec", options.get("timeoutSec", timeout_sec)) or timeout_sec)
        normalized_endpoints = normalize_endpoints(
            options.get("base_url") or options.get("baseUrl"),
            options.get("endpoints"),
        )
        self.base_url = normalized_endpoints[0].base_url
        self.endpoints = [
            CentralServiceEndpointOptions(
                base_url=item.base_url,
                priority=item.priority,
                max_attempts=item.max_attempts,
                circuit_breaker=None,
                order=item.order,
            )
            for item in normalized_endpoints
        ]
        self.transport = MultiEndpointTransport(normalized_endpoints, self.timeout_sec)

    def list(self, name: Optional[str] = None) -> Dict[str, Any]:
        query = f"?name={urllib.parse.quote(name)}" if name else ""
        transport = self._send("GET", f"/api/Service/list{query}", None)
        api = json.loads(transport.body_text or "{}")
        if not api.get("success"):
            raise create_parsed_error("GET", transport, parse_error("GET", transport.url, transport.status_code, transport.body_text))
        return api.get("data")

    def discover_roundrobin(self, service_name: str) -> Dict[str, Any]:
        return self._send_json("GET", f"/api/ServiceDiscovery/discover/roundrobin/{urllib.parse.quote(service_name)}", None)

    def discover_weighted(self, service_name: str) -> Dict[str, Any]:
        return self._send_json("GET", f"/api/ServiceDiscovery/discover/weighted/{urllib.parse.quote(service_name)}", None)

    def discover_best(self, service_name: str) -> Dict[str, Any]:
        return self._send_json("GET", f"/api/ServiceDiscovery/discover/best/{urllib.parse.quote(service_name)}", None)

    def get_network_all(self) -> Any:
        return self._send_json("GET", "/api/ServiceDiscovery/network/all", None)

    def get_network(self, service_id: str) -> Dict[str, Any]:
        return self._send_json("GET", f"/api/ServiceDiscovery/network/{urllib.parse.quote(service_id)}", None)

    def evaluate_network(self, service_id: str) -> Dict[str, Any]:
        return self._send_json("POST", f"/api/ServiceDiscovery/network/evaluate/{urllib.parse.quote(service_id)}", None)

    def _send_json(self, method: str, path: str, body: Optional[Dict[str, Any]]) -> Any:
        transport = self._send(method, path, body)
        return json.loads(transport.body_text or "null")

    def _send(self, method: str, path: str, body: Optional[Dict[str, Any]]):
        try:
            transport = self.transport.send(method, path, body)
        except Exception as exc:
            if type(exc).__name__ == "TransportExhaustedError":
                raise create_transport_error(method, exc) from exc
            raise

        if transport.status_code < 200 or transport.status_code > 299:
            raise create_parsed_error(method, transport, parse_error(method, transport.url, transport.status_code, transport.body_text))
        return transport


def default_base_url() -> str:
    """返回 CentralService SDK 示例中约定使用的 BaseUrl。"""

    options = load_sdk_options_from_env()
    endpoints = options.get("endpoints")
    if isinstance(endpoints, list) and endpoints:
        return str(endpoints[0].get("baseUrl") or endpoints[0].get("base_url") or DEFAULT_BASE_URL).rstrip("/")
    return str(options.get("base_url") or DEFAULT_BASE_URL).rstrip("/")


__all__ = [
    "CentralServiceEndpointOptions",
    "CentralServiceError",
    "CentralServiceDiscoveryClient",
    "calculate_network_score",
    "default_base_url",
    "load_sdk_options_from_env",
]
