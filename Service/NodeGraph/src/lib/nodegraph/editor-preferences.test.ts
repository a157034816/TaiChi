import { ConnectionLineType } from "@xyflow/react";
import { describe, expect, it } from "vitest";

import {
  DEFAULT_EDITOR_PREFERENCES,
  getConnectionLineType,
  getReactFlowEdgeType,
  readEditorPreferences,
  sanitizeEditorPreferences,
} from "@/lib/nodegraph/editor-preferences";

describe("nodegraph editor preferences", () => {
  it("falls back to defaults for invalid preference payloads", () => {
    expect(sanitizeEditorPreferences(null)).toEqual(DEFAULT_EDITOR_PREFERENCES);
    expect(sanitizeEditorPreferences({ locale: "fr", edgeStyle: "arc" })).toEqual(DEFAULT_EDITOR_PREFERENCES);
  });

  it("accepts a persisted locale when the active session allows it", () => {
    expect(
      sanitizeEditorPreferences(
        {
          locale: "fr-FR",
          edgeStyle: "step",
        },
        ["zh-CN", "en", "fr-FR"],
      ),
    ).toEqual({
      locale: "fr-FR",
      edgeStyle: "step",
    });
  });

  it("reads a persisted preference payload from storage", () => {
    const storage = {
      getItem() {
        return JSON.stringify({
          locale: "en",
          edgeStyle: "bezier",
        });
      },
    };

    expect(readEditorPreferences(storage)).toEqual({
      locale: "en",
      edgeStyle: "bezier",
    });
  });

  it("preserves a domain locale from storage when it is still allowed", () => {
    const storage = {
      getItem() {
        return JSON.stringify({
          locale: "fr-FR",
          edgeStyle: "bezier",
        });
      },
    };

    expect(readEditorPreferences(storage, ["zh-CN", "en", "fr-FR"])).toEqual({
      locale: "fr-FR",
      edgeStyle: "bezier",
    });
  });

  it("maps edge styles to React Flow rendering options", () => {
    expect(getReactFlowEdgeType("smoothstep")).toBe("smoothstep");
    expect(getReactFlowEdgeType("bezier")).toBe("default");
    expect(getConnectionLineType("step")).toBe(ConnectionLineType.Step);
    expect(getConnectionLineType("straight")).toBe(ConnectionLineType.Straight);
  });
});
