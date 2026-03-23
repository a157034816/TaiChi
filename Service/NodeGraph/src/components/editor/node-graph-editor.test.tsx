// @vitest-environment jsdom

import { act } from "react";
import { createRoot } from "react-dom/client";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { NodeGraphEditor } from "@/components/editor/node-graph-editor";
import type { EditorSessionPayload } from "@/lib/nodegraph/types";

/**
 * React Flow 依赖部分浏览器专属 API，测试只需要最小空实现即可。
 */
class ResizeObserverStub {
  observe() {}

  unobserve() {}

  disconnect() {}
}

const editorPayload: EditorSessionPayload = {
  session: {
    sessionId: "ngs_test",
    runtimeId: "rt_demo_001",
    domain: "hello-world",
    clientName: "NodeGraph Demo Client",
    graph: {
      name: "Hello World Flow",
      description: "Smoke-test graph",
      nodes: [],
      edges: [],
      viewport: {
        x: 0,
        y: 0,
        zoom: 1,
      },
    },
    metadata: {},
    accessType: "private",
    editorUrl: "http://localhost:3001/editor/ngs_test",
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
  ],
  typeMappings: [],
};

describe("NodeGraphEditor", () => {
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
    window.localStorage.clear();
    vi.restoreAllMocks();
  });

  it("mounts without triggering a maximum update depth error", async () => {
    const container = document.createElement("div");
    document.body.appendChild(container);

    const root = createRoot(container);
    const consoleErrors: string[] = [];

    vi.spyOn(console, "error").mockImplementation((...args) => {
      consoleErrors.push(args.map((value) => String(value)).join(" "));
    });

    await act(async () => {
      root.render(<NodeGraphEditor payload={editorPayload} />);
    });

    await act(async () => {
      await Promise.resolve();
    });

    expect(consoleErrors.join("\n")).not.toContain("Maximum update depth exceeded");

    await act(async () => {
      root.unmount();
    });
  });

  it("restores a persisted builtin locale on mount", async () => {
    window.localStorage.setItem(
      "nodegraph.editor.preferences.v1",
      JSON.stringify({
        locale: "en",
        edgeStyle: "smoothstep",
      }),
    );

    const container = document.createElement("div");
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(<NodeGraphEditor payload={editorPayload} />);
    });

    await act(async () => {
      await Promise.resolve();
    });

    expect(document.documentElement.lang).toBe("en");

    await act(async () => {
      root.unmount();
    });
  });

  it("refreshes the runtime library and surfaces invalid graph markers", async () => {
    window.localStorage.setItem(
      "nodegraph.editor.preferences.v1",
      JSON.stringify({
        locale: "en",
        edgeStyle: "smoothstep",
      }),
    );

    const refreshResponse = {
      runtime: {
        ...editorPayload.runtime,
        libraryVersion: "hello-world@2",
      },
      nodeLibrary: [
        {
          type: "greeting_source",
          displayName: "Greeting Source v2",
          description: "Create the latest greeting text.",
          category: "Hello Runtime",
          outputs: [{ id: "message", label: "Message" }],
        },
      ],
      migratedGraph: {
        ...editorPayload.session.graph,
        nodes: [
          {
            id: "node_output",
            type: "default",
            position: { x: 0, y: 0 },
            data: {
              label: "Console Output",
              category: "Hello Runtime",
              nodeType: "console_output",
              templateMarkers: [
                {
                  code: "missingNodeType",
                  reason: "节点类型 \"console_output\" 在最新节点库中不存在。",
                },
              ],
            },
          },
        ],
        edges: [
          {
            id: "edge_invalid",
            source: "node_output",
            target: "node_output",
            invalidReason: "源端口 \"text\" 已失效。",
          },
        ],
      },
    };

    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        json: async () => refreshResponse,
      }),
    );

    const container = document.createElement("div");
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(<NodeGraphEditor payload={editorPayload} />);
    });

    await act(async () => {
      await Promise.resolve();
    });

    const refreshButton = container.querySelector('[data-testid="refresh-library-button"]');
    expect(refreshButton).toBeTruthy();

    await act(async () => {
      (refreshButton as HTMLButtonElement).click();
      await Promise.resolve();
    });

    expect(container.querySelector('[data-testid="runtime-library-version"]')?.textContent).toContain("hello-world@2");
    expect(container.querySelector('[data-testid="invalid-graph-alert"]')?.textContent).toContain("2");

    await act(async () => {
      root.unmount();
    });
  });
});
