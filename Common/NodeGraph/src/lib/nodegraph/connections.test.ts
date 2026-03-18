import { describe, expect, it } from "vitest";

import { removeConflictingInputEdges } from "@/lib/nodegraph/connections";

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
});
