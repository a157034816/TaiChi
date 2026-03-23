import {
  createGraphDocument,
  createHelloWorldRuntime,
  defaultDebugBreakpoints,
  defaultExistingGraphName,
  defaultNewGraphName,
} from "./demo-data.mjs";
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

function normalizeGraphMode(value) {
  return value === "new" ? "new" : "existing";
}

function resolveGraphName(graphMode, graphName) {
  if (typeof graphName === "string" && graphName.trim()) {
    return graphName.trim();
  }

  return graphMode === "new" ? defaultNewGraphName : defaultExistingGraphName;
}

function createRuntimeInfo(runtime) {
  return {
    runtimeId: runtime.runtimeId,
    libraryVersion: runtime.libraryVersion,
    controlBaseUrl: runtime.controlBaseUrl,
    domain: runtime.domain,
    capabilities: runtime.capabilities,
  };
}

export function createApp({
  config = getDemoConfig(),
  state = createDemoState(),
  nodeGraphClient,
  runtime = createHelloWorldRuntime(config),
} = {}) {
  const client = nodeGraphClient ?? createNodeGraphClient(config);

  async function registerRuntime(force = false) {
    const response = await runtime.ensureRegistered(client, { force });
    state.lastRegistration = {
      registeredAt: new Date().toISOString(),
      force,
      request: runtime.createRegistrationRequest(),
      response,
    };
    return response;
  }

  return async function app(request, response) {
    const url = new URL(request.url ?? "/", config.demoClientBaseUrl);

    if (request.method === "GET" && url.pathname === "/api/health") {
      sendJson(response, 200, {
        status: "ok",
        service: "NodeGraph Demo Client",
        demoClientBaseUrl: config.demoClientBaseUrl,
        nodeGraphBaseUrl: config.nodeGraphBaseUrl,
        demoDomain: config.demoDomain,
        runtime: createRuntimeInfo(runtime),
      });
      return;
    }

    if (request.method === "GET" && url.pathname === "/") {
      sendHtml(
        response,
        renderHomePage({
          config,
          state,
          runtime: createRuntimeInfo(runtime),
          library: runtime.getLibrary(),
          sampleGraph: createGraphDocument(defaultExistingGraphName, "existing"),
        }),
      );
      return;
    }

    if (request.method === "GET" && (url.pathname === "/api/node-library" || url.pathname === "/api/runtime/library")) {
      sendJson(response, 200, {
        runtime: createRuntimeInfo(runtime),
        library: runtime.getLibrary(),
      });
      return;
    }

    if (request.method === "GET" && url.pathname === "/api/runtime/field-options") {
      const nodeType = url.searchParams.get("nodeType");
      const fieldKey = url.searchParams.get("fieldKey");
      const locale = url.searchParams.get("locale") ?? "en";

      if (nodeType === "demo_source" && fieldKey === "punctuation") {
        const isZh = locale.toLowerCase().startsWith("zh");
        sendJson(response, 200, {
          options: [
            { value: "!", label: isZh ? "感叹号 (!)" : "Exclamation (!)" },
            { value: "?", label: isZh ? "问号 (?)" : "Question (?)" },
            { value: ".", label: isZh ? "句号 (.)" : "Dot (.)" },
            { value: "...", label: isZh ? "省略号 (...)" : "Ellipsis (...)" },
          ],
        });
        return;
      }

      sendJson(response, 200, {
        options: [],
      });
      return;
    }

    if (request.method === "GET" && url.pathname === "/api/results/latest") {
      sendJson(response, 200, {
        runtime: createRuntimeInfo(runtime),
        lastRegistration: state.lastRegistration,
        lastSession: state.lastSession,
        lastExecution: state.lastExecution,
        lastDebug: state.lastDebug,
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

    if (request.method === "POST" && url.pathname === "/api/runtime/register") {
      try {
        const body = await readJsonBody(request);
        const registration = await registerRuntime(body.force === true);
        sendJson(response, 200, registration);
      } catch (error) {
        sendJson(response, 502, {
          error: error instanceof Error ? error.message : "Failed to register runtime.",
        });
      }

      return;
    }

    if (request.method === "POST" && url.pathname === "/api/runtime/execute") {
      try {
        const body = await readJsonBody(request);
        const graphMode = normalizeGraphMode(body.graphMode);
        const graphName = resolveGraphName(graphMode, body.graphName);
        const graph = body.graph ?? createGraphDocument(graphName, graphMode);
        const snapshot = await runtime.executeGraph(graph);

        state.lastExecution = {
          executedAt: new Date().toISOString(),
          graphMode,
          graphName,
          graph,
          snapshot,
        };

        sendJson(response, 200, state.lastExecution);
      } catch (error) {
        sendJson(response, 500, {
          error: error instanceof Error ? error.message : "Failed to execute runtime graph.",
        });
      }

      return;
    }

    if (request.method === "POST" && url.pathname === "/api/runtime/debug/sample") {
      try {
        const body = await readJsonBody(request);
        const graphMode = normalizeGraphMode(body.graphMode);
        const graphName = resolveGraphName(graphMode, body.graphName);
        const graph = body.graph ?? createGraphDocument(graphName, graphMode);
        const breakpoints =
          Array.isArray(body.breakpoints) && body.breakpoints.length
            ? body.breakpoints.map((entry) => String(entry))
            : defaultDebugBreakpoints;
        const debuggerSession = runtime.createDebugger(graph, {
          breakpoints,
        });

        const firstStep = await debuggerSession.step();
        const paused = await debuggerSession.continue();
        const completed = await debuggerSession.continue();

        state.lastDebug = {
          debuggedAt: new Date().toISOString(),
          graphMode,
          graphName,
          graph,
          breakpoints,
          firstStep,
          paused,
          completed,
        };

        sendJson(response, 200, state.lastDebug);
      } catch (error) {
        sendJson(response, 500, {
          error: error instanceof Error ? error.message : "Failed to run the debug sample.",
        });
      }

      return;
    }

    if (request.method === "POST" && url.pathname === "/api/create-session") {
      try {
        const body = await readJsonBody(request);
        const graphMode = normalizeGraphMode(body.graphMode);
        const graphName = resolveGraphName(graphMode, body.graphName);
        const registration = await registerRuntime(body.forceRefresh === true);
        const payload = await client.createSession({
          runtimeId: runtime.runtimeId,
          completionWebhook: `${config.demoClientBaseUrl}/api/completed`,
          graph: createGraphDocument(graphName, graphMode),
          metadata: {
            graphMode,
            source: "NodeGraph.DemoClient.JavaScript.HelloWorld",
          },
        });

        state.lastSession = {
          createdAt: new Date().toISOString(),
          request: {
            graphMode,
            graphName,
            forceRefresh: body.forceRefresh === true,
          },
          registration,
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
