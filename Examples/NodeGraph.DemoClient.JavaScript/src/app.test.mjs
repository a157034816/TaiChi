import assert from "node:assert/strict";
import { once } from "node:events";
import { createServer } from "node:http";
import test from "node:test";

import { createApp } from "./app.mjs";
import { createDemoState } from "./state.mjs";
import { getDemoConfig } from "./config.mjs";
import * as contracts from "./contracts.mjs";

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

test("GET /api/node-library returns the visual playground node library", async () => {
  const config = getDemoConfig({
    demoClientBaseUrl: "http://demo-client.test",
  });

  await withServer(createApp({ config }), async (baseUrl) => {
    const response = await fetch(`${baseUrl}/api/node-library`);
    const payload = await response.json();

    assert.equal(response.status, 200);
    assert.ok(Array.isArray(payload.nodes));
    assert.equal(payload.nodes.length, 5);
    assert.ok(Array.isArray(payload.typeMappings));
    assert.equal(payload.typeMappings.length, 3);
    assert.ok(payload.nodes.some((node) => Array.isArray(node.outputs) && node.outputs.length > 1));
    assert.ok(payload.nodes.some((node) => Array.isArray(node.inputs) && node.inputs.length > 1));
    assert.deepEqual(
      payload.nodes.map((node) => node.type),
      ["seed_source", "layer_fanout", "color_mix", "stylize_branch", "preview_output"],
    );
    assert.ok(payload.typeMappings.some((mapping) => mapping.canonicalId === "playground/seed"));
    assert.ok(payload.typeMappings.some((mapping) => mapping.canonicalId === "playground/layer"));
    assert.ok(payload.typeMappings.some((mapping) => mapping.canonicalId === "playground/frame"));
    assert.ok(payload.typeMappings.some((mapping) => mapping.type === contracts.GeneratorSeed?.name));
    assert.ok(payload.typeMappings.some((mapping) => mapping.type === contracts.LayerSignal?.name));
    assert.ok(payload.typeMappings.some((mapping) => mapping.type === contracts.PreviewFrame?.name));

    const fieldKinds = payload.nodes.flatMap((node) => node.fields ?? []).map((field) => field.kind);
    assert.deepEqual(fieldKinds.sort(), [
      "boolean",
      "color",
      "date",
      "decimal",
      "double",
      "float",
      "int",
      "select",
      "select",
      "select",
      "text",
      "textarea",
    ]);

    const remoteSelectFields = payload.nodes
      .flatMap((node) => node.fields ?? [])
      .filter((field) => field.kind === "select");
    assert.deepEqual(
      remoteSelectFields.map((field) => field.optionsEndpoint),
      [
        "http://demo-client.test/api/node-field-options/distributionMode",
        "http://demo-client.test/api/node-field-options/blendMode",
        "http://demo-client.test/api/node-field-options/previewShape",
      ],
    );
  });
});

test("GET /api/node-field-options/:fieldKey returns localized remote options", async () => {
  const config = getDemoConfig({
    demoClientBaseUrl: "http://demo-client.test",
  });

  await withServer(createApp({ config }), async (baseUrl) => {
    const cases = [
      {
        fieldKey: "distributionMode",
        locale: "en",
        expectedOptions: [
          { value: "burst", label: "Burst scatter" },
          { value: "spiral", label: "Spiral drift" },
          { value: "ribbon", label: "Ribbon sweep" },
        ],
      },
      {
        fieldKey: "blendMode",
        locale: "zh-CN",
        expectedOptions: [
          { value: "screen", label: "滤色叠加" },
          { value: "multiply", label: "正片叠底" },
          { value: "difference", label: "差值混合" },
        ],
      },
      {
        fieldKey: "previewShape",
        locale: "zh-CN",
        expectedOptions: [
          { value: "poster", label: "海报竖幅" },
          { value: "landscape", label: "横向画布" },
          { value: "square", label: "方形画布" },
        ],
      },
    ];

    for (const entry of cases) {
      const response = await fetch(
        `${baseUrl}/api/node-field-options/${entry.fieldKey}?locale=${encodeURIComponent(entry.locale)}&domain=demo-visual-playground&nodeType=preview_output&fieldKey=${entry.fieldKey}`,
      );
      const payload = await response.json();

      assert.equal(response.status, 200);
      assert.deepEqual(payload, {
        options: entry.expectedOptions,
      });
    }
  });
});

test("GET /api/health returns the demo client health payload", async () => {
  const config = getDemoConfig({
    demoClientBaseUrl: "http://demo-client.test",
    nodeGraphBaseUrl: "http://nodegraph.test",
  });

  await withServer(createApp({ config }), async (baseUrl) => {
    const response = await fetch(`${baseUrl}/api/health`);
    const payload = await response.json();

    assert.equal(response.status, 200);
    assert.equal(payload.status, "ok");
    assert.equal(payload.service, "NodeGraph Demo Client");
    assert.equal(payload.nodeGraphBaseUrl, "http://nodegraph.test");
  });
});

test("POST /api/completed stores the latest completion payload", async () => {
  const state = createDemoState();
  const config = getDemoConfig({
    demoClientBaseUrl: "http://demo-client.test",
  });

  await withServer(createApp({ config, state }), async (baseUrl) => {
    const completionPayload = {
      sessionId: "ngs_demo",
      domain: "demo-visual-playground",
      graph: {
        name: "Visual Playground Composition",
        nodes: [],
        edges: [],
        viewport: { x: 0, y: 0, zoom: 1 },
      },
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

test("GET / renders the latest editor URL when a session already exists", async () => {
  const state = createDemoState();
  state.lastSession = {
    createdAt: "2026-03-17T08:00:00.000Z",
    request: {
      graphMode: "existing",
      graphName: "Existing Demo Flow",
    },
    response: {
      sessionId: "ngs_render",
      editorUrl: "http://localhost:3300/editor/ngs_render",
      accessType: "private",
      domainCached: true,
    },
  };

  const config = getDemoConfig({
    demoClientBaseUrl: "http://demo-client.test",
  });

  await withServer(createApp({ config, state }), async (baseUrl) => {
    const response = await fetch(baseUrl);
    const html = await response.text();

    assert.equal(response.status, 200);
    assert.match(html, /Open editor page/);
    assert.match(html, /http:\/\/localhost:3300\/editor\/ngs_render/);
  });
});

test("GET / renders visual playground copy for the demo home page", async () => {
  const config = getDemoConfig({
    demoClientBaseUrl: "http://demo-client.test",
  });

  await withServer(createApp({ config }), async (baseUrl) => {
    const response = await fetch(baseUrl);
    const html = await response.text();

    assert.equal(response.status, 200);
    assert.match(html, /Visual Playground/);
    assert.match(html, /seed_source/);
    assert.match(html, /value="Visual Playground Composition"/);
    assert.doesNotMatch(html, /Business-side Example/);
  });
});

test("POST /api/create-session uses the provided NodeGraph client", async () => {
  let capturedRequest;

  const fakeClient = {
    async createSession(request) {
      capturedRequest = request;

      return {
        sessionId: "ngs_fake",
        editorUrl: "http://localhost:3000/editor/ngs_fake",
        accessType: "private",
        domainCached: true,
      };
    },
  };

  const state = createDemoState();
  const config = getDemoConfig({
    demoClientBaseUrl: "http://demo-client.test",
  });

  await withServer(createApp({ config, state, nodeGraphClient: fakeClient }), async (baseUrl) => {
    const response = await fetch(`${baseUrl}/api/create-session`, {
      method: "POST",
      headers: {
        "content-type": "application/json",
      },
      body: JSON.stringify({
        graphMode: "existing",
        graphName: "Existing Demo Flow",
      }),
    });

    const payload = await response.json();

    assert.equal(response.status, 200);
    assert.equal(payload.sessionId, "ngs_fake");
    assert.equal(state.lastSession.response.editorUrl, "http://localhost:3000/editor/ngs_fake");
    assert.equal(state.lastSession.request.graphMode, "existing");
    assert.equal(capturedRequest.domain, "demo-visual-playground");
    assert.deepEqual(
      capturedRequest.graph.nodes.map((node) => node.data.nodeType),
      ["seed_source", "layer_fanout", "color_mix", "stylize_branch", "preview_output"],
    );
    assert.equal(capturedRequest.graph.edges.length, 7);
    assert.deepEqual(
      capturedRequest.graph.edges.map((edge) => `${edge.sourceHandle}->${edge.targetHandle}`),
      [
        "seed->seed",
        "warm->warm",
        "cool->cool",
        "noise->noise",
        "frame->frame",
        "main->main",
        "variant->variant",
      ],
    );
  });
});
