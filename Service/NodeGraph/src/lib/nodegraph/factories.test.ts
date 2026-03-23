import { describe, expect, it } from "vitest";

import { createI18nRuntime } from "@/lib/nodegraph/localization";
import {
  buildFieldDefaults,
  buildNodeStyle,
  buildPortSnapshot,
  createNodeFromLibrary,
  normalizeNodeDataPorts,
} from "@/lib/nodegraph/factories";

const workflowRequestType = "workflow/request";
const approvalDecisionType = "workflow/approval-decision";
describe("nodegraph factories", () => {
  it("builds defaults for supported field kinds", () => {
    expect(
      buildFieldDefaults([
        { key: "owner", label: "Owner", kind: "text" },
        { key: "notes", label: "Notes", kind: "textarea" },
        { key: "priority", label: "Priority", kind: "select", optionsEndpoint: "https://client.example.com/options/priorities" },
        { key: "dueDate", label: "Due Date", kind: "date" },
        { key: "theme", label: "Theme", kind: "color" },
        { key: "retries", label: "Retries", kind: "int" },
        { key: "score", label: "Score", kind: "float" },
        { key: "ratio", label: "Ratio", kind: "double" },
        { key: "budget", label: "Budget", kind: "decimal" },
        { key: "required", label: "Required", kind: "boolean" },
      ]),
    ).toEqual({
      owner: "",
      notes: "",
      priority: "",
      dueDate: "",
      theme: "",
      retries: 0,
      score: 0,
      ratio: 0,
      budget: "",
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
      inputs: [{ id: "in", label: "输入" }],
      outputs: [{ id: "out", label: "输出" }],
    });
  });

  it("preserves explicit multi-port and empty-port definitions", () => {
    expect(
      buildPortSnapshot({
        inputs: [],
        outputs: [
          { id: "approved", label: "Approved", dataType: approvalDecisionType },
          { id: "rejected", label: "Rejected", dataType: approvalDecisionType },
        ],
      }),
    ).toEqual({
      inputs: [],
      outputs: [
        { id: "approved", label: "Approved", dataType: approvalDecisionType },
        { id: "rejected", label: "Rejected", dataType: approvalDecisionType },
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
          inputs: [{ id: "request", label: "Request", dataType: workflowRequestType }],
          outputs: [
            { id: "approved", label: "Approved", dataType: approvalDecisionType },
            { id: "rejected", label: "Rejected", dataType: approvalDecisionType },
          ],
        },
      ),
    ).toMatchObject({
      inputs: [{ id: "request", label: "Request", dataType: workflowRequestType }],
      outputs: [
        { id: "approved", label: "Approved", dataType: approvalDecisionType },
        { id: "rejected", label: "Rejected", dataType: approvalDecisionType },
      ],
    });
  });

  it("creates nodes from raw library strings and configured values", () => {
    const i18n = createI18nRuntime({
      locale: "en",
    });
    const node = createNodeFromLibrary(
      {
        type: "approval",
        displayName: "Approval",
        description: "Manual approval step.",
        category: "Workflow",
        inputs: [{ id: "request", label: "Request", dataType: workflowRequestType }],
        outputs: [
          { id: "approved", label: "Approved", dataType: approvalDecisionType },
          { id: "rejected", label: "Rejected", dataType: approvalDecisionType },
        ],
        fields: [
          { key: "owner", label: "Owner", kind: "text" },
          { key: "sla", label: "SLA", kind: "int", defaultValue: 48 },
          {
            key: "priority",
            label: "Priority",
            kind: "select",
            optionsEndpoint: "https://client.example.com/options/priorities",
            defaultValue: "email",
          },
          {
            key: "budget",
            label: "Budget",
            kind: "decimal",
            defaultValue: "99.90",
          },
        ],
        defaultData: {
          channel: "email",
        },
        appearance: {
          borderColor: "#f59e0b",
        },
      },
      { x: 120, y: 80 },
      i18n,
    );

    expect(node.type).toBe("default");
    expect(node.position).toEqual({ x: 120, y: 80 });
    expect(node.data.label).toBe("Approval");
    expect(node.data.description).toBe("Manual approval step.");
    expect(node.data.category).toBe("Workflow");
    expect(node.data.inputs).toEqual([{ id: "request", label: "Request", dataType: workflowRequestType }]);
    expect(node.data.outputs).toEqual([
      { id: "approved", label: "Approved", dataType: approvalDecisionType },
      { id: "rejected", label: "Rejected", dataType: approvalDecisionType },
    ]);
    expect(node.data.values).toEqual({
      owner: "",
      sla: 48,
      priority: "email",
      budget: "99.90",
      channel: "email",
    });
    expect(node.style).toMatchObject({
      background: "transparent",
      borderColor: "#f59e0b",
      width: 280,
    });
  });
});
