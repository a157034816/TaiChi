from __future__ import annotations

import os
import sys
import time
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
for project_root in (ROOT / "service", ROOT / "client"):
    project_root_str = str(project_root)
    if project_root_str not in sys.path:
        sys.path.insert(0, project_root_str)

from erp_centralservice_client import CentralServiceDiscoveryClient, calculate_network_score, load_sdk_options_from_env
from erp_centralservice_service import CentralServiceServiceClient


def get_timeout_ms() -> int:
    raw_value = os.environ.get("CENTRAL_SERVICE_TIMEOUT_MS") or os.environ.get("CENTRAL_SERVICE_E2E_TIMEOUT_MS") or "5000"
    try:
        value = int(raw_value)
    except ValueError:
        value = 5000
    return value if value > 0 else 5000


def get_timeout_sec() -> float:
    return get_timeout_ms() / 1000.0


def get_break_wait_seconds() -> float:
    raw_value = os.environ.get("CENTRAL_SERVICE_E2E_BREAK_WAIT_SECONDS") or "65"
    try:
        value = float(raw_value)
    except ValueError:
        value = 65.0
    return value if value > 0 else 65.0


def get_scenario() -> str:
    return os.environ.get("CENTRAL_SERVICE_E2E_SCENARIO", "smoke")


def get_service_name() -> str:
    return os.environ.get("CENTRAL_SERVICE_E2E_SERVICE_NAME", "SdkE2E")


def get_service_port() -> int:
    raw_value = os.environ.get("CENTRAL_SERVICE_E2E_SERVICE_PORT") or "18083"
    try:
        value = int(raw_value)
    except ValueError:
        value = 18083
    return value if value > 0 else 18083


def get_endpoints() -> list[dict]:
    options = load_sdk_options_from_env(timeout_sec=get_timeout_sec())
    endpoints = options.get("endpoints")
    if isinstance(endpoints, list) and endpoints:
        return endpoints
    return [{"baseUrl": options.get("base_url")}]


def create_single_endpoint_options(endpoint: dict) -> dict:
    return {"base_url": endpoint["baseUrl"], "timeout_sec": get_timeout_sec()}


def create_registration(service_id: str) -> dict:
    return {
        "id": service_id,
        "name": get_service_name(),
        "host": "127.0.0.1",
        "localIp": "127.0.0.1",
        "operatorIp": "127.0.0.1",
        "publicIp": "127.0.0.1",
        "port": get_service_port(),
        "serviceType": "Web",
        "healthCheckUrl": "/health",
        "healthCheckPort": 0,
        "heartbeatIntervalSeconds": 0,
        "weight": 1,
        "metadata": {"sdk": "python", "scenario": get_scenario()},
    }


def create_stable_service_id() -> str:
    return os.environ.get("CENTRAL_SERVICE_E2E_SERVICE_ID") or str(uuid.uuid4())


def assert_condition(condition: bool, message: str) -> None:
    if not condition:
        raise RuntimeError(f"[{get_scenario()}] {message}")


def assert_optional_expected_id(step_name: str, actual_id: str) -> None:
    env_name = f"CENTRAL_SERVICE_E2E_EXPECTED_{step_name.upper()}_ID"
    expected_id = os.environ.get(env_name)
    if expected_id:
        assert_condition(actual_id == expected_id, f"{step_name} 期望 id={expected_id}，实际={actual_id}")


def deregister_quietly(client: CentralServiceServiceClient, service_id: str) -> None:
    if not service_id:
        return
    try:
        client.deregister(service_id)
    except Exception:
        pass


def run_smoke() -> None:
    endpoint = get_endpoints()[0]
    service = CentralServiceServiceClient(create_single_endpoint_options(endpoint))
    discovery = CentralServiceDiscoveryClient(create_single_endpoint_options(endpoint))
    service_id = ""
    try:
        reg = service.register(create_registration(""))
        service_id = reg["id"]
        print(f"[py][smoke] registered id={service_id}")
        listed = discovery.list(get_service_name())
        assert_condition(any(item.get("id") == service_id for item in listed.get("services") or []), "list 未包含刚注册的服务")
        best = discovery.discover_best(get_service_name())
        assert_condition(bool(best.get("id")), "discover_best 未返回服务实例")
        evaluated = discovery.evaluate_network(service_id)
        print(f"[py][smoke] network evaluated score={calculate_network_score(evaluated)}")
        all_net = discovery.get_network_all()
        assert_condition(isinstance(all_net, list), "network/all 未返回数组")
    finally:
        deregister_quietly(service, service_id)


def run_service_fanout() -> None:
    endpoints = get_endpoints()
    assert_condition(len(endpoints) >= 2, "service_fanout 至少需要 2 个端点")
    service_id = create_stable_service_id()
    sessions: list[tuple[dict, CentralServiceServiceClient, str]] = []
    try:
        for endpoint in endpoints:
            client = CentralServiceServiceClient(create_single_endpoint_options(endpoint))
            reg = client.register(create_registration(service_id))
            sessions.append((endpoint, client, reg["id"]))

        for endpoint, _, registered_id in sessions:
            assert_condition(registered_id == service_id, f"端点 {endpoint['baseUrl']} 未复用同一个 serviceId")
            discovery = CentralServiceDiscoveryClient(create_single_endpoint_options(endpoint))
            listed = discovery.list(get_service_name())
            assert_condition(any(item.get("id") == service_id for item in listed.get("services") or []), f"端点 {endpoint['baseUrl']} 未查询到注册结果")
            best = discovery.discover_best(get_service_name())
            assert_condition(best.get("id") == service_id, f"端点 {endpoint['baseUrl']} discover_best 返回了错误实例")
    finally:
        for _, client, registered_id in sessions:
            deregister_quietly(client, registered_id)


def run_transport_failover() -> None:
    discovery = CentralServiceDiscoveryClient(load_sdk_options_from_env(timeout_sec=get_timeout_sec()))
    result = discovery.discover_best(get_service_name())
    assert_condition(bool(result.get("id")), "transport_failover 未返回服务实例")
    assert_optional_expected_id("first", result.get("id", ""))


def run_business_no_failover() -> None:
    discovery = CentralServiceDiscoveryClient(load_sdk_options_from_env(timeout_sec=get_timeout_sec()))
    endpoints = get_endpoints()
    try:
        discovery.discover_best(get_service_name())
        raise RuntimeError(f"[{get_scenario()}] 期望业务失败，但调用成功了")
    except Exception as exc:
        kind = getattr(exc, "kind", "")
        assert_condition(kind != "Transport", "业务失败场景不应被识别为传输失败")
        assert_condition(endpoints[0]["baseUrl"] in str(exc), "错误信息未包含主端点上下文")
        print(f"[py][business_no_failover] kind={kind}")


def run_max_attempts() -> None:
    discovery = CentralServiceDiscoveryClient(load_sdk_options_from_env(timeout_sec=get_timeout_sec()))
    result = discovery.discover_best(get_service_name())
    assert_condition(bool(result.get("id")), "max_attempts 未返回服务实例")
    assert_optional_expected_id("first", result.get("id", ""))


def run_circuit_open() -> None:
    discovery = CentralServiceDiscoveryClient(load_sdk_options_from_env(timeout_sec=get_timeout_sec()))
    first = discovery.discover_best(get_service_name())
    second = discovery.discover_best(get_service_name())
    assert_condition(bool(first.get("id")) and bool(second.get("id")), "circuit_open 未返回有效实例")
    assert_optional_expected_id("first", first.get("id", ""))
    assert_optional_expected_id("second", second.get("id", ""))


def run_circuit_recovery() -> None:
    discovery = CentralServiceDiscoveryClient(load_sdk_options_from_env(timeout_sec=get_timeout_sec()))
    first = discovery.discover_best(get_service_name())
    time.sleep(get_break_wait_seconds())
    second = discovery.discover_best(get_service_name())
    third = discovery.discover_best(get_service_name())
    assert_condition(
        bool(first.get("id")) and bool(second.get("id")) and bool(third.get("id")),
        "circuit_recovery 未返回有效实例",
    )
    assert_optional_expected_id("first", first.get("id", ""))
    assert_optional_expected_id("second", second.get("id", ""))
    assert_optional_expected_id("third", third.get("id", ""))


def run_half_open_reopen() -> None:
    discovery = CentralServiceDiscoveryClient(load_sdk_options_from_env(timeout_sec=get_timeout_sec()))
    first = discovery.discover_best(get_service_name())
    time.sleep(get_break_wait_seconds())
    second = discovery.discover_best(get_service_name())
    third = discovery.discover_best(get_service_name())
    assert_condition(bool(first.get("id")) and bool(second.get("id")) and bool(third.get("id")), "half_open_reopen 未返回有效实例")
    assert_optional_expected_id("first", first.get("id", ""))
    assert_optional_expected_id("second", second.get("id", ""))
    assert_optional_expected_id("third", third.get("id", ""))


def main() -> None:
    endpoints = get_endpoints()
    print(f"[py] scenario={get_scenario()}")
    print(f"[py] timeoutMs={get_timeout_ms()}")
    print(f"[py] endpoints={endpoints}")

    handlers = {
        "smoke": run_smoke,
        "service_fanout": run_service_fanout,
        "transport_failover": run_transport_failover,
        "business_no_failover": run_business_no_failover,
        "max_attempts": run_max_attempts,
        "circuit_open": run_circuit_open,
        "circuit_recovery": run_circuit_recovery,
        "half_open_reopen": run_half_open_reopen,
    }

    handler = handlers.get(get_scenario())
    assert_condition(handler is not None, f"不支持的场景: {get_scenario()}")
    handler()
    print(f"[py] scenario passed: {get_scenario()}")


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print(f"[py] scenario failed: {get_scenario()} {exc}", file=sys.stderr)
        raise
