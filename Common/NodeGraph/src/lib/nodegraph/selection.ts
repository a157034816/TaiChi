import type { NodeGraphEdge, NodeGraphNode } from "@/lib/nodegraph/types";

export type CanvasSelection =
  | { type: "node"; id: string }
  | { type: "edge"; id: string }
  | null;

interface SelectionChangeSnapshot {
  nodes: Array<Pick<NodeGraphNode, "id">>;
  edges: Array<Pick<NodeGraphEdge, "id">>;
}

const DEFAULT_FOCUS_LABEL = "Canvas";
const DEFAULT_TYPE_LABEL = "canvas focus";

function getNodeLabel(nodeId: string, nodes: NodeGraphNode[]) {
  return nodes.find((node) => node.id === nodeId)?.data.label ?? nodeId;
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

export function getCanvasFocusLabel(
  selection: CanvasSelection,
  nodes: NodeGraphNode[],
  edges: NodeGraphEdge[],
) {
  if (!selection) {
    return DEFAULT_FOCUS_LABEL;
  }

  if (selection.type === "node") {
    return nodes.find((node) => node.id === selection.id)?.data.label ?? DEFAULT_FOCUS_LABEL;
  }

  const edge = edges.find((item) => item.id === selection.id);

  if (!edge) {
    return DEFAULT_FOCUS_LABEL;
  }

  return `Link ${getNodeLabel(edge.source, nodes)} -> ${getNodeLabel(edge.target, nodes)}`;
}

export function getCanvasTypeLabel(
  selection: CanvasSelection,
  nodes: NodeGraphNode[],
  edges: NodeGraphEdge[],
) {
  if (!selection) {
    return DEFAULT_TYPE_LABEL;
  }

  if (selection.type === "node") {
    return nodes.find((node) => node.id === selection.id)?.data.nodeType ?? DEFAULT_TYPE_LABEL;
  }

  return edges.some((edge) => edge.id === selection.id) ? "link focus" : DEFAULT_TYPE_LABEL;
}
