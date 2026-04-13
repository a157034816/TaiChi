const crypto = require("crypto");
const { CentralServiceServiceClient, loadCentralServiceOptionsFromEnv } = require("../service");
const {
  CentralServiceDiscoveryClient,
  calculateNetworkScore,
} = require("../client");

function getScenario() {
  return process.env.CENTRAL_SERVICE_E2E_SCENARIO || "smoke";
}

function getTimeoutMs() {
  const value = Number(process.env.CENTRAL_SERVICE_TIMEOUT_MS || process.env.CENTRAL_SERVICE_E2E_TIMEOUT_MS || 5000);
  return Number.isFinite(value) && value > 0 ? value : 5000;
}

function getBreakWaitMs() {
  const value = Number(process.env.CENTRAL_SERVICE_E2E_BREAK_WAIT_SECONDS || 65);
  return (Number.isFinite(value) && value > 0 ? value : 65) * 1000;
}

function getServiceName() {
  return process.env.CENTRAL_SERVICE_E2E_SERVICE_NAME || "SdkE2E";
}

function getPort() {
  const value = Number(process.env.CENTRAL_SERVICE_E2E_SERVICE_PORT || 18082);
  return Number.isFinite(value) && value > 0 ? value : 18082;
}

function loadOptions() {
  return loadCentralServiceOptionsFromEnv({ timeoutMs: getTimeoutMs() });
}

function getEndpoints() {
  const options = loadOptions();
  return Array.isArray(options.endpoints) && options.endpoints.length > 0
    ? options.endpoints
    : [{ baseUrl: options.baseUrl }];
}

function createSingleEndpointOptions(endpoint) {
  return {
    baseUrl: endpoint.baseUrl,
    timeoutMs: getTimeoutMs(),
  };
}

function createRegistration(serviceId) {
  return {
    id: serviceId,
    name: getServiceName(),
    host: "127.0.0.1",
    localIp: "127.0.0.1",
    operatorIp: "127.0.0.1",
    publicIp: "127.0.0.1",
    port: getPort(),
    serviceType: "Web",
    healthCheckUrl: "/health",
    healthCheckPort: 0,
    heartbeatIntervalSeconds: 0,
    weight: 1,
    metadata: { sdk: "javascript", scenario: getScenario() },
  };
}

function createStableServiceId() {
  return process.env.CENTRAL_SERVICE_E2E_SERVICE_ID || crypto.randomUUID();
}

function assertCondition(condition, message) {
  if (!condition) {
    throw new Error("[" + getScenario() + "] " + message);
  }
}

function assertOptionalExpectedId(stepName, actualId) {
  const envName = "CENTRAL_SERVICE_E2E_EXPECTED_" + stepName.toUpperCase() + "_ID";
  const expectedId = process.env[envName];
  if (expectedId) {
    assertCondition(actualId === expectedId, stepName + " 期望 id=" + expectedId + "，实际=" + actualId);
  }
}

async function registerOnEndpoint(endpoint, serviceId) {
  const client = new CentralServiceServiceClient(createSingleEndpointOptions(endpoint));
  const registration = await client.register(createRegistration(serviceId));
  return { client, serviceId: registration.id };
}

async function deregisterQuietly(client, serviceId) {
  if (!client || !serviceId) return;
  try {
    await client.deregister(serviceId);
  } catch {
  }
}

async function runSmoke() {
  const endpoint = getEndpoints()[0];
  const service = new CentralServiceServiceClient(createSingleEndpointOptions(endpoint));
  const discovery = new CentralServiceDiscoveryClient(createSingleEndpointOptions(endpoint));
  let serviceId = "";
  try {
    const reg = await service.register(createRegistration(""));
    serviceId = reg.id;
    console.log("[js][smoke] registered id=" + serviceId);
    const listed = await discovery.list(getServiceName());
    assertCondition((listed?.services || []).some((item) => item.id === serviceId), "list 未包含刚注册的服务");
    const best = await discovery.discoverBest(getServiceName());
    assertCondition(Boolean(best?.id), "discoverBest 未返回服务 id");
    const evaluated = await discovery.evaluateNetwork(serviceId);
    console.log("[js][smoke] network evaluated score=" + calculateNetworkScore(evaluated));
    const all = await discovery.getNetworkAll();
    assertCondition(Array.isArray(all), "network/all 未返回数组");
  } finally {
    await deregisterQuietly(service, serviceId);
  }
}

async function runServiceFanout() {
  const endpoints = getEndpoints();
  assertCondition(endpoints.length >= 2, "service_fanout 至少需要 2 个端点");
  const serviceId = createStableServiceId();
  const sessions = [];
  try {
    for (const endpoint of endpoints) {
      const session = await registerOnEndpoint(endpoint, serviceId);
      sessions.push({ ...session, endpoint });
    }

    for (const session of sessions) {
      assertCondition(session.serviceId === serviceId, "端点 " + session.endpoint.baseUrl + " 未复用同一个 serviceId");
      const discovery = new CentralServiceDiscoveryClient(createSingleEndpointOptions(session.endpoint));
      const listed = await discovery.list(getServiceName());
      assertCondition((listed?.services || []).some((item) => item.id === serviceId), "端点 " + session.endpoint.baseUrl + " 未查询到注册结果");
      const best = await discovery.discoverBest(getServiceName());
      assertCondition(best?.id === serviceId, "端点 " + session.endpoint.baseUrl + " discoverBest 返回了错误实例");
    }
  } finally {
    for (const session of sessions) {
      await deregisterQuietly(session.client, session.serviceId);
    }
  }
}

async function runTransportFailover() {
  const discovery = new CentralServiceDiscoveryClient(loadOptions());
  const result = await discovery.discoverBest(getServiceName());
  assertCondition(Boolean(result?.id), "transport_failover 未返回服务实例");
  assertOptionalExpectedId("first", result.id);
}

async function runBusinessNoFailover() {
  const discovery = new CentralServiceDiscoveryClient(loadOptions());
  const endpoints = getEndpoints();
  try {
    await discovery.discoverBest(getServiceName());
    throw new Error("[" + getScenario() + "] 期望业务失败，但调用成功了");
  } catch (error) {
    assertCondition(error && error.kind !== "Transport", "业务失败场景不应被识别为传输失败");
    assertCondition(String(error.message || "").includes(endpoints[0].baseUrl), "错误信息未包含主端点上下文");
    console.log("[js][business_no_failover] kind=" + error.kind);
  }
}

async function runMaxAttempts() {
  const discovery = new CentralServiceDiscoveryClient(loadOptions());
  const result = await discovery.discoverBest(getServiceName());
  assertCondition(Boolean(result?.id), "max_attempts 未返回服务实例");
  assertOptionalExpectedId("first", result.id);
}

async function runCircuitOpen() {
  const discovery = new CentralServiceDiscoveryClient(loadOptions());
  const first = await discovery.discoverBest(getServiceName());
  const second = await discovery.discoverBest(getServiceName());
  assertCondition(Boolean(first?.id) && Boolean(second?.id), "circuit_open 未返回有效实例");
  assertOptionalExpectedId("first", first.id);
  assertOptionalExpectedId("second", second.id);
}

async function runCircuitRecovery() {
  const discovery = new CentralServiceDiscoveryClient(loadOptions());
  const first = await discovery.discoverBest(getServiceName());
  await new Promise((resolve) => setTimeout(resolve, getBreakWaitMs()));
  const second = await discovery.discoverBest(getServiceName());
  const third = await discovery.discoverBest(getServiceName());
  assertCondition(Boolean(first?.id) && Boolean(second?.id) && Boolean(third?.id), "circuit_recovery 未返回有效实例");
  assertOptionalExpectedId("first", first.id);
  assertOptionalExpectedId("second", second.id);
  assertOptionalExpectedId("third", third.id);
}

async function runHalfOpenReopen() {
  const discovery = new CentralServiceDiscoveryClient(loadOptions());
  const first = await discovery.discoverBest(getServiceName());
  await new Promise((resolve) => setTimeout(resolve, getBreakWaitMs()));
  const second = await discovery.discoverBest(getServiceName());
  const third = await discovery.discoverBest(getServiceName());
  assertCondition(Boolean(first?.id) && Boolean(second?.id) && Boolean(third?.id), "half_open_reopen 未返回有效实例");
  assertOptionalExpectedId("first", first.id);
  assertOptionalExpectedId("second", second.id);
  assertOptionalExpectedId("third", third.id);
}

async function main() {
  const options = loadOptions();
  const endpoints = getEndpoints();
  console.log("[js] scenario=" + getScenario());
  console.log("[js] baseUrl=" + options.baseUrl);
  console.log("[js] endpoints=" + JSON.stringify(endpoints));

  const handlers = {
    smoke: runSmoke,
    service_fanout: runServiceFanout,
    transport_failover: runTransportFailover,
    business_no_failover: runBusinessNoFailover,
    max_attempts: runMaxAttempts,
    circuit_open: runCircuitOpen,
    circuit_recovery: runCircuitRecovery,
    half_open_reopen: runHalfOpenReopen,
  };

  const handler = handlers[getScenario()];
  assertCondition(Boolean(handler), "不支持的场景: " + getScenario());
  await handler();
  console.log("[js] scenario passed: " + getScenario());
}

main().catch((err) => {
  console.error("[js] scenario failed:", getScenario(), err && err.stack ? err.stack : err);
  process.exitCode = 1;
});
