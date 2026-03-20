import {
  normalizeI18nBundle,
  validateNodeLibraryTranslationKeys,
} from "@/lib/nodegraph/localization";
import { nodeLibraryEnvelopeSchema } from "@/lib/nodegraph/schema";
import { normalizeTypeMappings, validateNodeLibraryTypeMappings } from "@/lib/nodegraph/type-mappings";
import type {
  CreateSessionRequest,
  DomainRegistryEntry,
  I18nBundle,
  NodeLibraryItem,
  TypeMappingEntry,
} from "@/lib/nodegraph/types";
import { getServerConfig } from "@/lib/server/config";
import { HttpError } from "@/lib/server/errors";
import { getRuntimeStore } from "@/lib/server/store";

function nowIso() {
  return new Date().toISOString();
}

export function normalizeNodeLibraryPayload(payload: unknown): {
  nodes: NodeLibraryItem[];
  i18n: I18nBundle;
  typeMappings?: TypeMappingEntry[];
} {
  const parsed = nodeLibraryEnvelopeSchema.safeParse(payload);
  if (!parsed.success) {
    throw new HttpError("The client node library payload is invalid.", 502);
  }

  const nodes = parsed.data.nodes;
  const i18n = normalizeI18nBundle(parsed.data.i18n);

  try {
    const typeMappings = normalizeTypeMappings(parsed.data.typeMappings);

    validateNodeLibraryTranslationKeys(nodes, i18n);
    validateNodeLibraryTypeMappings(nodes, typeMappings);

    return {
      nodes,
      i18n,
      typeMappings,
    };
  } catch (error) {
    throw new HttpError(error instanceof Error ? error.message : "The client node library payload is invalid.", 502);
  }
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

  const { nodes: nodeLibrary, i18n, typeMappings } = await fetchNodeLibrary(input.nodeLibraryEndpoint);
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
    i18n,
    typeMappings,
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
