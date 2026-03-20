from __future__ import annotations

import argparse
import os
import sys
from datetime import datetime
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from centralservice_e2e_support import (
    DotNetVariant,
    ScenarioContext,
    build_dotnet_variant,
    build_java_classes,
    ensure_health_stub,
    get_fault_request_count,
    invoke_centralservice_api,
    new_circuit_breaker,
    new_endpoint,
    new_language_service_name,
    register_seed_service,
    reset_fault_count,
    set_fault_state,
    start_centralservice_instance_with_retry,
    start_fault_proxy,
    start_fault_recovery_transition,
    start_health_stub_with_retry,
    test_dotnet_runtime,
)
from centralservice_script_common import (
    build_child_env,
    convert_to_json_array,
    current_python_executable,
    ensure_dir,
    get_unused_port,
    normalize_items,
    require_command,
    resolve_java_command,
    run_command,
    set_go_env,
    show_logs,
    stop_process,
    write_info,
    write_warn,
)


DEFAULT_LANGUAGES = ["dotnet", "javascript", "python", "rust", "java", "go"]
DEFAULT_SCENARIOS = [
    "smoke",
    "service_fanout",
    "transport_failover",
    "business_no_failover",
    "max_attempts",
    "circuit_open",
    "circuit_recovery",
    "half_open_reopen",
]


def parse_args() -> argparse.Namespace:
    """Parse CLI arguments while preserving the historical PowerShell names."""
    parser = argparse.ArgumentParser()
    parser.add_argument("-RepoRoot", "--repo-root", default="")
    parser.add_argument("-BaseUrl", "--base-url", default="")
    parser.add_argument("-Languages", "--languages", nargs="*", default=list(DEFAULT_LANGUAGES))
    parser.add_argument("-Scenarios", "--scenarios", nargs="*", default=list(DEFAULT_SCENARIOS))
    parser.add_argument("-HealthTimeoutSeconds", "--health-timeout-seconds", type=int, default=45)
    parser.add_argument("-BreakWaitSeconds", "--break-wait-seconds", type=int, default=65)
    parser.add_argument("-TimeoutMs", "--timeout-ms", type=int, default=800)
    return parser.parse_args()


def new_initial_fault_state(context: ScenarioContext) -> dict[str, object]:
    """Create the initial fault proxy state for a scenario."""
    return {
        "mode": "close",
        "delayMs": max(context.timeout_ms + 400, 1000),
        "targetUrl": "",
        "statusCode": 404,
        "body": "fault proxy",
    }


def start_scenario_context(repo_root: Path, scenario: str, timeout_ms: int, break_wait_seconds: int, health_timeout_seconds: int) -> ScenarioContext:
    """Start the infrastructure required by a scenario."""
    context_root = ensure_dir(
        repo_root
        / ".tool"
        / "centralservice-e2e"
        / f"{scenario}-{datetime.now().strftime('%Y%m%d%H%M%S%f')}"
    )
    context = ScenarioContext(scenario=scenario, root=context_root, timeout_ms=timeout_ms, break_wait_seconds=break_wait_seconds)

    if scenario == "smoke":
        primary = start_centralservice_instance_with_retry(repo_root, "primary", context_root, health_timeout_seconds)
        context.services.append(primary)
        context.primary_base_url = primary.base_url
        context.endpoints = [new_endpoint(primary.base_url, 0, None, None)]
    elif scenario == "service_fanout":
        primary = start_centralservice_instance_with_retry(repo_root, "primary", context_root, health_timeout_seconds)
        secondary = start_centralservice_instance_with_retry(repo_root, "secondary", context_root, health_timeout_seconds)
        context.services.extend([primary, secondary])
        context.primary_base_url = primary.base_url
        context.backup_base_url = secondary.base_url
        context.endpoints = [new_endpoint(primary.base_url, 0, None, None), new_endpoint(secondary.base_url, 1, None, None)]
    elif scenario == "business_no_failover":
        primary = start_centralservice_instance_with_retry(repo_root, "primary", context_root, health_timeout_seconds)
        secondary = start_centralservice_instance_with_retry(repo_root, "secondary", context_root, health_timeout_seconds)
        context.services.extend([primary, secondary])
        context.primary_base_url = primary.base_url
        context.backup_base_url = secondary.base_url
        context.endpoints = [new_endpoint(primary.base_url, 0, None, None), new_endpoint(secondary.base_url, 1, None, None)]
    elif scenario == "transport_failover":
        backup = start_centralservice_instance_with_retry(repo_root, "backup", context_root, health_timeout_seconds)
        proxy = start_fault_proxy(SCRIPT_DIR, get_unused_port(), context_root, "proxy")
        context.services.append(backup)
        context.proxies.append(proxy)
        context.primary_base_url = proxy.base_url
        context.backup_base_url = backup.base_url
        context.fault_proxy = proxy
        context.endpoints = [new_endpoint(proxy.base_url, 0, None, None), new_endpoint(backup.base_url, 1, None, None)]
    elif scenario == "max_attempts":
        backup = start_centralservice_instance_with_retry(repo_root, "backup", context_root, health_timeout_seconds)
        proxy = start_fault_proxy(SCRIPT_DIR, get_unused_port(), context_root, "proxy")
        context.services.append(backup)
        context.proxies.append(proxy)
        context.primary_base_url = proxy.base_url
        context.backup_base_url = backup.base_url
        context.fault_proxy = proxy
        context.endpoints = [new_endpoint(proxy.base_url, 0, 3, None), new_endpoint(backup.base_url, 1, None, None)]
    elif scenario == "circuit_open":
        backup = start_centralservice_instance_with_retry(repo_root, "backup", context_root, health_timeout_seconds)
        proxy = start_fault_proxy(SCRIPT_DIR, get_unused_port(), context_root, "proxy")
        context.services.append(backup)
        context.proxies.append(proxy)
        context.primary_base_url = proxy.base_url
        context.backup_base_url = backup.base_url
        context.fault_proxy = proxy
        context.endpoints = [
            new_endpoint(proxy.base_url, 0, None, new_circuit_breaker(1, 1, 1)),
            new_endpoint(backup.base_url, 1, None, None),
        ]
    elif scenario == "circuit_recovery":
        backup = start_centralservice_instance_with_retry(repo_root, "backup", context_root, health_timeout_seconds)
        proxy = start_fault_proxy(SCRIPT_DIR, get_unused_port(), context_root, "proxy")
        context.services.append(backup)
        context.proxies.append(proxy)
        context.primary_base_url = proxy.base_url
        context.backup_base_url = backup.base_url
        context.fault_proxy = proxy
        context.endpoints = [
            new_endpoint(proxy.base_url, 0, None, new_circuit_breaker(1, 1, 2)),
            new_endpoint(backup.base_url, 1, None, None),
        ]
    elif scenario == "half_open_reopen":
        backup = start_centralservice_instance_with_retry(repo_root, "backup", context_root, health_timeout_seconds)
        proxy = start_fault_proxy(SCRIPT_DIR, get_unused_port(), context_root, "proxy")
        context.services.append(backup)
        context.proxies.append(proxy)
        context.primary_base_url = proxy.base_url
        context.backup_base_url = backup.base_url
        context.fault_proxy = proxy
        context.endpoints = [
            new_endpoint(proxy.base_url, 0, None, new_circuit_breaker(1, 1, 1)),
            new_endpoint(backup.base_url, 1, None, None),
        ]
    else:
        raise RuntimeError(f"Unsupported scenario: {scenario}")

    if context.fault_proxy and context.fault_proxy.state_file:
        state = new_initial_fault_state(context)
        set_fault_state(
            context.fault_proxy.state_file,
            str(state["mode"]),
            int(state["delayMs"]),
            str(state["targetUrl"]),
            int(state["statusCode"]),
            str(state["body"]),
        )
    return context


def create_scenario_environment(context: ScenarioContext, language: str) -> dict[str, str]:
    """Prepare environment variables for a language/scenario pair."""
    env = build_child_env()
    clear_keys = [
        "CENTRAL_SERVICE_E2E_SERVICE_NAME",
        "CENTRAL_SERVICE_E2E_SERVICE_PORT",
        "CENTRAL_SERVICE_E2E_SERVICE_ID",
        "CENTRAL_SERVICE_E2E_EXPECTED_FIRST_ID",
        "CENTRAL_SERVICE_E2E_EXPECTED_SECOND_ID",
        "CENTRAL_SERVICE_E2E_EXPECTED_THIRD_ID",
    ]
    for key in clear_keys:
        env.pop(key, None)

    env["CENTRAL_SERVICE_BASEURL"] = context.primary_base_url
    env["CENTRAL_SERVICE_ENDPOINTS_JSON"] = convert_to_json_array(context.endpoints)
    env["CENTRAL_SERVICE_E2E_SCENARIO"] = context.scenario
    env["CENTRAL_SERVICE_BREAK_WAIT_SECONDS"] = str(context.break_wait_seconds)
    env["CENTRAL_SERVICE_E2E_BREAK_WAIT_SECONDS"] = str(context.break_wait_seconds)
    env["CENTRAL_SERVICE_TIMEOUT_MS"] = str(context.timeout_ms)
    env["CENTRAL_SERVICE_E2E_TIMEOUT_MS"] = str(context.timeout_ms)

    if context.fault_proxy and context.fault_proxy.state_file:
        state = new_initial_fault_state(context)
        set_fault_state(
            context.fault_proxy.state_file,
            str(state["mode"]),
            int(state["delayMs"]),
            str(state["targetUrl"]),
            int(state["statusCode"]),
            str(state["body"]),
        )
        reset_fault_count(context.fault_proxy.state_file)
        if context.scenario == "circuit_recovery":
            timer = start_fault_recovery_transition(context.fault_proxy.state_file, 5, context.backup_base_url)
            context.background_jobs.append(timer)

    if language in {"javascript", "python"}:
        if context.scenario in {"smoke", "service_fanout"}:
            stub = start_health_stub_with_retry(context, language)
            env["CENTRAL_SERVICE_E2E_SERVICE_NAME"] = new_language_service_name(language, context.scenario)
            env["CENTRAL_SERVICE_E2E_SERVICE_PORT"] = str(stub.port)
            if context.scenario == "service_fanout":
                env["CENTRAL_SERVICE_E2E_SERVICE_ID"] = os.urandom(16).hex()
        else:
            service_name = new_language_service_name(language, context.scenario)
            seed = register_seed_service(context, context.backup_base_url, service_name, language)
            env["CENTRAL_SERVICE_E2E_SERVICE_NAME"] = service_name
            if context.scenario != "business_no_failover":
                env["CENTRAL_SERVICE_E2E_EXPECTED_FIRST_ID"] = seed.service_id
                if context.scenario in {"circuit_open", "circuit_recovery", "half_open_reopen"}:
                    env["CENTRAL_SERVICE_E2E_EXPECTED_SECOND_ID"] = seed.service_id
                if context.scenario in {"circuit_recovery", "half_open_reopen"}:
                    env["CENTRAL_SERVICE_E2E_EXPECTED_THIRD_ID"] = seed.service_id
    elif language == "go":
        stub = start_health_stub_with_retry(context, "go")
        env["CENTRAL_SERVICE_E2E_SERVICE_PORT"] = str(stub.port)
    elif language == "java":
        stub = start_health_stub_with_retry(context, "java")
        env["CENTRAL_SERVICE_E2E_SERVICE_PORT"] = str(stub.port)
    elif language == "rust":
        if context.scenario == "business_no_failover":
            register_seed_service(context, context.backup_base_url, "rust-business-no-failover", "rust")
        else:
            stub = start_health_stub_with_retry(context, "rust")
            env["CENTRAL_SERVICE_E2E_SERVICE_PORT"] = str(stub.port)
    elif language == "dotnet":
        return env
    else:
        raise RuntimeError(f"Unsupported language: {language}")
    return env


def should_run_dotnet_variant(context: ScenarioContext, variant: DotNetVariant) -> bool:
    """Check whether one .NET runtime variant is available for the current scenario."""
    if variant.kind == "net40":
        if context.scenario != "smoke":
            write_warn("Skipping net40 for non-smoke scenario.")
            return False
        return True

    if not test_dotnet_runtime(str(variant.runtime_version)):
        write_warn(f"Skipping {variant.name} because runtime {variant.runtime_version} is not installed.")
        return False
    return True


def invoke_dotnet_variant(repo_root: Path, sdk_root: Path, context: ScenarioContext, env: dict[str, str], variant: DotNetVariant) -> None:
    """Run one .NET E2E variant."""
    if variant.kind == "net40":
        ensure_health_stub(context, 18085, "net40")
    else:
        ensure_health_stub(context, int(variant.service_port or 0), variant.name)

    artifact = build_dotnet_variant(repo_root, variant)
    if variant.kind == "net40":
        run_command([artifact], cwd=sdk_root, env=env, action=f"dotnet e2e {variant.name}")
    else:
        run_command(["dotnet", artifact], cwd=sdk_root, env=env, action=f"dotnet e2e {variant.name}")


def invoke_language_e2e(repo_root: Path, sdk_root: Path, context: ScenarioContext, language: str, env: dict[str, str]) -> None:
    """Invoke the language-specific E2E entry point."""
    if language == "dotnet":
        variants = [
            DotNetVariant(
                key="dotnet-net6",
                name="dotnet",
                kind="modern",
                runtime_version="6.0",
                service_port=18081,
                assembly_name="CentralService.DotNetE2e",
                target_framework="net6.0",
                project_path=sdk_root / "dotnet" / "examples" / "CentralService.DotNetE2e" / "CentralService.DotNetE2e.csproj",
            ),
            DotNetVariant(
                key="dotnet-net10",
                name="dotnet10",
                kind="modern",
                runtime_version="10.0",
                service_port=18086,
                assembly_name="CentralService.DotNet10E2e",
                target_framework="net10.0",
                project_path=sdk_root / "dotnet" / "examples" / "CentralService.DotNet10E2e" / "CentralService.DotNet10E2e.csproj",
            ),
            DotNetVariant(
                key="dotnet-netcore20",
                name="dotnetcore20",
                kind="modern",
                runtime_version="2.0",
                service_port=18082,
                assembly_name="CentralService.DotNetCore20E2e",
                target_framework="netcoreapp2.0",
                project_path=sdk_root / "dotnet" / "examples" / "CentralService.DotNetCore20E2e" / "CentralService.DotNetCore20E2e.csproj",
            ),
            DotNetVariant(key="dotnet-net40", name="net40", kind="net40"),
        ]
        for variant in variants:
            if not should_run_dotnet_variant(context, variant):
                continue
            variant_env = create_scenario_environment(context, language)
            invoke_dotnet_variant(repo_root, sdk_root, context, variant_env, variant)
            validate_scenario_for_language(context, f"{language}/{variant.name}")
        return

    if language == "javascript":
        require_command("node", "Install Node.js and ensure 'node' is in PATH.")
        run_command(["node", "examples/e2e.js"], cwd=sdk_root / "javascript", env=env, action="javascript e2e")
        return

    if language == "python":
        require_command("python", "Install Python 3.10+ and ensure 'python' is in PATH.")
        run_command([current_python_executable(), "-X", "utf8", "examples/e2e.py"], cwd=sdk_root / "python", env=env, action="python e2e")
        return

    if language == "rust":
        require_command("cargo", "Install Rust and ensure 'cargo' is in PATH.")
        run_command(["cargo", "run", "-p", "centralservice_sdk_e2e"], cwd=sdk_root / "rust", env=env, action="rust e2e")
        return

    if language == "java":
        build = build_java_classes(sdk_root)
        java = resolve_java_command("java.exe")
        run_command([java, "-cp", build.class_path, "E2EMain"], cwd=build.root, env=env, action="java e2e")
        return

    if language == "go":
        require_command("go", "Install Go 1.20+ and ensure 'go' is in PATH.")
        run_command(["go", "run", "."], cwd=sdk_root / "go" / "examples" / "e2e", env=set_go_env(repo_root, env), action="go e2e")
        return

    raise RuntimeError(f"Unsupported language: {language}")


def validate_scenario_for_language(context: ScenarioContext, language: str) -> None:
    """Validate fault proxy request counts when the scenario uses a proxy."""
    if not context.fault_proxy or not context.fault_proxy.state_file:
        return

    count = get_fault_request_count(context.fault_proxy.state_file)
    expected_language = language.split("/", 1)[0]
    if context.scenario == "transport_failover" and count < 1:
        raise RuntimeError(f"transport_failover expected proxy requestCount >= 1, actual={count}")
    if context.scenario == "max_attempts" and count != 3:
        raise RuntimeError(f"max_attempts expected proxy requestCount = 3, actual={count}")
    if context.scenario == "circuit_open":
        expected = 1 if expected_language == "python" else 2
        if count != expected:
            raise RuntimeError(f"circuit_open expected proxy requestCount = {expected}, actual={count}")
    if context.scenario == "circuit_recovery" and count not in {3, 4}:
        raise RuntimeError(f"circuit_recovery expected proxy requestCount in {{3, 4}}, actual={count}")
    if context.scenario == "half_open_reopen":
        expected = 2 if expected_language == "python" else 4
        if count != expected:
            raise RuntimeError(f"half_open_reopen expected proxy requestCount = {expected}, actual={count}")
    write_info(f"validated fault proxy requestCount={count} language={language} scenario={context.scenario}")


def stop_health_stub(stub: object) -> None:
    """Stop a health stub quietly."""
    try:
        stub.server.shutdown()
        stub.server.server_close()
        stub.thread.join(timeout=5)
    except Exception:
        pass


def stop_scenario_context(context: ScenarioContext | None) -> None:
    """Stop all resources created for a scenario."""
    if context is None:
        return
    for seed in context.seeded_services:
        try:
            invoke_centralservice_api(seed.endpoint_base_url, "DELETE", f"/api/service/deregister/{seed.service_id}", None)
        except Exception:
            pass
    for job in context.background_jobs:
        try:
            job.cancel()
        except Exception:
            pass
    for stub in context.health_stubs:
        stop_health_stub(stub)
    for proxy in context.proxies:
        stop_process(proxy.process)
    for service in context.services:
        stop_process(service.process)


def show_scenario_logs(context: ScenarioContext | None) -> None:
    """Show service/proxy logs for one failed scenario."""
    if context is None:
        return
    paths: list[Path] = []
    for service in context.services:
        paths.extend(service.log_paths)
    for proxy in context.proxies:
        paths.extend(proxy.log_paths)
        if proxy.state_file and proxy.state_file.exists():
            paths.append(proxy.state_file)
    show_logs(paths)


def main() -> None:
    """Execute all requested E2E scenarios."""
    args = parse_args()
    repo_root = Path(args.repo_root).resolve() if args.repo_root else SCRIPT_DIR.parents[3]
    sdk_root = SCRIPT_DIR.parent
    languages = [item.lower() for item in normalize_items(args.languages)]
    scenarios = [item.lower() for item in normalize_items(args.scenarios)]

    if args.base_url.strip():
        write_warn("BaseUrl parameter is ignored because e2e.py now self-hosts CentralService instances per scenario.")

    failures: list[str] = []
    for scenario in scenarios:
        for language in languages:
            write_info(f"scenario={scenario} language={language} starting")
            context: ScenarioContext | None = None
            try:
                context = start_scenario_context(repo_root, scenario, args.timeout_ms, args.break_wait_seconds, args.health_timeout_seconds)
                env = create_scenario_environment(context, language) if language != "dotnet" else {}
                invoke_language_e2e(repo_root, sdk_root, context, language, env)
                if language != "dotnet":
                    validate_scenario_for_language(context, language)
                write_info(f"scenario={scenario} language={language} passed")
            except Exception as exc:
                message = f"scenario={scenario} language={language} :: {exc}"
                failures.append(message)
                write_warn(message)
                show_scenario_logs(context)
            finally:
                stop_scenario_context(context)

    if failures:
        raise RuntimeError("CentralService SDK E2E failed:\n - " + "\n - ".join(failures))
    write_info("All CentralService SDK E2E scenarios passed.")


if __name__ == "__main__":
    main()
