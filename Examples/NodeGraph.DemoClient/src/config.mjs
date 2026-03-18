function readPositiveInteger(value, fallback) {
  const parsed = Number.parseInt(value ?? "", 10);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
}

export function getDemoConfig(overrides = {}) {
  const port = overrides.port ?? readPositiveInteger(process.env.DEMO_CLIENT_PORT, 3100);
  const host = overrides.host ?? process.env.DEMO_CLIENT_HOST ?? "127.0.0.1";

  return {
    port,
    host,
    demoClientBaseUrl:
      overrides.demoClientBaseUrl ??
      process.env.DEMO_CLIENT_BASE_URL ??
      `http://localhost:${port}`,
    nodeGraphBaseUrl:
      overrides.nodeGraphBaseUrl ??
      process.env.NODEGRAPH_BASE_URL ??
      "http://localhost:3000",
    demoDomain: overrides.demoDomain ?? process.env.DEMO_CLIENT_DOMAIN ?? "demo-workflow",
    clientName: overrides.clientName ?? "NodeGraph Demo Client",
  };
}
