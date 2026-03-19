import { describe, expect, it } from "vitest";

import { nodeGraphDocumentSchema, nodeLibraryEnvelopeSchema } from "@/lib/nodegraph/schema";

const workflowRequestType = "workflow/request";
const approvalDecisionType = "workflow/approval-decision";

describe("nodegraph schema", () => {
  it("accepts node library items with explicit multi-port definitions", () => {
    expect(
      nodeLibraryEnvelopeSchema.parse({
        nodes: [
          {
            type: "approval",
            label: "Approval",
            description: "Review and route the request",
            category: "workflow",
            inputs: [{ id: "request", label: "Request", dataType: workflowRequestType }],
            outputs: [
              { id: "approved", label: "Approved", dataType: approvalDecisionType },
              { id: "rejected", label: "Rejected", dataType: approvalDecisionType },
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
              inputs: [{ id: "request", label: "Request", dataType: workflowRequestType }],
              outputs: [
                { id: "approved", label: "Approved", dataType: approvalDecisionType },
                { id: "rejected", label: "Rejected", dataType: approvalDecisionType },
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
                { id: "success", label: "Success", dataType: approvalDecisionType },
                { id: "failure", label: "Failure", dataType: approvalDecisionType },
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
          label: "Start",
          description: "Kick off the flow",
          category: "control",
          outputs: [{ id: "next", label: "Next" }],
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
