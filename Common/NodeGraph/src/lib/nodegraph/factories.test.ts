import { describe, expect, it } from "vitest";

import { createLocalizedText } from "@/lib/nodegraph/localization";
import {
  buildFieldDefaults,
  buildNodeStyle,
  buildPortSnapshot,
  createNodeFromLibrary,
  normalizeNodeDataPorts,
} from "@/lib/nodegraph/factories";

const workflowRequestType = "workflow/request";
const approvalDecisionType = "workflow/approval-decision";
const text = (zhCN: string, en = zhCN) => createLocalizedText(zhCN, en);

describe("nodegraph factories", () => {
  it("builds defaults for supported field kinds", () => {
    expect(
      buildFieldDefaults([
        { key: "owner", label: text("Owner"), kind: "text" },
        { key: "notes", label: text("Notes"), kind: "textarea" },
        { key: "retries", label: text("Retries"), kind: "number" },
        { key: "required", label: text("Required"), kind: "boolean" },
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
      inputs: [{ id: "in", label: text("输入", "Input") }],
      outputs: [{ id: "out", label: text("输出", "Output") }],
    });
  });

  it("preserves explicit multi-port and empty-port definitions", () => {
    expect(
      buildPortSnapshot({
        inputs: [],
        outputs: [
          { id: "approved", label: text("Approved"), dataType: approvalDecisionType },
          { id: "rejected", label: text("Rejected"), dataType: approvalDecisionType },
        ],
      }),
    ).toEqual({
      inputs: [],
      outputs: [
        { id: "approved", label: text("Approved"), dataType: approvalDecisionType },
        { id: "rejected", label: text("Rejected"), dataType: approvalDecisionType },
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
          inputs: [{ id: "request", label: text("Request"), dataType: workflowRequestType }],
          outputs: [
            { id: "approved", label: text("Approved"), dataType: approvalDecisionType },
            { id: "rejected", label: text("Rejected"), dataType: approvalDecisionType },
          ],
        },
      ),
    ).toMatchObject({
      inputs: [{ id: "request", label: text("Request"), dataType: workflowRequestType }],
      outputs: [
        { id: "approved", label: text("Approved"), dataType: approvalDecisionType },
        { id: "rejected", label: text("Rejected"), dataType: approvalDecisionType },
      ],
    });
  });

  it("creates nodes with default type and configured values", () => {
    const node = createNodeFromLibrary(
      {
        type: "approval",
        label: text("Approval"),
        description: text("Manual approval step"),
        category: text("workflow"),
        inputs: [{ id: "request", label: text("Request"), dataType: workflowRequestType }],
        outputs: [
          { id: "approved", label: text("Approved"), dataType: approvalDecisionType },
          { id: "rejected", label: text("Rejected"), dataType: approvalDecisionType },
        ],
        fields: [
          { key: "owner", label: text("Owner"), kind: "text" },
          { key: "sla", label: text("SLA"), kind: "number", defaultValue: 48 },
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
    expect(node.data.inputs).toEqual([{ id: "request", label: text("Request"), dataType: workflowRequestType }]);
    expect(node.data.outputs).toEqual([
      { id: "approved", label: text("Approved"), dataType: approvalDecisionType },
      { id: "rejected", label: text("Rejected"), dataType: approvalDecisionType },
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
