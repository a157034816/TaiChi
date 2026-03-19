import { demoNodeLibrary, demoTypeMappings, createGraphDocument } from "./demo-data.mjs";
import { renderHomePage } from "./html.mjs";
import { createDemoState } from "./state.mjs";
import { getDemoConfig } from "./config.mjs";
import { NodeGraphClient } from "../../../SDK/NodeGraph/javascript/index.js";

function createNodeGraphClient(config) {
  return new NodeGraphClient({
    baseUrl: config.nodeGraphBaseUrl,
  });
}

function sendJson(response, statusCode, payload) {
  response.writeHead(statusCode, {
    "content-type": "application/json; charset=utf-8",
    "cache-control": "no-store",
  });
  response.end(JSON.stringify(payload, null, 2));
}

function sendHtml(response, html) {
  response.writeHead(200, {
    "content-type": "text/html; charset=utf-8",
    "cache-control": "no-store",
  });
  response.end(html);
}

async function readJsonBody(request) {
  const chunks = [];

  for await (const chunk of request) {
    chunks.push(chunk);
  }

  if (!chunks.length) {
    return {};
  }

  const raw = Buffer.concat(chunks).toString("utf8");
  return raw ? JSON.parse(raw) : {};
}

export function createApp({ config = getDemoConfig(), state = createDemoState(), nodeGraphClient } = {}) {
  const client = nodeGraphClient ?? createNodeGraphClient(config);

  return async function app(request, response) {
    const url = new URL(request.url ?? "/", config.demoClientBaseUrl);

    if (request.method === "GET" && url.pathname === "/api/health") {
      sendJson(response, 200, {
        status: "ok",
        service: "NodeGraph Demo Client",
        demoClientBaseUrl: config.demoClientBaseUrl,
        nodeGraphBaseUrl: config.nodeGraphBaseUrl,
        demoDomain: config.demoDomain,
      });
      return;
    }

    if (request.method === "GET" && url.pathname === "/") {
      sendHtml(response, renderHomePage({ config, state }));
      return;
    }

    if (request.method === "GET" && url.pathname === "/api/node-library") {
      sendJson(response, 200, {
        nodes: demoNodeLibrary,
        typeMappings: demoTypeMappings,
      });
      return;
    }

    if (request.method === "GET" && url.pathname === "/api/results/latest") {
      sendJson(response, 200, {
        lastSession: state.lastSession,
        latestCompletion: state.latestCompletion,
        callbackCount: state.callbackHistory.length,
      });
      return;
    }

    if (request.method === "POST" && url.pathname === "/api/completed") {
      try {
        const payload = await readJsonBody(request);
        const entry = {
          receivedAt: new Date().toISOString(),
          payload,
        };

        state.latestCompletion = entry;
        state.callbackHistory.push(entry);
        state.callbackHistory = state.callbackHistory.slice(-10);

        sendJson(response, 200, {
          success: true,
          receivedAt: entry.receivedAt,
        });
      } catch (error) {
        sendJson(response, 400, {
          error: error instanceof Error ? error.message : "Invalid completion payload.",
        });
      }

      return;
    }

    if (request.method === "POST" && url.pathname === "/api/create-session") {
      try {
        const body = await readJsonBody(request);
        const graphMode = body.graphMode === "existing" ? "existing" : "new";
        const graphName =
          typeof body.graphName === "string" && body.graphName.trim()
            ? body.graphName.trim()
            : graphMode === "existing"
              ? "Demo Existing Approval Flow"
              : "Demo Approval Flow";

        const payload = await client.createSession({
          domain: config.demoDomain,
          clientName: config.clientName,
          nodeLibraryEndpoint: `${config.demoClientBaseUrl}/api/node-library`,
          completionWebhook: `${config.demoClientBaseUrl}/api/completed`,
          graph: createGraphDocument(graphName, graphMode),
          metadata: {
            graphMode,
            source: "NodeGraph.DemoClient",
          },
        });

        state.lastSession = {
          createdAt: new Date().toISOString(),
          request: {
            graphMode,
            graphName,
          },
          response: payload,
        };

        sendJson(response, 200, payload);
      } catch (error) {
        sendJson(response, 502, {
          error: error instanceof Error ? error.message : "Failed to create NodeGraph session.",
        });
      }

      return;
    }

    sendJson(response, 404, {
      error: "Demo client endpoint not found.",
    });
  };
}
