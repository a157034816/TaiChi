import assert from "node:assert/strict";
import { once } from "node:events";
import { createServer } from "node:http";
import test from "node:test";

import { createApp } from "./app.mjs";
import { createDemoState } from "./state.mjs";
import { getDemoConfig } from "./config.mjs";
import { ApprovalDecision, ReviewTask, WorkflowRequest } from "./contracts.mjs";

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

test("GET /api/node-library returns the demo node library", async () => {
  const config = getDemoConfig({
    demoClientBaseUrl: "http://demo-client.test",
  });

  await withServer(createApp({ config }), async (baseUrl) => {
    const response = await fetch(`${baseUrl}/api/node-library`);
    const payload = await response.json();

    assert.equal(response.status, 200);
    assert.ok(Array.isArray(payload.nodes));
    assert.ok(payload.nodes.length >= 5);
    assert.ok(Array.isArray(payload.typeMappings));
    assert.equal(payload.typeMappings.length, 3);
    assert.ok(payload.nodes.some((node) => Array.isArray(node.outputs) && node.outputs.length > 1));
    assert.ok(payload.nodes.some((node) => Array.isArray(node.inputs) && node.inputs.length > 1));
    assert.ok(payload.typeMappings.some((mapping) => mapping.canonicalId === "workflow/request"));
    assert.ok(payload.typeMappings.some((mapping) => mapping.type === WorkflowRequest.name));
    assert.ok(payload.typeMappings.some((mapping) => mapping.type === ReviewTask.name));
    assert.ok(payload.typeMappings.some((mapping) => mapping.type === ApprovalDecision.name));
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
      domain: "demo-workflow",
      graph: {
        name: "Demo Approval Flow",
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
    assert.equal(capturedRequest.graph.edges.length, 7);
    assert.equal(capturedRequest.graph.edges[0].sourceHandle, "next");
    assert.equal(capturedRequest.graph.edges.at(-1).targetHandle, "failure");
  });
});
