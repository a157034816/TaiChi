import assert from "node:assert/strict";
import { once } from "node:events";
import { createServer } from "node:http";
import test from "node:test";

import { createApp } from "./app.mjs";
import { getDemoConfig } from "./config.mjs";
import { createDemoState } from "./state.mjs";
import { createGraphDocument, createHelloWorldRuntime } from "./demo-data.mjs";

async function withServer(handler, callback) {
  const server = createServer(handler);
  server.listen(0, "127.0.0.1");
  await once(server, "listening");

  const address = server.address();
  const baseUrl = `http://127.0.0.1:${address.port}`;

  try {
    await callback(baseUrl);
  } finally {
    server.close();
    await once(server, "close");
  }
}

function createRuntime(config) {
  return createHelloWorldRuntime(config, {
    runtimeId: "rt_demo_001",
    now: () => Date.parse("2026-03-21T00:00:00.000Z"),
  });
}

test("GET /api/runtime/library returns the embedded showcase node library", async () => {
  const config = getDemoConfig({
    demoClientBaseUrl: "http://demo-client.test",
  });
  const runtime = createRuntime(config);

  await withServer(createApp({ config, runtime }), async (baseUrl) => {
    const response = await fetch(`${baseUrl}/api/runtime/library`);
    const payload = await response.json();

    assert.equal(response.status, 200);
    assert.equal(payload.runtime.runtimeId, "rt_demo_001");
    assert.equal(payload.runtime.libraryVersion, "demo-showcase@1");
    assert.deepEqual(
      payload.library.nodes.map((node) => node.type),
      [
        "greeting_source",
        "console_output",
        "demo_source",
        "greeting_builder",
        "math_add",
        "if_text",
        "text_interpolate",
        "const_text",
        "const_number",
        "const_boolean",
        "const_date",
        "const_color",
        "const_decimal",
      ],
    );
    assert.equal(payload.library.nodes[0].displayName, "Greeting Source");
    assert.equal(payload.library.nodes[1].displayName, "Console Output");
    assert.equal(payload.library.typeMappings[0].canonicalId, "hello/text");
    assert.equal(payload.library.typeMappings[0].type, "DemoText");
    assert.equal(payload.library.typeMappings[1].canonicalId, "demo/number");
    assert.equal(payload.library.typeMappings[1].type, "DemoNumber");
  });
});

test("GET /api/runtime/field-options returns select options for the demo source punctuation field", async () => {
  const config = getDemoConfig({
    demoClientBaseUrl: "http://demo-client.test",
  });
  const runtime = createRuntime(config);

  await withServer(createApp({ config, runtime }), async (baseUrl) => {
    const response = await fetch(
      `${baseUrl}/api/runtime/field-options?domain=demo&nodeType=demo_source&fieldKey=punctuation&locale=zh-CN`,
    );
    const payload = await response.json();

    assert.equal(response.status, 200);
    assert.equal(payload.options.length, 4);
    assert.deepEqual(payload.options.map((option) => option.value), ["!", "?", ".", "..."]);
  });
});

test("GET /api/health returns runtime metadata", async () => {
  const config = getDemoConfig({
    demoClientBaseUrl: "http://demo-client.test",
    nodeGraphBaseUrl: "http://nodegraph.test",
  });
  const runtime = createRuntime(config);

  await withServer(createApp({ config, runtime }), async (baseUrl) => {
    const response = await fetch(`${baseUrl}/api/health`);
    const payload = await response.json();

    assert.equal(response.status, 200);
    assert.equal(payload.status, "ok");
    assert.equal(payload.nodeGraphBaseUrl, "http://nodegraph.test");
    assert.equal(payload.runtime.runtimeId, "rt_demo_001");
    assert.equal(payload.runtime.controlBaseUrl, "http://demo-client.test/api/runtime");
  });
});

test("POST /api/completed stores the latest completion payload", async () => {
  const state = createDemoState();
  const config = getDemoConfig({
    demoClientBaseUrl: "http://demo-client.test",
  });
  const runtime = createRuntime(config);

  await withServer(createApp({ config, state, runtime }), async (baseUrl) => {
    const completionPayload = {
      sessionId: "ngs_demo",
      runtimeId: "rt_demo_001",
      graph: createGraphDocument("Hello World Pipeline", "existing"),
    };

    const postResponse = await fetch(`${baseUrl}/api/completed`, {
      method: "POST",
      headers: {
        "content-type": "application/json",
      },
      body: JSON.stringify(completionPayload),
    });

    assert.equal(postResponse.status, 200);

    const latestResponse = await fetch(`${baseUrl}/api/results/latest`);
    const latestPayload = await latestResponse.json();

    assert.equal(latestPayload.latestCompletion.payload.sessionId, "ngs_demo");
    assert.equal(latestPayload.callbackCount, 1);
  });
});

test("POST /api/runtime/execute runs the Hello World graph and records profiler output", async () => {
  const config = getDemoConfig({
    demoClientBaseUrl: "http://demo-client.test",
  });
  const runtime = createRuntime(config);

  await withServer(createApp({ config, runtime }), async (baseUrl) => {
    const response = await fetch(`${baseUrl}/api/runtime/execute`, {
      method: "POST",
      headers: {
        "content-type": "application/json",
      },
      body: JSON.stringify({
        graphMode: "existing",
        graphName: "Demo Showcase Pipeline",
      }),
    });
    const payload = await response.json();

    assert.equal(response.status, 200);
    assert.equal(payload.snapshot.status, "completed");
    assert.deepEqual(payload.snapshot.results.console, [
      "Greeting: Hello, Codex!\nLucky: 12\nDate: 2026-03-21\nTheme: #2563eb\nAmount: 123.45",
    ]);
    assert.equal(payload.snapshot.profiler.node_source.callCount, 1);
    assert.equal(payload.snapshot.profiler.node_output.callCount, 1);
  });
});

test("POST /api/runtime/debug/sample returns a breakpoint walkthrough", async () => {
  const config = getDemoConfig({
    demoClientBaseUrl: "http://demo-client.test",
  });
  const runtime = createRuntime(config);

  await withServer(createApp({ config, runtime }), async (baseUrl) => {
    const response = await fetch(`${baseUrl}/api/runtime/debug/sample`, {
      method: "POST",
      headers: {
        "content-type": "application/json",
      },
      body: JSON.stringify({
        graphMode: "existing",
        graphName: "Demo Showcase Pipeline",
      }),
    });
    const payload = await response.json();

    assert.equal(response.status, 200);
    assert.equal(payload.firstStep.status, "paused");
    assert.equal(payload.firstStep.lastEvent.nodeId, "node_source");
    assert.equal(payload.paused.status, "paused");
    assert.equal(payload.paused.pauseReason, "breakpoint");
    assert.equal(payload.paused.pendingNodeId, "node_output");
    assert.equal(payload.completed.status, "completed");
    assert.deepEqual(payload.completed.results.console, [
      "Greeting: Hello, Codex!\nLucky: 12\nDate: 2026-03-21\nTheme: #2563eb\nAmount: 123.45",
    ]);
  });
});

test("POST /api/create-session registers the runtime and then creates a session with runtimeId", async () => {
  let registrationRequest;
  let sessionRequest;

  const fakeClient = {
    async registerRuntime(request) {
      registrationRequest = request;
      return {
        runtimeId: request.runtimeId,
        cached: false,
        expiresAt: "2026-03-21T00:30:00.000Z",
        libraryVersion: request.libraryVersion,
      };
    },
    async createSession(request) {
      sessionRequest = request;
      return {
        sessionId: "ngs_fake",
        runtimeId: request.runtimeId,
        editorUrl: "http://localhost:3000/editor/ngs_fake",
        accessType: "private",
      };
    },
  };

  const state = createDemoState();
  const config = getDemoConfig({
    demoClientBaseUrl: "http://demo-client.test",
  });
  const runtime = createRuntime(config);

  await withServer(createApp({ config, state, runtime, nodeGraphClient: fakeClient }), async (baseUrl) => {
    const response = await fetch(`${baseUrl}/api/create-session`, {
      method: "POST",
      headers: {
        "content-type": "application/json",
      },
      body: JSON.stringify({
        graphMode: "existing",
        graphName: "Demo Showcase Pipeline",
      }),
    });
    const payload = await response.json();

    assert.equal(response.status, 200);
    assert.equal(payload.sessionId, "ngs_fake");
    assert.equal(registrationRequest.runtimeId, "rt_demo_001");
    assert.equal(registrationRequest.library.nodes.length, 13);
    assert.equal(sessionRequest.runtimeId, "rt_demo_001");
    assert.equal(sessionRequest.completionWebhook, "http://demo-client.test/api/completed");
    assert.equal(sessionRequest.graph.nodes.length, 6);
    assert.deepEqual(
      sessionRequest.graph.nodes.map((node) => node.data.nodeType),
      ["demo_source", "greeting_builder", "math_add", "if_text", "text_interpolate", "console_output"],
    );
    assert.equal(state.lastSession.registration.libraryVersion, "demo-showcase@1");
    assert.equal(state.lastSession.response.editorUrl, "http://localhost:3000/editor/ngs_fake");
  });
});

test("GET / renders the showcase runtime copy", async () => {
  const config = getDemoConfig({
    demoClientBaseUrl: "http://demo-client.test",
  });
  const runtime = createRuntime(config);

  await withServer(createApp({ config, runtime }), async (baseUrl) => {
    const response = await fetch(baseUrl);
    const html = await response.text();

    assert.equal(response.status, 200);
    assert.match(html, /Showcase Runtime/);
    assert.match(html, /Create editor session/);
    assert.match(html, /Greeting Source/);
    assert.match(html, /rt_demo_001/);
  });
});
