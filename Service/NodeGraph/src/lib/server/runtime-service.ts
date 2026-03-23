import { nodeLibraryEnvelopeSchema, runtimeLibraryResponseSchema } from "@/lib/nodegraph/schema";
import type {
  RuntimeCapabilities,
  RuntimeRegistrationRequest,
  RuntimeRegistrationResponse,
  RuntimeRegistryEntry,
} from "@/lib/nodegraph/types";
import { getServerConfig } from "@/lib/server/config";
import { HttpError } from "@/lib/server/errors";
import { getRuntimeStore } from "@/lib/server/store";

function nowIso() {
  return new Date().toISOString();
}

function buildExpiryIso(now = Date.now()) {
  return new Date(now + getServerConfig().runtimeCacheTtlMs).toISOString();
}

function isExpired(entry: Pick<RuntimeRegistryEntry, "expiresAt">, now = Date.now()) {
  return new Date(entry.expiresAt).getTime() <= now;
}

function normalizeCapabilities(input?: Partial<RuntimeCapabilities>): RuntimeCapabilities {
  return {
    canExecute: input?.canExecute ?? true,
    canDebug: input?.canDebug ?? false,
    canProfile: input?.canProfile ?? false,
  };
}

function buildRuntimeControlUrl(controlBaseUrl: string, path: string) {
  const normalizedBaseUrl = controlBaseUrl.endsWith("/") ? controlBaseUrl : `${controlBaseUrl}/`;
  return new URL(path.replace(/^\//, ""), normalizedBaseUrl).toString();
}

async function fetchRuntimeLibrary(controlBaseUrl: string) {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), getServerConfig().libraryTimeoutMs);

  try {
    const response = await fetch(buildRuntimeControlUrl(controlBaseUrl, "library"), {
      method: "GET",
      headers: {
        accept: "application/json",
      },
      signal: controller.signal,
      cache: "no-store",
    });

    if (!response.ok) {
      throw new HttpError(`Runtime library endpoint returned ${response.status}.`, 502);
    }

    const parsed = runtimeLibraryResponseSchema.safeParse(await response.json());
    if (!parsed.success) {
      throw new HttpError("The runtime library payload is invalid.", 502);
    }

    return parsed.data;
  } catch (error) {
    if (error instanceof HttpError) {
      throw error;
    }

    throw new HttpError("NodeGraph could not refresh the runtime library.", 502);
  } finally {
    clearTimeout(timeout);
  }
}

/**
 * 读取运行时缓存条目；如果已过期，会先从内存中移除再返回空值。
 */
export function getRuntimeEntry(runtimeId: string) {
  const store = getRuntimeStore();
  const entry = store.runtimes.get(runtimeId);
  if (!entry) {
    return undefined;
  }

  if (isExpired(entry)) {
    store.runtimes.delete(runtimeId);
    return undefined;
  }

  return entry;
}

/**
 * 要求运行时缓存存在且未过期，否则抛出 404。
 */
export function requireRuntimeEntry(runtimeId: string) {
  const entry = getRuntimeEntry(runtimeId);
  if (!entry) {
    throw new HttpError(`The runtime "${runtimeId}" was not found or has expired.`, 404);
  }

  return entry;
}

/**
 * 显式注册宿主运行时及其当前节点库。
 */
export function registerRuntime(input: RuntimeRegistrationRequest): RuntimeRegistrationResponse {
  const parsedLibrary = nodeLibraryEnvelopeSchema.safeParse(input.library);
  if (!parsedLibrary.success) {
    throw new HttpError("The runtime node library payload is invalid.", 422);
  }

  if (!parsedLibrary.data.nodes.length) {
    throw new HttpError("The runtime node library cannot be empty.", 422);
  }

  const store = getRuntimeStore();
  const existing = getRuntimeEntry(input.runtimeId);
  const timestamp = nowIso();
  const expiresAt = buildExpiryIso();
  const cached =
    existing !== undefined &&
    existing.libraryVersion === input.libraryVersion &&
    existing.controlBaseUrl === input.controlBaseUrl &&
    existing.domain === input.domain;

  const nextEntry: RuntimeRegistryEntry = {
    runtimeId: input.runtimeId,
    domain: input.domain,
    clientName: input.clientName,
    controlBaseUrl: input.controlBaseUrl,
    libraryVersion: input.libraryVersion,
    capabilities: normalizeCapabilities(input.capabilities),
    nodeLibrary: parsedLibrary.data.nodes,
    typeMappings: parsedLibrary.data.typeMappings,
    createdAt: existing?.createdAt ?? timestamp,
    updatedAt: timestamp,
    expiresAt,
  };

  store.runtimes.set(input.runtimeId, nextEntry);

  return {
    runtimeId: input.runtimeId,
    cached,
    expiresAt,
    libraryVersion: nextEntry.libraryVersion,
  };
}

/**
 * 通过宿主控制端点重新拉取节点库，并刷新缓存与 TTL。
 */
export async function refreshRuntimeLibrary(runtimeId: string) {
  const existing = requireRuntimeEntry(runtimeId);
  const payload = await fetchRuntimeLibrary(existing.controlBaseUrl);
  const timestamp = nowIso();
  const refreshed: RuntimeRegistryEntry = {
    ...existing,
    nodeLibrary: payload.library.nodes,
    typeMappings: payload.library.typeMappings,
    libraryVersion: payload.libraryVersion ?? existing.libraryVersion,
    updatedAt: timestamp,
    expiresAt: buildExpiryIso(),
  };

  getRuntimeStore().runtimes.set(runtimeId, refreshed);
  return refreshed;
}
