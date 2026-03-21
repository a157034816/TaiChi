// @vitest-environment jsdom

import { act } from "react";
import { createRoot } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";

import { EditorI18nProvider } from "@/components/editor/editor-i18n-context";
import { NodeFieldEditor, coerceNodeFieldValue } from "@/components/editor/node-field-editor";
import { createI18nRuntime } from "@/lib/nodegraph/localization";
import type { NodeLibraryField } from "@/lib/nodegraph/types";

const i18n = createI18nRuntime({
  locale: "en",
  domainI18n: {
    defaultLocale: "en",
    locales: {
      en: {
        "editor.inspector.fieldOptionsError": "Failed to load options",
        "editor.inspector.fieldOptionsLoading": "Loading options",
        "editor.inspector.fieldOptionsPlaceholder": "Select an option",
        "fields.budget.label": "Budget",
        "fields.dueDate.label": "Due date",
        "fields.notes.label": "Notes",
        "fields.priority.label": "Priority",
        "fields.retries.label": "Retries",
        "fields.theme.label": "Theme",
      },
      "zh-CN": {
        "editor.inspector.fieldOptionsError": "选项加载失败",
        "editor.inspector.fieldOptionsLoading": "正在加载选项",
        "editor.inspector.fieldOptionsPlaceholder": "请选择一个选项",
        "fields.budget.label": "预算",
        "fields.dueDate.label": "截止日期",
        "fields.notes.label": "备注",
        "fields.priority.label": "优先级",
        "fields.retries.label": "重试次数",
        "fields.theme.label": "主题色",
      },
    },
  },
});

// React 19 在 jsdom 下需要显式声明 act 环境。
(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

function renderFieldEditor({
  field,
  onChange = vi.fn(),
  value,
}: {
  field: NodeLibraryField;
  onChange?: (value: string | number | boolean) => void;
  value: string | number | boolean | undefined;
}) {
  const container = document.createElement("div");
  document.body.appendChild(container);
  const root = createRoot(container);

  act(() => {
    root.render(
      <EditorI18nProvider value={i18n}>
        <NodeFieldEditor
          field={field}
          inputId={field.key}
          locale="en"
          nodeType="approval"
          onChange={onChange}
          sessionId="ngs_test"
          value={value}
        />
      </EditorI18nProvider>,
    );
  });

  return {
    container,
    root,
  };
}

describe("coerceNodeFieldValue", () => {
  it("keeps decimal values as strings while parsing numeric kinds as numbers", () => {
    expect(coerceNodeFieldValue({ key: "budget", labelKey: "fields.budget.label", kind: "decimal" }, "99.90")).toBe(
      "99.90",
    );
    expect(coerceNodeFieldValue({ key: "retries", labelKey: "fields.retries.label", kind: "int" }, "3")).toBe(3);
    expect(coerceNodeFieldValue({ key: "ratio", labelKey: "fields.ratio.label", kind: "float" }, "1.5")).toBe(1.5);
    expect(coerceNodeFieldValue({ key: "score", labelKey: "fields.score.label", kind: "double" }, "2.25")).toBe(
      2.25,
    );
  });
});

describe("NodeFieldEditor", () => {
  afterEach(() => {
    document.body.innerHTML = "";
    vi.restoreAllMocks();
  });

  it("renders specialized controls for textarea, date, and color field kinds", () => {
    const textarea = renderFieldEditor({
      field: { key: "notes", labelKey: "fields.notes.label", kind: "textarea" },
      value: "memo",
    });
    const date = renderFieldEditor({
      field: { key: "dueDate", labelKey: "fields.dueDate.label", kind: "date" },
      value: "2026-03-21",
    });
    const color = renderFieldEditor({
      field: { key: "theme", labelKey: "fields.theme.label", kind: "color" },
      value: "#ff9d1c",
    });

    expect(textarea.container.querySelector("textarea#notes")).toBeInstanceOf(HTMLTextAreaElement);
    expect(date.container.querySelector('input#dueDate[type="date"]')).toBeInstanceOf(HTMLInputElement);
    expect(color.container.querySelector('input#theme[type="color"]')).toBeInstanceOf(HTMLInputElement);

    act(() => {
      textarea.root.unmount();
      date.root.unmount();
      color.root.unmount();
    });
  });

  it("loads remote select options from the session proxy and emits selected values", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          options: [
            { value: "low", label: "Low" },
            { value: "high", label: "High" },
          ],
        }),
      ),
    );
    vi.stubGlobal("fetch", fetchMock);
    const onChange = vi.fn();
    const rendered = renderFieldEditor({
      field: {
        key: "priority",
        labelKey: "fields.priority.label",
        kind: "select",
        optionsEndpoint: "https://client.example.com/options/priorities",
      },
      onChange,
      value: "low",
    });

    await act(async () => {
      await Promise.resolve();
    });

    const select = rendered.container.querySelector("select#priority");
    expect(select).toBeInstanceOf(HTMLSelectElement);
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/editor/sessions/ngs_test/field-options?nodeType=approval&fieldKey=priority&locale=en",
    );

    act(() => {
      if (select instanceof HTMLSelectElement) {
        select.value = "high";
        select.dispatchEvent(new Event("change", { bubbles: true }));
      }
    });

    expect((select as HTMLSelectElement | null)?.options).toHaveLength(3);
    expect(onChange).toHaveBeenCalledWith("high");

    act(() => {
      rendered.root.unmount();
    });
  });
});
