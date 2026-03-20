import {
  DEFAULT_LOCALE,
  translate,
  type MessageKey,
  type MessageParams,
} from "@/i18n/core";

export type ApiResponse<T> = {
  success: boolean;
  errorCode?: number;
  errorKey?: string;
  errorMessage?: string;
  data: T;
};

export class ApiResponseError extends Error {
  public readonly code?: number;
  public readonly key?: string;
  public readonly messageKey?: MessageKey;

  constructor(
    message: string,
    code?: number,
    key?: string,
    messageKey?: MessageKey
  ) {
    super(message);
    this.name = "ApiResponseError";
    this.code = code;
    this.key = key;
    this.messageKey = messageKey;
  }
}

type MessageTranslator = (
  key: MessageKey,
  params?: MessageParams
) => string;

/**
 * Maps transport-level HTTP failures to stable translation keys so the UI can
 * localize frontend-generated fallback errors.
 */
function defaultHttpErrorMessageKey(status: number): MessageKey {
  if (status === 401) return "apiErrors.unauthorized";
  if (status === 403) return "apiErrors.forbidden";
  if (status === 404) return "apiErrors.notFound";
  if (status >= 500) return "apiErrors.serverError";
  return "apiErrors.requestFailed";
}

/**
 * Produces the default fallback error message in the site's default locale for
 * error paths that are generated entirely on the frontend.
 */
function defaultHttpErrorMessage(status: number): string {
  return translate(DEFAULT_LOCALE, defaultHttpErrorMessageKey(status));
}

function isProbablyHtml(text: string) {
  const t = text.trimStart().toLowerCase();
  return t.startsWith("<!doctype") || t.startsWith("<html") || t.startsWith("<");
}

async function apiRequest<T>(url: string, init?: RequestInit): Promise<T> {
  const res = await fetch(url, { ...init, credentials: "include" });
  const json = await parseJson<T>(res);

  if (!json.success) {
    throw new ApiResponseError(
      json.errorMessage ?? translate(DEFAULT_LOCALE, "apiErrors.requestFailed"),
      json.errorCode,
      json.errorKey,
      json.errorMessage ? undefined : "apiErrors.requestFailed"
    );
  }

  return json.data;
}

function safeParseJson(text: string): unknown {
  try {
    return JSON.parse(text);
  } catch {
    return null;
  }
}

function isApiResponseLike(value: unknown): value is ApiResponse<unknown> {
  return (
    !!value &&
    typeof value === "object" &&
    "success" in value &&
    typeof (value as { success?: unknown }).success === "boolean"
  );
}

async function parseJson<T>(res: Response): Promise<ApiResponse<T>> {
  const text = await res.text().catch(() => "");
  const trimmed = text.trim();

  if (!trimmed) {
    if (!res.ok) {
      throw new ApiResponseError(
        defaultHttpErrorMessage(res.status),
        res.status,
        undefined,
        defaultHttpErrorMessageKey(res.status)
      );
    }
    throw new ApiResponseError(
      translate(DEFAULT_LOCALE, "apiErrors.parseFailed"),
      res.status,
      undefined,
      "apiErrors.parseFailed"
    );
  }

  const parsed = safeParseJson(text);
  if (!parsed) {
    if (!res.ok) {
      throw new ApiResponseError(
        defaultHttpErrorMessage(res.status),
        res.status,
        undefined,
        defaultHttpErrorMessageKey(res.status)
      );
    }
    throw new ApiResponseError(
      translate(DEFAULT_LOCALE, "apiErrors.parseFailed"),
      res.status,
      undefined,
      "apiErrors.parseFailed"
    );
  }

  if (!isApiResponseLike(parsed)) {
    if (!res.ok) {
      throw new ApiResponseError(
        defaultHttpErrorMessage(res.status),
        res.status,
        undefined,
        defaultHttpErrorMessageKey(res.status)
      );
    }
    throw new ApiResponseError(
      translate(DEFAULT_LOCALE, "apiErrors.invalidResponse"),
      res.status,
      undefined,
      "apiErrors.invalidResponse"
    );
  }

  return parsed as ApiResponse<T>;
}

async function parseRawJson<T>(res: Response): Promise<T> {
  const text = await res.text().catch(() => "");
  const trimmed = text.trim();
  const parsed = trimmed ? safeParseJson(text) : null;

  if (!res.ok) {
    const message =
      typeof parsed === "string"
        ? parsed
        : parsed && typeof parsed === "object"
          ? ((parsed as { message?: unknown; errorMessage?: unknown }).errorMessage ??
              (parsed as { message?: unknown; errorMessage?: unknown }).message ??
              null)
          : null;

    const messageText = typeof message === "string" ? message.trim() : "";
    const fallback = defaultHttpErrorMessage(res.status);
    throw new ApiResponseError(
      messageText && !isProbablyHtml(messageText)
        ? messageText
        : trimmed && !isProbablyHtml(trimmed) && trimmed.length <= 200
          ? trimmed
          : fallback,
      res.status,
      undefined,
      messageText && !isProbablyHtml(messageText)
        ? undefined
        : trimmed && !isProbablyHtml(trimmed) && trimmed.length <= 200
          ? undefined
          : defaultHttpErrorMessageKey(res.status)
    );
  }

  if (!trimmed) {
    return undefined as T;
  }

  if (!parsed) {
    throw new ApiResponseError(
      translate(DEFAULT_LOCALE, "apiErrors.parseFailed"),
      res.status,
      undefined,
      "apiErrors.parseFailed"
    );
  }

  return parsed as T;
}

async function apiRawRequest<T>(url: string, init?: RequestInit): Promise<T> {
  const res = await fetch(url, { ...init, credentials: "include" });
  return await parseRawJson<T>(res);
}

export async function apiGet<T>(url: string): Promise<T> {
  return await apiRequest<T>(url);
}

export async function apiPost<T>(url: string, body?: unknown): Promise<T> {
  return await apiRequest<T>(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
}

export async function apiPut<T>(url: string, body?: unknown): Promise<T> {
  return await apiRequest<T>(url, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
}

export async function apiDelete<T>(url: string): Promise<T> {
  return await apiRequest<T>(url, { method: "DELETE" });
}

export async function apiGetRaw<T>(url: string): Promise<T> {
  return await apiRawRequest<T>(url);
}

export async function apiPostRaw<T>(url: string, body?: unknown): Promise<T> {
  const hasBody = body !== undefined;
  return await apiRawRequest<T>(url, {
    method: "POST",
    headers: hasBody ? { "Content-Type": "application/json" } : undefined,
    body: hasBody ? JSON.stringify(body) : undefined,
  });
}

/**
 * Resolves the user-facing message for an API error. Backend-provided messages
 * win; only frontend fallback messages are localized through message keys.
 */
export function getApiErrorMessage(
  error: ApiResponseError,
  translateMessage: MessageTranslator
): string {
  return error.messageKey ? translateMessage(error.messageKey) : error.message;
}
