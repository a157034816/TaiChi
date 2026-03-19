import { describe, expect, it } from "vitest";

import {
  buildConnectionForInsertedNode,
  findCompatibleOppositePort,
  getCompatibleNodeLibraryItems,
  getPortForHandle,
  removeConflictingInputEdges,
} from "@/lib/nodegraph/connections";

describe("nodegraph connections", () => {
  it("removes an existing edge when a new connection targets the same input handle", () => {
    expect(
      removeConflictingInputEdges(
        [
          {
            id: "edge_approval_notify_success",
            source: "node_approval",
            sourceHandle: "approved",
            target: "node_notify",
            targetHandle: "success",
          },
        ],
        {
          target: "node_notify",
          targetHandle: "success",
        },
      ),
    ).toEqual([]);
  });

  it("keeps edges on other target handles of the same node", () => {
    expect(
      removeConflictingInputEdges(
        [
          {
            id: "edge_approval_notify_success",
            source: "node_approval",
            sourceHandle: "approved",
            target: "node_notify",
            targetHandle: "success",
          },
        ],
        {
          target: "node_notify",
          targetHandle: "failure",
        },
      ),
    ).toEqual([
      {
        id: "edge_approval_notify_success",
        source: "node_approval",
        sourceHandle: "approved",
        target: "node_notify",
        targetHandle: "success",
      },
    ]);
  });

  it("treats legacy single-input edges without handle ids as a replaceable default input", () => {
    expect(
      removeConflictingInputEdges(
        [
          {
            id: "edge_start_approval",
            source: "node_start",
            target: "node_approval",
          },
        ],
        {
          target: "node_approval",
          targetHandle: null,
        },
      ),
    ).toEqual([]);
  });

  it("locates the dragged port from the correct side of a node", () => {
    expect(
      getPortForHandle(
        {
          inputs: [{ id: "request", label: "Request", dataType: "WorkflowRequest" }],
          outputs: [{ id: "approved", label: "Approved", dataType: "ApprovalDecision" }],
        },
        "source",
        "approved",
      ),
    ).toEqual({
      id: "approved",
      label: "Approved",
      dataType: "ApprovalDecision",
    });
  });

  it("prefers matching data types when choosing the opposite port", () => {
    expect(
      findCompatibleOppositePort(
        {
          inputs: [
            { id: "request", label: "Request", dataType: "WorkflowRequest" },
            { id: "fallback", label: "Fallback", dataType: "OtherType" },
          ],
          outputs: [],
        },
        {
          handleType: "source",
          startPort: { id: "next", label: "Next", dataType: "WorkflowRequest" },
        },
      ),
    ).toEqual({
      id: "request",
      label: "Request",
      dataType: "WorkflowRequest",
    });
  });

  it("falls back to the handle id when one side has no data type", () => {
    expect(
      findCompatibleOppositePort(
        {
          inputs: [
            { id: "approved", label: "Approved" },
            { id: "rejected", label: "Rejected" },
          ],
          outputs: [],
        },
        {
          handleType: "source",
          startPort: { id: "approved", label: "Approved", dataType: "ApprovalDecision" },
        },
      ),
    ).toEqual({
      id: "approved",
      label: "Approved",
    });
  });

  it("filters out nodes whose opposite ports all have explicit type mismatches", () => {
    expect(
      getCompatibleNodeLibraryItems(
        [
          {
            type: "notify",
            label: "Notify",
            description: "Send a notification",
            category: "integration",
            inputs: [{ id: "success", label: "Success", dataType: "ApprovalDecision" }],
            outputs: [],
          },
          {
            type: "merge",
            label: "Merge",
            description: "Collect branches",
            category: "control",
            inputs: [{ id: "request", label: "Request", dataType: "WorkflowRequest" }],
            outputs: [{ id: "next", label: "Next", dataType: "WorkflowRequest" }],
          },
        ],
        {
          handleType: "source",
        },
        { id: "approved", label: "Approved", dataType: "ApprovalDecision" },
      ).map((item) => item.type),
    ).toEqual(["notify"]);
  });

  it("builds a connection from an output drag into the inserted node", () => {
    expect(
      buildConnectionForInsertedNode({
        existingNodeId: "node_approval",
        existingHandleId: "approved",
        existingHandleType: "source",
        existingPort: { id: "approved", label: "Approved", dataType: "ApprovalDecision" },
        insertedNodeId: "node_notify",
        insertedNodeData: {
          inputs: [{ id: "success", label: "Success", dataType: "ApprovalDecision" }],
          outputs: [],
        },
      }),
    ).toEqual({
      source: "node_approval",
      sourceHandle: "approved",
      target: "node_notify",
      targetHandle: "success",
    });
  });

  it("builds a connection from the inserted node back into the dragged input", () => {
    expect(
      buildConnectionForInsertedNode({
        existingNodeId: "node_notify",
        existingHandleId: "success",
        existingHandleType: "target",
        existingPort: { id: "success", label: "Success", dataType: "ApprovalDecision" },
        insertedNodeId: "node_approval",
        insertedNodeData: {
          inputs: [{ id: "request", label: "Request", dataType: "WorkflowRequest" }],
          outputs: [{ id: "approved", label: "Approved", dataType: "ApprovalDecision" }],
        },
      }),
    ).toEqual({
      source: "node_approval",
      sourceHandle: "approved",
      target: "node_notify",
      targetHandle: "success",
    });
  });
});
