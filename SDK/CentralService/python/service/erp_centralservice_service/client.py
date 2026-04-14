"""CentralService 服务端 SDK。"""

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


class CentralServiceServiceClient:
    """面向服务自注册端点的客户端。"""

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

    def register(self, request: Dict[str, Any]) -> Dict[str, Any]:
        transport = self._send("POST", "/api/Service/register", request)
        api = json.loads(transport.body_text or "{}")
        if not api.get("success"):
            raise create_parsed_error("POST", transport, parse_error("POST", transport.url, transport.status_code, transport.body_text))
        return api.get("data")

    def deregister(self, service_id: str) -> None:
        self._send("DELETE", f"/api/Service/deregister/{urllib.parse.quote(service_id)}", None)

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
    "CentralServiceServiceClient",
    "default_base_url",
    "load_sdk_options_from_env",
]
