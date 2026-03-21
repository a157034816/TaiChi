"use client";

import { useEffect, useState } from "react";

import { useEditorI18n } from "@/components/editor/editor-i18n-context";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { resolveFieldPlaceholder } from "@/lib/nodegraph/localization";
import type { LocaleCode, NodeFieldOptionsResponse, NodeLibraryField } from "@/lib/nodegraph/types";

interface NodeFieldEditorProps {
  field: NodeLibraryField;
  inputId: string;
  locale: LocaleCode;
  nodeType: string;
  onChange: (value: string | number | boolean) => void;
  sessionId: string;
  value: string | number | boolean | undefined;
}

/**
 * 将编辑器输入值转换为字段约定的运行时类型。
 */
export function coerceNodeFieldValue(field: NodeLibraryField, rawValue: boolean | string) {
  switch (field.kind) {
    case "boolean":
      return typeof rawValue === "boolean" ? rawValue : rawValue === "true";
    case "int": {
      const parsed = Number.parseInt(String(rawValue), 10);
      return Number.isNaN(parsed) ? 0 : parsed;
    }
    case "float":
    case "double": {
      const parsed = Number(String(rawValue));
      return Number.isNaN(parsed) ? 0 : parsed;
    }
    default:
      return String(rawValue);
  }
}

/**
 * 按字段类型渲染匹配的编辑控件，并在需要时异步加载远端选项。
 */
export function NodeFieldEditor({
  field,
  inputId,
  locale,
  nodeType,
  onChange,
  sessionId,
  value,
}: NodeFieldEditorProps) {
  const i18n = useEditorI18n();
  const placeholder = resolveFieldPlaceholder(field, i18n);
  const [optionsState, setOptionsState] = useState<{
    options: NodeFieldOptionsResponse["options"];
    status: "idle" | "loading" | "ready" | "error";
  }>({
    options: [],
    status: field.kind === "select" ? "loading" : "idle",
  });

  useEffect(() => {
    if (field.kind !== "select") {
      return;
    }

    let cancelled = false;

    async function loadFieldOptions() {
      setOptionsState((current) => ({
        ...current,
        status: "loading",
      }));

      try {
        const response = await fetch(
          `/api/editor/sessions/${sessionId}/field-options?nodeType=${encodeURIComponent(nodeType)}&fieldKey=${encodeURIComponent(field.key)}&locale=${encodeURIComponent(locale)}`,
        );

        if (!response.ok) {
          throw new Error("Failed to load field options.");
        }

        const payload = (await response.json()) as NodeFieldOptionsResponse;
        if (cancelled) {
          return;
        }

        setOptionsState({
          options: payload.options,
          status: "ready",
        });
      } catch {
        if (cancelled) {
          return;
        }

        setOptionsState({
          options: [],
          status: "error",
        });
      }
    }

    void loadFieldOptions();

    return () => {
      cancelled = true;
    };
  }, [field, locale, nodeType, sessionId]);

  if (field.kind === "textarea") {
    return (
      <Textarea
        className="min-h-28 border-white/10 bg-black/35 text-[#edf3ff] shadow-none placeholder:text-[#73839f]"
        id={inputId}
        placeholder={placeholder}
        value={String(value ?? "")}
        onChange={(event) => onChange(event.target.value)}
      />
    );
  }

  if (field.kind === "boolean") {
    return (
      <input
        checked={Boolean(value)}
        className="size-4 rounded border-white/20 bg-transparent accent-[#ff9d1c]"
        id={inputId}
        onChange={(event) => onChange(event.target.checked)}
        type="checkbox"
      />
    );
  }

  if (field.kind === "select") {
    return (
      <select
        className="h-12 w-full rounded-md border border-white/10 bg-black/35 px-3 text-[#edf3ff] shadow-none"
        disabled={optionsState.status === "loading"}
        id={inputId}
        value={String(value ?? "")}
        onChange={(event) => onChange(event.target.value)}
      >
        <option value="">
          {optionsState.status === "error"
            ? i18n.text("editor.inspector.fieldOptionsError")
            : optionsState.status === "loading"
              ? i18n.text("editor.inspector.fieldOptionsLoading")
              : placeholder ?? i18n.text("editor.inspector.fieldOptionsPlaceholder")}
        </option>
        {optionsState.options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    );
  }

  return (
    <Input
      className="h-12 border-white/10 bg-black/35 text-[#edf3ff] shadow-none placeholder:text-[#73839f]"
      id={inputId}
      placeholder={placeholder}
      step={field.kind === "int" ? "1" : field.kind === "float" || field.kind === "double" ? "any" : undefined}
      type={
        field.kind === "date"
          ? "date"
          : field.kind === "color"
            ? "color"
            : field.kind === "int" || field.kind === "float" || field.kind === "double"
              ? "number"
              : "text"
      }
      value={String(value ?? "")}
      onChange={(event) => onChange(coerceNodeFieldValue(field, event.target.value))}
    />
  );
}
