const http = require("http");
const https = require("https");

const DEFAULT_BASE_URL = "http://127.0.0.1:5000";
const DEFAULT_TIMEOUT_MS = 5000;
const DEFAULT_MAX_ATTEMPTS = 2;

class CentralServiceError extends Error {
  constructor({ httpStatus, method, url, kind, message, errorCode, rawBody }) {
    super(message || "CentralServiceError");
    this.name = "CentralServiceError";
    this.httpStatus = httpStatus;
    this.method = method;
    this.url = url;
    this.kind = kind;
    this.errorCode = errorCode ?? null;
    this.rawBody = rawBody ?? "";
  }
}

function normalizeBaseUrl(baseUrl) {
  if (!baseUrl) throw new Error("baseUrl is required");
  return String(baseUrl).trim().replace(/\/+$/, "");
}

function normalizeMaxAttempts(value) {
  const normalized = Number(value ?? DEFAULT_MAX_ATTEMPTS);
  return Number.isFinite(normalized) && normalized >= 1 ? Math.trunc(normalized) : DEFAULT_MAX_ATTEMPTS;
}

function normalizeCircuitBreaker(circuitBreaker) {
  if (!circuitBreaker) return null;
  return {
    failureThreshold: Math.max(1, Number(circuitBreaker.failureThreshold ?? 1) || 1),
    breakDurationMinutes: Math.max(1, Number(circuitBreaker.breakDurationMinutes ?? 1) || 1),
    recoveryThreshold: Math.max(1, Number(circuitBreaker.recoveryThreshold ?? 1) || 1),
  };
}

function createTransportEndpoint(endpoint, order) {
  const normalizedBreaker = normalizeCircuitBreaker(endpoint.circuitBreaker);
  return {
    baseUrl: normalizeBaseUrl(endpoint.baseUrl),
    priority: Number(endpoint.priority ?? 0) || 0,
    maxAttempts: normalizeMaxAttempts(endpoint.maxAttempts),
    order,
    circuitBreaker: normalizedBreaker ? new CircuitBreakerState(normalizedBreaker) : null,
  };
}

function normalizeEndpoints(options = {}) {
  const rawEndpoints = Array.isArray(options.endpoints) && options.endpoints.length > 0
    ? options.endpoints
    : [{ baseUrl: options.baseUrl }];
  const normalized = rawEndpoints
    .filter((endpoint) => endpoint && endpoint.baseUrl)
    .map((endpoint, order) => createTransportEndpoint(endpoint, order))
    .sort((left, right) => left.priority - right.priority || left.order - right.order);
  if (normalized.length === 0) {
    throw new Error("at least one central service endpoint is required");
  }
  return normalized;
}

function buildClientRuntime(options = {}) {
  const timeoutMs = Number(options.timeoutMs ?? DEFAULT_TIMEOUT_MS);
  const normalizedTimeoutMs = Number.isFinite(timeoutMs) && timeoutMs > 0 ? timeoutMs : DEFAULT_TIMEOUT_MS;
  const endpoints = normalizeEndpoints(options);
  return {
    baseUrl: endpoints[0].baseUrl,
    timeoutMs: normalizedTimeoutMs,
    endpoints,
  };
}

function loadCentralServiceOptionsFromEnv(overrides = {}) {
  const resolvedTimeoutMs = Number(
    overrides.timeoutMs ?? process.env.CENTRAL_SERVICE_TIMEOUT_MS ?? process.env.CENTRAL_SERVICE_E2E_TIMEOUT_MS ?? DEFAULT_TIMEOUT_MS
  );
  const rawEndpoints = process.env.CENTRAL_SERVICE_ENDPOINTS_JSON;
  if (rawEndpoints) {
    const parsed = JSON.parse(rawEndpoints);
    return {
      ...overrides,
      endpoints: parsed,
      baseUrl: overrides.baseUrl,
      timeoutMs: Number.isFinite(resolvedTimeoutMs) && resolvedTimeoutMs > 0 ? resolvedTimeoutMs : DEFAULT_TIMEOUT_MS,
    };
  }

  return {
    ...overrides,
    baseUrl: (overrides.baseUrl || process.env.CENTRAL_SERVICE_BASEURL || DEFAULT_BASE_URL).replace(/\/+$/, ""),
    timeoutMs: Number.isFinite(resolvedTimeoutMs) && resolvedTimeoutMs > 0 ? resolvedTimeoutMs : DEFAULT_TIMEOUT_MS,
  };
}

function buildUrl(baseUrl, requestPath) {
  const base = normalizeBaseUrl(baseUrl);
  const normalizedPath = requestPath.startsWith("/") ? requestPath : "/" + requestPath;
  return base + normalizedPath;
}

function looksLikeJson(text) {
  if (!text) return false;
  const normalizedText = String(text).trimStart();
  return normalizedText.startsWith("{") || normalizedText.startsWith("[");
}

function parseError(method, url, statusCode, bodyText) {
  const rawBody = bodyText ?? "";
  const trimmedBody = String(rawBody).trim();
  if (!looksLikeJson(trimmedBody)) {
    return new CentralServiceError({
      httpStatus: statusCode,
      method,
      url,
      kind: "PlainText",
      message: trimmedBody || ("HTTP " + statusCode),
      rawBody,
    });
  }

  let parsedBody = null;
  try {
    parsedBody = JSON.parse(trimmedBody);
  } catch {
    return new CentralServiceError({
      httpStatus: statusCode,
      method,
      url,
      kind: "PlainText",
      message: trimmedBody || ("HTTP " + statusCode),
      rawBody,
    });
  }

  if (parsedBody && typeof parsedBody === "object" && parsedBody.errors) {
    return new CentralServiceError({
      httpStatus: statusCode,
      method,
      url,
      kind: "ValidationProblemDetails",
      message: parsedBody.title || "Validation error",
      rawBody,
    });
  }

  if (parsedBody && typeof parsedBody === "object" && parsedBody.title && parsedBody.status) {
    return new CentralServiceError({
      httpStatus: statusCode,
      method,
      url,
      kind: "ProblemDetails",
      message: parsedBody.title || "ProblemDetails",
      rawBody,
    });
  }

  if (parsedBody && typeof parsedBody === "object" && Object.prototype.hasOwnProperty.call(parsedBody, "success")) {
    return new CentralServiceError({
      httpStatus: statusCode,
      method,
      url,
      kind: "ApiResponse",
      message: parsedBody.errorMessage || "ApiResponse error",
      errorCode: parsedBody.errorCode ?? null,
      rawBody,
    });
  }

  return new CentralServiceError({
    httpStatus: statusCode,
    method,
    url,
    kind: "Unknown",
    message: "Unknown error",
    rawBody,
  });
}

function appendTransportContext(message, transportResult) {
  const segments = [
    "端点=" + transportResult.baseUrl,
    "尝试=" + transportResult.attempt + "/" + transportResult.maxAttempts,
  ];
  if (transportResult.skippedEndpoints.length > 0) {
    segments.push("已跳过=" + transportResult.skippedEndpoints.join("、"));
  }
  return (message || "") + " (" + segments.join("; ") + ")";
}

function createParsedError(method, transportResult, error) {
  return new CentralServiceError({
    httpStatus: error.httpStatus,
    method,
    url: transportResult.url,
    kind: error.kind,
    message: appendTransportContext(error.message, transportResult),
    errorCode: error.errorCode,
    rawBody: error.rawBody,
  });
}

function createTransportError(method, transportError) {
  return new CentralServiceError({
    httpStatus: 503,
    method,
    url: transportError.lastUrl || transportError.path,
    kind: "Transport",
    message: transportError.message,
    rawBody: transportError.rawDetail,
  });
}

function requestHttpJson({ method, url, body, timeoutMs }) {
  return new Promise((resolve, reject) => {
    const requestUrl = new URL(url);
    const isHttps = requestUrl.protocol === "https:";
    const transport = isHttps ? https : http;
    const payload = body == null ? null : JSON.stringify(body);

    const request = transport.request(
      {
        protocol: requestUrl.protocol,
        hostname: requestUrl.hostname,
        port: requestUrl.port ? Number(requestUrl.port) : isHttps ? 443 : 80,
        path: requestUrl.pathname + requestUrl.search,
        method,
        headers: {
          Accept: "application/json",
          ...(payload
            ? {
                "Content-Type": "application/json; charset=utf-8",
                "Content-Length": Buffer.byteLength(payload),
              }
            : {}),
        },
      },
      (response) => {
        const chunks = [];
        response.on("data", (chunk) => chunks.push(chunk));
        response.on("end", () => {
          resolve({
            statusCode: response.statusCode || 0,
            bodyText: Buffer.concat(chunks).toString("utf8"),
          });
        });
      }
    );

    request.on("error", (error) => reject(error));
    request.setTimeout(timeoutMs, () => {
      request.destroy(new Error("timeout after " + timeoutMs + "ms"));
    });

    if (payload) {
      request.write(payload);
    }

    request.end();
  });
}

class CircuitBreakerState {
  constructor({ failureThreshold, breakDurationMinutes, recoveryThreshold }) {
    this.failureThreshold = Math.max(1, Number(failureThreshold ?? 1) || 1);
    this.breakDurationMs = Math.max(1, Number(breakDurationMinutes ?? 1) || 1) * 60 * 1000;
    this.recoveryThreshold = Math.max(1, Number(recoveryThreshold ?? 1) || 1);
    this.mode = "closed";
    this.failureCount = 0;
    this.halfOpenSuccessCount = 0;
    this.openUntilMs = 0;
  }

  tryAllowRequest(nowMs) {
    if (this.mode === "open") {
      if (nowMs >= this.openUntilMs) {
        this.mode = "half-open";
        this.failureCount = 0;
        this.halfOpenSuccessCount = 0;
        return { allowed: true, skipReason: null };
      }

      const remainingSeconds = Math.max(1, Math.ceil((this.openUntilMs - nowMs) / 1000));
      return { allowed: false, skipReason: "熔断开启，剩余约 " + remainingSeconds + " 秒" };
    }

    return { allowed: true, skipReason: null };
  }

  reportSuccess() {
    if (this.mode === "half-open") {
      this.halfOpenSuccessCount += 1;
      if (this.halfOpenSuccessCount >= this.recoveryThreshold) {
        this.mode = "closed";
        this.failureCount = 0;
        this.halfOpenSuccessCount = 0;
        this.openUntilMs = 0;
      }
      return;
    }

    this.failureCount = 0;
  }

  reportFailure(nowMs) {
    if (this.mode === "half-open") {
      this.open(nowMs);
      return;
    }

    this.failureCount += 1;
    if (this.failureCount >= this.failureThreshold) {
      this.open(nowMs);
    }
  }

  open(nowMs) {
    this.mode = "open";
    this.failureCount = 0;
    this.halfOpenSuccessCount = 0;
    this.openUntilMs = nowMs + this.breakDurationMs;
  }
}

class TransportExhaustedError extends Error {
  constructor({ method, path, lastUrl, skippedEndpoints, failureSummaries, innerError }) {
    const segments = [];
    if (skippedEndpoints.length > 0) {
      segments.push("跳过端点: " + skippedEndpoints.join("; "));
    }
    if (failureSummaries.length > 0) {
      segments.push("失败详情: " + failureSummaries.join("; "));
    }
    if (segments.length === 0) {
      segments.push("未找到可用的中心服务端点。");
    }
    const summary = segments.join(" | ");
    super("中心服务调用失败，所有可用端点均已耗尽。 " + summary);
    this.name = "TransportExhaustedError";
    this.method = method;
    this.path = path;
    this.lastUrl = lastUrl || "";
    this.rawDetail = summary;
    this.innerError = innerError || null;
  }
}

class MultiEndpointTransport {
  constructor(endpoints, timeoutMs) {
    this.endpoints = endpoints;
    this.timeoutMs = timeoutMs;
  }

  async send(method, path, body) {
    const skippedEndpoints = [];
    const failureSummaries = [];
    let lastError = null;
    let lastUrl = null;

    for (const endpoint of this.endpoints) {
      const nowMs = Date.now();
      if (endpoint.circuitBreaker) {
        const verdict = endpoint.circuitBreaker.tryAllowRequest(nowMs);
        if (!verdict.allowed) {
          skippedEndpoints.push(endpoint.baseUrl + "（" + verdict.skipReason + "）");
          continue;
        }
      }

      for (let attempt = 1; attempt <= endpoint.maxAttempts; attempt += 1) {
        const url = buildUrl(endpoint.baseUrl, path);
        lastUrl = url;
        try {
          const response = await requestHttpJson({ method, url, body, timeoutMs: this.timeoutMs });
          if (endpoint.circuitBreaker) {
            endpoint.circuitBreaker.reportSuccess();
          }
          return {
            baseUrl: endpoint.baseUrl,
            url,
            attempt,
            maxAttempts: endpoint.maxAttempts,
            skippedEndpoints: skippedEndpoints.slice(),
            statusCode: response.statusCode,
            bodyText: response.bodyText,
          };
        } catch (error) {
          lastError = error;
          if (endpoint.circuitBreaker) {
            endpoint.circuitBreaker.reportFailure(Date.now());
          }
          failureSummaries.push(
            endpoint.baseUrl + " 第 " + attempt + "/" + endpoint.maxAttempts + " 次失败：" + error.name + ": " + error.message
          );
        }
      }
    }

    throw new TransportExhaustedError({
      method,
      path,
      lastUrl,
      skippedEndpoints,
      failureSummaries,
      innerError: lastError,
    });
  }
}

module.exports = {
  CentralServiceError,
  buildClientRuntime,
  buildUrl,
  createParsedError,
  createTransportError,
  loadCentralServiceOptionsFromEnv,
  MultiEndpointTransport,
  parseError,
};
