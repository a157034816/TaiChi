import { resolveNodeLabel, type I18nRuntime } from "@/lib/nodegraph/localization";
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

function getNodeLabel(nodeId: string, nodes: NodeGraphNode[], i18n: I18nRuntime) {
  const node = nodes.find((entry) => entry.id === nodeId);
  return node ? resolveNodeLabel(node.data, i18n) : nodeId;
}

function formatHandleLabel(handle: string | null | undefined, i18n: I18nRuntime) {
  return handle ?? i18n.text("editor.edgeInspector.defaultHandleLabel");
}

/**
 * Chooses the selection tab label for the inspector.
 */
export function getInspectorSelectionTabLabel(
  node: NodeGraphNode | null,
  edge: NodeGraphEdge | null,
  i18n: I18nRuntime,
) {
  if (edge) {
    return i18n.text("editor.edgeInspector.selectedLinkLabel");
  }

  if (node) {
    return i18n.text("editor.edgeInspector.selectedNodeLabel");
  }

  return i18n.text("editor.edgeInspector.selectionLabel");
}

/**
 * Picks the accent color used by the inspector header.
 */
export function getInspectorAccent(node: NodeGraphNode | null, edge: NodeGraphEdge | null) {
  if (node?.data.appearance?.borderColor) {
    return node.data.appearance.borderColor;
  }

  if (typeof edge?.style?.stroke === "string") {
    return edge.style.stroke;
  }

  return edge ? EDGE_INSPECTOR_ACCENT : DEFAULT_INSPECTOR_ACCENT;
}

/**
 * Builds the inspector detail cards for a selected edge.
 */
export function buildEdgeInspectorDetails(
  edge: NodeGraphEdge,
  nodes: NodeGraphNode[],
  i18n: I18nRuntime,
): EdgeInspectorDetails {
  const sourceLabel = getNodeLabel(edge.source, nodes, i18n);
  const targetLabel = getNodeLabel(edge.target, nodes, i18n);
  const sourceHandle = formatHandleLabel(edge.sourceHandle, i18n);
  const targetHandle = formatHandleLabel(edge.targetHandle, i18n);

  return {
    title: `${sourceLabel} -> ${targetLabel}`,
    subtitle: i18n.text("editor.edgeInspector.connectionSubtitle"),
    description: i18n.text("editor.edgeInspector.edgeDescription", {
      sourceHandle,
      sourceLabel,
      targetHandle,
      targetLabel,
    }),
    items: [
      { label: i18n.text("editor.edgeInspector.edgeIdLabel"), value: edge.id },
      { label: i18n.text("editor.edgeInspector.sourceNodeLabel"), value: `${sourceLabel} (${edge.source})` },
      { label: i18n.text("editor.edgeInspector.sourceHandleLabel"), value: sourceHandle },
      { label: i18n.text("editor.edgeInspector.targetNodeLabel"), value: `${targetLabel} (${edge.target})` },
      { label: i18n.text("editor.edgeInspector.targetHandleLabel"), value: targetHandle },
    ],
  };
}
