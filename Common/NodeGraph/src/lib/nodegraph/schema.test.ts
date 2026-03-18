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
            inputs: [{ id: "request", label: "Request" }],
            outputs: [
              { id: "approved", label: "Approved" },
              { id: "rejected", label: "Rejected" },
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
              inputs: [{ id: "request", label: "Request" }],
              outputs: [
                { id: "approved", label: "Approved" },
                { id: "rejected", label: "Rejected" },
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
                { id: "success", label: "Success" },
                { id: "failure", label: "Failure" },
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
});
