const {
  CentralServiceError,
  buildClientRuntime,
  createParsedError,
  createTransportError,
  loadCentralServiceOptionsFromEnv,
  MultiEndpointTransport,
  parseError,
} = require("./internal");

class CentralServiceServiceClient {
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

  async register(request) {
    const transport = await this.#send("POST", "/api/Service/register", request);
    const apiResponse = JSON.parse(transport.bodyText || "{}");
    if (!apiResponse.success) {
      throw createParsedError("POST", transport, parseError("POST", transport.url, transport.statusCode, transport.bodyText));
    }

    return apiResponse.data;
  }

  async deregister(serviceId) {
    await this.#send("DELETE", "/api/Service/deregister/" + encodeURIComponent(serviceId), null);
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
  CentralServiceServiceClient,
  loadCentralServiceOptionsFromEnv,
};
