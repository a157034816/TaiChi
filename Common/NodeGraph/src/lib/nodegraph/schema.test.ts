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
});
