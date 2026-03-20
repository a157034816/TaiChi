import { describe, expect, it } from "vitest";

import { createI18nRuntime } from "@/lib/nodegraph/localization";
import {
  buildEdgeInspectorDetails,
  getInspectorAccent,
  getInspectorSelectionTabLabel,
} from "@/lib/nodegraph/inspector";
import type { NodeGraphEdge, NodeGraphNode } from "@/lib/nodegraph/types";

const i18n = createI18nRuntime({
  locale: "en",
});
const nodes: NodeGraphNode[] = [
  {
    id: "node_start",
    position: { x: 0, y: 0 },
    data: {
      label: "Start",
      labelKey: "nodes.start.label",
      nodeType: "start",
      appearance: {
        borderColor: "#ff9d1c",
      },
    },
  },
  {
    id: "node_notify",
    position: { x: 200, y: 0 },
    data: {
      label: "Notify",
      labelKey: "nodes.notify.label",
      nodeType: "notify",
    },
  },
];

const edge: NodeGraphEdge = {
  id: "edge_start_notify",
  source: "node_start",
  sourceHandle: "next",
  target: "node_notify",
  targetHandle: null,
  style: {
    stroke: "#9fb3d9",
  },
};

describe("nodegraph inspector helpers", () => {
  it("returns a link-specific tab label when an edge is selected", () => {
    expect(getInspectorSelectionTabLabel(null, edge, i18n)).toBe("Selected link");
    expect(getInspectorSelectionTabLabel(nodes[0], null, i18n)).toBe("Selected node");
    expect(getInspectorSelectionTabLabel(null, null, i18n)).toBe("Selection");
  });

  it("builds readable edge inspector details with fallback handle labels", () => {
    expect(buildEdgeInspectorDetails(edge, nodes, i18n)).toEqual({
      title: "Start -> Notify",
      subtitle: "Connection",
      description: "This link routes next from Start into Default port on Notify.",
      items: [
        { label: "Edge id", value: "edge_start_notify" },
        { label: "Source node", value: "Start (node_start)" },
        { label: "Source handle", value: "next" },
        { label: "Target node", value: "Notify (node_notify)" },
        { label: "Target handle", value: "Default port" },
      ],
    });
  });

  it("prefers node accent colors and falls back to edge or default accents", () => {
    expect(getInspectorAccent(nodes[0], null)).toBe("#ff9d1c");
    expect(getInspectorAccent(null, edge)).toBe("#9fb3d9");
    expect(
      getInspectorAccent(null, {
        id: "edge_plain",
        source: "node_start",
        target: "node_notify",
      }),
    ).toBe("#57c7ff");
  });
});
