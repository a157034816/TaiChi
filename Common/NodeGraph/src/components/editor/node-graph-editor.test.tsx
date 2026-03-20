// @vitest-environment jsdom

import { act } from "react";
import { createRoot } from "react-dom/client";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { NodeGraphEditor } from "@/components/editor/node-graph-editor";
import type { EditorSessionPayload } from "@/lib/nodegraph/types";

/**
 * React Flow relies on a few browser-only APIs that JSDOM does not expose.
 * The editor smoke test only needs minimal no-op implementations.
 */
class ResizeObserverStub {
  observe() {}

  unobserve() {}

  disconnect() {}
}

const editorPayload: EditorSessionPayload = {
  session: {
    sessionId: "ngs_test",
    domain: "demo-workflow",
    clientName: "NodeGraph Demo Client",
    graph: {
      name: "Demo Approval Flow",
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
    nodeLibraryEndpoint: "http://localhost:3100/api/node-library",
    completionWebhook: "http://localhost:3100/api/completed",
    createdAt: "2026-03-19T00:00:00.000Z",
    updatedAt: "2026-03-19T00:00:00.000Z",
  },
  nodeLibrary: [
    {
      type: "start",
      labelKey: "nodes.start.label",
      descriptionKey: "nodes.start.description",
      categoryKey: "categories.control",
      outputs: [
        {
          id: "next",
          labelKey: "ports.next",
        },
      ],
    },
  ],
  i18n: {
    defaultLocale: "en",
    locales: {
      en: {
        "categories.control": "Control",
        "nodes.start.description": "Entry point for a new workflow.",
        "nodes.start.label": "Start",
        "ports.next": "Next",
      },
      "zh-CN": {
        "categories.control": "控制",
        "nodes.start.description": "新工作流的入口节点。",
        "nodes.start.label": "开始",
        "ports.next": "下一步",
      },
      "fr-FR": {
        "categories.control": "Controle",
        "nodes.start.description": "Point d'entree d'un nouveau workflow.",
        "nodes.start.label": "Demarrer",
        "ports.next": "Suivant",
      },
    },
  },
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

  it("restores a persisted domain locale on mount", async () => {
    window.localStorage.setItem(
      "nodegraph.editor.preferences.v1",
      JSON.stringify({
        locale: "fr-FR",
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

    expect(document.documentElement.lang).toBe("fr-FR");

    await act(async () => {
      root.unmount();
    });
  });
});
