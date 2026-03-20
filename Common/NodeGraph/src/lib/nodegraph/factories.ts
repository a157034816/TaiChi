import type {
  I18nBundle,
  NodeAppearance,
  NodeGraphDocument,
  NodeGraphNode,
  NodeGraphNodeData,
  NodeLibraryField,
  NodeLibraryItem,
  NodePortDefinition,
} from "@/lib/nodegraph/types";
import {
  createI18nRuntime,
  DEFAULT_LOCALE,
  resolveNodeLibraryDescription,
  resolveNodeLibraryLabel,
} from "@/lib/nodegraph/localization";

const DEFAULT_INPUT_PORTS: NodePortDefinition[] = [{ id: "in", labelKey: "editor.defaults.port.input" }];
const DEFAULT_OUTPUT_PORTS: NodePortDefinition[] = [{ id: "out", labelKey: "editor.defaults.port.output" }];

function cloneLegacyTextValue(value: NodePortDefinition["label"]) {
  if (!value || typeof value === "string") {
    return value;
  }

  return { ...value };
}

/**
 * Creates a new empty graph document with locale-aware default copy.
 */
export function createEmptyGraph(domain: string, domainI18n?: I18nBundle): NodeGraphDocument {
  const i18n = createI18nRuntime({
    locale: DEFAULT_LOCALE,
    domainI18n,
  });

  return {
    name: i18n.text("editor.graphDefaults.name", { domain }),
    description: i18n.text("editor.graphDefaults.description"),
    nodes: [],
    edges: [],
    viewport: {
      x: 0,
      y: 0,
      zoom: 1,
    },
  };
}

/**
 * Builds default values for editable node fields.
 */
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
  return ports.map((port) => ({
    ...port,
    label: cloneLegacyTextValue(port.label),
  }));
}

/**
 * Ensures every node has a stable input and output shape.
 */
export function buildPortSnapshot(item?: Pick<NodeLibraryItem, "inputs" | "outputs">) {
  return {
    inputs: clonePorts(item?.inputs ?? DEFAULT_INPUT_PORTS),
    outputs: clonePorts(item?.outputs ?? DEFAULT_OUTPUT_PORTS),
  };
}

/**
 * Hydrates saved nodes with template ports when older payloads omitted them.
 */
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

/**
 * Produces the shell style consumed by the custom blueprint node renderer.
 */
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

/**
 * Converts a library template into an editor node, preserving translation keys
 * alongside compatibility snapshots.
 */
export function createNodeFromLibrary(
  item: NodeLibraryItem,
  position: { x: number; y: number },
  i18n = createI18nRuntime({
    locale: DEFAULT_LOCALE,
  }),
): NodeGraphNode {
  const portSnapshot = buildPortSnapshot(item);
  const data: NodeGraphNodeData = {
    label: resolveNodeLibraryLabel(item, i18n),
    labelKey: item.labelKey,
    description: resolveNodeLibraryDescription(item, i18n),
    descriptionKey: item.descriptionKey,
    categoryKey: item.categoryKey,
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
