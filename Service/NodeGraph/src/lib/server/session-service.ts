import { createEmptyGraph } from "@/lib/nodegraph/factories";
import { migrateGraphWithNodeLibrary } from "@/lib/nodegraph/runtime-library-migration";
import { nodeFieldOptionsResponseSchema } from "@/lib/nodegraph/schema";
import type {
  CompletionWebhookPayload,
  CreateSessionRequest,
  CreateSessionResponse,
  EditorSessionPayload,
  LocaleCode,
  NodeFieldOptionsResponse,
  NodeGraphDocument,
  NodeLibraryField,
  NodeGraphSession,
  RuntimeLibraryRefreshResult,
} from "@/lib/nodegraph/types";
import { getServerConfig } from "@/lib/server/config";
import { HttpError } from "@/lib/server/errors";
import { resolveAccessType, resolveBaseUrl } from "@/lib/server/network";
import { refreshRuntimeLibrary, requireRuntimeEntry } from "@/lib/server/runtime-service";
import { getRuntimeStore } from "@/lib/server/store";

function nowIso() {
  return new Date().toISOString();
}

function buildEditorUrl(baseUrl: string, sessionId: string) {
  return new URL(`/editor/${sessionId}`, baseUrl).toString();
}

function toEditorRuntimePayload(runtimeId: string) {
  const runtime = requireRuntimeEntry(runtimeId);
  return {
    runtimeId: runtime.runtimeId,
    domain: runtime.domain,
    clientName: runtime.clientName,
    libraryVersion: runtime.libraryVersion,
    capabilities: runtime.capabilities,
    expiresAt: runtime.expiresAt,
  };
}

function resolveSelectField(
  session: NodeGraphSession,
  nodeType: string,
  fieldKey: string,
): NodeLibraryField & { kind: "select" } {
  const runtime = requireRuntimeEntry(session.runtimeId);
  const template = runtime.nodeLibrary.find((item) => item.type === nodeType);
  if (!template) {
    throw new HttpError(`The node type "${nodeType}" was not found in this session.`, 404);
  }

  const field = template.fields?.find((item) => item.key === fieldKey);
  if (!field) {
    throw new HttpError(`The field "${fieldKey}" was not found on node type "${nodeType}".`, 404);
  }

  if (field.kind !== "select") {
    throw new HttpError(`The field "${fieldKey}" does not provide remote select options.`, 422);
  }

  return field;
}

async function requestFieldOptions(
  endpoint: string,
  params: {
    domain: string;
    fieldKey: string;
    locale: LocaleCode;
    nodeType: string;
  },
): Promise<NodeFieldOptionsResponse> {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), getServerConfig().libraryTimeoutMs);
  const requestUrl = new URL(endpoint);
  requestUrl.searchParams.set("domain", params.domain);
  requestUrl.searchParams.set("fieldKey", params.fieldKey);
  requestUrl.searchParams.set("locale", params.locale);
  requestUrl.searchParams.set("nodeType", params.nodeType);

  try {
    const response = await fetch(requestUrl.toString(), {
      method: "GET",
      headers: {
        accept: "application/json",
      },
      signal: controller.signal,
      cache: "no-store",
    });

    if (!response.ok) {
      throw new HttpError(`Field options endpoint returned ${response.status}.`, 502);
    }

    const parsed = nodeFieldOptionsResponseSchema.safeParse(await response.json());
    if (!parsed.success) {
      throw new HttpError("The field options payload is invalid.", 502);
    }

    return parsed.data;
  } catch (error) {
    if (error instanceof HttpError) {
      throw error;
    }

    throw new HttpError("NodeGraph could not retrieve remote field options.", 502);
  } finally {
    clearTimeout(timeout);
  }
}

async function notifyClient(session: NodeGraphSession, graph: NodeGraphDocument) {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), getServerConfig().webhookTimeoutMs);
  const payload: CompletionWebhookPayload = {
    sessionId: session.sessionId,
    runtimeId: session.runtimeId,
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

/**
 * 基于已注册运行时创建一个新的编辑会话。
 */
export async function createSession(
  request: Request,
  input: CreateSessionRequest,
): Promise<CreateSessionResponse> {
  const runtime = requireRuntimeEntry(input.runtimeId);
  const sessionId = `ngs_${crypto.randomUUID()}`;
  const config = getServerConfig();
  const baseUrl = resolveBaseUrl(request, config.publicBaseUrl, config.privateBaseUrl);
  const accessType = resolveAccessType(request);
  const now = nowIso();
  const session: NodeGraphSession = {
    sessionId,
    runtimeId: runtime.runtimeId,
    domain: runtime.domain,
    clientName: runtime.clientName,
    graph: input.graph ?? createEmptyGraph(runtime.domain),
    metadata: input.metadata ?? {},
    accessType,
    editorUrl: buildEditorUrl(baseUrl, sessionId),
    status: "draft",
    completionWebhook: input.completionWebhook,
    createdAt: now,
    updatedAt: now,
  };

  getRuntimeStore().sessions.set(sessionId, session);

  return {
    sessionId,
    runtimeId: runtime.runtimeId,
    editorUrl: session.editorUrl,
    accessType,
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

  const runtime = requireRuntimeEntry(session.runtimeId);

  return {
    session,
    runtime: toEditorRuntimePayload(runtime.runtimeId),
    nodeLibrary: runtime.nodeLibrary,
    typeMappings: runtime.typeMappings,
  };
}

/**
 * 强制刷新宿主节点库，并把最新运行时元数据回传给编辑页。
 * 图迁移逻辑在当前阶段先由调用方基于返回数据自行处理。
 */
export async function refreshSessionRuntimeLibrary(
  sessionId: string,
  graph?: NodeGraphDocument,
): Promise<RuntimeLibraryRefreshResult> {
  const session = getSession(sessionId);
  if (!session) {
    throw new HttpError("NodeGraph session was not found.", 404);
  }

  const runtime = await refreshRuntimeLibrary(session.runtimeId);
  const migratedGraph = migrateGraphWithNodeLibrary(graph ?? session.graph, runtime.nodeLibrary);
  const updatedSession: NodeGraphSession = {
    ...session,
    graph: migratedGraph,
    updatedAt: nowIso(),
  };

  getRuntimeStore().sessions.set(sessionId, updatedSession);

  return {
    runtime: toEditorRuntimePayload(runtime.runtimeId),
    nodeLibrary: runtime.nodeLibrary,
    typeMappings: runtime.typeMappings,
    migratedGraph,
  };
}

export async function getFieldOptions(
  sessionId: string,
  input: {
    fieldKey: string;
    locale: LocaleCode;
    nodeType: string;
  },
) {
  const session = getSession(sessionId);
  if (!session) {
    throw new HttpError("NodeGraph session was not found.", 404);
  }

  const field = resolveSelectField(session, input.nodeType, input.fieldKey);

  return requestFieldOptions(field.optionsEndpoint, {
    domain: session.domain,
    fieldKey: input.fieldKey,
    locale: input.locale,
    nodeType: input.nodeType,
  });
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
