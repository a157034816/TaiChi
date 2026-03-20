import enCatalog from "@/i18n/en.json";
import zhCnCatalog from "@/i18n/zh-CN.json";
import type {
  I18nBundle,
  LegacyLocalizedText,
  LocaleCode,
  NodeGraphNodeData,
  NodeLibraryField,
  NodeLibraryItem,
  NodePortDefinition,
  TranslationCatalog,
} from "@/lib/nodegraph/types";

/**
 * The editor falls back to Simplified Chinese when a key is not available in
 * the active or domain-default locale.
 */
export const DEFAULT_LOCALE: LocaleCode = "zh-CN";

/**
 * Interpolation values supported by the flat `text(key, params)` runtime.
 */
export interface TextParams {
  [key: string]: boolean | number | string | null | undefined;
}

/**
 * Shared text lookup signature used by helper modules.
 */
export type TextLookup = (key: string, params?: TextParams) => string;

/**
 * The runtime bundle offered to editor components and pure helpers.
 */
export interface I18nRuntime {
  locale: LocaleCode;
  defaultLocale: LocaleCode;
  availableLocales: LocaleCode[];
  text: TextLookup;
  lookupText: (key: string, params?: TextParams) => string | undefined;
  getLocaleLabel: (localeCode: LocaleCode) => string;
}

const BUILTIN_CATALOGS: Record<LocaleCode, TranslationCatalog> = {
  "zh-CN": zhCnCatalog,
  en: enCatalog,
};

function dedupeLocales(locales: Array<LocaleCode | undefined>) {
  return locales.filter((locale, index, values): locale is LocaleCode =>
    Boolean(locale) && values.indexOf(locale) === index,
  );
}

function hasOwnKey(record: object, key: string) {
  return Object.prototype.hasOwnProperty.call(record, key);
}

function formatTemplate(template: string, params?: TextParams) {
  if (!params) {
    return template;
  }

  return template.replace(/\{([^}]+)\}/g, (match, token: string) => {
    const value = params[token];
    return value === undefined || value === null ? match : String(value);
  });
}

function isLegacyLocalizedText(value: unknown): value is LegacyLocalizedText {
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    return false;
  }

  return Object.values(value).every((entry) => typeof entry === "string");
}

function cloneCatalog(catalog: TranslationCatalog | undefined): TranslationCatalog {
  return catalog ? { ...catalog } : {};
}

function getBundleDefaultLocale(bundle?: I18nBundle) {
  if (!bundle) {
    return DEFAULT_LOCALE;
  }

  if (bundle.defaultLocale && hasOwnKey(bundle.locales, bundle.defaultLocale)) {
    return bundle.defaultLocale;
  }

  return Object.keys(bundle.locales)[0] ?? DEFAULT_LOCALE;
}

function mergeCatalogs(domainI18n?: I18nBundle) {
  const localeCodes = new Set<LocaleCode>([
    ...Object.keys(BUILTIN_CATALOGS),
    ...Object.keys(domainI18n?.locales ?? {}),
  ]);
  const merged: Record<LocaleCode, TranslationCatalog> = {};

  for (const localeCode of localeCodes) {
    merged[localeCode] = {
      ...cloneCatalog(domainI18n?.locales[localeCode]),
      ...cloneCatalog(BUILTIN_CATALOGS[localeCode]),
    };
  }

  return merged;
}

function getLookupLocales(locale: LocaleCode, domainDefaultLocale: LocaleCode) {
  return dedupeLocales([locale, domainDefaultLocale, DEFAULT_LOCALE, "en"]);
}

function resolveFromCatalogs(
  catalogs: Record<LocaleCode, TranslationCatalog>,
  locale: LocaleCode,
  domainDefaultLocale: LocaleCode,
  key: string,
  params?: TextParams,
) {
  for (const localeCode of getLookupLocales(locale, domainDefaultLocale)) {
    const catalog = catalogs[localeCode];
    if (!catalog || !hasOwnKey(catalog, key)) {
      continue;
    }

    return formatTemplate(catalog[key], params);
  }

  return undefined;
}

function resolveLegacyText(
  value: string | LegacyLocalizedText | undefined,
  locale: LocaleCode,
  domainDefaultLocale: LocaleCode,
) {
  if (typeof value === "string") {
    return value;
  }

  if (!isLegacyLocalizedText(value)) {
    return undefined;
  }

  for (const localeCode of getLookupLocales(locale, domainDefaultLocale)) {
    const match = value[localeCode];
    if (match !== undefined) {
      return match;
    }
  }

  return Object.values(value)[0];
}

/**
 * Exposes the built-in locale codes defined by `src/i18n/*.json`.
 */
export function getBuiltinLocaleCodes() {
  return [...Object.keys(BUILTIN_CATALOGS)];
}

/**
 * Combines shipped locales with any domain-provided locales in a stable order.
 */
export function getAvailableLocaleCodes(domainI18n?: I18nBundle) {
  return dedupeLocales([...getBuiltinLocaleCodes(), ...Object.keys(domainI18n?.locales ?? {})]);
}

/**
 * Normalizes a domain bundle so the runtime always has a stable default locale.
 */
export function normalizeI18nBundle(bundle?: I18nBundle): I18nBundle {
  const locales = { ...(bundle?.locales ?? {}) };

  return {
    defaultLocale: getBundleDefaultLocale(bundle),
    locales,
  };
}

/**
 * Creates a memo-friendly runtime for editor rendering.
 */
export function createI18nRuntime({
  locale,
  domainI18n,
}: {
  locale: LocaleCode;
  domainI18n?: I18nBundle;
}): I18nRuntime {
  const normalizedDomainI18n = normalizeI18nBundle(domainI18n);
  const mergedCatalogs = mergeCatalogs(normalizedDomainI18n);
  const lookupText = (key: string, params?: TextParams) =>
    resolveFromCatalogs(mergedCatalogs, locale, normalizedDomainI18n.defaultLocale ?? DEFAULT_LOCALE, key, params);

  return {
    locale,
    defaultLocale: normalizedDomainI18n.defaultLocale ?? DEFAULT_LOCALE,
    availableLocales: getAvailableLocaleCodes(normalizedDomainI18n),
    text: (key, params) => lookupText(key, params) ?? key,
    lookupText,
    getLocaleLabel: (localeCode) => getLocaleDisplayName(localeCode, locale),
  };
}

/**
 * Uses the current UI locale when possible and falls back to a readable code.
 */
export function getLocaleDisplayName(localeCode: LocaleCode, displayLocale: LocaleCode) {
  try {
    const locale = new Intl.Locale(localeCode);
    const languageNames = new Intl.DisplayNames([displayLocale], { type: "language" });
    const regionNames = locale.region
      ? new Intl.DisplayNames([displayLocale], { type: "region" })
      : null;
    const languageLabel = languageNames.of(locale.language);
    const regionLabel = locale.region ? regionNames?.of(locale.region) : null;

    if (languageLabel && regionLabel) {
      return `${languageLabel} (${regionLabel})`;
    }

    return languageLabel ?? localeCode;
  } catch {
    return localeCode;
  }
}

/**
 * Resolves a node-library item label from the merged catalog set.
 */
export function resolveNodeLibraryLabel(item: Pick<NodeLibraryItem, "labelKey">, i18n: I18nRuntime) {
  return i18n.lookupText(item.labelKey) ?? item.labelKey;
}

/**
 * Resolves a node-library item description from the merged catalog set.
 */
export function resolveNodeLibraryDescription(
  item: Pick<NodeLibraryItem, "descriptionKey">,
  i18n: I18nRuntime,
) {
  if (!item.descriptionKey) {
    return undefined;
  }

  return i18n.lookupText(item.descriptionKey) ?? item.descriptionKey;
}

/**
 * Resolves a node-library category from the merged catalog set.
 */
export function resolveNodeLibraryCategory(item: Pick<NodeLibraryItem, "categoryKey">, i18n: I18nRuntime) {
  return i18n.lookupText(item.categoryKey) ?? item.categoryKey;
}

/**
 * Resolves the display label for a stored graph node.
 */
export function resolveNodeLabel(
  data: Pick<NodeGraphNodeData, "label" | "labelKey" | "labelOverride">,
  i18n: I18nRuntime,
) {
  if (hasOwnKey(data, "labelOverride") && data.labelOverride !== undefined) {
    return data.labelOverride;
  }

  if (data.labelKey) {
    const translated = i18n.lookupText(data.labelKey);
    if (translated !== undefined) {
      return translated;
    }
  }

  return data.label || data.labelKey || "";
}

/**
 * Resolves the display description for a stored graph node.
 */
export function resolveNodeDescription(
  data: Pick<NodeGraphNodeData, "description" | "descriptionKey" | "descriptionOverride">,
  i18n: I18nRuntime,
) {
  if (hasOwnKey(data, "descriptionOverride") && data.descriptionOverride !== undefined) {
    return data.descriptionOverride;
  }

  if (data.descriptionKey) {
    const translated = i18n.lookupText(data.descriptionKey);
    if (translated !== undefined) {
      return translated;
    }
  }

  return data.description ?? data.descriptionKey;
}

/**
 * Resolves a node or template category string.
 */
export function resolveNodeCategory(
  data: Pick<NodeGraphNodeData, "category" | "categoryKey">,
  i18n: I18nRuntime,
) {
  if (data.categoryKey) {
    const translated = i18n.lookupText(data.categoryKey);
    if (translated !== undefined) {
      return translated;
    }
  }

  return resolveLegacyText(data.category, i18n.locale, i18n.defaultLocale);
}

/**
 * Resolves a port label from either a translation key or a legacy snapshot.
 */
export function resolvePortLabel(port: NodePortDefinition, i18n: I18nRuntime) {
  if (port.labelKey) {
    const translated = i18n.lookupText(port.labelKey);
    if (translated !== undefined) {
      return translated;
    }
  }

  return resolveLegacyText(port.label, i18n.locale, i18n.defaultLocale) ?? port.id;
}

/**
 * Resolves field labels from the merged catalog set.
 */
export function resolveFieldLabel(field: NodeLibraryField, i18n: I18nRuntime) {
  return i18n.lookupText(field.labelKey) ?? field.labelKey;
}

/**
 * Resolves field placeholders from the merged catalog set.
 */
export function resolveFieldPlaceholder(field: NodeLibraryField, i18n: I18nRuntime) {
  return field.placeholderKey ? (i18n.lookupText(field.placeholderKey) ?? field.placeholderKey) : undefined;
}

/**
 * Serializes node data so outgoing graph payloads keep compatibility snapshots
 * in sync with the currently visible locale.
 */
export function serializeNodeData(data: NodeGraphNodeData, i18n: I18nRuntime): NodeGraphNodeData {
  const nextDescription = resolveNodeDescription(data, i18n);

  return {
    ...data,
    label: resolveNodeLabel(data, i18n),
    description: nextDescription,
  };
}

/**
 * Ensures every key referenced by a node-library payload exists in the domain
 * default locale, which keeps fallbacks deterministic during editing.
 */
export function validateNodeLibraryTranslationKeys(nodes: NodeLibraryItem[], bundle: I18nBundle) {
  const normalizedBundle = normalizeI18nBundle(bundle);
  const defaultCatalog = normalizedBundle.locales[normalizedBundle.defaultLocale ?? DEFAULT_LOCALE] ?? {};

  for (const item of nodes) {
    const requiredKeys = [
      item.labelKey,
      item.descriptionKey,
      item.categoryKey,
      ...(item.inputs ?? []).map((port) => port.labelKey),
      ...(item.outputs ?? []).map((port) => port.labelKey),
      ...(item.fields ?? []).flatMap((field) => [field.labelKey, field.placeholderKey]),
    ].filter((key): key is string => Boolean(key));

    for (const key of requiredKeys) {
      if (!hasOwnKey(defaultCatalog, key)) {
        throw new Error(
          `nodeLibrary i18n missing key "${key}" in defaultLocale "${normalizedBundle.defaultLocale}".`,
        );
      }
    }
  }
}
