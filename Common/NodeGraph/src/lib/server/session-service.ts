import { createEmptyGraph } from "@/lib/nodegraph/factories";
import type {
  CompletionWebhookPayload,
  CreateSessionRequest,
  CreateSessionResponse,
  EditorSessionPayload,
  NodeGraphDocument,
  NodeGraphSession,
} from "@/lib/nodegraph/types";
import { getServerConfig } from "@/lib/server/config";
import { ensureDomain, getDomain } from "@/lib/server/domain-service";
import { HttpError } from "@/lib/server/errors";
import { resolveAccessType, resolveBaseUrl } from "@/lib/server/network";
import { getRuntimeStore } from "@/lib/server/store";

function nowIso() {
  return new Date().toISOString();
}

function buildEditorUrl(baseUrl: string, sessionId: string) {
  return new URL(`/editor/${sessionId}`, baseUrl).toString();
}

async function notifyClient(session: NodeGraphSession, graph: NodeGraphDocument) {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), getServerConfig().webhookTimeoutMs);
  const payload: CompletionWebhookPayload = {
    sessionId: session.sessionId,
    domain: session.domain,
    graph,
    metadata: session.metadata,
    completedAt: session.completedAt ?? nowIso(),
    status: "completed",
  };

  try {
    const response = await fetch(session.completionWebhook, {
      method: "POST",
      headers: {
        "content-type": "application/json",
      },
      body: JSON.stringify(payload),
      signal: controller.signal,
    });

    if (!response.ok) {
      throw new HttpError(`Completion webhook returned ${response.status}.`, 502);
    }

    return response.status;
  } catch (error) {
    if (error instanceof HttpError) {
      throw error;
    }

    throw new HttpError("NodeGraph could not deliver the completion webhook.", 502);
  } finally {
    clearTimeout(timeout);
  }
}

export async function createSession(
  request: Request,
  input: CreateSessionRequest,
): Promise<CreateSessionResponse> {
  const { domainCached } = await ensureDomain(input);
  const sessionId = `ngs_${crypto.randomUUID()}`;
  const config = getServerConfig();
  const baseUrl = resolveBaseUrl(request, config.publicBaseUrl, config.privateBaseUrl);
  const accessType = resolveAccessType(request);
  const now = nowIso();
  const session: NodeGraphSession = {
    sessionId,
    domain: input.domain,
    clientName: input.clientName,
    graph: input.graph ?? createEmptyGraph(input.domain),
    metadata: input.metadata ?? {},
    accessType,
    editorUrl: buildEditorUrl(baseUrl, sessionId),
    status: "draft",
    nodeLibraryEndpoint: input.nodeLibraryEndpoint,
    completionWebhook: input.completionWebhook,
    createdAt: now,
    updatedAt: now,
  };

  getRuntimeStore().sessions.set(sessionId, session);

  return {
    sessionId,
    editorUrl: session.editorUrl,
    accessType,
    domainCached,
  };
}

export function getSession(sessionId: string) {
  return getRuntimeStore().sessions.get(sessionId);
}

export function getEditorPayload(sessionId: string): EditorSessionPayload {
  const session = getSession(sessionId);
  if (!session) {
    throw new HttpError("NodeGraph session was not found.", 404);
  }

  const domain = getDomain(session.domain);
  if (!domain) {
    throw new HttpError("The domain registry entry was not found for this session.", 404);
  }

  return {
    session,
    nodeLibrary: domain.nodeLibrary,
    typeMappings: domain.typeMappings,
  };
}

export async function completeSession(sessionId: string, graph: NodeGraphDocument) {
  const session = getSession(sessionId);
  if (!session) {
    throw new HttpError("NodeGraph session was not found.", 404);
  }

  const updated: NodeGraphSession = {
    ...session,
    graph,
    status: "completed",
    updatedAt: nowIso(),
    completedAt: nowIso(),
  };

  getRuntimeStore().sessions.set(sessionId, updated);
  const webhookStatus = await notifyClient(updated, graph);

  return {
    success: true,
    delivered: true,
    webhookStatus,
  };
}
