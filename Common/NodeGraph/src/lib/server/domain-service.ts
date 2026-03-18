import { nodeLibraryEnvelopeSchema } from "@/lib/nodegraph/schema";
import type { CreateSessionRequest, DomainRegistryEntry, NodeLibraryItem } from "@/lib/nodegraph/types";
import { getServerConfig } from "@/lib/server/config";
import { HttpError } from "@/lib/server/errors";
import { getRuntimeStore } from "@/lib/server/store";

function nowIso() {
  return new Date().toISOString();
}

export function normalizeNodeLibraryPayload(payload: unknown): NodeLibraryItem[] {
  const parsed = nodeLibraryEnvelopeSchema.safeParse(payload);
  if (!parsed.success) {
    throw new HttpError("The client node library payload is invalid.", 502);
  }

  return Array.isArray(parsed.data) ? parsed.data : parsed.data.nodes;
}

async function fetchNodeLibrary(endpoint: string) {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), getServerConfig().libraryTimeoutMs);

  try {
    const response = await fetch(endpoint, {
      method: "GET",
      headers: {
        accept: "application/json",
      },
      signal: controller.signal,
      cache: "no-store",
    });

    if (!response.ok) {
      throw new HttpError(`Failed to fetch node library from client: ${response.status}.`, 502);
    }

    return normalizeNodeLibraryPayload(await response.json());
  } catch (error) {
    if (error instanceof HttpError) {
      throw error;
    }

    throw new HttpError("NodeGraph could not retrieve the client node library.", 502);
  } finally {
    clearTimeout(timeout);
  }
}

export async function ensureDomain(input: CreateSessionRequest) {
  const store = getRuntimeStore();
  const existing = store.domains.get(input.domain);
  const shouldRefresh =
    !existing ||
    existing.nodeLibraryEndpoint !== input.nodeLibraryEndpoint ||
    existing.completionWebhook !== input.completionWebhook;

  if (!shouldRefresh) {
    const updated: DomainRegistryEntry = {
      ...existing,
      clientName: input.clientName ?? existing.clientName,
      updatedAt: nowIso(),
    };
    store.domains.set(input.domain, updated);

    return {
      entry: updated,
      domainCached: true,
    };
  }

  const nodeLibrary = await fetchNodeLibrary(input.nodeLibraryEndpoint);
  if (!nodeLibrary.length) {
    throw new HttpError("The client returned an empty node library for this domain.", 422);
  }

  const timestamp = nowIso();
  const entry: DomainRegistryEntry = {
    domain: input.domain,
    clientName: input.clientName,
    nodeLibraryEndpoint: input.nodeLibraryEndpoint,
    completionWebhook: input.completionWebhook,
    nodeLibrary,
    createdAt: existing?.createdAt ?? timestamp,
    updatedAt: timestamp,
  };

  store.domains.set(input.domain, entry);

  return {
    entry,
    domainCached: Boolean(existing),
  };
}

export function getDomain(domain: string) {
  return getRuntimeStore().domains.get(domain);
}
