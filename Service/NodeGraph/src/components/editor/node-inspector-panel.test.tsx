// @vitest-environment jsdom

import { act } from "react";
import { createRoot } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";

import { EditorI18nProvider } from "@/components/editor/editor-i18n-context";
import { NodeInspectorPanel } from "@/components/editor/node-inspector-panel";
import { createI18nRuntime } from "@/lib/nodegraph/localization";
import type { NodeGraphNode } from "@/lib/nodegraph/types";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const i18n = createI18nRuntime({
  locale: "en",
  domainI18n: {
    defaultLocale: "en",
    locales: {
      en: {
        "categories.workflow": "Workflow",
        "editor.inspector.description": "Selection inspector",
        "editor.inspector.editableFields": "Editable fields",
        "editor.inspector.fallbackNodeDescription": "No description",
        "editor.inspector.kicker": "Inspector",
        "editor.inspector.nodeDescription": "Description",
        "editor.inspector.nodeLabel": "Label",
        "editor.inspector.tabs.graph": "Graph",
        "editor.inspector.tabs.selection": "Selection",
        "editor.inspector.tabs.settings": "Settings",
        "editor.inspector.title": "Inspector",
        "fields.priority.label": "Priority",
        "fields.priority.placeholder": "Select a priority",
        "fields.retries.label": "Retries",
        "nodes.approval.description": "Review node",
        "nodes.approval.label": "Approval",
      },
      "zh-CN": {
        "categories.workflow": "流程",
        "editor.inspector.description": "选择检查器",
        "editor.inspector.editableFields": "可编辑字段",
        "editor.inspector.fallbackNodeDescription": "暂无描述",
        "editor.inspector.kicker": "检查器",
        "editor.inspector.nodeDescription": "描述",
        "editor.inspector.nodeLabel": "标签",
        "editor.inspector.tabs.graph": "图",
        "editor.inspector.tabs.selection": "选中项",
        "editor.inspector.tabs.settings": "设置",
        "editor.inspector.title": "检查器",
        "fields.priority.label": "优先级",
        "fields.priority.placeholder": "请选择优先级",
        "fields.retries.label": "重试次数",
        "nodes.approval.description": "审核节点",
        "nodes.approval.label": "审批",
      },
    },
  },
});

const node: NodeGraphNode = {
  id: "node_approval",
  type: "default",
  position: { x: 0, y: 0 },
  data: {
    label: "Approval",
    labelKey: "nodes.approval.label",
    description: "Review node",
    descriptionKey: "nodes.approval.description",
    nodeType: "approval",
    values: {
      priority: "low",
      retries: 1,
    },
  },
};

describe("NodeInspectorPanel", () => {
  afterEach(() => {
    document.body.innerHTML = "";
    vi.restoreAllMocks();
  });

  it("renders typed field editors for select and numeric kinds inside the inspector", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            options: [
              { value: "low", label: "Low" },
              { value: "high", label: "High" },
            ],
          }),
        ),
      ),
    );
    const onNodeValueChange = vi.fn();
    const container = document.createElement("div");
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(
        <EditorI18nProvider value={i18n}>
          <NodeInspectorPanel
            edge={null}
            edgeStyle="smoothstep"
            graphDescription=""
            graphName="demo"
            locale="en"
            node={node}
            nodes={[node]}
            onEdgeStyleChange={vi.fn()}
            onGraphDescriptionChange={vi.fn()}
            onGraphNameChange={vi.fn()}
            onLocaleChange={vi.fn()}
            onNodeFieldChange={vi.fn()}
            onNodeValueChange={onNodeValueChange}
            sessionId="ngs_test"
            template={{
              type: "approval",
              labelKey: "nodes.approval.label",
              descriptionKey: "nodes.approval.description",
              categoryKey: "categories.workflow",
              fields: [
                {
                  key: "priority",
                  labelKey: "fields.priority.label",
                  kind: "select",
                  optionsEndpoint: "https://client.example.com/options/priorities",
                  placeholderKey: "fields.priority.placeholder",
                },
                {
                  key: "retries",
                  labelKey: "fields.retries.label",
                  kind: "int",
                },
              ],
            }}
          />
        </EditorI18nProvider>,
      );
    });

    await act(async () => {
      await Promise.resolve();
    });

    const select = container.querySelector("select#priority");
    const numberInput = container.querySelector('input#retries[type="number"]');
    expect(select).toBeInstanceOf(HTMLSelectElement);
    expect(numberInput).toBeInstanceOf(HTMLInputElement);

    act(() => {
      if (select instanceof HTMLSelectElement) {
        select.value = "high";
        select.dispatchEvent(new Event("change", { bubbles: true }));
      }
    });

    expect(onNodeValueChange).toHaveBeenCalledWith("priority", "high");

    await act(async () => {
      root.unmount();
    });
  });
});
