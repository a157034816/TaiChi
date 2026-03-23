import { NodeGraphRuntime } from "../../../SDK/NodeGraph/javascript/index.js";

export const helloTextType = "hello/text";
export const demoLibraryVersion = "hello-world@1";
export const defaultExistingGraphName = "Hello World Pipeline";
export const defaultNewGraphName = "Blank Hello World Graph";
export const defaultDebugBreakpoints = ["node_output"];

function createPort(id, label, dataType = helloTextType) {
  return { id, label, dataType };
}

function createStoredNode({
  id,
  label,
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
    type: String.name,
    color: "#2563eb",
  });

  runtime.registerNode({
    type: "greeting_source",
    displayName: "Greeting Source",
    description: "Create the greeting text that will be sent to the output node.",
    category: "Hello World",
    outputs: [createPort("text", "Text")],
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
      const name = typeof context.values.name === "string" && context.values.name.trim()
        ? context.values.name.trim()
        : "World";
      context.emit("text", `Hello, ${name}!`);
    },
  });

  runtime.registerNode({
    type: "console_output",
    displayName: "Console Output",
    description: "Collect the final greeting into the runtime result buffer.",
    category: "Hello World",
    inputs: [createPort("text", "Text")],
    appearance: {
      bgColor: "#f0fdf4",
      borderColor: "#16a34a",
      textColor: "#14532d",
    },
    execute(context) {
      context.pushResult("console", context.readInput("text") ?? "Hello, World!");
    },
  });

  return runtime;
}

export function createGraphDocument(graphName, graphMode = "existing") {
  if (graphMode === "new") {
    return {
      name: graphName || defaultNewGraphName,
      description: "Start from a blank Hello World graph.",
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
    graphId: "hello-world-demo-graph",
    name: graphName || defaultExistingGraphName,
    description: "A runnable Hello World graph hosted by the JavaScript SDK demo.",
    nodes: [
      createStoredNode({
        id: "node_source",
        label: "Greeting Source",
        nodeType: "greeting_source",
        position: { x: 80, y: 160 },
        outputs: [createPort("text", "Text")],
        values: {
          name: "Codex",
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
        nodeType: "console_output",
        position: { x: 380, y: 160 },
        inputs: [createPort("text", "Text")],
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
        id: "edge_source_output",
        source: "node_source",
        sourceHandle: "text",
        target: "node_output",
        targetHandle: "text",
      },
    ],
    viewport: {
      x: 40,
      y: 20,
      zoom: 0.95,
    },
  };
}
