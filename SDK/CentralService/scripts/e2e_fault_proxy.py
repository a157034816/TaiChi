from __future__ import annotations

import argparse
import http.client
import json
import socketserver
import threading
import time
from http import HTTPStatus
from pathlib import Path
from typing import Any
from urllib.parse import urlsplit

from centralservice_script_common import ensure_dir


DEFAULT_STATE = {
    "mode": "close",
    "delayMs": 500,
    "targetUrl": "",
    "statusCode": 404,
    "body": "service not found",
    "requestCount": 0,
}


STATE_FILE = Path()
STATE_LOCK = threading.Lock()


def read_state() -> dict[str, Any]:
    """Read the proxy state file or return the default state."""
    if not STATE_FILE.exists():
        return dict(DEFAULT_STATE)
    raw = STATE_FILE.read_text(encoding="utf-8").strip()
    if not raw:
        return dict(DEFAULT_STATE)
    data = json.loads(raw)
    merged = dict(DEFAULT_STATE)
    merged.update(data)
    return merged


def write_state(state: dict[str, Any]) -> None:
    """Persist the proxy state file."""
    ensure_dir(STATE_FILE.parent)
    STATE_FILE.write_text(json.dumps(state, ensure_ascii=False, indent=2), encoding="utf-8")


def bump_request_count() -> dict[str, Any]:
    """Increment requestCount and return the latest state."""
    with STATE_LOCK:
        state = read_state()
        state["requestCount"] = int(state.get("requestCount") or 0) + 1
        write_state(state)
        return state


def proxy_request(method: str, path_and_query: str, body: bytes, target_url: str) -> tuple[int, str, bytes]:
    """Forward a request to the configured upstream."""
    parsed = urlsplit(target_url.rstrip("/") + path_and_query)
    connection_cls = http.client.HTTPSConnection if parsed.scheme == "https" else http.client.HTTPConnection
    connection = connection_cls(parsed.hostname, parsed.port, timeout=15)
    try:
        target_path = parsed.path or "/"
        if parsed.query:
            target_path = f"{target_path}?{parsed.query}"
        headers = {}
        payload = None
        if body:
            headers["Content-Type"] = "application/json"
            payload = body
        connection.request(method, target_path, body=payload, headers=headers)
        response = connection.getresponse()
        content = response.read()
        content_type = response.getheader("Content-Type") or "application/json; charset=utf-8"
        return int(response.status), content_type, content
    finally:
        connection.close()


class FaultProxyHandler(socketserver.StreamRequestHandler):
    """Serve requests according to the current fault mode."""

    def handle(self) -> None:
        request_line = self.rfile.readline().decode("utf-8", errors="replace").strip()
        if not request_line:
            return

        parts = request_line.split(" ")
        method = parts[0].upper() if parts else "GET"
        path_and_query = parts[1] if len(parts) > 1 else "/"
        content_length = 0

        while True:
            line = self.rfile.readline()
            if not line or line in {b"\r\n", b"\n"}:
                break
            try:
                text = line.decode("utf-8", errors="replace")
            except Exception:
                text = ""
            if text.lower().startswith("content-length:"):
                try:
                    content_length = max(0, int(text.split(":", 1)[1].strip()))
                except ValueError:
                    content_length = 0

        body = self.rfile.read(content_length) if content_length > 0 else b""
        state = bump_request_count()
        mode = str(state.get("mode") or "close").strip().lower()

        if mode == "close":
            delay_ms = max(0, int(state.get("delayMs") or 0))
            if delay_ms > 0:
                time.sleep(delay_ms / 1000.0)
            return

        if mode == "httperror":
            status_code = int(state.get("statusCode") or 404)
            if status_code < 100:
                status_code = 404
            payload = str(state.get("body") or "service not found").encode("utf-8")
            self._write_response(status_code, "text/plain; charset=utf-8", payload)
            return

        if mode == "proxy":
            target_url = str(state.get("targetUrl") or "").strip()
            if not target_url:
                return
            try:
                status_code, content_type, payload = proxy_request(method, path_and_query, body, target_url)
                self._write_response(status_code, content_type, payload)
            except Exception:
                return

    def _write_response(self, status_code: int, content_type: str, payload: bytes) -> None:
        """Write a minimal HTTP response."""
        try:
            status_name = HTTPStatus(status_code).phrase
        except ValueError:
            status_name = "OK"
        header = (
            f"HTTP/1.1 {status_code} {status_name}\r\n"
            f"Content-Type: {content_type}\r\n"
            f"Content-Length: {len(payload)}\r\n"
            "Connection: close\r\n\r\n"
        )
        self.wfile.write(header.encode("ascii"))
        if payload:
            self.wfile.write(payload)
        self.wfile.flush()


class ThreadedTcpServer(socketserver.ThreadingMixIn, socketserver.TCPServer):
    """A threaded TCP server for the fault proxy."""

    daemon_threads = True
    allow_reuse_address = True


def parse_args() -> argparse.Namespace:
    """Parse CLI arguments."""
    parser = argparse.ArgumentParser()
    parser.add_argument("-Port", "--port", type=int, required=True)
    parser.add_argument("-StateFile", "--state-file", required=True)
    return parser.parse_args()


def main() -> None:
    """Run the fault proxy until interrupted."""
    global STATE_FILE

    args = parse_args()
    STATE_FILE = Path(args.state_file).resolve()
    if not STATE_FILE.exists():
        write_state(dict(DEFAULT_STATE))

    with ThreadedTcpServer(("127.0.0.1", int(args.port)), FaultProxyHandler) as server:
        server.serve_forever()


if __name__ == "__main__":
    main()
