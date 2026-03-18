import type {
  NodeAppearance,
  NodeGraphDocument,
  NodeGraphNode,
  NodeGraphNodeData,
  NodeLibraryField,
  NodeLibraryItem,
  NodePortDefinition,
} from "@/lib/nodegraph/types";

const DEFAULT_INPUT_PORTS: NodePortDefinition[] = [{ id: "in", label: "Input" }];
const DEFAULT_OUTPUT_PORTS: NodePortDefinition[] = [{ id: "out", label: "Output" }];

export function createEmptyGraph(domain: string): NodeGraphDocument {
  return {
    name: `${domain} flow`,
    description: "A new node graph session created by NodeGraph.",
    nodes: [],
    edges: [],
    viewport: {
      x: 0,
      y: 0,
      zoom: 1,
    },
  };
}

export function buildFieldDefaults(fields?: NodeLibraryField[]) {
  if (!fields?.length) {
    return {};
  }

  return Object.fromEntries(
    fields.map((field) => [field.key, field.defaultValue ?? getFallbackValue(field.kind)]),
  );
}

function getFallbackValue(kind: NodeLibraryField["kind"]) {
  switch (kind) {
    case "number":
      return 0;
    case "boolean":
      return false;
    default:
      return "";
  }
}

function clonePorts(ports: NodePortDefinition[]) {
  return ports.map((port) => ({ ...port }));
}

export function buildPortSnapshot(item?: Pick<NodeLibraryItem, "inputs" | "outputs">) {
  return {
    inputs: clonePorts(item?.inputs ?? DEFAULT_INPUT_PORTS),
    outputs: clonePorts(item?.outputs ?? DEFAULT_OUTPUT_PORTS),
  };
}

export function normalizeNodeDataPorts(
  data: NodeGraphNodeData,
  item?: Pick<NodeLibraryItem, "inputs" | "outputs">,
): NodeGraphNodeData {
  const fallbackPorts = buildPortSnapshot(item);

  return {
    ...data,
    inputs: clonePorts(data.inputs ?? fallbackPorts.inputs),
    outputs: clonePorts(data.outputs ?? fallbackPorts.outputs),
  };
}

export function buildNodeStyle(appearance?: NodeAppearance) {
  return {
    background: "transparent",
    border: "none",
    borderColor: appearance?.borderColor ?? "#ff9d1c",
    color: appearance?.textColor ?? "#f7fbff",
    boxShadow: "none",
    borderRadius: 18,
    minWidth: 280,
    width: 280,
  };
}

export function createNodeFromLibrary(
  item: NodeLibraryItem,
  position: { x: number; y: number },
): NodeGraphNode {
  const portSnapshot = buildPortSnapshot(item);
  const data: NodeGraphNodeData = {
    label: item.label,
    description: item.description,
    category: item.category,
    nodeType: item.type,
    inputs: portSnapshot.inputs,
    outputs: portSnapshot.outputs,
    values: {
      ...buildFieldDefaults(item.fields),
      ...(item.defaultData ?? {}),
    },
    appearance: item.appearance,
  };

  return {
    id: `node_${crypto.randomUUID()}`,
    type: "default",
    position,
    data,
    style: buildNodeStyle(item.appearance),
  };
}
