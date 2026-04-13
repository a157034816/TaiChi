from __future__ import annotations

import json
import shutil
import subprocess
import threading
import time
import uuid
from dataclasses import dataclass, field
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Any
from urllib import request
from urllib.error import HTTPError, URLError

from centralservice_script_common import (
    current_python_executable,
    ensure_dir,
    get_unused_port,
    read_json_file,
    require_command,
    resolve_java_command,
    run_command,
    set_dotnet_env,
    set_go_env,
    show_logs,
    start_background_process,
    stop_process,
    wait_for_health,
    wait_for_port,
    write_json_file,
)


BUILD_CACHE: dict[str, Any] = {}
CENTRAL_SERVICE_DLL: Path | None = None


@dataclass
class ManagedProcess:
    """A background process started by the E2E harness."""

    name: str
    port: int
    base_url: str
    process: subprocess.Popen[str]
    log_paths: list[Path]
    state_file: Path | None = None


@dataclass
class HealthStub:
    """A lightweight HTTP stub used for SDK registration and health checks."""

    name: str
    port: int
    server: ThreadingHTTPServer
    thread: threading.Thread


@dataclass
class SeededService:
    """A service seeded directly through CentralService APIs."""

    endpoint_base_url: str
    service_id: str
    service_name: str


@dataclass
class JavaBuild:
    """Compiled Java output used by the Java E2E runner."""

    root: Path
    class_path: str


@dataclass
class ScenarioContext:
    """State owned by a single E2E scenario execution."""

    scenario: str
    root: Path
    timeout_ms: int
    break_wait_seconds: int
    services: list[ManagedProcess] = field(default_factory=list)
    proxies: list[ManagedProcess] = field(default_factory=list)
    health_stubs: list[HealthStub] = field(default_factory=list)
    seeded_services: list[SeededService] = field(default_factory=list)
    background_jobs: list[threading.Timer] = field(default_factory=list)
    endpoints: list[dict[str, Any]] = field(default_factory=list)
    primary_base_url: str = ""
    backup_base_url: str = ""
    fault_proxy: ManagedProcess | None = None


@dataclass
class DotNetVariant:
    """A .NET runtime variant executed by the E2E harness."""

    key: str
    name: str
    kind: str
    runtime_version: str | None = None
    service_port: int | None = None
    assembly_name: str | None = None
    target_framework: str | None = None
    project_path: Path | None = None


class _HealthStubHandler(BaseHTTPRequestHandler):
    """Return 200 OK for every incoming request."""

    server_version = "CentralServiceHealthStub/1.0"
    protocol_version = "HTTP/1.1"

    def do_GET(self) -> None:  # noqa: N802
        self._write_ok()

    def do_POST(self) -> None:  # noqa: N802
        self._drain_body()
        self._write_ok()

    def do_DELETE(self) -> None:  # noqa: N802
        self._write_ok()

    def log_message(self, format: str, *args: Any) -> None:  # noqa: A003
        return

    def _drain_body(self) -> None:
        length = int(self.headers.get("Content-Length", "0") or "0")
        if length > 0:
            self.rfile.read(length)

    def _write_ok(self) -> None:
        payload = b"ok"
        self.send_response(200)
        self.send_header("Content-Type", "text/plain; charset=utf-8")
        self.send_header("Content-Length", str(len(payload)))
        self.send_header("Connection", "close")
        self.end_headers()
        self.wfile.write(payload)
        self.wfile.flush()


def new_endpoint(
    base_url: str,
    priority: int,
    max_attempts: int | None,
    circuit_breaker: dict[str, int] | None,
) -> dict[str, Any]:
    """Create an endpoint payload passed to each SDK implementation."""
    payload: dict[str, Any] = {"baseUrl": base_url.rstrip("/"), "priority": priority}
    if max_attempts is not None:
        payload["maxAttempts"] = int(max_attempts)
    if circuit_breaker is not None:
        payload["circuitBreaker"] = circuit_breaker
    return payload


def new_circuit_breaker(failure_threshold: int, break_duration_minutes: int, recovery_threshold: int) -> dict[str, int]:
    """Create a circuit breaker payload."""
    return {
        "failureThreshold": failure_threshold,
        "breakDurationMinutes": break_duration_minutes,
        "recoveryThreshold": recovery_threshold,
    }


def build_centralservice_host(root: Path) -> Path:
    """Build the CentralService host once and reuse the resulting DLL."""
    global CENTRAL_SERVICE_DLL

    if CENTRAL_SERVICE_DLL and CENTRAL_SERVICE_DLL.exists():
        return CENTRAL_SERVICE_DLL

    require_command("dotnet", "Install .NET SDK and ensure 'dotnet' is in PATH.")
    project = root / "TaiChi" / "Service" / "CentralService" / "CentralService.csproj"
    env = set_dotnet_env(root)
    run_command(
        [
            "dotnet",
            "build",
            str(project),
            "-c",
            "Release",
            "-p:RestoreSources=https://api.nuget.org/v3/index.json",
            "-p:RestoreIgnoreFailedSources=true",
        ],
        cwd=root,
        env=env,
        action="dotnet build CentralService host",
    )

    dll = root / "TaiChi" / "Service" / "CentralService" / "bin" / "Release" / "net8.0" / "CentralService.dll"
    if not dll.exists():
        raise RuntimeError(f"CentralService host output not found: {dll}")
    CENTRAL_SERVICE_DLL = dll
    return dll


def start_centralservice_instance(root: Path, name: str, port: int, workspace_root: Path, health_timeout_seconds: int) -> ManagedProcess:
    """Start one self-hosted CentralService instance."""
    dll = build_centralservice_host(root)
    instance_dir = ensure_dir(workspace_root / name)
    stdout = instance_dir / "stdout.log"
    stderr = instance_dir / "stderr.log"
    db_path = instance_dir / "central-service-admin.db"
    env = set_dotnet_env(
        root,
        {
            "ASPNETCORE_URLS": f"http://127.0.0.1:{port}",
            "ASPNETCORE_ENVIRONMENT": "Development",
            "CentralServiceAdminDb__ConnectionString": f"Data Source={db_path}",
        },
    )
    process = start_background_process(
        ["dotnet", str(dll)],
        cwd=dll.parent,
        env=env,
        stdout_path=stdout,
        stderr_path=stderr,
    )

    base_url = f"http://127.0.0.1:{port}"
    if not wait_for_health(base_url, health_timeout_seconds):
        stop_process(process)
        show_logs([stdout, stderr])
        raise RuntimeError(f"CentralService instance failed to become healthy: {name} ({base_url})")
    return ManagedProcess(name=name, port=port, base_url=base_url, process=process, log_paths=[stdout, stderr])


def start_centralservice_instance_with_retry(
    root: Path,
    name: str,
    workspace_root: Path,
    health_timeout_seconds: int,
    max_attempts: int = 3,
) -> ManagedProcess:
    """Retry starting a CentralService instance on a fresh port."""
    last_error: Exception | None = None
    for _ in range(max_attempts):
        try:
            return start_centralservice_instance(root, name, get_unused_port(), workspace_root, health_timeout_seconds)
        except Exception as exc:  # pragma: no cover - retry path depends on environment
            last_error = exc
            time.sleep(0.3)
    if last_error is not None:
        raise last_error
    raise RuntimeError(f"CentralService instance failed to start: {name}")


def start_fault_proxy(script_dir: Path, port: int, workspace_root: Path, name: str) -> ManagedProcess:
    """Start the Python fault proxy process."""
    proxy_dir = ensure_dir(workspace_root / name)
    stdout = proxy_dir / "stdout.log"
    stderr = proxy_dir / "stderr.log"
    state_file = proxy_dir / "state.json"
    process = start_background_process(
        [current_python_executable(), "-X", "utf8", str(script_dir / "e2e_fault_proxy.py"), "-Port", str(port), "-StateFile", str(state_file)],
        cwd=script_dir,
        env=None,
        stdout_path=stdout,
        stderr_path=stderr,
    )
    if not wait_for_port(port, 10):
        stop_process(process)
        show_logs([stdout, stderr])
        raise RuntimeError(f"Fault proxy failed to listen on port {port}")
    return ManagedProcess(
        name=name,
        port=port,
        base_url=f"http://127.0.0.1:{port}",
        process=process,
        log_paths=[stdout, stderr],
        state_file=state_file,
    )


def set_fault_state(state_file: Path, mode: str, delay_ms: int, target_url: str, status_code: int, body: str) -> None:
    """Write fault proxy state while preserving requestCount."""
    current = read_json_file(state_file) or {}
    request_count = int(current.get("requestCount") or 0)
    write_json_file(
        state_file,
        {
            "mode": mode,
            "delayMs": delay_ms,
            "targetUrl": target_url,
            "statusCode": status_code,
            "body": body,
            "requestCount": request_count,
        },
    )


def reset_fault_count(state_file: Path) -> None:
    """Reset the fault proxy request counter."""
    current = read_json_file(state_file)
    if current is None:
        return
    current["requestCount"] = 0
    write_json_file(state_file, current)


def get_fault_request_count(state_file: Path) -> int:
    """Read the fault proxy request counter."""
    current = read_json_file(state_file) or {}
    return int(current.get("requestCount") or 0)


def start_fault_recovery_transition(state_file: Path, delay_seconds: int, target_url: str) -> threading.Timer:
    """Switch the fault proxy into proxy mode after a delay."""

    def update_state() -> None:
        if not state_file.exists():
            return
        state = read_json_file(state_file)
        if not state:
            return
        state["mode"] = "proxy"
        state["targetUrl"] = target_url
        state["delayMs"] = 0
        write_json_file(state_file, state)

    timer = threading.Timer(delay_seconds, update_state)
    timer.daemon = True
    timer.start()
    return timer


def stop_background_job(job: threading.Timer | None) -> None:
    """Stop a timer-based background job."""
    if job is None:
        return
    try:
        job.cancel()
    except Exception:
        pass


def start_health_stub(port: int, name: str) -> HealthStub:
    """Start a local HTTP health stub."""
    server = ThreadingHTTPServer(("127.0.0.1", port), _HealthStubHandler)
    thread = threading.Thread(target=server.serve_forever, name=f"health-stub-{name}", daemon=True)
    thread.start()
    if not wait_for_port(port, 10):
        server.shutdown()
        server.server_close()
        raise RuntimeError(f"Health stub failed to listen on port {port}")
    return HealthStub(name=name, port=port, server=server, thread=thread)


def ensure_health_stub(context: ScenarioContext, port: int, name: str) -> HealthStub:
    """Reuse an existing stub on the same port when possible."""
    for stub in context.health_stubs:
        if stub.port == port:
            return stub
    stub = start_health_stub(port, name)
    context.health_stubs.append(stub)
    return stub


def start_health_stub_with_retry(context: ScenarioContext, name: str, max_attempts: int = 5) -> HealthStub:
    """Retry health stub startup on a fresh port."""
    last_error: Exception | None = None
    for _ in range(max_attempts):
        try:
            return ensure_health_stub(context, get_unused_port(), name)
        except Exception as exc:  # pragma: no cover - retry path depends on environment
            last_error = exc
            time.sleep(0.2)
    if last_error is not None:
        raise last_error
    raise RuntimeError(f"Health stub failed to start: {name}")


def invoke_centralservice_api(base_url: str, method: str, path: str, body: Any | None) -> Any:
    """Call a CentralService JSON API endpoint."""
    url = f"{base_url.rstrip('/')}{path}"
    headers = {"Accept": "application/json"}
    payload = None
    if body is not None:
        headers["Content-Type"] = "application/json"
        payload = json.dumps(body, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    request_obj = request.Request(url, data=payload, method=method.upper(), headers=headers)
    try:
        with request.urlopen(request_obj, timeout=10) as response:
            raw = response.read().decode("utf-8")
    except HTTPError as exc:
        detail = exc.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"CentralService API call failed: {method} {url} :: {exc.reason} {detail}") from exc
    except URLError as exc:
        raise RuntimeError(f"CentralService API call failed: {method} {url} :: {exc.reason}") from exc
    return json.loads(raw) if raw else None


def new_language_service_name(language: str, scenario: str) -> str:
    """Create a unique service name for one language/scenario pair."""
    return f"SdkE2E-{language}-{scenario}-{uuid.uuid4().hex}".replace("_", "-")


def register_seed_service(context: ScenarioContext, endpoint_base_url: str, service_name: str, sdk_label: str) -> SeededService:
    """Seed a service instance directly through CentralService APIs."""
    port = get_unused_port()
    ensure_health_stub(context, port, f"{sdk_label}-seed")
    body = {
        "id": uuid.uuid4().hex,
        "name": service_name,
        "host": "127.0.0.1",
        "localIp": "127.0.0.1",
        "operatorIp": "127.0.0.1",
        "publicIp": "127.0.0.1",
        "port": port,
        "serviceType": "Web",
        "healthCheckUrl": "/health",
        "healthCheckPort": 0,
        "heartbeatIntervalSeconds": 0,
        "weight": 1,
        "metadata": {"sdk": sdk_label, "scenario": context.scenario},
    }
    response = invoke_centralservice_api(endpoint_base_url, "POST", "/api/service/register", body)
    if not response or not response.get("success") or not response.get("data", {}).get("id"):
        raise RuntimeError(f"Failed to seed service '{service_name}' on {endpoint_base_url}")
    service_id = str(response["data"]["id"])
    # WebSocket 心跳模式下：当 heartbeatIntervalSeconds 为 0 时注册即在线，无需额外心跳请求。
    seed = SeededService(endpoint_base_url=endpoint_base_url, service_id=service_id, service_name=service_name)
    context.seeded_services.append(seed)
    return seed


def build_java_classes(sdk_root: Path) -> JavaBuild:
    """Compile Java service/client/examples once per script execution."""
    if "java" in BUILD_CACHE:
        return BUILD_CACHE["java"]

    java_root = sdk_root / "java"
    service_out = java_root / "build" / "service"
    client_out = java_root / "build" / "client"
    e2e_out = java_root / "build" / "e2e"
    for path in (service_out, client_out, e2e_out):
        shutil.rmtree(path, ignore_errors=True)
        ensure_dir(path)

    javac = resolve_java_command("javac.exe")
    service_files = [str(path) for path in (java_root / "service" / "src" / "main" / "java").rglob("*.java")]
    client_files = [str(path) for path in (java_root / "client" / "src" / "main" / "java").rglob("*.java")]
    example_files = [str(path) for path in (java_root / "examples").glob("*.java")]
    run_command([javac, "-encoding", "UTF-8", "-source", "8", "-target", "8", "-d", str(service_out), *service_files], cwd=java_root, env=None, action="javac service")
    run_command([javac, "-encoding", "UTF-8", "-source", "8", "-target", "8", "-d", str(client_out), *client_files], cwd=java_root, env=None, action="javac client")
    run_command(
        [javac, "-encoding", "UTF-8", "-source", "8", "-target", "8", "-cp", f"{service_out};{client_out}", "-d", str(e2e_out), *example_files],
        cwd=java_root,
        env=None,
        action="javac e2e",
    )

    result = JavaBuild(root=java_root, class_path=f"{service_out};{client_out};{e2e_out}")
    BUILD_CACHE["java"] = result
    return result


def test_dotnet_runtime(major_minor: str) -> bool:
    """Check whether a specific .NET runtime is installed."""
    require_command("dotnet", "Install .NET SDK and ensure 'dotnet' is in PATH.")
    completed = subprocess.run(["dotnet", "--list-runtimes"], capture_output=True, text=True, encoding="utf-8", errors="replace", check=False)
    if completed.returncode != 0:
        return False
    return f"Microsoft.NETCore.App {major_minor}." in completed.stdout


def build_dotnet_variant(repo_root: Path, variant: DotNetVariant) -> str:
    """Build one .NET variant and return the runnable artifact."""
    if variant.key in BUILD_CACHE:
        return str(BUILD_CACHE[variant.key])

    if variant.kind == "net40":
        require_command("powershell", "Windows PowerShell is required for the legacy net40 build script.")
        build_script = repo_root / "TaiChi" / "SDK" / "CentralService" / "dotnet" / "net40" / "build.ps1"
        run_command(["powershell", "-ExecutionPolicy", "Bypass", "-File", str(build_script), "-Configuration", "Release"], cwd=repo_root, env=None, action="net40 build")
        artifact = repo_root / "TaiChi" / "SDK" / "CentralService" / "dotnet" / "net40" / "examples" / "CentralService.Net40E2e" / "bin" / "Release" / "CentralService.Net40E2e.exe"
        BUILD_CACHE[variant.key] = artifact
        return str(artifact)

    if variant.project_path is None or variant.target_framework is None or variant.assembly_name is None:
        raise RuntimeError(f"Incomplete .NET variant definition: {variant}")
    env = set_dotnet_env(repo_root)
    run_command(
        [
            "dotnet",
            "build",
            str(variant.project_path),
            "-c",
            "Release",
            "-p:RestoreSources=https://api.nuget.org/v3/index.json",
            "-p:RestoreIgnoreFailedSources=true",
        ],
        cwd=repo_root,
        env=env,
        action=f"dotnet build {variant.name}",
    )
    artifact = variant.project_path.parent / "bin" / "Release" / variant.target_framework / f"{variant.assembly_name}.dll"
    BUILD_CACHE[variant.key] = artifact
    return str(artifact)
