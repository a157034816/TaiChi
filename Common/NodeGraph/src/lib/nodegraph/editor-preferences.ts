import { ConnectionLineType } from "@xyflow/react";

import type { LocaleCode } from "@/lib/nodegraph/types";
import { DEFAULT_LOCALE, getBuiltinLocaleCodes } from "@/lib/nodegraph/localization";

export const EDITOR_PREFERENCES_STORAGE_KEY = "nodegraph.editor.preferences.v1";

export const editorEdgeStyles = ["smoothstep", "bezier", "straight", "step"] as const;

export type EditorEdgeStyle = (typeof editorEdgeStyles)[number];

export interface EditorPreferences {
  locale: LocaleCode;
  edgeStyle: EditorEdgeStyle;
}

export const DEFAULT_EDITOR_PREFERENCES: EditorPreferences = {
  locale: DEFAULT_LOCALE,
  edgeStyle: "smoothstep",
};

function isSupportedLocale(value: unknown, allowedLocales: LocaleCode[]): value is LocaleCode {
  return typeof value === "string" && allowedLocales.includes(value);
}

function isEditorEdgeStyle(value: unknown): value is EditorEdgeStyle {
  return typeof value === "string" && editorEdgeStyles.includes(value as EditorEdgeStyle);
}

/**
 * Sanitizes persisted editor preferences before they are applied to UI state.
 */
export function sanitizeEditorPreferences(
  value: unknown,
  allowedLocales: LocaleCode[] = getBuiltinLocaleCodes(),
): EditorPreferences {
  if (!value || typeof value !== "object") {
    return DEFAULT_EDITOR_PREFERENCES;
  }

  const candidate = value as Partial<EditorPreferences>;

  return {
    locale: isSupportedLocale(candidate.locale, allowedLocales) ? candidate.locale : DEFAULT_EDITOR_PREFERENCES.locale,
    edgeStyle: isEditorEdgeStyle(candidate.edgeStyle) ? candidate.edgeStyle : DEFAULT_EDITOR_PREFERENCES.edgeStyle,
  };
}

/**
 * Reads persisted editor preferences from browser storage.
 */
export function readEditorPreferences(
  storage: Pick<Storage, "getItem"> | null,
  allowedLocales: LocaleCode[] = getBuiltinLocaleCodes(),
) {
  if (!storage) {
    return DEFAULT_EDITOR_PREFERENCES;
  }

  const raw = storage.getItem(EDITOR_PREFERENCES_STORAGE_KEY);
  if (!raw) {
    return DEFAULT_EDITOR_PREFERENCES;
  }

  try {
    return sanitizeEditorPreferences(JSON.parse(raw), allowedLocales);
  } catch {
    return DEFAULT_EDITOR_PREFERENCES;
  }
}

/**
 * Persists editor preferences without mutating unrelated state.
 */
export function persistEditorPreferences(
  storage: Pick<Storage, "setItem"> | null,
  preferences: EditorPreferences,
) {
  storage?.setItem(EDITOR_PREFERENCES_STORAGE_KEY, JSON.stringify(preferences));
}

/**
 * Maps a human-friendly style preference to the React Flow edge type.
 */
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

/**
 * Maps a style preference to the temporary connection preview type.
 */
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
