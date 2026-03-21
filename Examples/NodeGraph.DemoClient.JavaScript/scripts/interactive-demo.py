#!/usr/bin/env python3
"""Interactive demo launcher for NodeGraph and the demo client."""

from __future__ import annotations

import argparse
import errno
import json
import os
import signal
import socket
import subprocess
import sys
import threading
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Any
from urllib import error, request


SCRIPT_DIR = Path(__file__).resolve().parent
DEMO_CLIENT_DIR = SCRIPT_DIR.parent
REPO_ROOT = DEMO_CLIENT_DIR.parent.parent
NODEGRAPH_DIR = REPO_ROOT / "Service" / "NodeGraph"
NPM_COMMAND = "npm.cmd" if os.name == "nt" else "npm"


@dataclass
class ManagedChild:
    label: str
    process: subprocess.Popen[str]


def read_positive_integer(value: Any, fallback: int) -> int:
    try:
        parsed = int(value)
    except (TypeError, ValueError):
        return fallback

    return parsed if parsed > 0 else fallback


def create_launch_config() -> dict[str, Any]:
    parser = argparse.ArgumentParser(description="Launch the interactive NodeGraph demo flow.")
    parser.add_argument("--graph-mode", choices=("new", "existing"))
    parser.add_argument("--graph-name")
    parser.add_argument("--nodegraph-port", type=int)
    parser.add_argument("--demo-port", type=int)
    args = parser.parse_args()

    return {
        "graph_mode": args.graph_mode or ("new" if os.environ.get("INTERACTIVE_GRAPH_MODE") == "new" else "existing"),
        "graph_name": args.graph_name or os.environ.get("INTERACTIVE_GRAPH_NAME") or "Interactive Approval Flow",
        "preferred_nodegraph_port": read_positive_integer(
            args.nodegraph_port or os.environ.get("INTERACTIVE_NODEGRAPH_PORT"),
            3300,
        ),
        "preferred_demo_port": read_positive_integer(
            args.demo_port or os.environ.get("INTERACTIVE_DEMO_PORT"),
            3101,
        ),
        "demo_client_host": os.environ.get("DEMO_CLIENT_HOST", "127.0.0.1"),
    }


def find_available_port(start_port: int, host: str = "127.0.0.1") -> int:
    candidate = start_port

    while True:
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
            sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            try:
                sock.bind((host, candidate))
            except OSError as exc:
                if exc.errno in {errno.EADDRINUSE, errno.EACCES, 10048, 10013}:
                    candidate += 1
                    continue
                raise

        return candidate


def prefix_stream(stream: Any, label: str, writer: Any) -> threading.Thread:
    def pump() -> None:
        for raw_line in iter(stream.readline, ""):
            line = raw_line.rstrip()
            if not line.strip():
                continue
            writer(f"[{label}] {line}\n")

        stream.close()

    thread = threading.Thread(target=pump, daemon=True)
    thread.start()
    return thread


def spawn_npm_process(
    label: str,
    cwd: Path,
    args: list[str],
    extra_env: dict[str, str],
) -> ManagedChild:
    if os.name == "nt":
        command = ["cmd.exe", "/d", "/s", "/c", NPM_COMMAND, *args]
        creationflags = subprocess.CREATE_NEW_PROCESS_GROUP
        popen_kwargs: dict[str, Any] = {"creationflags": creationflags}
    else:
        command = [NPM_COMMAND, *args]
        popen_kwargs = {"start_new_session": True}

    process = subprocess.Popen(
        command,
        cwd=str(cwd),
        env={**os.environ, **extra_env},
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        stdin=subprocess.DEVNULL,
        text=True,
        encoding="utf-8",
        errors="replace",
        bufsize=1,
        **popen_kwargs,
    )

    if process.stdout is not None:
        prefix_stream(process.stdout, label, sys.stdout.write)
    if process.stderr is not None:
        prefix_stream(process.stderr, label, sys.stderr.write)

    return ManagedChild(label=label, process=process)


def decode_json(payload: bytes) -> dict[str, Any]:
    if not payload:
        return {}
    return json.loads(payload.decode("utf-8"))


def try_get_health(url: str) -> dict[str, Any] | None:
    try:
        with request.urlopen(url, timeout=3) as response:
            if response.status != 200:
                return None
            return decode_json(response.read())
    except (error.URLError, error.HTTPError, TimeoutError, json.JSONDecodeError):
        return None


def wait_for_healthy(url: str, label: str, children: list[ManagedChild], timeout_seconds: int = 120) -> dict[str, Any]:
    deadline = time.time() + timeout_seconds
    last_error = "service did not become healthy in time"

    while time.time() < deadline:
        ensure_children_alive(children)
        try:
            with request.urlopen(url, timeout=5) as response:
                if response.status == 200:
                    return decode_json(response.read())
                last_error = f"{label} returned {response.status}"
        except error.HTTPError as exc:
            last_error = f"{label} returned {exc.code}"
        except error.URLError as exc:
            last_error = str(exc.reason)
        except (TimeoutError, json.JSONDecodeError) as exc:
            last_error = str(exc)

        time.sleep(1)

    raise RuntimeError(f"{label} health check failed: {last_error}")


def post_json(url: str, payload: dict[str, Any]) -> dict[str, Any]:
    data = json.dumps(payload).encode("utf-8")
    req = request.Request(
        url,
        data=data,
        headers={"content-type": "application/json"},
        method="POST",
    )

    try:
        with request.urlopen(req, timeout=10) as response:
            return decode_json(response.read())
    except error.HTTPError as exc:
        try:
            body = decode_json(exc.read())
        except json.JSONDecodeError:
            body = {"error": exc.reason}
        raise RuntimeError(f"{url} returned {exc.code}: {json.dumps(body, ensure_ascii=False)}") from exc


def fetch_latest_result(url: str) -> dict[str, Any] | None:
    try:
        with request.urlopen(url, timeout=5) as response:
            if response.status != 200:
                return None
            return decode_json(response.read())
    except (error.URLError, error.HTTPError, TimeoutError, json.JSONDecodeError):
        return None


def ensure_children_alive(children: list[ManagedChild]) -> None:
    for child in children:
        code = child.process.poll()
        if code is not None:
            raise RuntimeError(f"{child.label} exited unexpectedly with code {code}.")


def terminate_child(child: ManagedChild) -> None:
    process = child.process
    if process.poll() is not None:
        return

    if os.name == "nt":
        subprocess.run(
            ["taskkill", "/pid", str(process.pid), "/t", "/f"],
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            check=False,
        )
        return

    os.killpg(process.pid, signal.SIGTERM)
    try:
        process.wait(timeout=5)
    except subprocess.TimeoutExpired:
        os.killpg(process.pid, signal.SIGKILL)
        process.wait(timeout=5)


def main() -> int:
    preferred_config = create_launch_config()

    existing_nodegraph = try_get_health(
        f"http://localhost:{preferred_config['preferred_nodegraph_port']}/api/health"
    )
    reuse_nodegraph = existing_nodegraph is not None and existing_nodegraph.get("service") == "NodeGraph"
    nodegraph_port = (
        preferred_config["preferred_nodegraph_port"]
        if reuse_nodegraph
        else find_available_port(preferred_config["preferred_nodegraph_port"])
    )

    config = {
        **preferred_config,
        "nodegraph_port": nodegraph_port,
        "nodegraph_base_url": f"http://localhost:{nodegraph_port}",
    }

    existing_demo_client = try_get_health(
        f"http://localhost:{preferred_config['preferred_demo_port']}/api/health"
    )
    reuse_demo_client = (
        existing_demo_client is not None
        and existing_demo_client.get("service") == "NodeGraph Demo Client"
        and existing_demo_client.get("nodeGraphBaseUrl") == config["nodegraph_base_url"]
    )
    demo_port = (
        preferred_config["preferred_demo_port"]
        if reuse_demo_client
        else find_available_port(preferred_config["preferred_demo_port"], preferred_config["demo_client_host"])
    )
    config["demo_port"] = demo_port
    config["demo_client_base_url"] = f"http://localhost:{demo_port}"

    children: list[ManagedChild] = []

    def handle_interrupt(_signum: int, _frame: Any) -> None:
        raise KeyboardInterrupt

    signal.signal(signal.SIGINT, handle_interrupt)
    if hasattr(signal, "SIGTERM"):
        signal.signal(signal.SIGTERM, handle_interrupt)

    try:
        print("[Interactive Demo] starting Service/NodeGraph and Demo Client...")
        print(f"[Interactive Demo] NodeGraph will listen on {config['nodegraph_base_url']}")
        print(f"[Interactive Demo] Demo Client will listen on {config['demo_client_base_url']}")
        if reuse_nodegraph:
            print(f"[Interactive Demo] reusing existing NodeGraph on port {config['nodegraph_port']}.")
        if reuse_demo_client:
            print(f"[Interactive Demo] reusing existing Demo Client on port {config['demo_port']}.")
        if (
            not reuse_nodegraph
            and not reuse_demo_client
            and (
                config["nodegraph_port"] != config["preferred_nodegraph_port"]
                or config["demo_port"] != config["preferred_demo_port"]
            )
        ):
            print(
                "[Interactive Demo] detected occupied default ports, switched to "
                f"NodeGraph {config['nodegraph_port']} / Demo Client {config['demo_port']}."
            )

        if not reuse_nodegraph:
            children.append(
                spawn_npm_process(
                    "NodeGraph",
                    NODEGRAPH_DIR,
                    ["run", "dev"],
                    {
                        "PORT": str(config["nodegraph_port"]),
                        "NODEGRAPH_PUBLIC_BASE_URL": config["nodegraph_base_url"],
                        "NODEGRAPH_PRIVATE_BASE_URL": f"http://127.0.0.1:{config['nodegraph_port']}",
                    },
                )
            )

        if not reuse_demo_client:
            children.append(
                spawn_npm_process(
                    "DemoClient",
                    DEMO_CLIENT_DIR,
                    ["start"],
                    {
                        "DEMO_CLIENT_PORT": str(config["demo_port"]),
                        "DEMO_CLIENT_HOST": config["demo_client_host"],
                        "DEMO_CLIENT_BASE_URL": config["demo_client_base_url"],
                        "NODEGRAPH_BASE_URL": config["nodegraph_base_url"],
                    },
                )
            )

        wait_for_healthy(f"{config['nodegraph_base_url']}/api/health", "NodeGraph", children)
        wait_for_healthy(f"{config['demo_client_base_url']}/api/health", "Demo Client", children)

        session = post_json(
            f"{config['demo_client_base_url']}/api/create-session",
            {
                "graphMode": config["graph_mode"],
                "graphName": config["graph_name"],
            },
        )

        print("")
        print("[Interactive Demo] environment is ready.")
        print(f"[Interactive Demo] Demo page: {config['demo_client_base_url']}/")
        print(f"[Interactive Demo] Session ID: {session['sessionId']}")
        print(f"[Interactive Demo] Editor URL: {session['editorUrl']}")
        print("[Interactive Demo] Next steps:")
        print("  1. Manually open the editor URL above.")
        print('  2. Add or edit nodes, then click "Complete editing".')
        print("  3. Return to the demo page to inspect the latest callback payload.")
        print("  4. Press Ctrl+C here when you are finished.")
        print("")

        completion_notified = False
        while True:
            ensure_children_alive(children)
            if not completion_notified:
                payload = fetch_latest_result(f"{config['demo_client_base_url']}/api/results/latest")
                latest_entry = (payload or {}).get("latestCompletion") or {}
                latest_completion = latest_entry.get("payload", {})
                if latest_completion.get("sessionId") == session["sessionId"]:
                    completion_notified = True
                    completed_at = latest_entry.get("receivedAt", "")
                    print("[Interactive Demo] completion webhook received.")
                    print(f"[Interactive Demo] Completed at: {completed_at}")
                    print(f"[Interactive Demo] View result: {config['demo_client_base_url']}/")
                    print("")

            time.sleep(3)
    except KeyboardInterrupt:
        print("\n[Interactive Demo] stopping services...")
        return_code = 0
    except Exception as exc:  # noqa: BLE001
        print(f"[Interactive Demo] failed to start: {exc}", file=sys.stderr)
        return_code = 1
    finally:
        for child in children:
            terminate_child(child)

    return return_code


if __name__ == "__main__":
    raise SystemExit(main())
