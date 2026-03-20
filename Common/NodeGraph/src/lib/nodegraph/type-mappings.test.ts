import { describe, expect, it } from "vitest";

import {
  DEFAULT_TYPE_COLOR,
  buildTypeMappingIndex,
  normalizeTypeMappings,
  validateNodeLibraryTypeMappings,
} from "@/lib/nodegraph/type-mappings";
import type { NodeLibraryItem, TypeMappingEntry } from "@/lib/nodegraph/types";

const workflowRequestType = "workflow/request";
const reviewTaskType = "workflow/review-task";

describe("nodegraph type mappings", () => {
  it("indexes one current-sdk type for each canonical id", () => {
    const mappings: TypeMappingEntry[] = [
      {
        canonicalId: workflowRequestType,
        type: "WorkflowRequest",
      },
      {
        canonicalId: reviewTaskType,
        type: "ReviewTask",
      },
    ];

    const result = buildTypeMappingIndex(mappings);

    expect(result.canonicalToType.get(workflowRequestType)).toBe("WorkflowRequest");
    expect(result.typeToCanonical.get("ReviewTask")).toBe(reviewTaskType);
    expect(result.canonicalToColor.get(workflowRequestType)).toBe(DEFAULT_TYPE_COLOR);
  });

  it("rejects one current-sdk type pointing at multiple canonical ids", () => {
    expect(() =>
      buildTypeMappingIndex([
        {
          canonicalId: workflowRequestType,
          type: "SharedType",
        },
        {
          canonicalId: reviewTaskType,
          type: "SharedType",
        },
      ]),
    ).toThrow(/type/);
  });

  it("rejects one canonical id pointing at multiple current-sdk types", () => {
    expect(() =>
      buildTypeMappingIndex([
        {
          canonicalId: workflowRequestType,
          type: "WorkflowRequest",
        },
        {
          canonicalId: workflowRequestType,
          type: "WorkflowRequestV2",
        },
      ]),
    ).toThrow(/canonicalId/);
  });

  it("rejects invalid hex colors", () => {
    expect(() =>
      buildTypeMappingIndex([
        {
          canonicalId: workflowRequestType,
          type: "WorkflowRequest",
          color: "red",
        },
      ]),
    ).toThrow(/invalid color/i);
  });

  it("fills missing colors with the default grey", () => {
    expect(
      normalizeTypeMappings([
        {
          canonicalId: workflowRequestType,
          type: "WorkflowRequest",
        },
      ]),
    ).toEqual([
      {
        canonicalId: workflowRequestType,
        type: "WorkflowRequest",
        color: DEFAULT_TYPE_COLOR,
      },
    ]);
  });

  it("requires mapped canonical ids for typed node ports when mappings exist", () => {
    const nodes: NodeLibraryItem[] = [
      {
        type: "approval",
        labelKey: "nodes.approval.label",
        descriptionKey: "nodes.approval.description",
        categoryKey: "categories.workflow",
        inputs: [{ id: "request", labelKey: "ports.request", dataType: workflowRequestType }],
      },
    ];

    expect(() =>
      validateNodeLibraryTypeMappings(nodes, [
        {
          canonicalId: reviewTaskType,
          type: "ReviewTask",
        },
      ]),
    ).toThrow(new RegExp(workflowRequestType));
  });

  it("treats an empty typeMappings array as an explicit contract", () => {
    expect(() =>
      validateNodeLibraryTypeMappings(
        [
          {
            type: "approval",
            labelKey: "nodes.approval.label",
            descriptionKey: "nodes.approval.description",
            categoryKey: "categories.workflow",
            inputs: [{ id: "request", labelKey: "ports.request", dataType: workflowRequestType }],
          },
        ],
        [],
      ),
    ).toThrow(new RegExp(workflowRequestType));
  });

  it("keeps legacy libraries valid when typeMappings are omitted", () => {
    expect(() =>
      validateNodeLibraryTypeMappings(
        [
          {
            type: "start",
            labelKey: "nodes.start.label",
            descriptionKey: "nodes.start.description",
            categoryKey: "categories.control",
            outputs: [{ id: "next", labelKey: "ports.next", dataType: workflowRequestType }],
          },
        ],
        undefined,
      ),
    ).not.toThrow();
  });
});
