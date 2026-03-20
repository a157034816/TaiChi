import { enUSMessages, zhCNMessages, type Messages } from "./messages.ts";

export type { Messages } from "./messages.ts";

/**
 * Enumerates the only locales supported by the current admin site.
 */
export const SUPPORTED_LOCALES = ["zh-CN", "en-US"] as const;

/**
 * Represents the locale values accepted by the site.
 */
export type SupportedLocale = (typeof SUPPORTED_LOCALES)[number];

/**
 * Stores the locale cookie name shared by the provider and the server layout.
 */
export const LOCALE_COOKIE_NAME = "central-service-admin-locale";

/**
 * Defines the fallback locale used whenever no supported locale is available.
 */
export const DEFAULT_LOCALE: SupportedLocale = "zh-CN";

/**
 * Recursively expands a nested message tree into dot-separated lookup keys.
 */
type NestedKeyOf<T> = {
  [Key in keyof T & string]: T[Key] extends string
    ? Key
    : T[Key] extends Record<string, unknown>
      ? `${Key}.${NestedKeyOf<T[Key]>}`
      : never;
}[keyof T & string];

/**
 * Represents every translatable message key available in the catalog.
 */
export type MessageKey = NestedKeyOf<Messages>;

/**
 * Represents placeholder values used when formatting a translated message.
 */
export type MessageParams = Record<
  string,
  string | number | boolean | null | undefined
>;

/**
 * Allows callers to control locale-sensitive date formatting when needed.
 */
export type FormatDateTimeOptions = {
  timeZone?: string;
};

const messageCatalog: Record<SupportedLocale, Messages> = {
  "zh-CN": zhCNMessages,
  "en-US": enUSMessages,
};

/**
 * Checks whether the provided value already matches one of the supported
 * locale identifiers exactly.
 */
export function isSupportedLocale(value: string): value is SupportedLocale {
  return SUPPORTED_LOCALES.includes(value as SupportedLocale);
}

/**
 * Normalizes locale aliases into the two supported locale identifiers.
 */
export function normalizeLocale(
  value?: string | null
): SupportedLocale | null {
  const normalized = value?.trim().toLowerCase();
  if (!normalized) {
    return null;
  }

  if (
    normalized === "zh" ||
    normalized === "zh-cn" ||
    normalized === "zh-hans" ||
    normalized === "zh-hans-cn"
  ) {
    return "zh-CN";
  }

  if (normalized === "en" || normalized === "en-us") {
    return "en-US";
  }

  return null;
}

/**
 * Resolves an arbitrary locale value into a guaranteed supported locale.
 */
export function resolveLocale(value?: string | null): SupportedLocale {
  if (value && isSupportedLocale(value)) {
    return value;
  }

  return normalizeLocale(value) ?? DEFAULT_LOCALE;
}

/**
 * Returns the message catalog that belongs to the requested locale.
 */
export function getMessages(locale: SupportedLocale): Messages {
  return messageCatalog[locale];
}

/**
 * Returns the translated text for a given key, falling back to Simplified
 * Chinese and finally to the key itself when a translation is missing.
 */
export function translate(
  locale: SupportedLocale,
  key: MessageKey,
  params?: MessageParams
): string {
  const localized =
    getMessageValue(getMessages(locale), key) ??
    getMessageValue(getMessages(DEFAULT_LOCALE), key) ??
    key;

  return formatTemplate(localized, params);
}

/**
 * Formats a date-time value using the requested locale while keeping invalid
 * values readable instead of throwing.
 */
export function formatDateTime(
  locale: SupportedLocale,
  value?: string | null,
  options?: FormatDateTimeOptions
): string {
  if (!value) {
    return "-";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return new Intl.DateTimeFormat(locale, {
    year: "numeric",
    month: "numeric",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    hour12: false,
    ...(options?.timeZone ? { timeZone: options.timeZone } : {}),
  })
    .format(date)
    .replaceAll("\u202f", " ");
}

/**
 * Compares two strings with locale-aware ordering so lists stay stable when the
 * active locale changes.
 */
export function compareText(
  locale: SupportedLocale,
  left: string,
  right: string
): number {
  return new Intl.Collator(locale, {
    numeric: true,
    sensitivity: "accent",
  }).compare(left, right);
}

/**
 * Walks the message tree using a dot-separated path and returns the matching
 * message when it exists.
 */
function getMessageValue(
  messages: Messages,
  key: MessageKey
): string | undefined {
  const segments = key.split(".");
  let current: unknown = messages;

  for (const segment of segments) {
    if (!current || typeof current !== "object") {
      return undefined;
    }
    current = (current as Record<string, unknown>)[segment];
  }

  return typeof current === "string" ? current : undefined;
}

/**
 * Replaces `{{placeholder}}` markers with stringified parameter values.
 */
function formatTemplate(template: string, params?: MessageParams): string {
  if (!params) {
    return template;
  }

  return template.replace(/\{\{\s*([^}]+?)\s*\}\}/g, (_match, rawKey) => {
    const value = params[rawKey];
    return value == null ? "" : String(value);
  });
}
