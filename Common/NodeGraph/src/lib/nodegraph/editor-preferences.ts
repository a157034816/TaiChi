import { ConnectionLineType } from "@xyflow/react";

import { supportedLocales, type SupportedLocale } from "@/lib/nodegraph/types";
import { DEFAULT_LOCALE } from "@/lib/nodegraph/localization";

export const EDITOR_PREFERENCES_STORAGE_KEY = "nodegraph.editor.preferences.v1";

export const editorEdgeStyles = ["smoothstep", "bezier", "straight", "step"] as const;

export type EditorEdgeStyle = (typeof editorEdgeStyles)[number];

export interface EditorPreferences {
  locale: SupportedLocale;
  edgeStyle: EditorEdgeStyle;
}

export const DEFAULT_EDITOR_PREFERENCES: EditorPreferences = {
  locale: DEFAULT_LOCALE,
  edgeStyle: "smoothstep",
};

function isSupportedLocale(value: unknown): value is SupportedLocale {
  return typeof value === "string" && supportedLocales.includes(value as SupportedLocale);
}

function isEditorEdgeStyle(value: unknown): value is EditorEdgeStyle {
  return typeof value === "string" && editorEdgeStyles.includes(value as EditorEdgeStyle);
}

export function sanitizeEditorPreferences(value: unknown): EditorPreferences {
  if (!value || typeof value !== "object") {
    return DEFAULT_EDITOR_PREFERENCES;
  }

  const candidate = value as Partial<EditorPreferences>;

  return {
    locale: isSupportedLocale(candidate.locale) ? candidate.locale : DEFAULT_EDITOR_PREFERENCES.locale,
    edgeStyle: isEditorEdgeStyle(candidate.edgeStyle) ? candidate.edgeStyle : DEFAULT_EDITOR_PREFERENCES.edgeStyle,
  };
}

export function readEditorPreferences(storage: Pick<Storage, "getItem"> | null) {
  if (!storage) {
    return DEFAULT_EDITOR_PREFERENCES;
  }

  const raw = storage.getItem(EDITOR_PREFERENCES_STORAGE_KEY);
  if (!raw) {
    return DEFAULT_EDITOR_PREFERENCES;
  }

  try {
    return sanitizeEditorPreferences(JSON.parse(raw));
  } catch {
    return DEFAULT_EDITOR_PREFERENCES;
  }
}

export function persistEditorPreferences(
  storage: Pick<Storage, "setItem"> | null,
  preferences: EditorPreferences,
) {
  storage?.setItem(EDITOR_PREFERENCES_STORAGE_KEY, JSON.stringify(preferences));
}

export function getReactFlowEdgeType(edgeStyle: EditorEdgeStyle) {
  switch (edgeStyle) {
    case "bezier":
      return "default";
    case "straight":
      return "straight";
    case "step":
      return "step";
    default:
      return "smoothstep";
  }
}

export function getConnectionLineType(edgeStyle: EditorEdgeStyle) {
  switch (edgeStyle) {
    case "bezier":
      return ConnectionLineType.Bezier;
    case "straight":
      return ConnectionLineType.Straight;
    case "step":
      return ConnectionLineType.Step;
    default:
      return ConnectionLineType.SmoothStep;
  }
}
