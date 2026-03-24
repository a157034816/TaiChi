import { createDebugSessionRequestSchema, debugSessionPayloadSchema, updateDebugBreakpointsRequestSchema } from "@/lib/nodegraph/schema";
import type {
  ActiveDebugSession,
  CloseDebugSessionResponse,
  CreateDebugSessionRequest,
  DebugSessionPayload,
} from "@/lib/nodegraph/types";
import { getServerConfig } from "@/lib/server/config";
import { HttpError } from "@/lib/server/errors";
import { requireRuntimeEntry } from "@/lib/server/runtime-service";
import { getSession } from "@/lib/server/session-service";
import { getRuntimeStore } from "@/lib/server/store";

function nowIso() {
  return new Date().toISOString();
}

function buildRuntimeControlUrl(controlBaseUrl: string, path: string) {
  const normalizedBaseUrl = controlBaseUrl.endsWith("/") ? controlBaseUrl : `${controlBaseUrl}/`;
  return new URL(path.replace(/^\//, ""), normalizedBaseUrl).toString();
}

function requireDebugCapability(sessionId: string) {
  const session = getSession(sessionId);
  if (!session) {
    throw new HttpError("NodeGraph session was not found.", 404);
  }

  const runtime = requireRuntimeEntry(session.runtimeId);
  if (!runtime.capabilities.canDebug) {
    throw new HttpError("The runtime does not expose debug capability.", 409);
  }

  return {
    session,
    runtime,
  };
}

async function requestRuntimeDebugPayload(
  runtimeControlBaseUrl: string,
  path: string,
  init: RequestInit,
): Promise<DebugSessionPayload> {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), getServerConfig().libraryTimeoutMs);

  try {
    const response = await fetch(buildRuntimeControlUrl(runtimeControlBaseUrl, path), {
      ...init,
      headers: {
        "content-type": "application/json",
        accept: "application/json",
        ...(init.headers ?? {}),
      },
      signal: controller.signal,
      cache: "no-store",
    });

    if (!response.ok) {
      throw new HttpError(`Runtime debug endpoint returned ${response.status}.`, 502);
    }

    const parsed = debugSessionPayloadSchema.safeParse(await response.json());
    if (!parsed.success) {
      throw new HttpError("The runtime debug payload is invalid.", 502);
    }

    return parsed.data;
  } catch (error) {
    if (error instanceof HttpError) {
      throw error;
    }

    throw new HttpError("NodeGraph could not proxy the runtime debug request.", 502);
  } finally {
    clearTimeout(timeout);
  }
}

async function requestRuntimeDebugClose(
  runtimeControlBaseUrl: string,
  debugSessionId: string,
): Promise<CloseDebugSessionResponse> {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), getServerConfig().libraryTimeoutMs);

  try {
    const response = await fetch(buildRuntimeControlUrl(runtimeControlBaseUrl, `debug/sessions/${debugSessionId}`), {
      method: "DELETE",
      headers: {
        accept: "application/json",
      },
      signal: controller.signal,
      cache: "no-store",
    });

    if (!response.ok) {
      throw new HttpError(`Runtime debug close endpoint returned ${response.status}.`, 502);
    }

    const payload = (await response.json()) as CloseDebugSessionResponse;
    return {
      closed: payload.closed === true,
    };
  } catch (error) {
    if (error instanceof HttpError) {
      throw error;
    }

    throw new HttpError("NodeGraph could not close the runtime debug session.", 502);
  } finally {
    clearTimeout(timeout);
  }
}

function toActiveDebugSession(
  sessionId: string,
  runtimeId: string,
  payload: DebugSessionPayload,
  existing?: ActiveDebugSession,
): ActiveDebugSession {
  return {
    ...payload,
    sessionId,
    runtimeId,
    createdAt: existing?.createdAt ?? nowIso(),
    updatedAt: nowIso(),
  };
}

export function getActiveDebugSession(sessionId: string) {
  return getRuntimeStore().debugSessions.get(sessionId);
}

export async function createDebugSession(sessionId: string, input: CreateDebugSessionRequest) {
  const parsedInput = createDebugSessionRequestSchema.parse(input);
  const { runtime } = requireDebugCapability(sessionId);
  const existing = getActiveDebugSession(sessionId);

  if (existing) {
    await requestRuntimeDebugClose(runtime.controlBaseUrl, existing.debugSessionId);
    getRuntimeStore().debugSessions.delete(sessionId);
  }

  const payload = await requestRuntimeDebugPayload(runtime.controlBaseUrl, "debug/sessions", {
    method: "POST",
    body: JSON.stringify(parsedInput),
  });
  const nextSession = toActiveDebugSession(sessionId, runtime.runtimeId, payload);
  getRuntimeStore().debugSessions.set(sessionId, nextSession);
  return nextSession;
}

export async function updateDebugSessionBreakpoints(sessionId: string, breakpoints: string[]) {
  const { runtime } = requireDebugCapability(sessionId);
  const active = getActiveDebugSession(sessionId);
  if (!active) {
    throw new HttpError("NodeGraph debug session was not found.", 404);
  }

  const parsedInput = updateDebugBreakpointsRequestSchema.parse({
    breakpoints,
  });
  const payload = await requestRuntimeDebugPayload(
    runtime.controlBaseUrl,
    `debug/sessions/${active.debugSessionId}/breakpoints`,
    {
      method: "PUT",
      body: JSON.stringify(parsedInput),
    },
  );
  const nextSession = toActiveDebugSession(sessionId, runtime.runtimeId, payload, active);
  getRuntimeStore().debugSessions.set(sessionId, nextSession);
  return nextSession;
}

export async function stepDebugSession(sessionId: string) {
  const { runtime } = requireDebugCapability(sessionId);
  const active = getActiveDebugSession(sessionId);
  if (!active) {
    throw new HttpError("NodeGraph debug session was not found.", 404);
  }

  const payload = await requestRuntimeDebugPayload(
    runtime.controlBaseUrl,
    `debug/sessions/${active.debugSessionId}/step`,
    {
      method: "POST",
      body: JSON.stringify({}),
    },
  );
  const nextSession = toActiveDebugSession(sessionId, runtime.runtimeId, payload, active);
  getRuntimeStore().debugSessions.set(sessionId, nextSession);
  return nextSession;
}

export async function continueDebugSession(sessionId: string) {
  const { runtime } = requireDebugCapability(sessionId);
  const active = getActiveDebugSession(sessionId);
  if (!active) {
    throw new HttpError("NodeGraph debug session was not found.", 404);
  }

  const payload = await requestRuntimeDebugPayload(
    runtime.controlBaseUrl,
    `debug/sessions/${active.debugSessionId}/continue`,
    {
      method: "POST",
      body: JSON.stringify({}),
    },
  );
  const nextSession = toActiveDebugSession(sessionId, runtime.runtimeId, payload, active);
  getRuntimeStore().debugSessions.set(sessionId, nextSession);
  return nextSession;
}

export async function closeDebugSession(sessionId: string) {
  const { runtime } = requireDebugCapability(sessionId);
  const active = getActiveDebugSession(sessionId);
  if (!active) {
    throw new HttpError("NodeGraph debug session was not found.", 404);
  }

  const result = await requestRuntimeDebugClose(runtime.controlBaseUrl, active.debugSessionId);
  getRuntimeStore().debugSessions.delete(sessionId);
  return result;
}
