import { beforeEach, describe, expect, it, vi } from "vitest";

import type { CreateSessionRequest, RuntimeRegistrationRequest } from "@/lib/nodegraph/types";
import { createDebugSession, getActiveDebugSession, updateDebugSessionBreakpoints, continueDebugSession, closeDebugSession } from "@/lib/server/debug-session-service";
import { createSession } from "@/lib/server/session-service";
import { registerRuntime } from "@/lib/server/runtime-service";
import { getRuntimeStore } from "@/lib/server/store";

const runtimeInput = {
  runtimeId: "rt_demo_001",
  domain: "hello-world",
  clientName: "Hello World Host",
  controlBaseUrl: "https://client.example.com/nodegraph/runtime",
  libraryVersion: "hello-world@1",
  capabilities: {
    canDebug: true,
    canExecute: true,
    canProfile: true,
  },
  library: {
    nodes: [
      {
        type: "greeting_source",
        displayName: "Greeting Source",
        description: "Create the base greeting text.",
        category: "Hello World",
        outputs: [{ id: "text", label: "Text", dataType: "hello/text" }],
      },
      {
        type: "console_output",
        displayName: "Console Output",
        description: "Write the message to the host console.",
        category: "Hello World",
        inputs: [{ id: "text", label: "Text", dataType: "hello/text" }],
      },
    ],
  },
} satisfies RuntimeRegistrationRequest;

const sessionInput = {
  runtimeId: runtimeInput.runtimeId,
  completionWebhook: "https://client.example.com/completed",
  graph: {
    name: "Hello World",
    nodes: [],
    edges: [],
    viewport: { x: 0, y: 0, zoom: 1 },
  },
  metadata: {
    ticketId: "HELLO-1",
  },
} satisfies CreateSessionRequest;

function createSnapshot(status: "idle" | "paused" | "completed") {
  return {
    status,
    pauseReason: status === "paused" ? "breakpoint" : null,
    pendingNodeId: status === "paused" ? "node_output" : null,
    lastError: null,
    lastEvent: null,
    profiler: {},
    results:
      status === "completed"
        ? {
            console: ["Hello, Codex!"],
          }
        : {},
    events: [],
  };
}

describe("debug session service", () => {
  beforeEach(() => {
    const store = getRuntimeStore();
    store.runtimes.clear();
    store.sessions.clear();
    store.debugSessions.clear();
    vi.restoreAllMocks();
  });

  it("creates a host debug session, updates breakpoints, continues it, and closes it", async () => {
    registerRuntime(runtimeInput);

    const created = await createSession(
      new Request("http://localhost/api/sdk/sessions", {
        headers: {
          "x-forwarded-for": "10.0.0.25",
        },
      }),
      sessionInput,
    );

    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(
        new Response(
          JSON.stringify({
            debugSessionId: "dbg_001",
            graph: sessionInput.graph,
            breakpoints: [],
            snapshot: createSnapshot("idle"),
          }),
        ),
      )
      .mockResolvedValueOnce(
        new Response(
          JSON.stringify({
            debugSessionId: "dbg_001",
            graph: sessionInput.graph,
            breakpoints: ["node_output"],
            snapshot: createSnapshot("idle"),
          }),
        ),
      )
      .mockResolvedValueOnce(
        new Response(
          JSON.stringify({
            debugSessionId: "dbg_001",
            graph: sessionInput.graph,
            breakpoints: ["node_output"],
            snapshot: createSnapshot("paused"),
          }),
        ),
      )
      .mockResolvedValueOnce(
        new Response(
          JSON.stringify({
            closed: true,
          }),
        ),
      );
    vi.stubGlobal("fetch", fetchMock);

    const debugSession = await createDebugSession(created.sessionId, {
      graph: sessionInput.graph,
      breakpoints: [],
    });
    expect(debugSession.debugSessionId).toBe("dbg_001");
    expect(getActiveDebugSession(created.sessionId)?.debugSessionId).toBe("dbg_001");
    expect(String(fetchMock.mock.calls[0][0])).toBe("https://client.example.com/nodegraph/runtime/debug/sessions");

    const updated = await updateDebugSessionBreakpoints(created.sessionId, ["node_output"]);
    expect(updated.breakpoints).toEqual(["node_output"]);
    expect(String(fetchMock.mock.calls[1][0])).toBe(
      "https://client.example.com/nodegraph/runtime/debug/sessions/dbg_001/breakpoints",
    );

    const paused = await continueDebugSession(created.sessionId);
    expect(paused.snapshot.status).toBe("paused");
    expect(paused.snapshot.pendingNodeId).toBe("node_output");
    expect(String(fetchMock.mock.calls[2][0])).toBe(
      "https://client.example.com/nodegraph/runtime/debug/sessions/dbg_001/continue",
    );

    const closed = await closeDebugSession(created.sessionId);
    expect(closed.closed).toBe(true);
    expect(getActiveDebugSession(created.sessionId)).toBeUndefined();
    expect(String(fetchMock.mock.calls[3][0])).toBe(
      "https://client.example.com/nodegraph/runtime/debug/sessions/dbg_001",
    );
  });
});
