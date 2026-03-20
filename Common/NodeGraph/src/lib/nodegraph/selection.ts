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

export interface CanvasSelectionMessages {
  defaultFocusLabel: string;
  defaultTypeLabel: string;
  linkFocusLabel: string;
  formatLinkFocusLabel: (sourceLabel: string, targetLabel: string) => string;
}

function getNodeLabel(nodeId: string, nodes: NodeGraphNode[]) {
  return nodes.find((node) => node.id === nodeId)?.data.label ?? nodeId;
}

export function createEmptyCanvasSelectionSnapshot(): CanvasSelectionSnapshot {
  return {
    nodeIds: [],
    edgeIds: [],
  };
}

export function createCanvasSelectionSnapshot({ nodes, edges }: SelectionChangeSnapshot): CanvasSelectionSnapshot {
  return {
    nodeIds: nodes.map((node) => node.id),
    edgeIds: edges.map((edge) => edge.id),
  };
}

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

export function resolveCanvasSelectionFromSnapshot(selection: CanvasSelectionSnapshot): CanvasSelection {
  return resolveCanvasSelection({
    nodes: selection.nodeIds.map((id) => ({ id })),
    edges: selection.edgeIds.map((id) => ({ id })),
  });
}

export function getCanvasFocusLabel(
  selection: CanvasSelection,
  nodes: NodeGraphNode[],
  edges: NodeGraphEdge[],
  messages: CanvasSelectionMessages,
) {
  if (!selection) {
    return messages.defaultFocusLabel;
  }

  if (selection.type === "node") {
    return nodes.find((node) => node.id === selection.id)?.data.label ?? messages.defaultFocusLabel;
  }

  const edge = edges.find((item) => item.id === selection.id);

  if (!edge) {
    return messages.defaultFocusLabel;
  }

  return messages.formatLinkFocusLabel(getNodeLabel(edge.source, nodes), getNodeLabel(edge.target, nodes));
}

export function getCanvasTypeLabel(
  selection: CanvasSelection,
  nodes: NodeGraphNode[],
  edges: NodeGraphEdge[],
  messages: CanvasSelectionMessages,
) {
  if (!selection) {
    return messages.defaultTypeLabel;
  }

  if (selection.type === "node") {
    return nodes.find((node) => node.id === selection.id)?.data.nodeType ?? messages.defaultTypeLabel;
  }

  return edges.some((edge) => edge.id === selection.id) ? messages.linkFocusLabel : messages.defaultTypeLabel;
}
