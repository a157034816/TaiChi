import { describe, expect, it } from "vitest";

import { buildClipboardFromSelection, pasteClipboardAtPosition } from "@/lib/nodegraph/clipboard";
import type { NodeGraphEdge, NodeGraphNode } from "@/lib/nodegraph/types";

const nodes: NodeGraphNode[] = [
  {
    id: "node_start",
    position: { x: 120, y: 80 },
    data: {
      label: "Start",
      nodeType: "trigger",
    },
    selected: true,
  },
  {
    id: "node_approval",
    position: { x: 360, y: 140 },
    data: {
      label: "Approval",
      nodeType: "approval",
    },
    selected: true,
  },
  {
    id: "node_notify",
    position: { x: 700, y: 220 },
    data: {
      label: "Notify",
      nodeType: "action",
    },
  },
];

const edges: NodeGraphEdge[] = [
  {
    id: "edge_start_approval",
    source: "node_start",
    target: "node_approval",
    selected: true,
  },
  {
    id: "edge_approval_notify",
    source: "node_approval",
    target: "node_notify",
  },
];

describe("nodegraph clipboard", () => {
  it("returns null when no node is selected", () => {
    expect(buildClipboardFromSelection(nodes, edges, [])).toBeNull();
  });

  it("copies selected nodes and only preserves edges inside the selection", () => {
    const clipboard = buildClipboardFromSelection(nodes, edges, ["node_start", "node_approval"]);

    expect(clipboard).toMatchObject({
      nodes: [
        { id: "node_start" },
        { id: "node_approval" },
      ],
      edges: [{ id: "edge_start_approval" }],
      origin: { x: 120, y: 80 },
    });
  });

  it("pastes clipboard nodes at the requested anchor and remaps internal edges", () => {
    const clipboard = buildClipboardFromSelection(nodes, edges, ["node_start", "node_approval"]);

    if (!clipboard) {
      throw new Error("clipboard should be available for the selected nodes");
    }

    let nodeCounter = 0;
    let edgeCounter = 0;
    const pasted = pasteClipboardAtPosition(clipboard, {
      position: { x: 420, y: 480 },
      createId: (kind) => {
        if (kind === "node") {
          nodeCounter += 1;
          return `node_copy_${nodeCounter}`;
        }

        edgeCounter += 1;
        return `edge_copy_${edgeCounter}`;
      },
    });

    expect(pasted.nodes).toMatchObject([
      {
        id: "node_copy_1",
        position: { x: 420, y: 480 },
        selected: false,
      },
      {
        id: "node_copy_2",
        position: { x: 660, y: 540 },
        selected: false,
      },
    ]);
    expect(pasted.edges).toEqual([
      expect.objectContaining({
        id: "edge_copy_1",
        source: "node_copy_1",
        target: "node_copy_2",
        selected: false,
      }),
    ]);
  });

  it("applies a cascade offset for repeated paste actions", () => {
    const clipboard = buildClipboardFromSelection(nodes, edges, ["node_start", "node_approval"]);

    if (!clipboard) {
      throw new Error("clipboard should be available for the selected nodes");
    }

    const pasted = pasteClipboardAtPosition(clipboard, {
      position: { x: 420, y: 480 },
      cascadeIndex: 2,
      cascadeOffset: { x: 20, y: 30 },
      createId: (kind) => `${kind}_copy`,
    });

    expect(pasted.nodes[0]?.position).toEqual({ x: 460, y: 540 });
    expect(pasted.nodes[1]?.position).toEqual({ x: 700, y: 600 });
  });
});
