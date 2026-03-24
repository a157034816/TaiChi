// @vitest-environment jsdom

import { act } from "react";
import { createRoot } from "react-dom/client";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { NodeGraphDebugger } from "@/components/editor/node-graph-debugger";
import type { EditorSessionPayload } from "@/lib/nodegraph/types";

/**
 * React Flow 依赖部分浏览器专属 API，测试只需要最小空实现即可。
 */
class ResizeObserverStub {
  observe() {}

  unobserve() {}

  disconnect() {}
}

const debuggerPayload: EditorSessionPayload = {
  session: {
    sessionId: "ngs_debug",
    runtimeId: "rt_demo_001",
    domain: "hello-world",
    clientName: "NodeGraph Demo Client",
    graph: {
      name: "Hello World Debug",
      description: "Debugger smoke-test graph",
      nodes: [
        {
          id: "node_source",
          type: "default",
          position: { x: 0, y: 0 },
          data: {
            label: "Greeting Source",
            nodeType: "greeting_source",
            outputs: [{ id: "text", label: "Text", dataType: "hello/text" }],
            values: {
              name: "Codex",
            },
          },
        },
        {
          id: "node_output",
          type: "default",
          position: { x: 280, y: 0 },
          data: {
            label: "Console Output",
            nodeType: "console_output",
            inputs: [{ id: "text", label: "Text", dataType: "hello/text" }],
            values: {},
          },
        },
      ],
      edges: [
        {
          id: "edge_source_output",
          source: "node_source",
          sourceHandle: "text",
          target: "node_output",
          targetHandle: "text",
        },
      ],
      viewport: {
        x: 0,
        y: 0,
        zoom: 1,
      },
    },
    metadata: {},
    accessType: "private",
    editorUrl: "http://localhost:3001/editor/ngs_debug",
    status: "draft",
    completionWebhook: "http://localhost:3100/api/completed",
    createdAt: "2026-03-19T00:00:00.000Z",
    updatedAt: "2026-03-19T00:00:00.000Z",
  },
  runtime: {
    runtimeId: "rt_demo_001",
    domain: "hello-world",
    clientName: "NodeGraph Demo Client",
    libraryVersion: "hello-world@1",
    capabilities: {
      canDebug: true,
      canExecute: true,
      canProfile: true,
    },
    expiresAt: "2026-03-19T00:30:00.000Z",
  },
  nodeLibrary: [
    {
      type: "greeting_source",
      displayName: "Greeting Source",
      description: "Create the base greeting text.",
      category: "Hello World",
      outputs: [
        {
          id: "text",
          label: "Text",
        },
      ],
    },
    {
      type: "console_output",
      displayName: "Console Output",
      description: "Write the greeting to the host result buffer.",
      category: "Hello World",
      inputs: [
        {
          id: "text",
          label: "Text",
        },
      ],
    },
  ],
  typeMappings: [],
};

describe("NodeGraphDebugger", () => {
  beforeEach(() => {
    vi.stubGlobal("ResizeObserver", ResizeObserverStub);
    vi.stubGlobal("requestAnimationFrame", (callback: FrameRequestCallback) => {
      callback(0);
      return 1;
    });
    vi.stubGlobal("cancelAnimationFrame", vi.fn());
    vi.stubGlobal(
      "matchMedia",
      vi.fn().mockReturnValue({
        matches: false,
        media: "",
        onchange: null,
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
        addListener: vi.fn(),
        removeListener: vi.fn(),
        dispatchEvent: vi.fn(),
      }),
    );

    Object.defineProperty(HTMLElement.prototype, "scrollTo", {
      configurable: true,
      value: vi.fn(),
    });
  });

  afterEach(() => {
    document.body.innerHTML = "";
    vi.restoreAllMocks();
  });

  it("loads the active debug session, toggles breakpoints, and continues execution", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async (input, init) => {
        const url = String(input);

        if (url.endsWith("/api/editor/sessions/ngs_debug/debug") && (!init || init.method === "GET")) {
          return {
            ok: true,
            json: async () => ({
              debugSessionId: "dbg_001",
              graph: debuggerPayload.session.graph,
              breakpoints: [],
              snapshot: {
                status: "idle",
                pauseReason: null,
                pendingNodeId: null,
                lastError: null,
                lastEvent: null,
                profiler: {},
                results: {},
                events: [],
              },
            }),
          };
        }

        if (url.endsWith("/api/editor/sessions/ngs_debug/debug/breakpoints")) {
          return {
            ok: true,
            json: async () => ({
              debugSessionId: "dbg_001",
              graph: debuggerPayload.session.graph,
              breakpoints: ["node_output"],
              snapshot: {
                status: "idle",
                pauseReason: null,
                pendingNodeId: null,
                lastError: null,
                lastEvent: null,
                profiler: {},
                results: {},
                events: [],
              },
            }),
          };
        }

        if (url.endsWith("/api/editor/sessions/ngs_debug/debug/continue")) {
          return {
            ok: true,
            json: async () => ({
              debugSessionId: "dbg_001",
              graph: debuggerPayload.session.graph,
              breakpoints: ["node_output"],
              snapshot: {
                status: "paused",
                pauseReason: "breakpoint",
                pendingNodeId: "node_output",
                lastError: null,
                lastEvent: {
                  nodeId: "node_source",
                  step: 1,
                },
                profiler: {},
                results: {},
                events: [],
              },
            }),
          };
        }

        throw new Error(`Unexpected fetch: ${url}`);
      }),
    );

    const container = document.createElement("div");
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(<NodeGraphDebugger payload={debuggerPayload} />);
    });

    await act(async () => {
      await Promise.resolve();
    });

    const breakpointButton = container.querySelector('[data-testid="debug-node-breakpoint-toggle-node_output"]');
    expect(breakpointButton).toBeTruthy();

    await act(async () => {
      (breakpointButton as HTMLButtonElement).click();
      await Promise.resolve();
    });

    expect(fetch).toHaveBeenCalledWith(
      "/api/editor/sessions/ngs_debug/debug/breakpoints",
      expect.objectContaining({
        method: "PUT",
      }),
    );

    const continueButton = container.querySelector('[data-testid="debug-continue-button"]');
    expect(continueButton).toBeTruthy();

    await act(async () => {
      (continueButton as HTMLButtonElement).click();
      await Promise.resolve();
    });

    expect(fetch).toHaveBeenCalledWith(
      "/api/editor/sessions/ngs_debug/debug/continue",
      expect.objectContaining({
        method: "POST",
      }),
    );
    expect(container.textContent).toContain("node_output");

    await act(async () => {
      root.unmount();
    });
  });
});
