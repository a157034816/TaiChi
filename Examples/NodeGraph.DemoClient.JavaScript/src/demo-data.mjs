import { NodeGraphRuntime } from "../../../SDK/NodeGraph/javascript/index.js";

/**
 * Demo Showcase 运行时与默认图数据。
 *
 * 该文件的目标是：用尽量少的节点和连线，覆盖节点编辑器最核心的能力：
 * - 多 canonical 类型（端口着色、类型提示）
 * - 多种字段编辑器（text/textarea/boolean/select/int/double/date/color/decimal）
 * - 多输入触发的执行语义（通过节点状态避免重复发射）
 * - 可运行的默认 Showcase 图（existing 模式）
 */
export const helloTextType = "hello/text";
export const demoNumberType = "demo/number";
export const demoBooleanType = "demo/boolean";
export const demoDateType = "demo/date";
export const demoColorType = "demo/color";
export const demoDecimalType = "demo/decimal";

export const demoLibraryVersion = "demo-showcase@1";
export const defaultExistingGraphName = "Demo Showcase Pipeline";
export const defaultNewGraphName = "Blank Demo Showcase Graph";
export const defaultDebugBreakpoints = ["node_output"];

/**
 * 创建端口定义（用于节点库与存储图数据）。
 */
function createPort(id, label, dataType) {
  return { id, label, dataType };
}

/**
 * 创建可被 NodeGraph 编辑器直接加载的“存储节点”结构。
 *
 * `appearance` 用于提供语义化配色；`style` 则用于兼容/覆盖 ReactFlow 样式渲染。
 */
function createStoredNode({
  id,
  label,
  description,
  category,
  nodeType,
  position,
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
      label,
      description,
      category,
      nodeType,
      inputs,
      outputs,
      values,
      appearance,
    },
    style: appearance
      ? {
          background: appearance.bgColor,
          borderColor: appearance.borderColor,
          borderRadius: 18,
          borderWidth: 1,
          color: appearance.textColor,
        }
      : undefined,
  };
}

function isBlankString(value) {
  return typeof value !== "string" || !value.trim();
}

/**
 * 将输入值尽量规整成非空字符串。
 */
function coerceString(value, fallback) {
  if (typeof value === "string" && value.trim()) {
    return value.trim();
  }

  return fallback;
}

/**
 * 将输入值尽量规整成“非空字符串”，但保留用户输入的首尾空白。
 *
 * 适用于需要精确保留空格/换行的字段（例如前缀、模板等）。
 */
function coerceNonBlankString(value, fallback) {
  if (typeof value === "string" && value.trim()) {
    return value;
  }

  return fallback;
}

/**
 * 将输入值尽量规整成布尔值。
 */
function coerceBoolean(value, fallback) {
  if (typeof value === "boolean") {
    return value;
  }

  if (typeof value === "string") {
    const normalized = value.trim().toLowerCase();
    if (normalized === "true") {
      return true;
    }
    if (normalized === "false") {
      return false;
    }
  }

  if (typeof value === "number") {
    return value !== 0;
  }

  return fallback;
}

/**
 * 将输入值尽量规整成数字。
 */
function coerceNumber(value, fallback) {
  const parsed = typeof value === "number" ? value : Number(value);
  return Number.isFinite(parsed) ? parsed : fallback;
}

/**
 * 标记当前节点在本次执行过程中已经输出过（避免多输入触发导致重复 emit）。
 */
function markEmittedOnce(context) {
  context.state.__emitted = true;
}

/**
 * 判断当前节点在本次执行过程中是否已经输出过。
 */
function hasEmittedOnce(context) {
  return context.state.__emitted === true;
}

/**
 * 用 `{token}` 形式的占位符替换模板中的字段。
 *
 * - 未提供的 token 保持原样
 * - `null/undefined` 会被替换为空字符串
 */
function replaceTemplateTokens(template, replacements) {
  return template.replace(/\{([^}]+)\}/g, (match, token) => {
    if (Object.prototype.hasOwnProperty.call(replacements, token)) {
      const value = replacements[token];
      return value === undefined || value === null ? "" : String(value);
    }

    return match;
  });
}

/**
 * 创建 Demo Showcase 运行时（内置节点库 + 可执行逻辑）。
 *
 * 注意：为了在 Demo 中稳定展示“多输入触发”的语义，部分节点使用 `context.state.__emitted`
 * 做“只输出一次”的门控，防止同一节点在输入逐个到达时重复 emit。
 */
export function createHelloWorldRuntime(config, overrides = {}) {
  const runtime = new NodeGraphRuntime({
    domain: config.demoDomain,
    clientName: config.clientName,
    controlBaseUrl: `${config.demoClientBaseUrl}/api/runtime`,
    libraryVersion: overrides.libraryVersion ?? demoLibraryVersion,
    runtimeId: overrides.runtimeId,
    now: overrides.now,
  });

  runtime.registerTypeMapping({
    canonicalId: helloTextType,
    type: "DemoText",
    color: "#2563eb",
  });

  runtime.registerTypeMapping({
    canonicalId: demoNumberType,
    type: "DemoNumber",
    color: "#f97316",
  });

  runtime.registerTypeMapping({
    canonicalId: demoBooleanType,
    type: "DemoBoolean",
    color: "#22c55e",
  });

  runtime.registerTypeMapping({
    canonicalId: demoDateType,
    type: "DemoDate",
    color: "#a855f7",
  });

  runtime.registerTypeMapping({
    canonicalId: demoColorType,
    type: "DemoColor",
    color: "#e11d48",
  });

  runtime.registerTypeMapping({
    canonicalId: demoDecimalType,
    type: "DemoDecimal",
    color: "#06b6d4",
  });

  runtime.registerNode({
    type: "greeting_source",
    displayName: "Greeting Source",
    description: "Create the greeting text that will be sent to the output node.",
    category: "Hello World",
    outputs: [createPort("text", "Text", helloTextType)],
    fields: [
      {
        key: "name",
        label: "Name",
        kind: "text",
        defaultValue: "World",
        placeholder: "Who should be greeted?",
      },
    ],
    appearance: {
      bgColor: "#eff6ff",
      borderColor: "#2563eb",
      textColor: "#1e3a8a",
    },
    execute(context) {
      const name = coerceString(context.values.name, "World");
      context.emit("text", `Hello, ${name}!`);
    },
  });

  runtime.registerNode({
    type: "console_output",
    displayName: "Console Output",
    description: "Collect the final greeting into the runtime result buffer.",
    category: "Debug",
    inputs: [createPort("text", "Text", helloTextType)],
    appearance: {
      bgColor: "#f0fdf4",
      borderColor: "#16a34a",
      textColor: "#14532d",
    },
    execute(context) {
      context.pushResult("console", context.readInput("text") ?? "Hello, World!");
    },
  });

  runtime.registerNode({
    type: "demo_source",
    displayName: "Demo Source",
    description: "Emit a bundle of typed values so downstream nodes can demonstrate rich editor features.",
    category: "Playground",
    outputs: [
      createPort("name", "Name", helloTextType),
      createPort("punctuation", "Punctuation", helloTextType),
      createPort("enabled", "Enabled", demoBooleanType),
      createPort("baseNumber", "Base", demoNumberType),
      createPort("delta", "Delta", demoNumberType),
      createPort("today", "Date", demoDateType),
      createPort("theme", "Theme", demoColorType),
      createPort("amount", "Amount", demoDecimalType),
    ],
    fields: [
      {
        key: "name",
        label: "Name",
        kind: "text",
        defaultValue: "Codex",
        placeholder: "The name used in the greeting pipeline.",
      },
      {
        key: "punctuation",
        label: "Punctuation",
        kind: "select",
        defaultValue: "!",
        optionsEndpoint: `${config.demoClientBaseUrl}/api/runtime/field-options`,
      },
      {
        key: "enabled",
        label: "Enabled",
        kind: "boolean",
        defaultValue: true,
      },
      {
        key: "baseNumber",
        label: "Base Number",
        kind: "int",
        defaultValue: 7,
      },
      {
        key: "delta",
        label: "Delta",
        kind: "double",
        defaultValue: 5,
      },
      {
        key: "today",
        label: "Date",
        kind: "date",
        defaultValue: "2026-03-21",
      },
      {
        key: "theme",
        label: "Theme Color",
        kind: "color",
        defaultValue: "#2563eb",
      },
      {
        key: "amount",
        label: "Amount (decimal string)",
        kind: "decimal",
        defaultValue: "123.45",
      },
    ],
    appearance: {
      bgColor: "#0b1220",
      borderColor: "#38bdf8",
      textColor: "#e0f2fe",
    },
    execute(context) {
      const name = coerceString(context.values.name, "Codex");
      const punctuation = coerceString(context.values.punctuation, "!");
      const enabled = coerceBoolean(context.values.enabled, true);
      const baseNumber = coerceNumber(context.values.baseNumber, 7);
      const delta = coerceNumber(context.values.delta, 5);
      const today = coerceString(context.values.today, "2026-03-21");
      const theme = coerceString(context.values.theme, "#2563eb");
      const amount = coerceString(context.values.amount, "123.45");

      context.emit("name", name);
      context.emit("punctuation", punctuation);
      context.emit("enabled", enabled);
      context.emit("baseNumber", baseNumber);
      context.emit("delta", delta);
      context.emit("today", today);
      context.emit("theme", theme);
      context.emit("amount", amount);
    },
  });

  runtime.registerNode({
    type: "greeting_builder",
    displayName: "Greeting Builder",
    description: "Build a greeting message from inputs and emit it only when both inputs are ready.",
    category: "Text",
    inputs: [
      createPort("name", "Name", helloTextType),
      createPort("punctuation", "Punctuation", helloTextType),
    ],
    outputs: [createPort("text", "Greeting", helloTextType)],
    fields: [
      {
        key: "prefix",
        label: "Prefix",
        kind: "text",
        defaultValue: "Hello, ",
        placeholder: "Text inserted before the name.",
      },
    ],
    appearance: {
      bgColor: "#eff6ff",
      borderColor: "#2563eb",
      textColor: "#1e3a8a",
    },
    execute(context) {
      if (hasEmittedOnce(context)) {
        return;
      }

      const name = context.readInput("name");
      const punctuation = context.readInput("punctuation");
      if (name === undefined || punctuation === undefined) {
        return;
      }

      const prefix = coerceNonBlankString(context.values.prefix, "Hello, ");
      context.emit("text", `${prefix}${coerceString(name, "World")}${coerceString(punctuation, "!")}`);
      markEmittedOnce(context);
    },
  });

  runtime.registerNode({
    type: "math_add",
    displayName: "Add Numbers",
    description: "Add two numeric inputs and emit a sum once both values are available.",
    category: "Math",
    inputs: [
      createPort("a", "A", demoNumberType),
      createPort("b", "B", demoNumberType),
    ],
    outputs: [createPort("sum", "Sum", demoNumberType)],
    appearance: {
      bgColor: "#fff7ed",
      borderColor: "#f97316",
      textColor: "#7c2d12",
    },
    execute(context) {
      if (hasEmittedOnce(context)) {
        return;
      }

      const a = context.readInput("a");
      const b = context.readInput("b");
      if (a === undefined || b === undefined) {
        return;
      }

      context.emit("sum", coerceNumber(a, 0) + coerceNumber(b, 0));
      markEmittedOnce(context);
    },
  });

  runtime.registerNode({
    type: "if_text",
    displayName: "If (Text)",
    description: "Select between two text branches based on a boolean condition.",
    category: "Logic",
    inputs: [
      createPort("condition", "Condition", demoBooleanType),
      createPort("whenTrue", "When True", helloTextType),
      createPort("whenFalse", "When False", helloTextType),
    ],
    outputs: [createPort("text", "Text", helloTextType)],
    fields: [
      {
        key: "fallback",
        label: "Fallback",
        kind: "text",
        defaultValue: "(disabled)",
        placeholder: "Used when condition=false and the whenFalse port is not connected.",
      },
    ],
    appearance: {
      bgColor: "#f0fdf4",
      borderColor: "#22c55e",
      textColor: "#14532d",
    },
    execute(context) {
      if (hasEmittedOnce(context)) {
        return;
      }

      const condition = context.readInput("condition");
      const whenTrue = context.readInput("whenTrue");
      if (condition === undefined || whenTrue === undefined) {
        return;
      }

      const conditionValue = coerceBoolean(condition, false);
      if (conditionValue) {
        context.emit("text", coerceString(whenTrue, ""));
      } else {
        const whenFalse = context.readInput("whenFalse");
        if (whenFalse !== undefined && !isBlankString(whenFalse)) {
          context.emit("text", coerceString(whenFalse, ""));
        } else {
          context.emit("text", coerceString(context.values.fallback, "(disabled)"));
        }
      }

      markEmittedOnce(context);
    },
  });

  runtime.registerNode({
    type: "text_interpolate",
    displayName: "Text Interpolate",
    description: "Render a multi-line template by interpolating typed inputs.",
    category: "Text",
    inputs: [
      createPort("greeting", "Greeting", helloTextType),
      createPort("lucky", "Lucky Number", demoNumberType),
      createPort("today", "Date", demoDateType),
      createPort("theme", "Theme Color", demoColorType),
      createPort("amount", "Amount", demoDecimalType),
    ],
    outputs: [createPort("text", "Text", helloTextType)],
    fields: [
      {
        key: "template",
        label: "Template",
        kind: "textarea",
        defaultValue: "Greeting: {greeting}\nLucky: {lucky}\nDate: {today}\nTheme: {theme}\nAmount: {amount}",
        placeholder: "Use tokens like {greeting} to interpolate values.",
      },
    ],
    appearance: {
      bgColor: "#eff6ff",
      borderColor: "#2563eb",
      textColor: "#1e3a8a",
    },
    execute(context) {
      if (hasEmittedOnce(context)) {
        return;
      }

      const greeting = context.readInput("greeting");
      const lucky = context.readInput("lucky");
      const today = context.readInput("today");
      const theme = context.readInput("theme");
      const amount = context.readInput("amount");

      if ([greeting, lucky, today, theme, amount].some((value) => value === undefined)) {
        return;
      }

      const template = coerceString(
        context.values.template,
        "Greeting: {greeting}\nLucky: {lucky}\nDate: {today}\nTheme: {theme}\nAmount: {amount}",
      );

      const rendered = replaceTemplateTokens(template, {
        greeting: coerceString(greeting, ""),
        lucky: coerceNumber(lucky, 0),
        today: coerceString(today, ""),
        theme: coerceString(theme, ""),
        amount: coerceString(amount, ""),
      });

      context.emit("text", rendered);
      markEmittedOnce(context);
    },
  });

  runtime.registerNode({
    type: "const_text",
    displayName: "Const (Text)",
    description: "Emit a constant text value.",
    category: "Inputs",
    outputs: [createPort("text", "Text", helloTextType)],
    fields: [
      {
        key: "text",
        label: "Text",
        kind: "textarea",
        defaultValue: "Hello",
        placeholder: "Constant text emitted by this node.",
      },
    ],
    appearance: {
      bgColor: "#eff6ff",
      borderColor: "#2563eb",
      textColor: "#1e3a8a",
    },
    execute(context) {
      if (hasEmittedOnce(context)) {
        return;
      }

      context.emit("text", coerceString(context.values.text, ""));
      markEmittedOnce(context);
    },
  });

  runtime.registerNode({
    type: "const_number",
    displayName: "Const (Number)",
    description: "Emit a constant numeric value.",
    category: "Inputs",
    outputs: [createPort("number", "Number", demoNumberType)],
    fields: [
      {
        key: "value",
        label: "Value",
        kind: "double",
        defaultValue: 0,
        placeholder: "Constant number emitted by this node.",
      },
    ],
    appearance: {
      bgColor: "#fff7ed",
      borderColor: "#f97316",
      textColor: "#7c2d12",
    },
    execute(context) {
      if (hasEmittedOnce(context)) {
        return;
      }

      context.emit("number", coerceNumber(context.values.value, 0));
      markEmittedOnce(context);
    },
  });

  runtime.registerNode({
    type: "const_boolean",
    displayName: "Const (Boolean)",
    description: "Emit a constant boolean value.",
    category: "Inputs",
    outputs: [createPort("value", "Value", demoBooleanType)],
    fields: [
      {
        key: "value",
        label: "Value",
        kind: "boolean",
        defaultValue: true,
      },
    ],
    appearance: {
      bgColor: "#f0fdf4",
      borderColor: "#22c55e",
      textColor: "#14532d",
    },
    execute(context) {
      if (hasEmittedOnce(context)) {
        return;
      }

      context.emit("value", coerceBoolean(context.values.value, true));
      markEmittedOnce(context);
    },
  });

  runtime.registerNode({
    type: "const_date",
    displayName: "Const (Date)",
    description: "Emit a constant date string (YYYY-MM-DD).",
    category: "Inputs",
    outputs: [createPort("date", "Date", demoDateType)],
    fields: [
      {
        key: "date",
        label: "Date",
        kind: "date",
        defaultValue: "2026-03-21",
      },
    ],
    appearance: {
      bgColor: "#faf5ff",
      borderColor: "#a855f7",
      textColor: "#581c87",
    },
    execute(context) {
      if (hasEmittedOnce(context)) {
        return;
      }

      context.emit("date", coerceString(context.values.date, "2026-03-21"));
      markEmittedOnce(context);
    },
  });

  runtime.registerNode({
    type: "const_color",
    displayName: "Const (Color)",
    description: "Emit a constant color string (#RRGGBB).",
    category: "Inputs",
    outputs: [createPort("color", "Color", demoColorType)],
    fields: [
      {
        key: "color",
        label: "Color",
        kind: "color",
        defaultValue: "#2563eb",
      },
    ],
    appearance: {
      bgColor: "#fff1f2",
      borderColor: "#e11d48",
      textColor: "#881337",
    },
    execute(context) {
      if (hasEmittedOnce(context)) {
        return;
      }

      context.emit("color", coerceString(context.values.color, "#2563eb"));
      markEmittedOnce(context);
    },
  });

  runtime.registerNode({
    type: "const_decimal",
    displayName: "Const (Decimal)",
    description: "Emit a decimal value as a string to demonstrate the decimal field editor.",
    category: "Inputs",
    outputs: [createPort("decimal", "Decimal", demoDecimalType)],
    fields: [
      {
        key: "value",
        label: "Value",
        kind: "decimal",
        defaultValue: "123.45",
        placeholder: "A decimal string like 123.45",
      },
    ],
    appearance: {
      bgColor: "#ecfeff",
      borderColor: "#06b6d4",
      textColor: "#164e63",
    },
    execute(context) {
      if (hasEmittedOnce(context)) {
        return;
      }

      context.emit("decimal", coerceString(context.values.value, "123.45"));
      markEmittedOnce(context);
    },
  });

  return runtime;
}

/**
 * 创建 Demo Showcase 的默认图文档。
 *
 * - `graphMode="existing"`：返回可直接运行的 Showcase Pipeline（6 节点 12 边）
 * - `graphMode="new"`：返回空白图（用于从零搭建）
 */
export function createGraphDocument(graphName, graphMode = "existing") {
  if (graphMode === "new") {
    return {
      name: graphName || defaultNewGraphName,
      description: "Start from a blank Demo Showcase graph.",
      nodes: [],
      edges: [],
      viewport: {
        x: 0,
        y: 0,
        zoom: 1,
      },
    };
  }

  return {
    graphId: "demo-showcase-graph",
    name: graphName || defaultExistingGraphName,
    description: "A runnable Demo Showcase graph hosted by the JavaScript SDK demo.",
    nodes: [
      createStoredNode({
        id: "node_source",
        label: "Demo Source",
        description: "Emit typed values to drive the showcase pipeline.",
        category: "Playground",
        nodeType: "demo_source",
        position: { x: 80, y: 220 },
        outputs: [
          createPort("name", "Name", helloTextType),
          createPort("punctuation", "Punctuation", helloTextType),
          createPort("enabled", "Enabled", demoBooleanType),
          createPort("baseNumber", "Base", demoNumberType),
          createPort("delta", "Delta", demoNumberType),
          createPort("today", "Date", demoDateType),
          createPort("theme", "Theme", demoColorType),
          createPort("amount", "Amount", demoDecimalType),
        ],
        values: {
          name: "Codex",
          punctuation: "!",
          enabled: true,
          baseNumber: 7,
          delta: 5,
          today: "2026-03-21",
          theme: "#2563eb",
          amount: "123.45",
        },
        appearance: {
          bgColor: "#0b1220",
          borderColor: "#38bdf8",
          textColor: "#e0f2fe",
        },
      }),
      createStoredNode({
        id: "node_greet",
        label: "Greeting Builder",
        description: "Build the greeting message.",
        category: "Text",
        nodeType: "greeting_builder",
        position: { x: 420, y: 140 },
        inputs: [
          createPort("name", "Name", helloTextType),
          createPort("punctuation", "Punctuation", helloTextType),
        ],
        outputs: [createPort("text", "Greeting", helloTextType)],
        values: {
          prefix: "Hello, ",
        },
        appearance: {
          bgColor: "#eff6ff",
          borderColor: "#2563eb",
          textColor: "#1e3a8a",
        },
      }),
      createStoredNode({
        id: "node_add",
        label: "Add Numbers",
        description: "Compute a derived lucky number.",
        category: "Math",
        nodeType: "math_add",
        position: { x: 420, y: 360 },
        inputs: [
          createPort("a", "A", demoNumberType),
          createPort("b", "B", demoNumberType),
        ],
        outputs: [createPort("sum", "Sum", demoNumberType)],
        values: {},
        appearance: {
          bgColor: "#fff7ed",
          borderColor: "#f97316",
          textColor: "#7c2d12",
        },
      }),
      createStoredNode({
        id: "node_gate",
        label: "If (Text)",
        description: "Gate the greeting pipeline with a boolean condition.",
        category: "Logic",
        nodeType: "if_text",
        position: { x: 720, y: 140 },
        inputs: [
          createPort("condition", "Condition", demoBooleanType),
          createPort("whenTrue", "When True", helloTextType),
          createPort("whenFalse", "When False", helloTextType),
        ],
        outputs: [createPort("text", "Text", helloTextType)],
        values: {
          fallback: "(disabled)",
        },
        appearance: {
          bgColor: "#f0fdf4",
          borderColor: "#22c55e",
          textColor: "#14532d",
        },
      }),
      createStoredNode({
        id: "node_format",
        label: "Text Interpolate",
        description: "Combine typed inputs into a multi-line summary.",
        category: "Text",
        nodeType: "text_interpolate",
        position: { x: 980, y: 240 },
        inputs: [
          createPort("greeting", "Greeting", helloTextType),
          createPort("lucky", "Lucky Number", demoNumberType),
          createPort("today", "Date", demoDateType),
          createPort("theme", "Theme Color", demoColorType),
          createPort("amount", "Amount", demoDecimalType),
        ],
        outputs: [createPort("text", "Text", helloTextType)],
        values: {
          template: "Greeting: {greeting}\nLucky: {lucky}\nDate: {today}\nTheme: {theme}\nAmount: {amount}",
        },
        appearance: {
          bgColor: "#eff6ff",
          borderColor: "#2563eb",
          textColor: "#1e3a8a",
        },
      }),
      createStoredNode({
        id: "node_output",
        label: "Console Output",
        description: "Collect the final text into the runtime result buffer.",
        category: "Debug",
        nodeType: "console_output",
        position: { x: 1320, y: 240 },
        inputs: [createPort("text", "Text", helloTextType)],
        values: {},
        appearance: {
          bgColor: "#f0fdf4",
          borderColor: "#16a34a",
          textColor: "#14532d",
        },
      }),
    ],
    edges: [
      {
        id: "edge_source_name",
        source: "node_source",
        sourceHandle: "name",
        target: "node_greet",
        targetHandle: "name",
      },
      {
        id: "edge_source_punct",
        source: "node_source",
        sourceHandle: "punctuation",
        target: "node_greet",
        targetHandle: "punctuation",
      },
      {
        id: "edge_greet_text",
        source: "node_greet",
        sourceHandle: "text",
        target: "node_gate",
        targetHandle: "whenTrue",
      },
      {
        id: "edge_source_enabled",
        source: "node_source",
        sourceHandle: "enabled",
        target: "node_gate",
        targetHandle: "condition",
      },
      {
        id: "edge_source_base",
        source: "node_source",
        sourceHandle: "baseNumber",
        target: "node_add",
        targetHandle: "a",
      },
      {
        id: "edge_source_delta",
        source: "node_source",
        sourceHandle: "delta",
        target: "node_add",
        targetHandle: "b",
      },
      {
        id: "edge_add_sum",
        source: "node_add",
        sourceHandle: "sum",
        target: "node_format",
        targetHandle: "lucky",
      },
      {
        id: "edge_gate_text",
        source: "node_gate",
        sourceHandle: "text",
        target: "node_format",
        targetHandle: "greeting",
      },
      {
        id: "edge_source_today",
        source: "node_source",
        sourceHandle: "today",
        target: "node_format",
        targetHandle: "today",
      },
      {
        id: "edge_source_theme",
        source: "node_source",
        sourceHandle: "theme",
        target: "node_format",
        targetHandle: "theme",
      },
      {
        id: "edge_source_amount",
        source: "node_source",
        sourceHandle: "amount",
        target: "node_format",
        targetHandle: "amount",
      },
      {
        id: "edge_format_text",
        source: "node_format",
        sourceHandle: "text",
        target: "node_output",
        targetHandle: "text",
      },
    ],
    viewport: {
      x: 40,
      y: 80,
      zoom: 0.85,
    },
  };
}
