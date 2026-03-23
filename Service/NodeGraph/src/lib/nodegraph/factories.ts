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

function getDefaultInputPorts(i18n = createI18nRuntime({ locale: DEFAULT_LOCALE })): NodePortDefinition[] {
  return [{ id: "in", label: i18n.text("editor.defaults.port.input") }];
}

function getDefaultOutputPorts(i18n = createI18nRuntime({ locale: DEFAULT_LOCALE })): NodePortDefinition[] {
  return [{ id: "out", label: i18n.text("editor.defaults.port.output") }];
}

/**
 * 创建空白图文档，并使用 NodeGraph 内建 UI 文案填充默认名称与描述。
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
 * 为节点字段生成默认值快照，供新建节点与运行时迁移复用。
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
    case "int":
    case "float":
    case "double":
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
  }));
}

/**
 * 生成端口快照，保证节点始终拥有稳定的输入输出结构。
 */
export function buildPortSnapshot(
  item?: Pick<NodeLibraryItem, "inputs" | "outputs">,
  i18n = createI18nRuntime({ locale: DEFAULT_LOCALE }),
) {
  return {
    inputs: clonePorts(item?.inputs ?? getDefaultInputPorts(i18n)),
    outputs: clonePorts(item?.outputs ?? getDefaultOutputPorts(i18n)),
  };
}

/**
 * 为缺失端口快照的节点补齐模板端口，兼容端口信息缺省的已保存图数据。
 */
export function normalizeNodeDataPorts(
  data: NodeGraphNodeData,
  item?: Pick<NodeLibraryItem, "inputs" | "outputs">,
  i18n = createI18nRuntime({ locale: DEFAULT_LOCALE }),
): NodeGraphNodeData {
  const fallbackPorts = buildPortSnapshot(item, i18n);

  return {
    ...data,
    inputs: clonePorts(data.inputs ?? fallbackPorts.inputs),
    outputs: clonePorts(data.outputs ?? fallbackPorts.outputs),
  };
}

/**
 * 生成蓝图节点渲染器需要的基础样式。
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
 * 根据节点库模板创建编辑器节点，并固化宿主提供的原始展示文本与端口快照。
 */
export function createNodeFromLibrary(
  item: NodeLibraryItem,
  position: { x: number; y: number },
  i18n = createI18nRuntime({
    locale: DEFAULT_LOCALE,
  }),
): NodeGraphNode {
  const portSnapshot = buildPortSnapshot(item, i18n);
  const data: NodeGraphNodeData = {
    label: resolveNodeLibraryLabel(item, i18n),
    description: resolveNodeLibraryDescription(item, i18n),
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
