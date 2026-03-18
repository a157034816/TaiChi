import type { NodeGraphEdge, NodeGraphNode } from "@/lib/nodegraph/types";

export interface InspectorDetailItem {
  label: string;
  value: string;
}

export interface EdgeInspectorDetails {
  title: string;
  subtitle: string;
  description: string;
  items: InspectorDetailItem[];
}

const DEFAULT_INSPECTOR_ACCENT = "#ff9d1c";
const EDGE_INSPECTOR_ACCENT = "#57c7ff";
const DEFAULT_HANDLE_LABEL = "Default port";

function getNodeLabel(nodeId: string, nodes: NodeGraphNode[]) {
  return nodes.find((node) => node.id === nodeId)?.data.label ?? nodeId;
}

function formatHandleLabel(handle: string | null | undefined) {
  return handle ?? DEFAULT_HANDLE_LABEL;
}

export function getInspectorSelectionTabLabel(node: NodeGraphNode | null, edge: NodeGraphEdge | null) {
  if (edge) {
    return "Selected link";
  }

  if (node) {
    return "Selected node";
  }

  return "Selection";
}

export function getInspectorAccent(node: NodeGraphNode | null, edge: NodeGraphEdge | null) {
  if (node?.data.appearance?.borderColor) {
    return node.data.appearance.borderColor;
  }

  if (typeof edge?.style?.stroke === "string") {
    return edge.style.stroke;
  }

  return edge ? EDGE_INSPECTOR_ACCENT : DEFAULT_INSPECTOR_ACCENT;
}

export function buildEdgeInspectorDetails(edge: NodeGraphEdge, nodes: NodeGraphNode[]): EdgeInspectorDetails {
  const sourceLabel = getNodeLabel(edge.source, nodes);
  const targetLabel = getNodeLabel(edge.target, nodes);
  const sourceHandle = formatHandleLabel(edge.sourceHandle);
  const targetHandle = formatHandleLabel(edge.targetHandle);

  return {
    title: `${sourceLabel} -> ${targetLabel}`,
    subtitle: "Connection",
    description: `This link routes ${sourceHandle} from ${sourceLabel} into ${targetHandle} on ${targetLabel}.`,
    items: [
      { label: "Edge id", value: edge.id },
      { label: "Source node", value: `${sourceLabel} (${edge.source})` },
      { label: "Source handle", value: sourceHandle },
      { label: "Target node", value: `${targetLabel} (${edge.target})` },
      { label: "Target handle", value: targetHandle },
    ],
  };
}
