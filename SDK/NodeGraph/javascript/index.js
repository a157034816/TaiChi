export class NodeGraphError extends Error {
  constructor(message, status, payload) {
    super(message);
    this.name = "NodeGraphError";
    this.status = status;
    this.payload = payload;
  }
}

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
