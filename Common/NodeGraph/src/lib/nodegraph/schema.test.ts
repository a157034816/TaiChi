import { describe, expect, it } from "vitest";

import { nodeGraphDocumentSchema, nodeLibraryEnvelopeSchema } from "@/lib/nodegraph/schema";

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
            inputs: [{ id: "request", label: "Request", dataType: "WorkflowRequest" }],
            outputs: [
              { id: "approved", label: "Approved", dataType: "ApprovalDecision" },
              { id: "rejected", label: "Rejected", dataType: "ApprovalDecision" },
            ],
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
              inputs: [{ id: "request", label: "Request", dataType: "WorkflowRequest" }],
              outputs: [
                { id: "approved", label: "Approved", dataType: "ApprovalDecision" },
                { id: "rejected", label: "Rejected", dataType: "ApprovalDecision" },
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
                { id: "success", label: "Success", dataType: "ApprovalDecision" },
                { id: "failure", label: "Failure", dataType: "ApprovalDecision" },
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
});
