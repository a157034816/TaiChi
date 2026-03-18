import { describe, expect, it } from "vitest";

import {
  createCanvasSelectionSnapshot,
  createEmptyCanvasSelectionSnapshot,
  getCanvasFocusLabel,
  getCanvasTypeLabel,
  resolveCanvasSelection,
  resolveCanvasSelectionFromSnapshot,
} from "@/lib/nodegraph/selection";
import type { NodeGraphEdge, NodeGraphNode } from "@/lib/nodegraph/types";

const nodes: NodeGraphNode[] = [
  {
    id: "node_start",
    position: { x: 0, y: 0 },
    data: {
      label: "Start",
      nodeType: "trigger",
    },
  },
  {
    id: "node_notify",
    position: { x: 240, y: 80 },
    data: {
      label: "Notify",
      nodeType: "action",
    },
  },
];

const edges: NodeGraphEdge[] = [
  {
    id: "edge_start_notify",
    source: "node_start",
    target: "node_notify",
  },
];

describe("nodegraph selection", () => {
  it("creates an empty selection snapshot", () => {
    expect(createEmptyCanvasSelectionSnapshot()).toEqual({
      nodeIds: [],
      edgeIds: [],
    });
  });

  it("captures all selected node and edge ids in a selection snapshot", () => {
    expect(
      createCanvasSelectionSnapshot({
        nodes: [{ id: "node_start" }, { id: "node_notify" }],
        edges: [{ id: "edge_start_notify" }],
      }),
    ).toEqual({
      nodeIds: ["node_start", "node_notify"],
      edgeIds: ["edge_start_notify"],
    });
  });

  it("prefers the first selected node when nodes and edges are both selected", () => {
    expect(
      resolveCanvasSelection({
        nodes: [{ id: "node_start" }],
        edges: [{ id: "edge_start_notify" }],
      }),
    ).toEqual({
      type: "node",
      id: "node_start",
    });
  });

  it("resolves the primary selection from a selection snapshot", () => {
    expect(
      resolveCanvasSelectionFromSnapshot({
        nodeIds: ["node_start", "node_notify"],
        edgeIds: ["edge_start_notify"],
      }),
    ).toEqual({
      type: "node",
      id: "node_start",
    });

    expect(
      resolveCanvasSelectionFromSnapshot({
        nodeIds: [],
        edgeIds: ["edge_start_notify"],
      }),
    ).toEqual({
      type: "edge",
      id: "edge_start_notify",
    });
  });

  it("falls back to the first selected edge when no node is selected", () => {
    expect(
      resolveCanvasSelection({
        nodes: [],
        edges: [{ id: "edge_start_notify" }],
      }),
    ).toEqual({
      type: "edge",
      id: "edge_start_notify",
    });
  });

  it("builds a readable focus label for a selected edge", () => {
    expect(
      getCanvasFocusLabel(
        {
          type: "edge",
          id: "edge_start_notify",
        },
        nodes,
        edges,
      ),
    ).toBe("Link Start -> Notify");
  });

  it("returns the node type for selected nodes and a link label for selected edges", () => {
    expect(
      getCanvasTypeLabel(
        {
          type: "node",
          id: "node_start",
        },
        nodes,
        edges,
      ),
    ).toBe("trigger");

    expect(
      getCanvasTypeLabel(
        {
          type: "edge",
          id: "edge_start_notify",
        },
        nodes,
        edges,
      ),
    ).toBe("link focus");
  });
});
