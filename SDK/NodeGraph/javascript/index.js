const DEFAULT_RUNTIME_CACHE_TTL_MS = 30 * 60 * 1000;
const DEFAULT_MAX_STEPS = 1_000;
const DEFAULT_MAX_WALL_TIME_MS = 5_000;

function getNowMs(now) {
  return typeof now === "function" ? Number(now()) : Date.now();
}

function getPerformanceNow() {
  return globalThis.performance?.now?.() ?? Date.now();
}

function cloneJsonLike(value) {
  return value === undefined ? undefined : JSON.parse(JSON.stringify(value));
}

function buildRuntimeId() {
  return `rt_${globalThis.crypto?.randomUUID?.() ?? Math.random().toString(36).slice(2)}`;
}

function normalizeCapabilities(capabilities) {
  return {
    canExecute: capabilities?.canExecute ?? true,
    canDebug: capabilities?.canDebug ?? true,
    canProfile: capabilities?.canProfile ?? true,
  };
}

function createProfilerRecord() {
  return {
    averageDurationMs: 0,
    callCount: 0,
    lastDurationMs: 0,
    totalDurationMs: 0,
  };
}

function ensureArrayMapValue(map, key) {
  const existing = map.get(key);
  if (existing) {
    return existing;
  }

  const created = [];
  map.set(key, created);
  return created;
}

function createNodeKey(nodeId, handleId) {
  return `${nodeId}::${handleId ?? ""}`;
}

function findNodeDefinition(runtime, nodeType) {
  const definition = runtime._nodeDefinitions.get(nodeType);
  if (!definition) {
    throw new Error(`NodeGraphRuntime could not find a node definition for "${nodeType}".`);
  }

  return definition;
}

function copyNodeTemplate(definition) {
  const { execute, ...template } = definition;
  return cloneJsonLike(template);
}

class RuntimeDebuggerSession {
  constructor(runtime, graph, options = {}) {
    this.runtime = runtime;
    this.graph = cloneJsonLike(graph);
    this.maxSteps = options.maxSteps ?? DEFAULT_MAX_STEPS;
    this.maxWallTimeMs = options.maxWallTimeMs ?? DEFAULT_MAX_WALL_TIME_MS;
    this.breakpoints = new Set(options.breakpoints ?? []);
    this.status = "idle";
    this.pauseReason = null;
    this.pendingNodeId = null;
    this.lastEvent = null;
    this.lastError = null;
    this.startedAtMs = getNowMs(runtime.now);
    this.stepCount = 0;
    this.results = {};
    this.events = [];
    this.profiler = {};
    this.nodeState = new Map();
    this.inbox = new Map();
    this.nodeMap = new Map(this.graph.nodes.map((node) => [node.id, node]));
    this.queue = [];
    this.outgoingEdges = new Map();

    for (const edge of this.graph.edges) {
      const key = createNodeKey(edge.source, edge.sourceHandle ?? null);
      ensureArrayMapValue(this.outgoingEdges, key).push(edge);
    }

    for (const node of this.graph.nodes) {
      if ((node.data?.inputs ?? []).length === 0) {
        this.queue.push({
          nodeId: node.id,
          reason: "initial",
        });
      }
    }
  }

  #recordEvent(event) {
    this.lastEvent = event;
    this.events.push(event);
  }

  #ensureBudget() {
    const nowMs = getNowMs(this.runtime.now);
    if (this.stepCount >= this.maxSteps) {
      this.status = "budget_exceeded";
      this.pauseReason = "maxSteps";
      return false;
    }

    if (nowMs - this.startedAtMs > this.maxWallTimeMs) {
      this.status = "budget_exceeded";
      this.pauseReason = "maxWallTimeMs";
      return false;
    }

    return true;
  }

  #getNodeState(nodeId) {
    const existing = this.nodeState.get(nodeId);
    if (existing) {
      return existing;
    }

    const created = {};
    this.nodeState.set(nodeId, created);
    return created;
  }

  #getInbox(nodeId) {
    const existing = this.inbox.get(nodeId);
    if (existing) {
      return existing;
    }

    const created = new Map();
    this.inbox.set(nodeId, created);
    return created;
  }

  #queueEmission(node, portId, value) {
    const edgeKey = createNodeKey(node.id, portId);
    const fallbackKey = createNodeKey(node.id, null);
    const outgoingEdges = [
      ...(this.outgoingEdges.get(edgeKey) ?? []),
      ...(this.outgoingEdges.get(fallbackKey) ?? []),
    ];

    for (const edge of outgoingEdges) {
      const targetInbox = this.#getInbox(edge.target);
      const targetPort = edge.targetHandle ?? "__default__";
      const targetValues = targetInbox.get(targetPort) ?? [];
      targetValues.push(cloneJsonLike(value));
      targetInbox.set(targetPort, targetValues);
      this.queue.push({
        nodeId: edge.target,
        reason: "message",
        portId: edge.targetHandle ?? undefined,
        value: cloneJsonLike(value),
      });
    }
  }

  #createExecutionContext(node, trigger) {
    const inbox = this.#getInbox(node.id);
    const values = cloneJsonLike(node.data?.values ?? {});

    return {
      graph: this.graph,
      node,
      state: this.#getNodeState(node.id),
      trigger,
      values,
      getInputs: () =>
        Object.fromEntries(
          [...inbox.entries()].map(([portId, items]) => [portId === "__default__" ? "default" : portId, [...items]]),
        ),
      readInput: (portId) => {
        const items = inbox.get(portId) ?? inbox.get("__default__");
        return items?.at(-1);
      },
      emit: (portId, value) => {
        this.#queueEmission(node, portId, value);
      },
      pushResult: (channel, value) => {
        const existing = this.results[channel] ?? [];
        existing.push(cloneJsonLike(value));
        this.results[channel] = existing;
      },
    };
  }

  async #executeQueueItem(item) {
    const node = this.nodeMap.get(item.nodeId);
    if (!node) {
      throw new Error(`NodeGraphRuntime could not find node "${item.nodeId}" in the graph.`);
    }

    const definition = findNodeDefinition(this.runtime, node.data?.nodeType);
    const startedAt = getPerformanceNow();
    await definition.execute(this.#createExecutionContext(node, item));
    const durationMs = Math.max(0, getPerformanceNow() - startedAt);
    const profiler = this.profiler[node.id] ?? createProfilerRecord();
    profiler.callCount += 1;
    profiler.lastDurationMs = durationMs;
    profiler.totalDurationMs += durationMs;
    profiler.averageDurationMs = profiler.totalDurationMs / profiler.callCount;
    this.profiler[node.id] = profiler;
    this.#recordEvent({
      step: this.stepCount,
      kind: "nodeExecuted",
      nodeId: node.id,
      nodeType: node.data?.nodeType,
      durationMs,
      reason: item.reason,
      portId: item.portId ?? null,
    });
  }

  async #drain({ singleStep = false } = {}) {
    let ignoreBreakpointForNodeId = null;
    if (this.status === "paused" && this.pauseReason === "breakpoint" && this.pendingNodeId) {
      ignoreBreakpointForNodeId = this.pendingNodeId;
    }

    this.status = "running";
    this.pauseReason = null;

    while (this.queue.length > 0) {
      if (!this.#ensureBudget()) {
        return this.#buildSnapshot();
      }

      const nextItem = this.queue[0];
      if (
        this.breakpoints.has(nextItem.nodeId) &&
        ignoreBreakpointForNodeId !== nextItem.nodeId
      ) {
        this.status = "paused";
        this.pauseReason = "breakpoint";
        this.pendingNodeId = nextItem.nodeId;
        return this.#buildSnapshot();
      }

      ignoreBreakpointForNodeId = null;
      this.stepCount += 1;
      const item = this.queue.shift();

      try {
        await this.#executeQueueItem(item);
      } catch (error) {
        this.status = "failed";
        this.pauseReason = "error";
        this.lastError = error instanceof Error ? error : new Error(String(error));
        return this.#buildSnapshot();
      }

      if (singleStep) {
        this.status = this.queue.length ? "paused" : "completed";
        this.pauseReason = this.queue.length ? "step" : null;
        this.pendingNodeId = this.queue[0]?.nodeId ?? null;
        return this.#buildSnapshot();
      }
    }

    this.status = "completed";
    this.pendingNodeId = null;
    return this.#buildSnapshot();
  }

  #buildSnapshot() {
    return {
      status: this.status,
      pauseReason: this.pauseReason,
      pendingNodeId: this.pendingNodeId,
      lastError: this.lastError,
      lastEvent: this.lastEvent,
      profiler: cloneJsonLike(this.profiler),
      results: cloneJsonLike(this.results),
      events: cloneJsonLike(this.events),
    };
  }

  async step() {
    return this.#drain({ singleStep: true });
  }

  async continue() {
    return this.#drain({ singleStep: false });
  }
}

export class NodeGraphError extends Error {
  constructor(message, status, payload) {
    super(message);
    this.name = "NodeGraphError";
    this.status = status;
    this.payload = payload;
  }
}

/**
 * 面向 NodeGraph 服务的 HTTP 客户端。
 */
export class NodeGraphClient {
  constructor(options) {
    if (!options?.baseUrl) {
      throw new Error("NodeGraphClient requires a baseUrl.");
    }

    this.baseUrl = options.baseUrl.replace(/\/$/, "");
    this.fetchImpl = options.fetch ?? globalThis.fetch?.bind(globalThis);

    if (!this.fetchImpl) {
      throw new Error("A fetch implementation is required.");
    }
  }

  async registerRuntime(request) {
    return this.#request("/api/sdk/runtimes/register", {
      method: "POST",
      body: JSON.stringify(request),
    });
  }

  async createSession(request) {
    return this.#request("/api/sdk/sessions", {
      method: "POST",
      body: JSON.stringify(request),
    });
  }

  async getSession(sessionId) {
    return this.#request(`/api/sdk/sessions/${encodeURIComponent(sessionId)}`, {
      method: "GET",
    });
  }

  async #request(path, init) {
    const response = await this.fetchImpl(`${this.baseUrl}${path}`, {
      ...init,
      headers: {
        "content-type": "application/json",
        ...(init?.headers ?? {}),
      },
    });

    const text = await response.text();
    const payload = text ? JSON.parse(text) : null;

    if (!response.ok) {
      throw new NodeGraphError(payload?.error ?? "NodeGraph request failed.", response.status, payload);
    }

    return payload;
  }
}

/**
 * SDK 侧运行时，负责生成运行时标识、输出节点库、注册缓存以及执行图谱。
 */
export class NodeGraphRuntime {
  constructor(options) {
    if (!options?.domain) {
      throw new Error("NodeGraphRuntime requires a domain.");
    }

    if (!options?.controlBaseUrl) {
      throw new Error("NodeGraphRuntime requires a controlBaseUrl.");
    }

    if (!options?.libraryVersion) {
      throw new Error("NodeGraphRuntime requires a libraryVersion.");
    }

    this.domain = options.domain;
    this.clientName = options.clientName;
    this.controlBaseUrl = options.controlBaseUrl.replace(/\/$/, "");
    this.libraryVersion = options.libraryVersion;
    this.capabilities = normalizeCapabilities(options.capabilities);
    this.runtimeId = options.runtimeId ?? buildRuntimeId();
    this.now = options.now;
    this.cacheTtlMs = options.cacheTtlMs ?? DEFAULT_RUNTIME_CACHE_TTL_MS;
    this._nodeDefinitions = new Map();
    this._typeMappings = [];
    this._lastRegisteredAtMs = null;
  }

  registerNode(definition) {
    if (!definition?.type) {
      throw new Error("Node definitions must provide a type.");
    }

    if (typeof definition.execute !== "function") {
      throw new Error(`Node definition "${definition.type}" must provide an execute handler.`);
    }

    this._nodeDefinitions.set(definition.type, cloneJsonLike({
      ...definition,
      execute: undefined,
    }));
    this._nodeDefinitions.get(definition.type).execute = definition.execute;
    return this;
  }

  registerTypeMapping(mapping) {
    this._typeMappings.push(cloneJsonLike(mapping));
    return this;
  }

  getLibrary() {
    return {
      nodes: [...this._nodeDefinitions.values()].map((definition) => copyNodeTemplate(definition)),
      typeMappings: this._typeMappings.length ? cloneJsonLike(this._typeMappings) : undefined,
    };
  }

  createRegistrationRequest() {
    return {
      runtimeId: this.runtimeId,
      domain: this.domain,
      clientName: this.clientName,
      controlBaseUrl: this.controlBaseUrl,
      libraryVersion: this.libraryVersion,
      capabilities: cloneJsonLike(this.capabilities),
      library: this.getLibrary(),
    };
  }

  async ensureRegistered(client, options = {}) {
    const nowMs = getNowMs(this.now);
    const shouldRegister =
      options.force === true ||
      this._lastRegisteredAtMs === null ||
      nowMs - this._lastRegisteredAtMs >= this.cacheTtlMs;

    if (!shouldRegister) {
      return {
        runtimeId: this.runtimeId,
        cached: true,
        expiresAt: new Date(this._lastRegisteredAtMs + this.cacheTtlMs).toISOString(),
        libraryVersion: this.libraryVersion,
      };
    }

    const response = await client.registerRuntime(this.createRegistrationRequest());
    this._lastRegisteredAtMs = nowMs;
    return response;
  }

  createDebugger(graph, options = {}) {
    return new RuntimeDebuggerSession(this, graph, options);
  }

  async executeGraph(graph, options = {}) {
    const debugSession = this.createDebugger(graph, options);
    return debugSession.continue();
  }
}
