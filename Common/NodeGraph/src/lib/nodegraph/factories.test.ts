import { describe, expect, it } from "vitest";

import { createI18nRuntime } from "@/lib/nodegraph/localization";
import {
  buildFieldDefaults,
  buildNodeStyle,
  buildPortSnapshot,
  createNodeFromLibrary,
  normalizeNodeDataPorts,
} from "@/lib/nodegraph/factories";

const workflowRequestType = "workflow/request";
const approvalDecisionType = "workflow/approval-decision";
const domainI18n = {
  defaultLocale: "en",
  locales: {
    en: {
      "categories.workflow": "Workflow",
      "fields.owner.label": "Owner",
      "fields.sla.label": "SLA",
      "nodes.approval.description": "Manual approval step.",
      "nodes.approval.label": "Approval",
      "ports.approved": "Approved",
      "ports.rejected": "Rejected",
      "ports.request": "Request",
    },
    "zh-CN": {
      "categories.workflow": "流程",
      "fields.owner.label": "负责人",
      "fields.sla.label": "SLA 小时",
      "nodes.approval.description": "人工审批步骤。",
      "nodes.approval.label": "审批",
      "ports.approved": "通过",
      "ports.rejected": "驳回",
      "ports.request": "请求",
    },
  },
} as const;

describe("nodegraph factories", () => {
  it("builds defaults for supported field kinds", () => {
    expect(
      buildFieldDefaults([
        { key: "owner", labelKey: "fields.owner.label", kind: "text" },
        { key: "notes", labelKey: "fields.notes.label", kind: "textarea" },
        { key: "retries", labelKey: "fields.retries.label", kind: "number" },
        { key: "required", labelKey: "fields.required.label", kind: "boolean" },
      ]),
    ).toEqual({
      owner: "",
      notes: "",
      retries: 0,
      required: false,
    });
  });

  it("returns blueprint-friendly wrapper styles", () => {
    expect(
      buildNodeStyle({
        borderColor: "#1fb6ff",
        textColor: "#101521",
      }),
    ).toMatchObject({
      background: "transparent",
      border: "none",
      borderColor: "#1fb6ff",
      color: "#101521",
      minWidth: 280,
      width: 280,
    });
  });

  it("builds default single ports when a template omits them", () => {
    expect(buildPortSnapshot()).toEqual({
      inputs: [{ id: "in", labelKey: "editor.defaults.port.input" }],
      outputs: [{ id: "out", labelKey: "editor.defaults.port.output" }],
    });
  });

  it("preserves explicit multi-port and empty-port definitions", () => {
    expect(
      buildPortSnapshot({
        inputs: [],
        outputs: [
          { id: "approved", labelKey: "ports.approved", dataType: approvalDecisionType },
          { id: "rejected", labelKey: "ports.rejected", dataType: approvalDecisionType },
        ],
      }),
    ).toEqual({
      inputs: [],
      outputs: [
        { id: "approved", labelKey: "ports.approved", dataType: approvalDecisionType },
        { id: "rejected", labelKey: "ports.rejected", dataType: approvalDecisionType },
      ],
    });
  });

  it("hydrates legacy node data ports from the matching template", () => {
    expect(
      normalizeNodeDataPorts(
        {
          label: "Approval",
          nodeType: "approval",
        },
        {
          inputs: [{ id: "request", labelKey: "ports.request", dataType: workflowRequestType }],
          outputs: [
            { id: "approved", labelKey: "ports.approved", dataType: approvalDecisionType },
            { id: "rejected", labelKey: "ports.rejected", dataType: approvalDecisionType },
          ],
        },
      ),
    ).toMatchObject({
      inputs: [{ id: "request", labelKey: "ports.request", dataType: workflowRequestType }],
      outputs: [
        { id: "approved", labelKey: "ports.approved", dataType: approvalDecisionType },
        { id: "rejected", labelKey: "ports.rejected", dataType: approvalDecisionType },
      ],
    });
  });

  it("creates locale-aware nodes with translation keys, snapshots, and configured values", () => {
    const i18n = createI18nRuntime({
      locale: "en",
      domainI18n,
    });
    const node = createNodeFromLibrary(
      {
        type: "approval",
        labelKey: "nodes.approval.label",
        descriptionKey: "nodes.approval.description",
        categoryKey: "categories.workflow",
        inputs: [{ id: "request", labelKey: "ports.request", dataType: workflowRequestType }],
        outputs: [
          { id: "approved", labelKey: "ports.approved", dataType: approvalDecisionType },
          { id: "rejected", labelKey: "ports.rejected", dataType: approvalDecisionType },
        ],
        fields: [
          { key: "owner", labelKey: "fields.owner.label", kind: "text" },
          { key: "sla", labelKey: "fields.sla.label", kind: "number", defaultValue: 48 },
        ],
        defaultData: {
          channel: "email",
        },
        appearance: {
          borderColor: "#f59e0b",
        },
      },
      { x: 120, y: 80 },
      i18n,
    );

    expect(node.type).toBe("default");
    expect(node.position).toEqual({ x: 120, y: 80 });
    expect(node.data.label).toBe("Approval");
    expect(node.data.labelKey).toBe("nodes.approval.label");
    expect(node.data.description).toBe("Manual approval step.");
    expect(node.data.descriptionKey).toBe("nodes.approval.description");
    expect(node.data.categoryKey).toBe("categories.workflow");
    expect(node.data.inputs).toEqual([{ id: "request", labelKey: "ports.request", dataType: workflowRequestType }]);
    expect(node.data.outputs).toEqual([
      { id: "approved", labelKey: "ports.approved", dataType: approvalDecisionType },
      { id: "rejected", labelKey: "ports.rejected", dataType: approvalDecisionType },
    ]);
    expect(node.data.values).toEqual({
      owner: "",
      sla: 48,
      channel: "email",
    });
    expect(node.style).toMatchObject({
      background: "transparent",
      borderColor: "#f59e0b",
      width: 280,
    });
  });
});
