import { describe, expect, it } from "vitest";

import { nodeGraphDocumentSchema, nodeLibraryEnvelopeSchema } from "@/lib/nodegraph/schema";

const workflowRequestType = "workflow/request";
const approvalDecisionType = "workflow/approval-decision";

const nodeLibraryI18n = {
  defaultLocale: "en",
  locales: {
    en: {
      "categories.workflow": "Workflow",
      "nodes.approval.description": "Review and route the request.",
      "nodes.approval.label": "Approval",
      "nodes.start.label": "Start",
      "ports.approved": "Approved",
      "ports.next": "Next",
      "ports.rejected": "Rejected",
      "ports.request": "Request",
    },
    "zh-CN": {
      "categories.workflow": "流程",
      "nodes.approval.description": "审核并路由请求。",
      "nodes.approval.label": "审批",
      "nodes.start.label": "开始",
      "ports.approved": "通过",
      "ports.next": "下一步",
      "ports.rejected": "驳回",
      "ports.request": "请求",
    },
  },
} as const;

describe("nodegraph schema", () => {
  it("accepts node library items that reference translation keys", () => {
    expect(
      nodeLibraryEnvelopeSchema.parse({
        nodes: [
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
          },
        ],
        i18n: nodeLibraryI18n,
        typeMappings: [
          {
            canonicalId: workflowRequestType,
            type: "WorkflowRequest",
            color: "#0ea5e9",
          },
          {
            canonicalId: approvalDecisionType,
            type: "ApprovalDecision",
            color: "#f97316",
          },
        ],
      }),
    ).toBeTruthy();
  });

  it("accepts saved edge handle ids for key-based multi-port graphs", () => {
    expect(
      nodeGraphDocumentSchema.parse({
        name: "Blueprint review flow",
        nodes: [
          {
            id: "node_approval",
            type: "default",
            position: { x: 0, y: 0 },
            data: {
              label: "Approval",
              labelKey: "nodes.approval.label",
              description: "Review and route the request.",
              descriptionKey: "nodes.approval.description",
              categoryKey: "categories.workflow",
              nodeType: "approval",
              inputs: [{ id: "request", labelKey: "ports.request", dataType: workflowRequestType }],
              outputs: [
                { id: "approved", labelKey: "ports.approved", dataType: approvalDecisionType },
                { id: "rejected", labelKey: "ports.rejected", dataType: approvalDecisionType },
              ],
            },
          },
          {
            id: "node_notify",
            type: "default",
            position: { x: 280, y: 0 },
            data: {
              label: "Notify",
              nodeType: "notify",
              inputs: [
                { id: "success", labelKey: "ports.approved", dataType: approvalDecisionType },
                { id: "failure", labelKey: "ports.rejected", dataType: approvalDecisionType },
              ],
              outputs: [],
            },
          },
        ],
        edges: [
          {
            id: "edge_approval_notify",
            source: "node_approval",
            sourceHandle: "approved",
            target: "node_notify",
            targetHandle: "success",
          },
        ],
        viewport: { x: 0, y: 0, zoom: 1 },
      }),
    ).toBeTruthy();
  });

  it("keeps legacy saved node port label objects valid", () => {
    expect(
      nodeGraphDocumentSchema.parse({
        name: "Legacy graph",
        nodes: [
          {
            id: "node_start",
            type: "default",
            position: { x: 0, y: 0 },
            data: {
              label: "Start",
              nodeType: "start",
              category: {
                "zh-CN": "控制",
                en: "Control",
              },
              outputs: [
                {
                  id: "next",
                  label: {
                    "zh-CN": "下一步",
                    en: "Next",
                  },
                },
              ],
            },
          },
        ],
        edges: [],
        viewport: { x: 0, y: 0, zoom: 1 },
      }),
    ).toBeTruthy();
  });

  it("rejects node libraries that omit the i18n bundle", () => {
    expect(() =>
      nodeLibraryEnvelopeSchema.parse({
        nodes: [
          {
            type: "start",
            labelKey: "nodes.start.label",
            categoryKey: "categories.workflow",
          },
        ],
      }),
    ).toThrow();
  });

  it("rejects legacy inline localized node library labels", () => {
    expect(() =>
      nodeLibraryEnvelopeSchema.parse({
        nodes: [
          {
            type: "start",
            label: {
              "zh-CN": "开始",
              en: "Start",
            },
            descriptionKey: "nodes.approval.description",
            categoryKey: "categories.workflow",
          },
        ],
        i18n: nodeLibraryI18n,
      }),
    ).toThrow();
  });

  it("rejects invalid color formats in typeMappings", () => {
    expect(() =>
      nodeLibraryEnvelopeSchema.parse({
        nodes: [],
        i18n: nodeLibraryI18n,
        typeMappings: [
          {
            canonicalId: workflowRequestType,
            type: "WorkflowRequest",
            color: "red",
          },
        ],
      }),
    ).toThrow();
  });

  it("accepts typed field definitions for editors beyond plain text", () => {
    expect(
      nodeLibraryEnvelopeSchema.parse({
        nodes: [
          {
            type: "approval",
            labelKey: "nodes.approval.label",
            descriptionKey: "nodes.approval.description",
            categoryKey: "categories.workflow",
            fields: [
              {
                key: "assignee",
                labelKey: "fields.assignee.label",
                kind: "text",
                placeholderKey: "fields.assignee.placeholder",
              },
              {
                key: "notes",
                labelKey: "fields.notes.label",
                kind: "textarea",
              },
              {
                key: "active",
                labelKey: "fields.active.label",
                kind: "boolean",
                defaultValue: true,
              },
              {
                key: "priority",
                labelKey: "fields.priority.label",
                kind: "select",
                optionsEndpoint: "https://client.example.com/options/priorities",
                defaultValue: "high",
              },
              {
                key: "dueDate",
                labelKey: "fields.dueDate.label",
                kind: "date",
                defaultValue: "2026-03-21",
              },
              {
                key: "theme",
                labelKey: "fields.theme.label",
                kind: "color",
                defaultValue: "#ff9d1c",
              },
              {
                key: "retries",
                labelKey: "fields.retries.label",
                kind: "int",
                defaultValue: 3,
              },
              {
                key: "score",
                labelKey: "fields.score.label",
                kind: "float",
                defaultValue: 1.5,
              },
              {
                key: "ratio",
                labelKey: "fields.ratio.label",
                kind: "double",
                defaultValue: 0.125,
              },
              {
                key: "budget",
                labelKey: "fields.budget.label",
                kind: "decimal",
                defaultValue: "99.90",
              },
            ],
          },
        ],
        i18n: {
          defaultLocale: "en",
          locales: {
            en: {
              ...nodeLibraryI18n.locales.en,
              "fields.active.label": "Active",
              "fields.assignee.label": "Assignee",
              "fields.assignee.placeholder": "Enter assignee",
              "fields.budget.label": "Budget",
              "fields.dueDate.label": "Due date",
              "fields.notes.label": "Notes",
              "fields.priority.label": "Priority",
              "fields.ratio.label": "Ratio",
              "fields.retries.label": "Retries",
              "fields.score.label": "Score",
              "fields.theme.label": "Theme",
            },
            "zh-CN": {
              ...nodeLibraryI18n.locales["zh-CN"],
              "fields.active.label": "启用",
              "fields.assignee.label": "负责人",
              "fields.assignee.placeholder": "请输入负责人",
              "fields.budget.label": "预算",
              "fields.dueDate.label": "截止日期",
              "fields.notes.label": "备注",
              "fields.priority.label": "优先级",
              "fields.ratio.label": "比例",
              "fields.retries.label": "重试次数",
              "fields.score.label": "评分",
              "fields.theme.label": "主题色",
            },
          },
        },
      }),
    ).toBeTruthy();
  });

  it("rejects legacy number field kinds after the numeric split", () => {
    expect(() =>
      nodeLibraryEnvelopeSchema.parse({
        nodes: [
          {
            type: "approval",
            labelKey: "nodes.approval.label",
            categoryKey: "categories.workflow",
            fields: [
              {
                key: "retries",
                labelKey: "fields.retries.label",
                kind: "number",
              },
            ],
          },
        ],
        i18n: {
          defaultLocale: "en",
          locales: {
            en: {
              "categories.workflow": "Workflow",
              "fields.retries.label": "Retries",
              "nodes.approval.label": "Approval",
            },
          },
        },
      }),
    ).toThrow(/Invalid input/i);
  });

  it("rejects select fields without an options endpoint", () => {
    expect(() =>
      nodeLibraryEnvelopeSchema.parse({
        nodes: [
          {
            type: "approval",
            labelKey: "nodes.approval.label",
            categoryKey: "categories.workflow",
            fields: [
              {
                key: "priority",
                labelKey: "fields.priority.label",
                kind: "select",
              },
            ],
          },
        ],
        i18n: {
          defaultLocale: "en",
          locales: {
            en: {
              "categories.workflow": "Workflow",
              "fields.priority.label": "Priority",
              "nodes.approval.label": "Approval",
            },
          },
        },
      }),
    ).toThrow(/optionsEndpoint/i);
  });

  it("rejects decimal defaults that are not stored as strings", () => {
    expect(() =>
      nodeLibraryEnvelopeSchema.parse({
        nodes: [
          {
            type: "approval",
            labelKey: "nodes.approval.label",
            categoryKey: "categories.workflow",
            fields: [
              {
                key: "budget",
                labelKey: "fields.budget.label",
                kind: "decimal",
                defaultValue: 12.5,
              },
            ],
          },
        ],
        i18n: {
          defaultLocale: "en",
          locales: {
            en: {
              "categories.workflow": "Workflow",
              "fields.budget.label": "Budget",
              "nodes.approval.label": "Approval",
            },
          },
        },
      }),
    ).toThrow(/expected string/i);
  });

  it("rejects color defaults that are not hex strings", () => {
    expect(() =>
      nodeLibraryEnvelopeSchema.parse({
        nodes: [
          {
            type: "approval",
            labelKey: "nodes.approval.label",
            categoryKey: "categories.workflow",
            fields: [
              {
                key: "theme",
                labelKey: "fields.theme.label",
                kind: "color",
                defaultValue: "orange",
              },
            ],
          },
        ],
        i18n: {
          defaultLocale: "en",
          locales: {
            en: {
              "categories.workflow": "Workflow",
              "fields.theme.label": "Theme",
              "nodes.approval.label": "Approval",
            },
          },
        },
      }),
    ).toThrow(/RRGGBB/i);
  });
});
