import { GeneratorSeed, LayerSignal, PreviewFrame } from "./contracts.mjs";

const generatorSeedType = "playground/seed";
const layerSignalType = "playground/layer";
const previewFrameType = "playground/frame";

const categoryKeys = {
  output: "categories.output",
  source: "categories.source",
  transform: "categories.transform",
};

const nodeKeys = {
  colorMix: {
    description: "nodes.colorMix.description",
    label: "nodes.colorMix.label",
  },
  layerFanout: {
    description: "nodes.layerFanout.description",
    label: "nodes.layerFanout.label",
  },
  previewOutput: {
    description: "nodes.previewOutput.description",
    label: "nodes.previewOutput.label",
  },
  seedSource: {
    description: "nodes.seedSource.description",
    label: "nodes.seedSource.label",
  },
  stylizeBranch: {
    description: "nodes.stylizeBranch.description",
    label: "nodes.stylizeBranch.label",
  },
};

const fieldKeys = {
  anchorDate: "fields.anchorDate.label",
  baseTint: "fields.baseTint.label",
  blendMode: "fields.blendMode.label",
  distributionMode: "fields.distributionMode.label",
  frequency: "fields.frequency.label",
  notes: "fields.notes.label",
  opacity: "fields.opacity.label",
  previewShape: "fields.previewShape.label",
  sampleCount: "fields.sampleCount.label",
  seedName: "fields.seedName.label",
  showGrid: "fields.showGrid.label",
  variance: "fields.variance.label",
};

const fieldPlaceholderKeys = {
  notes: "fields.notes.placeholder",
  seedName: "fields.seedName.placeholder",
};

const portKeys = {
  cool: "ports.cool",
  frame: "ports.frame",
  main: "ports.main",
  noise: "ports.noise",
  seed: "ports.seed",
  variant: "ports.variant",
  warm: "ports.warm",
};

const optionLabelKeys = {
  blendMode: {
    difference: "options.blendMode.difference",
    multiply: "options.blendMode.multiply",
    screen: "options.blendMode.screen",
  },
  distributionMode: {
    burst: "options.distributionMode.burst",
    ribbon: "options.distributionMode.ribbon",
    spiral: "options.distributionMode.spiral",
  },
  previewShape: {
    landscape: "options.previewShape.landscape",
    poster: "options.previewShape.poster",
    square: "options.previewShape.square",
  },
};

const nodeAppearances = {
  colorMix: {
    bgColor: "#fce7f3",
    borderColor: "#db2777",
    textColor: "#831843",
  },
  layerFanout: {
    bgColor: "#e0f2fe",
    borderColor: "#0284c7",
    textColor: "#0c4a6e",
  },
  previewOutput: {
    bgColor: "#dcfce7",
    borderColor: "#16a34a",
    textColor: "#14532d",
  },
  seedSource: {
    bgColor: "#fff7ed",
    borderColor: "#f97316",
    textColor: "#7c2d12",
  },
  stylizeBranch: {
    bgColor: "#ede9fe",
    borderColor: "#7c3aed",
    textColor: "#4c1d95",
  },
};

const fieldOptionCatalog = {
  distributionMode: [
    { value: "burst", labelKey: optionLabelKeys.distributionMode.burst },
    { value: "spiral", labelKey: optionLabelKeys.distributionMode.spiral },
    { value: "ribbon", labelKey: optionLabelKeys.distributionMode.ribbon },
  ],
  blendMode: [
    { value: "screen", labelKey: optionLabelKeys.blendMode.screen },
    { value: "multiply", labelKey: optionLabelKeys.blendMode.multiply },
    { value: "difference", labelKey: optionLabelKeys.blendMode.difference },
  ],
  previewShape: [
    { value: "poster", labelKey: optionLabelKeys.previewShape.poster },
    { value: "landscape", labelKey: optionLabelKeys.previewShape.landscape },
    { value: "square", labelKey: optionLabelKeys.previewShape.square },
  ],
};

export const demoI18n = {
  defaultLocale: "en",
  locales: {
    en: {
      [categoryKeys.output]: "Output",
      [categoryKeys.source]: "Source",
      [categoryKeys.transform]: "Transform",
      [fieldKeys.anchorDate]: "Anchor date",
      [fieldKeys.baseTint]: "Base tint",
      [fieldKeys.blendMode]: "Blend mode",
      [fieldKeys.distributionMode]: "Distribution mode",
      [fieldKeys.frequency]: "Frequency",
      [fieldKeys.notes]: "Notes",
      [fieldKeys.opacity]: "Opacity",
      [fieldKeys.previewShape]: "Preview shape",
      [fieldKeys.sampleCount]: "Sample count",
      [fieldKeys.seedName]: "Seed name",
      [fieldKeys.showGrid]: "Show grid",
      [fieldKeys.variance]: "Variance",
      [fieldPlaceholderKeys.notes]: "Describe the mood or composition idea",
      [fieldPlaceholderKeys.seedName]: "Name this generator seed",
      [nodeKeys.colorMix.description]:
        "Blend warm, cool, and noise layers into a frame-ready composition.",
      [nodeKeys.colorMix.label]: "Color Mix",
      [nodeKeys.layerFanout.description]:
        "Expand one seed into multiple tonal channels for downstream composition.",
      [nodeKeys.layerFanout.label]: "Layer Fanout",
      [nodeKeys.previewOutput.description]:
        "Collect the main and variant frames for final preview output.",
      [nodeKeys.previewOutput.label]: "Preview Output",
      [nodeKeys.seedSource.description]:
        "Create the base seed metadata that drives the rest of the playground.",
      [nodeKeys.seedSource.label]: "Seed Source",
      [nodeKeys.stylizeBranch.description]:
        "Branch the frame into primary and alternate stylized variants.",
      [nodeKeys.stylizeBranch.label]: "Stylize Branch",
      [optionLabelKeys.blendMode.difference]: "Difference blend",
      [optionLabelKeys.blendMode.multiply]: "Multiply overlay",
      [optionLabelKeys.blendMode.screen]: "Screen wash",
      [optionLabelKeys.distributionMode.burst]: "Burst scatter",
      [optionLabelKeys.distributionMode.ribbon]: "Ribbon sweep",
      [optionLabelKeys.distributionMode.spiral]: "Spiral drift",
      [optionLabelKeys.previewShape.landscape]: "Landscape canvas",
      [optionLabelKeys.previewShape.poster]: "Poster portrait",
      [optionLabelKeys.previewShape.square]: "Square tile",
      [portKeys.cool]: "Cool",
      [portKeys.frame]: "Frame",
      [portKeys.main]: "Main",
      [portKeys.noise]: "Noise",
      [portKeys.seed]: "Seed",
      [portKeys.variant]: "Variant",
      [portKeys.warm]: "Warm",
    },
    "zh-CN": {
      [categoryKeys.output]: "输出",
      [categoryKeys.source]: "源节点",
      [categoryKeys.transform]: "变换",
      [fieldKeys.anchorDate]: "锚点日期",
      [fieldKeys.baseTint]: "基础色调",
      [fieldKeys.blendMode]: "混合模式",
      [fieldKeys.distributionMode]: "分布模式",
      [fieldKeys.frequency]: "频率",
      [fieldKeys.notes]: "备注",
      [fieldKeys.opacity]: "透明度",
      [fieldKeys.previewShape]: "预览比例",
      [fieldKeys.sampleCount]: "采样数量",
      [fieldKeys.seedName]: "种子名称",
      [fieldKeys.showGrid]: "显示网格",
      [fieldKeys.variance]: "随机波动",
      [fieldPlaceholderKeys.notes]: "记录这次视觉实验的氛围、质感或布局想法",
      [fieldPlaceholderKeys.seedName]: "给这组生成参数起个名字",
      [nodeKeys.colorMix.description]: "把暖色、冷色与噪声图层混合成可继续处理的画面。",
      [nodeKeys.colorMix.label]: "色彩混合",
      [nodeKeys.layerFanout.description]: "把单个种子拆分成多条色调通道，供后续合成使用。",
      [nodeKeys.layerFanout.label]: "图层分发",
      [nodeKeys.previewOutput.description]: "收集主输出和变体输出，作为最终预览节点。",
      [nodeKeys.previewOutput.label]: "预览输出",
      [nodeKeys.seedSource.description]: "创建驱动整个视觉 playground 的基础种子信息。",
      [nodeKeys.seedSource.label]: "种子源",
      [nodeKeys.stylizeBranch.description]: "把画面分支成主风格与变体风格两路输出。",
      [nodeKeys.stylizeBranch.label]: "风格分支",
      [optionLabelKeys.blendMode.difference]: "差值混合",
      [optionLabelKeys.blendMode.multiply]: "正片叠底",
      [optionLabelKeys.blendMode.screen]: "滤色叠加",
      [optionLabelKeys.distributionMode.burst]: "爆发散射",
      [optionLabelKeys.distributionMode.ribbon]: "丝带扫掠",
      [optionLabelKeys.distributionMode.spiral]: "螺旋漂移",
      [optionLabelKeys.previewShape.landscape]: "横向画布",
      [optionLabelKeys.previewShape.poster]: "海报竖幅",
      [optionLabelKeys.previewShape.square]: "方形画布",
      [portKeys.cool]: "冷色",
      [portKeys.frame]: "画面",
      [portKeys.main]: "主输出",
      [portKeys.noise]: "噪声",
      [portKeys.seed]: "种子",
      [portKeys.variant]: "变体",
      [portKeys.warm]: "暖色",
    },
  },
};

function resolveLocale(locale) {
  return demoI18n.locales[locale] ? locale : demoI18n.defaultLocale;
}

function translate(locale, key) {
  const activeLocale = resolveLocale(locale);
  return demoI18n.locales[activeLocale]?.[key] ?? demoI18n.locales[demoI18n.defaultLocale]?.[key] ?? key;
}

/**
 * 统一构造端口定义，保持 Demo library 与 existing graph 使用相同结构。
 */
function createPort(id, labelKey, dataType) {
  return dataType ? { id, labelKey, dataType } : { id, labelKey };
}

/**
 * 按 NodeGraph schema 生成字段定义。
 * select 字段通过 optionsEndpoint 指向 Demo 自己的远端选项接口。
 */
function createField({ key, labelKey, kind, defaultValue, optionsEndpoint, placeholderKey }) {
  const field = { key, labelKey, kind };

  if (defaultValue !== undefined) {
    field.defaultValue = defaultValue;
  }

  if (optionsEndpoint) {
    field.optionsEndpoint = optionsEndpoint;
  }

  if (placeholderKey) {
    field.placeholderKey = placeholderKey;
  }

  return field;
}

/**
 * 生成 existing graph 中保存的节点快照。
 * label 与 description 会预先展开为默认语言，方便直接展示。
 */
function createStoredNode({
  id,
  nodeType,
  position,
  labelKey,
  descriptionKey,
  categoryKey,
  inputs = [],
  outputs = [],
  values = {},
  appearance,
}) {
  return {
    id,
    type: "default",
    position,
    data: {
      label: translate("en", labelKey),
      labelKey,
      description: translate("en", descriptionKey),
      descriptionKey,
      categoryKey,
      nodeType,
      inputs,
      outputs,
      values,
      appearance,
    },
    style: {
      background: appearance?.bgColor,
      borderColor: appearance?.borderColor,
      borderRadius: 20,
      borderWidth: 1,
      color: appearance?.textColor,
    },
  };
}

function createFieldOptionsEndpoint(baseUrl, fieldKey) {
  return new URL(`/api/node-field-options/${encodeURIComponent(fieldKey)}`, `${baseUrl}/`).toString();
}

function createNodeLibrary(baseUrl) {
  return [
    {
      type: "seed_source",
      labelKey: nodeKeys.seedSource.label,
      descriptionKey: nodeKeys.seedSource.description,
      categoryKey: categoryKeys.source,
      inputs: [],
      outputs: [createPort("seed", portKeys.seed, generatorSeedType)],
      fields: [
        createField({
          key: "seedName",
          labelKey: fieldKeys.seedName,
          kind: "text",
          defaultValue: "Aurora Seed",
          placeholderKey: fieldPlaceholderKeys.seedName,
        }),
        createField({
          key: "notes",
          labelKey: fieldKeys.notes,
          kind: "textarea",
          defaultValue: "Start from layered gradients and leave room for a sharp highlight.",
          placeholderKey: fieldPlaceholderKeys.notes,
        }),
        createField({
          key: "anchorDate",
          labelKey: fieldKeys.anchorDate,
          kind: "date",
          defaultValue: "2026-03-21",
        }),
      ],
      appearance: nodeAppearances.seedSource,
    },
    {
      type: "layer_fanout",
      labelKey: nodeKeys.layerFanout.label,
      descriptionKey: nodeKeys.layerFanout.description,
      categoryKey: categoryKeys.transform,
      inputs: [createPort("seed", portKeys.seed, generatorSeedType)],
      outputs: [
        createPort("warm", portKeys.warm, layerSignalType),
        createPort("cool", portKeys.cool, layerSignalType),
        createPort("noise", portKeys.noise, layerSignalType),
      ],
      fields: [
        createField({
          key: "distributionMode",
          labelKey: fieldKeys.distributionMode,
          kind: "select",
          defaultValue: "spiral",
          optionsEndpoint: createFieldOptionsEndpoint(baseUrl, "distributionMode"),
        }),
        createField({
          key: "sampleCount",
          labelKey: fieldKeys.sampleCount,
          kind: "int",
          defaultValue: 24,
        }),
        createField({
          key: "variance",
          labelKey: fieldKeys.variance,
          kind: "float",
          defaultValue: 0.35,
        }),
      ],
      appearance: nodeAppearances.layerFanout,
    },
    {
      type: "color_mix",
      labelKey: nodeKeys.colorMix.label,
      descriptionKey: nodeKeys.colorMix.description,
      categoryKey: categoryKeys.transform,
      inputs: [
        createPort("warm", portKeys.warm, layerSignalType),
        createPort("cool", portKeys.cool, layerSignalType),
        createPort("noise", portKeys.noise, layerSignalType),
      ],
      outputs: [createPort("frame", portKeys.frame, previewFrameType)],
      fields: [
        createField({
          key: "baseTint",
          labelKey: fieldKeys.baseTint,
          kind: "color",
          defaultValue: "#ff9d1c",
        }),
        createField({
          key: "blendMode",
          labelKey: fieldKeys.blendMode,
          kind: "select",
          defaultValue: "screen",
          optionsEndpoint: createFieldOptionsEndpoint(baseUrl, "blendMode"),
        }),
        createField({
          key: "opacity",
          labelKey: fieldKeys.opacity,
          kind: "decimal",
          defaultValue: "0.82",
        }),
      ],
      appearance: nodeAppearances.colorMix,
    },
    {
      type: "stylize_branch",
      labelKey: nodeKeys.stylizeBranch.label,
      descriptionKey: nodeKeys.stylizeBranch.description,
      categoryKey: categoryKeys.transform,
      inputs: [createPort("frame", portKeys.frame, previewFrameType)],
      outputs: [
        createPort("main", portKeys.main, previewFrameType),
        createPort("variant", portKeys.variant, previewFrameType),
      ],
      fields: [
        createField({
          key: "frequency",
          labelKey: fieldKeys.frequency,
          kind: "double",
          defaultValue: 1.75,
        }),
      ],
      appearance: nodeAppearances.stylizeBranch,
    },
    {
      type: "preview_output",
      labelKey: nodeKeys.previewOutput.label,
      descriptionKey: nodeKeys.previewOutput.description,
      categoryKey: categoryKeys.output,
      inputs: [
        createPort("main", portKeys.main, previewFrameType),
        createPort("variant", portKeys.variant, previewFrameType),
      ],
      outputs: [],
      fields: [
        createField({
          key: "showGrid",
          labelKey: fieldKeys.showGrid,
          kind: "boolean",
          defaultValue: true,
        }),
        createField({
          key: "previewShape",
          labelKey: fieldKeys.previewShape,
          kind: "select",
          defaultValue: "poster",
          optionsEndpoint: createFieldOptionsEndpoint(baseUrl, "previewShape"),
        }),
      ],
      appearance: nodeAppearances.previewOutput,
    },
  ];
}

export const demoTypeMappings = [
  {
    canonicalId: generatorSeedType,
    type: GeneratorSeed.name,
    color: "#f97316",
  },
  {
    canonicalId: layerSignalType,
    type: LayerSignal.name,
    color: "#0284c7",
  },
  {
    canonicalId: previewFrameType,
    type: PreviewFrame.name,
    color: "#16a34a",
  },
];

/**
 * 根据当前配置生成节点库响应，确保 select 字段总是返回可访问的绝对 optionsEndpoint。
 */
export function createDemoLibraryBundle(config) {
  return {
    nodes: createNodeLibrary(config.demoClientBaseUrl),
    i18n: demoI18n,
    typeMappings: demoTypeMappings,
  };
}

/**
 * 返回 Demo 自己托管的 select 字段选项。
 * NodeGraph 代理请求时会附带额外查询参数，这里只消费 locale 并忽略其余参数。
 */
export function createDemoFieldOptionsPayload(fieldKey, locale) {
  const entries = fieldOptionCatalog[fieldKey];
  if (!entries) {
    return null;
  }

  return {
    options: entries.map((entry) => ({
      value: entry.value,
      label: translate(locale, entry.labelKey),
    })),
  };
}

export function createGraphDocument(graphName, graphMode = "new") {
  if (graphMode === "existing") {
    return createExistingGraph(graphName);
  }

  return {
    name: graphName,
    description: "Created from the NodeGraph visual playground demo client.",
    nodes: [],
    edges: [],
    viewport: {
      x: 0,
      y: 0,
      zoom: 1,
    },
  };
}

function createExistingGraph(graphName) {
  return {
    graphId: "demo-existing-playground-graph",
    name: graphName,
    description: "A pre-filled visual playground used to demonstrate typed node editors.",
    nodes: [
      createStoredNode({
        id: "node_seed_source",
        nodeType: "seed_source",
        position: { x: 80, y: 220 },
        labelKey: nodeKeys.seedSource.label,
        descriptionKey: nodeKeys.seedSource.description,
        categoryKey: categoryKeys.source,
        outputs: [createPort("seed", portKeys.seed, generatorSeedType)],
        values: {
          seedName: "Aurora Seed",
          notes: "Lean toward neon fog, long highlights, and one asymmetric focal point.",
          anchorDate: "2026-03-21",
        },
        appearance: nodeAppearances.seedSource,
      }),
      createStoredNode({
        id: "node_layer_fanout",
        nodeType: "layer_fanout",
        position: { x: 360, y: 220 },
        labelKey: nodeKeys.layerFanout.label,
        descriptionKey: nodeKeys.layerFanout.description,
        categoryKey: categoryKeys.transform,
        inputs: [createPort("seed", portKeys.seed, generatorSeedType)],
        outputs: [
          createPort("warm", portKeys.warm, layerSignalType),
          createPort("cool", portKeys.cool, layerSignalType),
          createPort("noise", portKeys.noise, layerSignalType),
        ],
        values: {
          distributionMode: "spiral",
          sampleCount: 24,
          variance: 0.35,
        },
        appearance: nodeAppearances.layerFanout,
      }),
      createStoredNode({
        id: "node_color_mix",
        nodeType: "color_mix",
        position: { x: 740, y: 220 },
        labelKey: nodeKeys.colorMix.label,
        descriptionKey: nodeKeys.colorMix.description,
        categoryKey: categoryKeys.transform,
        inputs: [
          createPort("warm", portKeys.warm, layerSignalType),
          createPort("cool", portKeys.cool, layerSignalType),
          createPort("noise", portKeys.noise, layerSignalType),
        ],
        outputs: [createPort("frame", portKeys.frame, previewFrameType)],
        values: {
          baseTint: "#ff9d1c",
          blendMode: "screen",
          opacity: "0.82",
        },
        appearance: nodeAppearances.colorMix,
      }),
      createStoredNode({
        id: "node_stylize_branch",
        nodeType: "stylize_branch",
        position: { x: 1080, y: 220 },
        labelKey: nodeKeys.stylizeBranch.label,
        descriptionKey: nodeKeys.stylizeBranch.description,
        categoryKey: categoryKeys.transform,
        inputs: [createPort("frame", portKeys.frame, previewFrameType)],
        outputs: [
          createPort("main", portKeys.main, previewFrameType),
          createPort("variant", portKeys.variant, previewFrameType),
        ],
        values: {
          frequency: 1.75,
        },
        appearance: nodeAppearances.stylizeBranch,
      }),
      createStoredNode({
        id: "node_preview_output",
        nodeType: "preview_output",
        position: { x: 1420, y: 220 },
        labelKey: nodeKeys.previewOutput.label,
        descriptionKey: nodeKeys.previewOutput.description,
        categoryKey: categoryKeys.output,
        inputs: [
          createPort("main", portKeys.main, previewFrameType),
          createPort("variant", portKeys.variant, previewFrameType),
        ],
        values: {
          showGrid: true,
          previewShape: "poster",
        },
        appearance: nodeAppearances.previewOutput,
      }),
    ],
    edges: [
      {
        id: "edge_seed_source_layer_fanout",
        source: "node_seed_source",
        sourceHandle: "seed",
        target: "node_layer_fanout",
        targetHandle: "seed",
      },
      {
        id: "edge_layer_fanout_color_mix_warm",
        source: "node_layer_fanout",
        sourceHandle: "warm",
        target: "node_color_mix",
        targetHandle: "warm",
      },
      {
        id: "edge_layer_fanout_color_mix_cool",
        source: "node_layer_fanout",
        sourceHandle: "cool",
        target: "node_color_mix",
        targetHandle: "cool",
      },
      {
        id: "edge_layer_fanout_color_mix_noise",
        source: "node_layer_fanout",
        sourceHandle: "noise",
        target: "node_color_mix",
        targetHandle: "noise",
      },
      {
        id: "edge_color_mix_stylize_branch",
        source: "node_color_mix",
        sourceHandle: "frame",
        target: "node_stylize_branch",
        targetHandle: "frame",
      },
      {
        id: "edge_stylize_branch_preview_output_main",
        source: "node_stylize_branch",
        sourceHandle: "main",
        target: "node_preview_output",
        targetHandle: "main",
      },
      {
        id: "edge_stylize_branch_preview_output_variant",
        source: "node_stylize_branch",
        sourceHandle: "variant",
        target: "node_preview_output",
        targetHandle: "variant",
      },
    ],
    viewport: {
      x: 120,
      y: 40,
      zoom: 0.72,
    },
  };
}
