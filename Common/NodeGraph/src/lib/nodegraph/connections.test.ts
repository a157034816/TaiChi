import { describe, expect, it } from "vitest";

import { isInputHandleOccupied } from "@/lib/nodegraph/connections";

describe("nodegraph connections", () => {
  it("marks a target handle as occupied when another edge already uses it", () => {
    expect(
      isInputHandleOccupied(
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
    ).toBe(true);
  });

  it("treats different target handles on the same node as independent inputs", () => {
    expect(
      isInputHandleOccupied(
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
    ).toBe(false);
  });

  it("treats legacy single-input edges without handle ids as a single occupied input", () => {
    expect(
      isInputHandleOccupied(
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
    ).toBe(true);
  });
});
