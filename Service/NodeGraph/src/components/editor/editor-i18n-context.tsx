"use client";

import { createContext, useContext, type ReactNode } from "react";

import type { I18nRuntime } from "@/lib/nodegraph/localization";

const EditorI18nContext = createContext<I18nRuntime | null>(null);

/**
 * Shares the resolved editor i18n runtime across all localized components.
 */
export function EditorI18nProvider({
  children,
  value,
}: {
  children: ReactNode;
  value: I18nRuntime;
}) {
  return <EditorI18nContext.Provider value={value}>{children}</EditorI18nContext.Provider>;
}

/**
 * Reads the active editor i18n runtime and fails fast when the provider is
 * missing from the component tree.
 */
export function useEditorI18n() {
  const value = useContext(EditorI18nContext);

  if (!value) {
    throw new Error("EditorI18nProvider is required for localized editor rendering.");
  }

  return value;
}
