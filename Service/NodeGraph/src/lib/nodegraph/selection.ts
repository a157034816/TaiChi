import { resolveNodeLabel, type I18nRuntime } from "@/lib/nodegraph/localization";
import type { NodeGraphEdge, NodeGraphNode } from "@/lib/nodegraph/types";

export type CanvasSelection =
  | { type: "node"; id: string }
  | { type: "edge"; id: string }
  | null;

export interface CanvasSelectionSnapshot {
  nodeIds: string[];
  edgeIds: string[];
}

interface SelectionChangeSnapshot {
  nodes: Array<Pick<NodeGraphNode, "id">>;
  edges: Array<Pick<NodeGraphEdge, "id">>;
}

function getNodeLabel(nodeId: string, nodes: NodeGraphNode[], i18n: I18nRuntime) {
  const node = nodes.find((entry) => entry.id === nodeId);
  return node ? resolveNodeLabel(node.data, i18n) : nodeId;
}

/**
 * Creates an empty selection payload for the canvas.
 */
export function createEmptyCanvasSelectionSnapshot(): CanvasSelectionSnapshot {
  return {
    nodeIds: [],
    edgeIds: [],
  };
}

/**
 * Captures the selected node and edge ids from React Flow callbacks.
 */
export function createCanvasSelectionSnapshot({ nodes, edges }: SelectionChangeSnapshot): CanvasSelectionSnapshot {
  return {
    nodeIds: nodes.map((node) => node.id),
    edgeIds: edges.map((edge) => edge.id),
  };
}

/**
 * Chooses the primary selection target from a mixed selection set.
 */
export function resolveCanvasSelection({ nodes, edges }: SelectionChangeSnapshot): CanvasSelection {
  const selectedNode = nodes[0];

  if (selectedNode) {
    return {
      type: "node",
      id: selectedNode.id,
    };
  }

  const selectedEdge = edges[0];

  if (selectedEdge) {
    return {
      type: "edge",
      id: selectedEdge.id,
    };
  }

  return null;
}

/**
 * Rehydrates the primary selection from a stored snapshot.
 */
export function resolveCanvasSelectionFromSnapshot(selection: CanvasSelectionSnapshot): CanvasSelection {
  return resolveCanvasSelection({
    nodes: selection.nodeIds.map((id) => ({ id })),
    edges: selection.edgeIds.map((id) => ({ id })),
  });
}

/**
 * Builds the HUD focus label for the current selection.
 */
export function getCanvasFocusLabel(
  selection: CanvasSelection,
  nodes: NodeGraphNode[],
  edges: NodeGraphEdge[],
  i18n: I18nRuntime,
) {
  if (!selection) {
    return i18n.text("editor.selection.defaultFocusLabel");
  }

  if (selection.type === "node") {
    const node = nodes.find((entry) => entry.id === selection.id);
    return node ? resolveNodeLabel(node.data, i18n) : i18n.text("editor.selection.defaultFocusLabel");
  }

  const edge = edges.find((item) => item.id === selection.id);

  if (!edge) {
    return i18n.text("editor.selection.defaultFocusLabel");
  }

  return i18n.text("editor.selection.linkFocusTitle", {
    sourceLabel: getNodeLabel(edge.source, nodes, i18n),
    targetLabel: getNodeLabel(edge.target, nodes, i18n),
  });
}

/**
 * Builds the HUD type label for the current selection.
 */
export function getCanvasTypeLabel(
  selection: CanvasSelection,
  nodes: NodeGraphNode[],
  edges: NodeGraphEdge[],
  i18n: I18nRuntime,
) {
  if (!selection) {
    return i18n.text("editor.selection.defaultTypeLabel");
  }

  if (selection.type === "node") {
    return nodes.find((node) => node.id === selection.id)?.data.nodeType ?? i18n.text("editor.selection.defaultTypeLabel");
  }

  return edges.some((edge) => edge.id === selection.id)
    ? i18n.text("editor.selection.linkFocusLabel")
    : i18n.text("editor.selection.defaultTypeLabel");
}
