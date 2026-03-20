const {
  CentralServiceError,
  buildClientRuntime,
  createParsedError,
  createTransportError,
  loadCentralServiceOptionsFromEnv,
  MultiEndpointTransport,
  parseError,
} = require("./internal");

function calculateNetworkScore(status) {
  if (!status || !status.isAvailable) return 0;
  const responseTime = Number(status.responseTime ?? 0);
  const packetLoss = Number(status.packetLoss ?? 0);
  const responseTimeScore =
    responseTime >= 1000 ? 0 : responseTime <= 50 ? 50 : Math.floor(50 * (1 - (responseTime - 50) / 950.0));
  const packetLossScore =
    packetLoss >= 50 ? 0 : packetLoss <= 0 ? 50 : Math.floor(50 * (1 - packetLoss / 50.0));
  return responseTimeScore + packetLossScore;
}

class CentralServiceDiscoveryClient {
  constructor(options) {
    const runtime = buildClientRuntime(options || {});
    this.baseUrl = runtime.baseUrl;
    this.endpoints = runtime.endpoints.map((endpoint) => ({
      baseUrl: endpoint.baseUrl,
      priority: endpoint.priority,
      maxAttempts: endpoint.maxAttempts,
      circuitBreaker: endpoint.circuitBreaker
        ? {
            failureThreshold: endpoint.circuitBreaker.failureThreshold,
            breakDurationMinutes: endpoint.circuitBreaker.breakDurationMs / 60000,
            recoveryThreshold: endpoint.circuitBreaker.recoveryThreshold,
          }
        : null,
    }));
    this.timeoutMs = runtime.timeoutMs;
    this.transport = new MultiEndpointTransport(runtime.endpoints, runtime.timeoutMs);
  }

  async list(name) {
    const query = name ? "?name=" + encodeURIComponent(name) : "";
    const transport = await this.#send("GET", "/api/Service/list" + query, null);
    const apiResponse = JSON.parse(transport.bodyText || "{}");
    if (!apiResponse.success) {
      throw createParsedError("GET", transport, parseError("GET", transport.url, transport.statusCode, transport.bodyText));
    }
    return apiResponse.data;
  }

  async discoverRoundRobin(serviceName) {
    return this.#getJson("/api/ServiceDiscovery/discover/roundrobin/" + encodeURIComponent(serviceName));
  }

  async discoverWeighted(serviceName) {
    return this.#getJson("/api/ServiceDiscovery/discover/weighted/" + encodeURIComponent(serviceName));
  }

  async discoverBest(serviceName) {
    return this.#getJson("/api/ServiceDiscovery/discover/best/" + encodeURIComponent(serviceName));
  }

  async getNetworkAll() {
    return this.#getJson("/api/ServiceDiscovery/network/all");
  }

  async getNetwork(serviceId) {
    return this.#getJson("/api/ServiceDiscovery/network/" + encodeURIComponent(serviceId));
  }

  async evaluateNetwork(serviceId) {
    return this.#sendJson("POST", "/api/ServiceDiscovery/network/evaluate/" + encodeURIComponent(serviceId), null);
  }

  async #getJson(path) {
    return this.#sendJson("GET", path, null);
  }

  async #sendJson(method, path, body) {
    const transport = await this.#send(method, path, body);
    return JSON.parse(transport.bodyText || "null");
  }

  async #send(method, path, body) {
    let transport;
    try {
      transport = await this.transport.send(method, path, body);
    } catch (error) {
      throw createTransportError(method, error);
    }

    if (transport.statusCode < 200 || transport.statusCode > 299) {
      throw createParsedError(method, transport, parseError(method, transport.url, transport.statusCode, transport.bodyText));
    }

    return transport;
  }
}

module.exports = {
  CentralServiceError,
  CentralServiceDiscoveryClient,
  calculateNetworkScore,
  loadCentralServiceOptionsFromEnv,
};
