import { describe, expect, it } from "vitest";

import {
  buildFieldDefaults,
  buildNodeStyle,
  buildPortSnapshot,
  createNodeFromLibrary,
  normalizeNodeDataPorts,
} from "@/lib/nodegraph/factories";

describe("nodegraph factories", () => {
  it("builds defaults for supported field kinds", () => {
    expect(
      buildFieldDefaults([
        { key: "owner", label: "Owner", kind: "text" },
        { key: "notes", label: "Notes", kind: "textarea" },
        { key: "retries", label: "Retries", kind: "number" },
        { key: "required", label: "Required", kind: "boolean" },
      ]),
    ).toEqual({
      owner: "",
      notes: "",
      retries: 0,
      required: false,
    });
  });

  it("returns blueprint-friendly wrapper styles", () => {
    expect(
      buildNodeStyle({
        borderColor: "#1fb6ff",
        textColor: "#101521",
      }),
    ).toMatchObject({
      background: "transparent",
      border: "none",
      borderColor: "#1fb6ff",
      color: "#101521",
      minWidth: 280,
      width: 280,
    });
  });

  it("builds default single ports when a template omits them", () => {
    expect(buildPortSnapshot()).toEqual({
      inputs: [{ id: "in", label: "Input" }],
      outputs: [{ id: "out", label: "Output" }],
    });
  });

  it("preserves explicit multi-port and empty-port definitions", () => {
    expect(
      buildPortSnapshot({
        inputs: [],
        outputs: [
          { id: "approved", label: "Approved" },
          { id: "rejected", label: "Rejected" },
        ],
      }),
    ).toEqual({
      inputs: [],
      outputs: [
        { id: "approved", label: "Approved" },
        { id: "rejected", label: "Rejected" },
      ],
    });
  });

  it("hydrates legacy node data ports from the matching template", () => {
    expect(
      normalizeNodeDataPorts(
        {
          label: "Approval",
          nodeType: "approval",
        },
        {
          inputs: [{ id: "request", label: "Request" }],
          outputs: [
            { id: "approved", label: "Approved" },
            { id: "rejected", label: "Rejected" },
          ],
        },
      ),
    ).toMatchObject({
      inputs: [{ id: "request", label: "Request" }],
      outputs: [
        { id: "approved", label: "Approved" },
        { id: "rejected", label: "Rejected" },
      ],
    });
  });

  it("creates nodes with default type and configured values", () => {
    const node = createNodeFromLibrary(
      {
        type: "approval",
        label: "Approval",
        description: "Manual approval step",
        category: "workflow",
        inputs: [{ id: "request", label: "Request" }],
        outputs: [
          { id: "approved", label: "Approved" },
          { id: "rejected", label: "Rejected" },
        ],
        fields: [
          { key: "owner", label: "Owner", kind: "text" },
          { key: "sla", label: "SLA", kind: "number", defaultValue: 48 },
        ],
        defaultData: {
          channel: "email",
        },
        appearance: {
          borderColor: "#f59e0b",
        },
      },
      { x: 120, y: 80 },
    );

    expect(node.type).toBe("default");
    expect(node.position).toEqual({ x: 120, y: 80 });
    expect(node.data.inputs).toEqual([{ id: "request", label: "Request" }]);
    expect(node.data.outputs).toEqual([
      { id: "approved", label: "Approved" },
      { id: "rejected", label: "Rejected" },
    ]);
    expect(node.data.values).toEqual({
      owner: "",
      sla: 48,
      channel: "email",
    });
    expect(node.style).toMatchObject({
      background: "transparent",
      borderColor: "#f59e0b",
      width: 280,
    });
  });
});
