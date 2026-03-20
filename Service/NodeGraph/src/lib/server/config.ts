const DEFAULT_PUBLIC_BASE_URL = "http://localhost:3000";
const DEFAULT_PRIVATE_BASE_URL = "http://127.0.0.1:3000";

function readTimeout(rawValue: string | undefined, fallback: number) {
  const parsed = Number(rawValue);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
}

export function getServerConfig() {
  return {
    publicBaseUrl: process.env.NODEGRAPH_PUBLIC_BASE_URL ?? DEFAULT_PUBLIC_BASE_URL,
    privateBaseUrl: process.env.NODEGRAPH_PRIVATE_BASE_URL ?? DEFAULT_PRIVATE_BASE_URL,
    libraryTimeoutMs: readTimeout(process.env.NODEGRAPH_LIBRARY_TIMEOUT_MS, 5000),
    webhookTimeoutMs: readTimeout(process.env.NODEGRAPH_WEBHOOK_TIMEOUT_MS, 5000),
  };
}
