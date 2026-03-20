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

export interface InspectorTextMessages {
  defaultHandleLabel: string;
  selectedLinkLabel: string;
  selectedNodeLabel: string;
  selectionLabel: string;
  connectionSubtitle: string;
  formatEdgeDescription: (
    sourceHandle: string,
    sourceLabel: string,
    targetHandle: string,
    targetLabel: string,
  ) => string;
  edgeIdLabel: string;
  sourceNodeLabel: string;
  sourceHandleLabel: string;
  targetNodeLabel: string;
  targetHandleLabel: string;
}

function getNodeLabel(nodeId: string, nodes: NodeGraphNode[]) {
  return nodes.find((node) => node.id === nodeId)?.data.label ?? nodeId;
}

function formatHandleLabel(handle: string | null | undefined, messages: InspectorTextMessages) {
  return handle ?? messages.defaultHandleLabel;
}

export function getInspectorSelectionTabLabel(
  node: NodeGraphNode | null,
  edge: NodeGraphEdge | null,
  messages: InspectorTextMessages,
) {
  if (edge) {
    return messages.selectedLinkLabel;
  }

  if (node) {
    return messages.selectedNodeLabel;
  }

  return messages.selectionLabel;
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

export function buildEdgeInspectorDetails(
  edge: NodeGraphEdge,
  nodes: NodeGraphNode[],
  messages: InspectorTextMessages,
): EdgeInspectorDetails {
  const sourceLabel = getNodeLabel(edge.source, nodes);
  const targetLabel = getNodeLabel(edge.target, nodes);
  const sourceHandle = formatHandleLabel(edge.sourceHandle, messages);
  const targetHandle = formatHandleLabel(edge.targetHandle, messages);

  return {
    title: `${sourceLabel} -> ${targetLabel}`,
    subtitle: messages.connectionSubtitle,
    description: messages.formatEdgeDescription(sourceHandle, sourceLabel, targetHandle, targetLabel),
    items: [
      { label: messages.edgeIdLabel, value: edge.id },
      { label: messages.sourceNodeLabel, value: `${sourceLabel} (${edge.source})` },
      { label: messages.sourceHandleLabel, value: sourceHandle },
      { label: messages.targetNodeLabel, value: `${targetLabel} (${edge.target})` },
      { label: messages.targetHandleLabel, value: targetHandle },
    ],
  };
}
