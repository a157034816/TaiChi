import type { NodeGraphEdge, NodeGraphNode } from "@/lib/nodegraph/types";

export type ClipboardIdKind = "node" | "edge";

export interface NodeClipboardPayload {
  nodes: NodeGraphNode[];
  edges: NodeGraphEdge[];
  origin: NodeGraphNode["position"];
}

interface PasteClipboardOptions {
  position: NodeGraphNode["position"];
  cascadeIndex?: number;
  cascadeOffset?: NodeGraphNode["position"];
  createId?: (kind: ClipboardIdKind) => string;
}

const DEFAULT_CASCADE_OFFSET = { x: 36, y: 36 };

function cloneGraphValue<T>(value: T): T {
  return structuredClone(value);
}

function getClipboardOrigin(nodes: NodeGraphNode[]): NodeGraphNode["position"] {
  return {
    x: Math.min(...nodes.map((node) => node.position.x)),
    y: Math.min(...nodes.map((node) => node.position.y)),
  };
}

export function createClipboardId(kind: ClipboardIdKind) {
  return `${kind}_${crypto.randomUUID()}`;
}

export function buildClipboardFromSelection(
  nodes: NodeGraphNode[],
  edges: NodeGraphEdge[],
  selectedNodeIds: string[],
): NodeClipboardPayload | null {
  if (!selectedNodeIds.length) {
    return null;
  }

  const selectedNodeIdSet = new Set(selectedNodeIds);
  const selectedNodes = nodes
    .filter((node) => selectedNodeIdSet.has(node.id))
    .map((node) => cloneGraphValue(node));

  if (!selectedNodes.length) {
    return null;
  }

  const selectedEdges = edges
    .filter((edge) => selectedNodeIdSet.has(edge.source) && selectedNodeIdSet.has(edge.target))
    .map((edge) => cloneGraphValue(edge));

  return {
    nodes: selectedNodes,
    edges: selectedEdges,
    origin: getClipboardOrigin(selectedNodes),
  };
}

export function pasteClipboardAtPosition(
  clipboard: NodeClipboardPayload,
  {
    position,
    cascadeIndex = 0,
    cascadeOffset = DEFAULT_CASCADE_OFFSET,
    createId = createClipboardId,
  }: PasteClipboardOptions,
) {
  const cascadedPosition = {
    x: position.x + cascadeOffset.x * cascadeIndex,
    y: position.y + cascadeOffset.y * cascadeIndex,
  };
  const offset = {
    x: cascadedPosition.x - clipboard.origin.x,
    y: cascadedPosition.y - clipboard.origin.y,
  };
  const nodeIdMap = new Map<string, string>();

  const nodes = clipboard.nodes.map((node) => {
    const nextId = createId("node");
    nodeIdMap.set(node.id, nextId);

    return {
      ...cloneGraphValue(node),
      id: nextId,
      position: {
        x: node.position.x + offset.x,
        y: node.position.y + offset.y,
      },
      dragging: false,
      selected: false,
    };
  });

  const edges = clipboard.edges.map((edge) => ({
    ...cloneGraphValue(edge),
    id: createId("edge"),
    source: nodeIdMap.get(edge.source) ?? edge.source,
    target: nodeIdMap.get(edge.target) ?? edge.target,
    selected: false,
  }));

  return {
    nodes,
    edges,
  };
}
