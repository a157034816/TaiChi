"use client";

import {
  createContext,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from "react";

import {
  LOCALE_COOKIE_NAME,
  compareText as compareTextByLocale,
  formatDateTime as formatDateTimeByLocale,
  getMessages,
  resolveLocale,
  translate,
  type MessageKey,
  type MessageParams,
  type Messages,
  type SupportedLocale,
} from "@/i18n/core";

/**
 * Describes the locale-aware helpers exposed to client components.
 */
export type I18nContextValue = {
  locale: SupportedLocale;
  messages: Messages;
  setLocale: (locale: SupportedLocale) => void;
  t: (key: MessageKey, params?: MessageParams) => string;
  formatDateTime: (value?: string | null) => string;
  compareText: (left: string, right: string) => number;
};

const I18nContext = createContext<I18nContextValue | null>(null);

type Props = {
  initialLocale: SupportedLocale;
  children: ReactNode;
};

/**
 * Provides the current locale, translation access, and common formatting
 * helpers to the admin site's client components.
 */
export function I18nProvider({ initialLocale, children }: Props) {
  const [locale, setLocaleState] = useState<SupportedLocale>(
    resolveLocale(initialLocale)
  );

  useEffect(() => {
    document.documentElement.lang = locale;
    document.title = translate(locale, "metadata.title");
    document.cookie = `${LOCALE_COOKIE_NAME}=${encodeURIComponent(locale)}; path=/; max-age=31536000; samesite=lax`;
  }, [locale]);

  const messages = getMessages(locale);

  const value: I18nContextValue = {
    locale,
    messages,
    setLocale: (nextLocale) => setLocaleState(resolveLocale(nextLocale)),
    t: (key, params) => translate(locale, key, params),
    formatDateTime: (value) => formatDateTimeByLocale(locale, value),
    compareText: (left, right) => compareTextByLocale(locale, left, right),
  };

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

/**
 * Returns the current i18n helpers and enforces provider usage at runtime.
 */
export function useI18n() {
  const context = useContext(I18nContext);
  if (!context) {
    throw new Error("useI18n must be used within I18nProvider.");
  }

  return context;
}

/**
 * Returns just the locale state helpers for components that do not need the
 * full translation API.
 */
export function useLocale() {
  const { locale, setLocale } = useI18n();
  return { locale, setLocale };
}
