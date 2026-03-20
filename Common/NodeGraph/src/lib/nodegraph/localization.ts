import type { CanvasSelectionMessages } from "@/lib/nodegraph/selection";
import type { InspectorTextMessages } from "@/lib/nodegraph/inspector";
import type {
  AccessType,
  LocalizedText,
  NodeLibraryField,
  NodePortDefinition,
  SupportedLocale,
} from "@/lib/nodegraph/types";
import type { EditorEdgeStyle } from "@/lib/nodegraph/editor-preferences";

export const DEFAULT_LOCALE: SupportedLocale = "zh-CN";

export interface ResolvedNodePortDefinition extends Omit<NodePortDefinition, "label"> {
  label: string;
}

export interface EditorMessages {
  localeNames: Record<SupportedLocale, string>;
  accessTypeLabels: Record<AccessType, string>;
  edgeStyleNames: Record<EditorEdgeStyle, string>;
  header: {
    activeGraphKicker: string;
    accessUrlSuffix: string;
    libraryNodes: (count: number) => string;
    fallbackDescription: string;
    sessionLabel: (sessionId: string) => string;
  };
  stats: {
    nodes: string;
    links: string;
    focus: string;
    activeLinks: (count: number) => string;
  };
  save: {
    delivered: string;
    failed: string;
    submitting: string;
    completeEditing: string;
  };
  canvas: {
    kicker: string;
    title: string;
    subtitle: string;
  };
  footer: {
    summary: string;
  };
  library: {
    kicker: string;
    title: string;
    description: string;
    itemCount: (count: number) => string;
    searchPlaceholder: string;
    editableFields: (count: number) => string;
    addNode: string;
    emptySearch: string;
  };
  inspector: {
    kicker: string;
    title: string;
    description: string;
    tabs: {
      graph: string;
      settings: string;
    };
    graphName: string;
    graphDescription: string;
    graphHint: string;
    settingsTitle: string;
    settingsDescription: string;
    languageLabel: string;
    languageDescription: string;
    edgeStyleLabel: string;
    edgeStyleDescription: string;
    nodeLabel: string;
    nodeDescription: string;
    editableFields: string;
    linkBadge: string;
    linkDetails: string;
    emptySelection: string;
    fallbackNodeDescription: string;
  };
  contextMenu: {
    edit: string;
    pasteNodes: string;
    createConnectedNode: string;
    addNode: string;
    noCompatibleNodes: string;
    noNodesAvailable: string;
    copyNode: string;
    copySelectedNodes: string;
    cutNode: string;
    cutSelectedNodes: string;
    deleteEdge: string;
    deleteNode: string;
    deleteSelectedNodes: string;
    deleteSelectedEdges: string;
    deleteSelection: string;
  };
  selection: CanvasSelectionMessages;
  edgeInspector: InspectorTextMessages;
  blueprint: {
    fallbackCategory: string;
    ready: string;
    fallbackDescription: string;
    inputs: string;
    outputs: string;
    noInputs: string;
    noOutputs: string;
    values: string;
    noEditableFields: string;
    enabled: string;
    disabled: string;
    empty: string;
    items: (count: number) => string;
    configured: string;
    unset: string;
    moreFieldRows: (count: number) => string;
  };
  graphDefaults: {
    name: (domain: string) => string;
    description: string;
  };
}

export function createLocalizedText(zhCN: string, en: string): LocalizedText {
  return {
    "zh-CN": zhCN,
    en,
  };
}

export function resolveLocalizedText(text: LocalizedText, locale: SupportedLocale) {
  return text[locale] || text[DEFAULT_LOCALE] || text.en || text["zh-CN"];
}

export function resolveOptionalLocalizedText(text: LocalizedText | undefined, locale: SupportedLocale) {
  return text ? resolveLocalizedText(text, locale) : undefined;
}

export function resolveNodePortDefinitions(
  ports: NodePortDefinition[] | undefined,
  locale: SupportedLocale,
): ResolvedNodePortDefinition[] {
  return (ports ?? []).map((port) => ({
    ...port,
    label: resolveLocalizedText(port.label, locale),
  }));
}

export function resolveFieldLabel(field: NodeLibraryField, locale: SupportedLocale) {
  return resolveLocalizedText(field.label, locale);
}

export function resolveFieldPlaceholder(field: NodeLibraryField, locale: SupportedLocale) {
  return resolveOptionalLocalizedText(field.placeholder, locale);
}

const editorMessages: Record<SupportedLocale, EditorMessages> = {
  "zh-CN": {
    localeNames: {
      "zh-CN": "简体中文",
      en: "English",
    },
    accessTypeLabels: {
      public: "公网",
      private: "内网",
    },
    edgeStyleNames: {
      smoothstep: "平滑折线",
      bezier: "贝塞尔曲线",
      straight: "直线",
      step: "阶梯折线",
    },
    header: {
      activeGraphKicker: "当前图谱",
      accessUrlSuffix: "地址",
      libraryNodes: (count) => `${count} 个节点模板`,
      fallbackDescription: "在这里构建并检查当前会话图谱，完成后再把最终结果通过 webhook 回传给调用方。",
      sessionLabel: (sessionId) => `会话 ${sessionId}`,
    },
    stats: {
      nodes: "节点",
      links: "连接",
      focus: "焦点",
      activeLinks: (count) => `${count} 条活动连接`,
    },
    save: {
      delivered: "Webhook 已发送",
      failed: "完成回调失败，请调整数据后重试。",
      submitting: "提交中...",
      completeEditing: "完成编辑",
    },
    canvas: {
      kicker: "蓝图工作区",
      title: "节点画布",
      subtitle: "拖动调整节点位置，把输出连接到输入，或把连接线拖到空白处就地创建下一个兼容节点。",
    },
    footer: {
      summary: "当前改造保持原有会话编辑流程不变：从左侧节点库添加节点，在画布上连线，在右侧检查器编辑细节，最后提交完成图谱。",
    },
    library: {
      kicker: "节点库",
      title: "节点模板",
      description: "检索当前领域可用节点，比较分类信息，并将构建块直接加入画布。",
      itemCount: (count) => `${count} 项`,
      searchPlaceholder: "搜索节点类型、分类或标签",
      editableFields: (count) => `${count} 个可编辑字段`,
      addNode: "添加节点",
      emptySearch: "当前搜索条件下没有匹配的节点类型。",
    },
    inspector: {
      kicker: "面板",
      title: "检查器",
      description: "在不离开画布的情况下调整图元信息、编辑本地设置，并查看当前节点或连接线详情。",
      tabs: {
        graph: "图谱",
        settings: "设置",
      },
      graphName: "图谱名称",
      graphDescription: "图谱说明",
      graphHint: "图谱标签页用于维护当前会话的整体名称与摘要说明，节点与连接线细节保留在相邻的详情标签页。",
      settingsTitle: "本地全局设置",
      settingsDescription: "这些偏好仅保存在当前浏览器中，用于控制编辑器显示语言与连接线风格，不会写入图谱数据。",
      languageLabel: "界面语言",
      languageDescription: "切换编辑器内置文案，以及节点库、端口和字段模板的展示语言。",
      edgeStyleLabel: "连接线风格",
      edgeStyleDescription: "切换画布上已有连接线与新建连接线的显示形态，不影响最终保存的图谱结构。",
      nodeLabel: "节点标题",
      nodeDescription: "节点说明",
      editableFields: "可编辑字段",
      linkBadge: "连接线",
      linkDetails: "连接线详情",
      emptySelection: "在画布中选择一个节点或连接线，这里会显示对应的详细信息。",
      fallbackNodeDescription: "这个节点已经准备好进行字段级调整。",
    },
    contextMenu: {
      edit: "编辑",
      pasteNodes: "粘贴节点",
      createConnectedNode: "创建并连接节点",
      addNode: "添加节点",
      noCompatibleNodes: "当前没有可用于补全这条连接的兼容节点类型。",
      noNodesAvailable: "当前节点库中没有可用节点。",
      copyNode: "复制节点",
      copySelectedNodes: "复制所选节点",
      cutNode: "剪切节点",
      cutSelectedNodes: "剪切所选节点",
      deleteEdge: "删除连接线",
      deleteNode: "删除节点",
      deleteSelectedNodes: "删除所选节点",
      deleteSelectedEdges: "删除所选连接线",
      deleteSelection: "删除所选内容",
    },
    selection: {
      defaultFocusLabel: "画布",
      defaultTypeLabel: "画布焦点",
      linkFocusLabel: "连接焦点",
      formatLinkFocusLabel: (sourceLabel, targetLabel) => `连接 ${sourceLabel} -> ${targetLabel}`,
    },
    edgeInspector: {
      defaultHandleLabel: "默认端口",
      selectedLinkLabel: "选中连接",
      selectedNodeLabel: "选中节点",
      selectionLabel: "详情",
      connectionSubtitle: "连接关系",
      formatEdgeDescription: (sourceHandle, sourceLabel, targetHandle, targetLabel) =>
        `这条连接会把 ${sourceLabel} 的 ${sourceHandle} 路由到 ${targetLabel} 的 ${targetHandle}。`,
      edgeIdLabel: "连接 ID",
      sourceNodeLabel: "源节点",
      sourceHandleLabel: "源端口",
      targetNodeLabel: "目标节点",
      targetHandleLabel: "目标端口",
    },
    blueprint: {
      fallbackCategory: "节点",
      ready: "就绪",
      fallbackDescription: "这个节点已经准备好进行字段驱动配置。",
      inputs: "输入",
      outputs: "输出",
      noInputs: "无输入",
      noOutputs: "无输出",
      values: "取值",
      noEditableFields: "没有可编辑字段",
      enabled: "已启用",
      disabled: "已禁用",
      empty: "空",
      items: (count) => `${count} 项`,
      configured: "已配置",
      unset: "未设置",
      moreFieldRows: (count) => `检查器中还有 ${count} 行字段`,
    },
    graphDefaults: {
      name: (domain) => `${domain} 流程图`,
      description: "这是由 NodeGraph 创建的新会话图谱。",
    },
  },
  en: {
    localeNames: {
      "zh-CN": "Simplified Chinese",
      en: "English",
    },
    accessTypeLabels: {
      public: "Public",
      private: "Private",
    },
    edgeStyleNames: {
      smoothstep: "Smooth Step",
      bezier: "Bezier",
      straight: "Straight",
      step: "Step",
    },
    header: {
      activeGraphKicker: "Active graph",
      accessUrlSuffix: "URL",
      libraryNodes: (count) => `${count} library nodes`,
      fallbackDescription: "Build and review the current session graph here, then push the final document back through the completion webhook.",
      sessionLabel: (sessionId) => `Session ${sessionId}`,
    },
    stats: {
      nodes: "Nodes",
      links: "Links",
      focus: "Focus",
      activeLinks: (count) => `${count} active links`,
    },
    save: {
      delivered: "Webhook delivered",
      failed: "The completion webhook failed. Adjust the data and try again.",
      submitting: "Submitting...",
      completeEditing: "Complete editing",
    },
    canvas: {
      kicker: "Blueprint workspace",
      title: "Node canvas",
      subtitle: "Drag to position nodes, connect outputs into inputs, or drop a link on empty space to create the next compatible node in place.",
    },
    footer: {
      summary: "This refit keeps the existing session workflow intact: add nodes from the library, wire them on the canvas, inspect field values on the right, then submit the finished graph.",
    },
    library: {
      kicker: "Palette",
      title: "Node library",
      description: "Search domain nodes, compare categories, and drop building blocks straight onto the canvas.",
      itemCount: (count) => `${count} items`,
      searchPlaceholder: "Search node types, categories, or labels",
      editableFields: (count) => `${count} editable fields`,
      addNode: "Add node",
      emptySearch: "No node types match the current search term.",
    },
    inspector: {
      kicker: "Dock",
      title: "Inspector",
      description: "Adjust graph metadata, edit local settings, and inspect the currently selected node or link without leaving the canvas workflow.",
      tabs: {
        graph: "Graph",
        settings: "Settings",
      },
      graphName: "Graph name",
      graphDescription: "Graph description",
      graphHint: "Use the graph tab for session-level naming and summary text. Node and link details stay in the adjacent selection tab.",
      settingsTitle: "Local global settings",
      settingsDescription: "These preferences are stored only in this browser. They control editor language and edge styling, and are never written into the graph document.",
      languageLabel: "Interface language",
      languageDescription: "Switch built-in editor copy plus node-library, port, and field-template presentation.",
      edgeStyleLabel: "Connection style",
      edgeStyleDescription: "Change how existing and newly created links are drawn on the canvas without affecting the saved graph structure.",
      nodeLabel: "Label",
      nodeDescription: "Description",
      editableFields: "Editable fields",
      linkBadge: "Link",
      linkDetails: "Link details",
      emptySelection: "Select a node or link on the canvas to inspect its details here.",
      fallbackNodeDescription: "This node is ready for field-level adjustments.",
    },
    contextMenu: {
      edit: "Edit",
      pasteNodes: "Paste nodes",
      createConnectedNode: "Create connected node",
      addNode: "Add node",
      noCompatibleNodes: "No compatible node types can complete this connection.",
      noNodesAvailable: "No nodes are available in the current library.",
      copyNode: "Copy node",
      copySelectedNodes: "Copy selected nodes",
      cutNode: "Cut node",
      cutSelectedNodes: "Cut selected nodes",
      deleteEdge: "Delete edge",
      deleteNode: "Delete node",
      deleteSelectedNodes: "Delete selected nodes",
      deleteSelectedEdges: "Delete selected edges",
      deleteSelection: "Delete selection",
    },
    selection: {
      defaultFocusLabel: "Canvas",
      defaultTypeLabel: "canvas focus",
      linkFocusLabel: "link focus",
      formatLinkFocusLabel: (sourceLabel, targetLabel) => `Link ${sourceLabel} -> ${targetLabel}`,
    },
    edgeInspector: {
      defaultHandleLabel: "Default port",
      selectedLinkLabel: "Selected link",
      selectedNodeLabel: "Selected node",
      selectionLabel: "Selection",
      connectionSubtitle: "Connection",
      formatEdgeDescription: (sourceHandle, sourceLabel, targetHandle, targetLabel) =>
        `This link routes ${sourceHandle} from ${sourceLabel} into ${targetHandle} on ${targetLabel}.`,
      edgeIdLabel: "Edge id",
      sourceNodeLabel: "Source node",
      sourceHandleLabel: "Source handle",
      targetNodeLabel: "Target node",
      targetHandleLabel: "Target handle",
    },
    blueprint: {
      fallbackCategory: "Node",
      ready: "Ready",
      fallbackDescription: "This node is ready for field-driven configuration.",
      inputs: "Inputs",
      outputs: "Outputs",
      noInputs: "No inputs",
      noOutputs: "No outputs",
      values: "Values",
      noEditableFields: "No editable fields",
      enabled: "Enabled",
      disabled: "Disabled",
      empty: "Empty",
      items: (count) => `${count} items`,
      configured: "Configured",
      unset: "Unset",
      moreFieldRows: (count) => `+${count} more field rows in inspector`,
    },
    graphDefaults: {
      name: (domain) => `${domain} flow`,
      description: "A new node graph session created by NodeGraph.",
    },
  },
};

export function getEditorMessages(locale: SupportedLocale) {
  return editorMessages[locale];
}
