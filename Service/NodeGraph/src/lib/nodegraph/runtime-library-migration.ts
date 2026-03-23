import { buildFieldDefaults, buildPortSnapshot } from "@/lib/nodegraph/factories";
import type {
  InvalidTemplateMarker,
  NodeGraphDocument,
  NodeGraphEdge,
  NodeGraphNode,
  NodeLibraryItem,
} from "@/lib/nodegraph/types";

function cloneMarkers(markers?: InvalidTemplateMarker[]) {
  return markers ? markers.map((marker) => ({ ...marker })) : [];
}

function hasPort(handleId: string | null | undefined, ports: NodeGraphNode["data"]["inputs"] | NodeGraphNode["data"]["outputs"]) {
  if (!handleId) {
    return true;
  }

  return Boolean(ports?.some((port) => port.id === handleId));
}

function addMarker(markers: InvalidTemplateMarker[], code: string, reason: string) {
  if (markers.some((marker) => marker.code === code && marker.reason === reason)) {
    return;
  }

  markers.push({ code, reason });
}

function migrateNode(node: NodeGraphNode, template?: NodeLibraryItem): NodeGraphNode {
  const markers = cloneMarkers(node.data.templateMarkers);

  if (!template) {
    addMarker(markers, "missingNodeType", `节点类型 "${node.data.nodeType}" 在最新节点库中不存在。`);
    return {
      ...node,
      data: {
        ...node.data,
        templateMarkers: markers,
      },
    };
  }

  const nextPorts = buildPortSnapshot(template);
  const existingInputIds = new Set((node.data.inputs ?? []).map((port) => port.id));
  const existingOutputIds = new Set((node.data.outputs ?? []).map((port) => port.id));
  const nextInputIds = new Set(nextPorts.inputs.map((port) => port.id));
  const nextOutputIds = new Set(nextPorts.outputs.map((port) => port.id));

  for (const inputId of existingInputIds) {
    if (!nextInputIds.has(inputId)) {
      addMarker(markers, "removedPort", `输入端口 "${inputId}" 已从节点 "${node.data.nodeType}" 中移除。`);
    }
  }

  for (const outputId of existingOutputIds) {
    if (!nextOutputIds.has(outputId)) {
      addMarker(markers, "removedPort", `输出端口 "${outputId}" 已从节点 "${node.data.nodeType}" 中移除。`);
    }
  }

  const existingValues = { ...(node.data.values ?? {}) };
  const nextValues = {
    ...buildFieldDefaults(template.fields),
    ...existingValues,
  };
  const currentFieldKeys = new Set((template.fields ?? []).map((field) => field.key));

  for (const key of Object.keys(existingValues)) {
    if (!currentFieldKeys.has(key)) {
      addMarker(markers, "removedField", `字段 "${key}" 已从节点 "${node.data.nodeType}" 中移除。`);
    }
  }

  return {
    ...node,
    data: {
      ...node.data,
      label: node.data.labelOverride ?? template.displayName ?? node.data.label,
      description: node.data.descriptionOverride ?? template.description ?? node.data.description,
      category: template.category ?? node.data.category,
      inputs: nextPorts.inputs,
      outputs: nextPorts.outputs,
      values: nextValues,
      appearance: template.appearance,
      templateMarkers: markers.length ? markers : undefined,
    },
    style: {
      ...(node.style ?? {}),
      borderColor: markers.length ? "#ef4444" : (template.appearance?.borderColor ?? node.style?.borderColor),
    },
  };
}

function migrateEdge(edge: NodeGraphEdge, nodeMap: Map<string, NodeGraphNode>): NodeGraphEdge {
  const reasons: string[] = [];
  const sourceNode = nodeMap.get(edge.source);
  const targetNode = nodeMap.get(edge.target);

  if (!sourceNode) {
    reasons.push(`源节点 "${edge.source}" 不存在。`);
  }

  if (!targetNode) {
    reasons.push(`目标节点 "${edge.target}" 不存在。`);
  }

  if (sourceNode?.data.templateMarkers?.some((marker) => marker.code === "missingNodeType")) {
    reasons.push(`源节点类型 "${sourceNode.data.nodeType}" 已失效。`);
  }

  if (targetNode?.data.templateMarkers?.some((marker) => marker.code === "missingNodeType")) {
    reasons.push(`目标节点类型 "${targetNode.data.nodeType}" 已失效。`);
  }

  if (sourceNode && !hasPort(edge.sourceHandle, sourceNode.data.outputs)) {
    reasons.push(`源端口 "${edge.sourceHandle}" 已失效。`);
  }

  if (targetNode && !hasPort(edge.targetHandle, targetNode.data.inputs)) {
    reasons.push(`目标端口 "${edge.targetHandle}" 已失效。`);
  }

  return {
    ...edge,
    invalidReason: reasons.length ? reasons.join(" ") : undefined,
  };
}

/**
 * 根据最新节点库迁移当前图快照。
 * 迁移会尽量保留用户数据，并把失效节点与连线标记出来，供编辑器立刻展示。
 */
export function migrateGraphWithNodeLibrary(graph: NodeGraphDocument, nodeLibrary: NodeLibraryItem[]): NodeGraphDocument {
  const templateMap = new Map(nodeLibrary.map((item) => [item.type, item] as const));
  const migratedNodes = graph.nodes.map((node) => migrateNode(node, templateMap.get(node.data.nodeType)));
  const nodeMap = new Map(migratedNodes.map((node) => [node.id, node] as const));
  const migratedEdges = graph.edges.map((edge) => migrateEdge(edge, nodeMap));

  return {
    ...graph,
    nodes: migratedNodes,
    edges: migratedEdges,
  };
}
