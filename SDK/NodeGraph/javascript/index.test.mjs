import assert from "node:assert/strict";
import test from "node:test";

import {
  NodeGraphClient,
  NodeGraphRuntime,
} from "./index.js";

function createHelloRuntime({ now } = {}) {
  const runtime = new NodeGraphRuntime({
    domain: "hello-world",
    clientName: "Hello Runtime Host",
    controlBaseUrl: "http://127.0.0.1:4310/runtime",
    libraryVersion: "hello-world@1",
    now,
  });

  runtime.registerNode({
    type: "greeting_source",
    displayName: "Greeting Source",
    description: "Create the base greeting text.",
    category: "Hello World",
    outputs: [{ id: "text", label: "Text", dataType: "hello/text" }],
    fields: [
      {
        key: "name",
        label: "Name",
        kind: "text",
        defaultValue: "World",
      },
    ],
    execute(context) {
      context.emit("text", `Hello, ${context.values.name}!`);
    },
  });

  runtime.registerNode({
    type: "console_output",
    displayName: "Console Output",
    description: "Write the greeting to the host result buffer.",
    category: "Hello World",
    inputs: [{ id: "text", label: "Text", dataType: "hello/text" }],
    execute(context) {
      context.pushResult("console", context.readInput("text"));
    },
  });

  return runtime;
}

function createHelloGraph() {
  return {
    name: "Hello World",
    nodes: [
      {
        id: "node_source",
        type: "default",
        position: { x: 0, y: 0 },
        data: {
          label: "Greeting Source",
          nodeType: "greeting_source",
          outputs: [{ id: "text", label: "Text", dataType: "hello/text" }],
          values: {
            name: "Codex",
          },
        },
      },
      {
        id: "node_output",
        type: "default",
        position: { x: 280, y: 0 },
        data: {
          label: "Console Output",
          nodeType: "console_output",
          inputs: [{ id: "text", label: "Text", dataType: "hello/text" }],
          values: {},
        },
      },
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
    viewport: { x: 0, y: 0, zoom: 1 },
  };
}

test("NodeGraphClient registers runtimes and creates sessions with runtime ids", async () => {
  const calls = [];
  const client = new NodeGraphClient({
    baseUrl: "http://localhost:3000",
    fetch: async (url, init) => {
      calls.push({ url, init });

      if (String(url).endsWith("/api/sdk/runtimes/register")) {
        return new Response(
          JSON.stringify({
            runtimeId: "rt_demo_001",
            cached: false,
            expiresAt: "2026-03-21T00:30:00.000Z",
            libraryVersion: "hello-world@1",
          }),
          {
            status: 201,
            headers: {
              "content-type": "application/json",
            },
          },
        );
      }

      return new Response(
        JSON.stringify({
          sessionId: "ngs_demo",
          runtimeId: "rt_demo_001",
          editorUrl: "http://localhost:3000/editor/ngs_demo",
          accessType: "private",
        }),
        {
          status: 201,
          headers: {
            "content-type": "application/json",
          },
        },
      );
    },
  });

  await client.registerRuntime({
    runtimeId: "rt_demo_001",
    domain: "hello-world",
    controlBaseUrl: "http://127.0.0.1:4310/runtime",
    libraryVersion: "hello-world@1",
    library: {
      nodes: [],
    },
  });

  await client.createSession({
    runtimeId: "rt_demo_001",
    completionWebhook: "http://127.0.0.1:4310/api/completed",
  });

  assert.equal(calls.length, 2);
  assert.match(String(calls[0].url), /\/api\/sdk\/runtimes\/register$/);
  assert.match(String(calls[1].url), /\/api\/sdk\/sessions$/);
  assert.match(String(calls[1].init.body), /"runtimeId":"rt_demo_001"/);
});

test("NodeGraphRuntime keeps a stable runtime id and skips redundant registration within the cache ttl", async () => {
  let currentTime = Date.parse("2026-03-21T00:00:00.000Z");
  const runtime = createHelloRuntime({
    now: () => currentTime,
  });
  const registrationCalls = [];
  const client = {
    async registerRuntime(request) {
      registrationCalls.push(request);
      return {
        runtimeId: request.runtimeId,
        cached: registrationCalls.length > 1,
        expiresAt: "2026-03-21T00:30:00.000Z",
        libraryVersion: request.libraryVersion,
      };
    },
  };

  await runtime.ensureRegistered(client);
  await runtime.ensureRegistered(client);
  currentTime += 31 * 60 * 1000;
  await runtime.ensureRegistered(client);

  assert.equal(typeof runtime.runtimeId, "string");
  assert.equal(registrationCalls.length, 2);
  assert.equal(registrationCalls[0].runtimeId, runtime.runtimeId);
  assert.equal(registrationCalls[1].runtimeId, runtime.runtimeId);
  assert.deepEqual(runtime.getLibrary().nodes.map((node) => node.type), ["greeting_source", "console_output"]);
});

test("NodeGraphRuntime executes a hello-world graph and records profiling", async () => {
  const runtime = createHelloRuntime();

  const result = await runtime.executeGraph(createHelloGraph());

  assert.equal(result.status, "completed");
  assert.deepEqual(result.results.console, ["Hello, Codex!"]);
  assert.equal(result.profiler.node_source.callCount, 1);
  assert.equal(result.profiler.node_output.callCount, 1);
  assert.ok(result.profiler.node_output.totalDurationMs >= 0);
});

test("NodeGraphRuntime debugger pauses on breakpoints and can continue to completion", async () => {
  const runtime = createHelloRuntime();
  const debugSession = runtime.createDebugger(createHelloGraph(), {
    breakpoints: ["node_output"],
  });

  const firstStep = await debugSession.step();
  assert.equal(firstStep.status, "paused");
  assert.equal(firstStep.lastEvent?.nodeId, "node_source");

  const paused = await debugSession.continue();
  assert.equal(paused.status, "paused");
  assert.equal(paused.pauseReason, "breakpoint");
  assert.equal(paused.pendingNodeId, "node_output");

  const completed = await debugSession.continue();
  assert.equal(completed.status, "completed");
  assert.deepEqual(completed.results.console, ["Hello, Codex!"]);
  assert.equal(completed.profiler.node_output.callCount, 1);
});

test("NodeGraphRuntime debugger can replace breakpoints while reusing the same session", async () => {
  const runtime = createHelloRuntime();
  const debugSession = runtime.createDebugger(createHelloGraph());

  const firstStep = await debugSession.step();
  assert.equal(firstStep.status, "paused");
  assert.equal(firstStep.pendingNodeId, "node_output");

  debugSession.setBreakpoints(["node_output"]);

  const paused = await debugSession.continue();
  assert.equal(paused.status, "paused");
  assert.equal(paused.pauseReason, "breakpoint");
  assert.equal(paused.pendingNodeId, "node_output");

  debugSession.setBreakpoints([]);

  const completed = await debugSession.continue();
  assert.equal(completed.status, "completed");
  assert.deepEqual(completed.results.console, ["Hello, Codex!"]);
});

test("NodeGraphRuntime debugger does not count paused wait time between step calls against the wall-time budget", async () => {
  let currentTime = Date.parse("2026-03-21T00:00:00.000Z");
  const runtime = createHelloRuntime({
    now: () => currentTime,
  });
  const debugSession = runtime.createDebugger(createHelloGraph(), {
    maxWallTimeMs: 5_000,
  });

  const firstStep = await debugSession.step();
  assert.equal(firstStep.status, "paused");
  assert.equal(firstStep.pendingNodeId, "node_output");

  currentTime += 60_000;

  const completed = await debugSession.step();
  assert.equal(completed.status, "completed");
  assert.deepEqual(completed.results.console, ["Hello, Codex!"]);
});
