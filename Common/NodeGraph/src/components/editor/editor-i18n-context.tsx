"use client";

import { createContext, useContext, type ReactNode } from "react";

import type { EditorMessages } from "@/lib/nodegraph/localization";
import type { SupportedLocale } from "@/lib/nodegraph/types";

interface EditorI18nValue {
  locale: SupportedLocale;
  messages: EditorMessages;
}

const EditorI18nContext = createContext<EditorI18nValue | null>(null);

export function EditorI18nProvider({
  children,
  value,
}: {
  children: ReactNode;
  value: EditorI18nValue;
}) {
  return <EditorI18nContext.Provider value={value}>{children}</EditorI18nContext.Provider>;
}

export function useEditorI18n() {
  const value = useContext(EditorI18nContext);

  if (!value) {
    throw new Error("EditorI18nProvider is required for localized editor rendering.");
  }

  return value;
}
