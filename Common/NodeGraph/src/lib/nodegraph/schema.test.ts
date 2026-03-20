import { describe, expect, it } from "vitest";

import { createLocalizedText } from "@/lib/nodegraph/localization";
import { nodeGraphDocumentSchema, nodeLibraryEnvelopeSchema } from "@/lib/nodegraph/schema";

const workflowRequestType = "workflow/request";
const approvalDecisionType = "workflow/approval-decision";
const text = (zhCN: string, en = zhCN) => createLocalizedText(zhCN, en);

describe("nodegraph schema", () => {
  it("accepts node library items with explicit multi-port definitions", () => {
    expect(
      nodeLibraryEnvelopeSchema.parse({
        nodes: [
          {
            type: "approval",
            label: text("Approval"),
            description: text("Review and route the request"),
            category: text("workflow"),
            inputs: [{ id: "request", label: text("Request"), dataType: workflowRequestType }],
            outputs: [
              { id: "approved", label: text("Approved"), dataType: approvalDecisionType },
              { id: "rejected", label: text("Rejected"), dataType: approvalDecisionType },
            ],
          },
        ],
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

  it("accepts saved edge handle ids for multi-port graphs", () => {
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
              nodeType: "approval",
              inputs: [{ id: "request", label: text("Request"), dataType: workflowRequestType }],
              outputs: [
                { id: "approved", label: text("Approved"), dataType: approvalDecisionType },
                { id: "rejected", label: text("Rejected"), dataType: approvalDecisionType },
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
                { id: "success", label: text("Success"), dataType: approvalDecisionType },
                { id: "failure", label: text("Failure"), dataType: approvalDecisionType },
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

  it("keeps node port data types optional for legacy libraries", () => {
    expect(
      nodeLibraryEnvelopeSchema.parse([
        {
          type: "start",
          label: text("Start"),
          description: text("Kick off the flow"),
          category: text("control"),
          outputs: [{ id: "next", label: text("Next") }],
        },
      ]),
    ).toBeTruthy();
  });

  it("rejects legacy languageType field names in typeMappings", () => {
    expect(() =>
      nodeLibraryEnvelopeSchema.parse({
        nodes: [],
        typeMappings: [
          {
            canonicalId: workflowRequestType,
            languageType: "WorkflowRequest",
          },
        ],
      }),
    ).toThrow();
  });

  it("rejects legacy single-language node labels", () => {
    expect(() =>
      nodeLibraryEnvelopeSchema.parse({
        nodes: [
          {
            type: "start",
            label: "Start",
            description: text("Entry node"),
            category: text("control"),
          },
        ],
      }),
    ).toThrow();
  });

  it("rejects invalid color formats in typeMappings", () => {
    expect(() =>
      nodeLibraryEnvelopeSchema.parse({
        nodes: [],
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
