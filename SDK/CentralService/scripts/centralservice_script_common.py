from __future__ import annotations

import json
import os
import shutil
import socket
import subprocess
import sys
import time
from contextlib import closing
from pathlib import Path
from typing import Any, Iterable
from urllib import request
from urllib.error import URLError

try:
    import winreg
except ImportError:  # pragma: no cover - non-Windows fallback
    winreg = None


def write_info(message: str) -> None:
    """Write an informational message."""
    print(f"[INFO] {message}")


def write_warn(message: str) -> None:
    """Write a warning message."""
    print(f"[WARN] {message}")


def ensure_dir(path: Path) -> Path:
    """Create a directory if it does not already exist."""
    path.mkdir(parents=True, exist_ok=True)
    return path


def require_command(name: str, hint: str) -> str:
    """Ensure a command is available in PATH and return its executable path."""
    command = shutil.which(name)
    if command:
        return command
    raise RuntimeError(f"Missing command: {name}. {hint}")


def normalize_items(items: Iterable[str]) -> list[str]:
    """Split comma or semicolon separated items and remove duplicates."""
    values: list[str] = []
    seen: set[str] = set()
    for item in items:
        if not item:
            continue
        for part in str(item).split(","):
            for piece in part.split(";"):
                candidate = piece.strip()
                if not candidate:
                    continue
                key = candidate.lower()
                if key in seen:
                    continue
                seen.add(key)
                values.append(candidate)
    return values


def normalize_base_url(url: str | None, default: str = "http://127.0.0.1:5000") -> str:
    """Normalize a base URL and remove the trailing slash."""
    if not url or not url.strip():
        return default
    return url.strip().rstrip("/")


def run_command(
    args: list[str],
    *,
    cwd: Path | None = None,
    env: dict[str, str] | None = None,
    action: str,
    capture_output: bool = False,
) -> subprocess.CompletedProcess[str]:
    """Run a foreground command and raise when it fails."""
    completed = subprocess.run(
        args,
        cwd=str(cwd) if cwd else None,
        env=env,
        text=True,
        capture_output=capture_output,
        encoding="utf-8",
        errors="replace",
        check=False,
    )
    if completed.returncode != 0:
        raise RuntimeError(f"{action} failed with exit code {completed.returncode}")
    return completed


def start_background_process(
    args: list[str],
    *,
    cwd: Path | None,
    env: dict[str, str] | None,
    stdout_path: Path,
    stderr_path: Path,
) -> subprocess.Popen[str]:
    """Start a background process and redirect stdout/stderr to files."""
    ensure_dir(stdout_path.parent)
    stdout_file = stdout_path.open("w", encoding="utf-8", newline="")
    stderr_file = stderr_path.open("w", encoding="utf-8", newline="")
    try:
        process = subprocess.Popen(
            args,
            cwd=str(cwd) if cwd else None,
            env=env,
            stdout=stdout_file,
            stderr=stderr_file,
            text=True,
            encoding="utf-8",
            errors="replace",
        )
    except Exception:
        stdout_file.close()
        stderr_file.close()
        raise

    process._centralservice_stdout = stdout_file  # type: ignore[attr-defined]
    process._centralservice_stderr = stderr_file  # type: ignore[attr-defined]
    return process


def stop_process(process: subprocess.Popen[str] | None) -> None:
    """Terminate a process quietly and close redirected streams."""
    if process is None:
        return
    try:
        if process.poll() is None:
            process.kill()
            process.wait(timeout=5)
    except Exception:
        pass
    finally:
        for attr in ("_centralservice_stdout", "_centralservice_stderr"):
            handle = getattr(process, attr, None)
            if handle is not None:
                try:
                    handle.close()
                except Exception:
                    pass


def wait_for_port(port: int, timeout_seconds: float) -> bool:
    """Wait until a local TCP port becomes reachable."""
    deadline = time.time() + timeout_seconds
    while time.time() < deadline:
        with closing(socket.socket(socket.AF_INET, socket.SOCK_STREAM)) as client:
            client.settimeout(0.5)
            try:
                client.connect(("127.0.0.1", port))
                return True
            except OSError:
                time.sleep(0.25)
    return False


def wait_for_health(url: str, timeout_seconds: float) -> bool:
    """Wait until the /health endpoint returns a 2xx response."""
    deadline = time.time() + timeout_seconds
    health_url = f"{url.rstrip('/')}/health"
    while time.time() < deadline:
        try:
            with request.urlopen(health_url, timeout=2) as response:
                if 200 <= getattr(response, "status", 0) < 300:
                    return True
        except URLError:
            time.sleep(0.5)
        except OSError:
            time.sleep(0.5)
    return False


def get_unused_port() -> int:
    """Allocate an unused local TCP port."""
    with closing(socket.socket(socket.AF_INET, socket.SOCK_STREAM)) as listener:
        listener.bind(("127.0.0.1", 0))
        listener.listen(1)
        return int(listener.getsockname()[1])


def write_json_file(path: Path, value: Any) -> None:
    """Write JSON data using UTF-8."""
    ensure_dir(path.parent)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")


def read_json_file(path: Path) -> Any | None:
    """Read JSON data, returning None for missing or empty files."""
    if not path.exists():
        return None
    raw = path.read_text(encoding="utf-8").strip()
    if not raw:
        return None
    return json.loads(raw)


def convert_to_json_array(items: list[Any]) -> str:
    """Serialize a list to compact JSON."""
    return json.dumps(items, ensure_ascii=False, separators=(",", ":"))


def show_logs(paths: Iterable[Path]) -> None:
    """Print log files that exist."""
    for path in paths:
        if not path.exists():
            continue
        write_warn(f"---- {path} ----")
        try:
            print(path.read_text(encoding="utf-8", errors="replace"))
        except Exception as exc:  # pragma: no cover - best effort logging
            write_warn(f"failed to read log {path}: {exc}")


def build_child_env(overrides: dict[str, str] | None = None) -> dict[str, str]:
    """Create a child environment with optional overrides."""
    env = os.environ.copy()
    if overrides:
        for key, value in overrides.items():
            env[key] = value
    return env


def set_dotnet_env(project_root: Path, env: dict[str, str] | None = None) -> dict[str, str]:
    """Populate the .NET environment variables used by the scripts."""
    target = build_child_env(env)
    dotnet_home = ensure_dir(project_root / ".tool" / "dotnet-home")
    target["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1"
    target["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
    target["DOTNET_NOLOGO"] = "1"
    target["DOTNET_CLI_HOME"] = str(dotnet_home)
    target["NUGET_PACKAGES"] = str(dotnet_home / ".nuget" / "packages")
    target["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "0"
    target["MSBuildEnableWorkloadResolver"] = "false"
    target["MSBuildDisableWorkloadResolver"] = "1"
    return target


def set_go_env(project_root: Path, env: dict[str, str] | None = None) -> dict[str, str]:
    """Populate the Go environment variables used by the scripts."""
    target = build_child_env(env)
    go_root = ensure_dir(project_root / ".tool" / "go")
    ensure_dir(go_root / "pkg" / "mod")
    go_cache = ensure_dir(project_root / ".tool" / "go-cache")
    go_tmp = ensure_dir(project_root / ".tool" / "go-tmp")
    target["GOPATH"] = str(go_root)
    target["GOMODCACHE"] = str(go_root / "pkg" / "mod")
    target["GOCACHE"] = str(go_cache)
    target["GOTMPDIR"] = str(go_tmp)
    return target


def set_node_env(project_root: Path, env: dict[str, str] | None = None) -> dict[str, str]:
    """Populate the npm environment variables used by the scripts."""
    target = build_child_env(env)
    npm_cache = ensure_dir(project_root / ".tool" / "npm-cache")
    target["npm_config_cache"] = str(npm_cache)
    target["npm_config_loglevel"] = "error"
    return target


def resolve_java_home() -> str | None:
    """Resolve JAVA_HOME from environment or Windows registry."""
    candidates: list[str] = []
    java_home = os.environ.get("JAVA_HOME")
    if java_home:
        candidates.append(java_home)

    if winreg is not None:
        try:
            with winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\JavaSoft\JDK") as root:
                try:
                    current_version = winreg.QueryValueEx(root, "CurrentVersion")[0]
                    with winreg.OpenKey(root, current_version) as current:
                        candidates.append(winreg.QueryValueEx(current, "JavaHome")[0])
                except OSError:
                    pass

                index = 0
                while True:
                    try:
                        version = winreg.EnumKey(root, index)
                    except OSError:
                        break
                    try:
                        with winreg.OpenKey(root, version) as version_key:
                            candidates.append(winreg.QueryValueEx(version_key, "JavaHome")[0])
                    except OSError:
                        pass
                    index += 1
        except OSError:
            pass

    for candidate in candidates:
        java_path = Path(candidate)
        if (java_path / "bin" / "java.exe").exists() and (java_path / "bin" / "javac.exe").exists():
            return str(java_path)
    return None


def resolve_java_command(name: str) -> str:
    """Resolve a Java executable from JAVA_HOME."""
    java_home = resolve_java_home()
    if not java_home:
        raise RuntimeError(
            "Java JDK not found. Configure JAVA_HOME or install a JDK registered under HKLM:/SOFTWARE/JavaSoft/JDK."
        )
    command_path = Path(java_home) / "bin" / name
    if not command_path.exists():
        raise RuntimeError(f"Java command not found: {command_path}")
    return str(command_path)


def current_python_executable() -> str:
    """Return the current Python interpreter."""
    return sys.executable or require_command("python", "Install Python 3.10+ and ensure 'python' is in PATH.")
