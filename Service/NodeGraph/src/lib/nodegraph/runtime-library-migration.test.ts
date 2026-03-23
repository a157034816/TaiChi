import { describe, expect, it } from "vitest";

import { migrateGraphWithNodeLibrary } from "@/lib/nodegraph/runtime-library-migration";
import type { NodeGraphDocument, NodeLibraryItem } from "@/lib/nodegraph/types";

const existingGraph: NodeGraphDocument = {
  name: "Hello World",
  description: "Before refresh",
  nodes: [
    {
      id: "node_source",
      type: "default",
      position: { x: 0, y: 0 },
      data: {
        label: "Greeting Source",
        description: "Create the old greeting text.",
        category: "Hello World",
        nodeType: "greeting_source",
        outputs: [{ id: "text", label: "Text", dataType: "hello/text" }],
        values: {
          name: "Codex",
          tone: "friendly",
        },
      },
    },
    {
      id: "node_output",
      type: "default",
      position: { x: 280, y: 0 },
      data: {
        label: "Console Output",
        description: "Write the message to the host console.",
        category: "Hello World",
        nodeType: "console_output",
        inputs: [{ id: "text", label: "Text", dataType: "hello/text" }],
        values: {
          channel: "stdout",
        },
      },
    },
  ],
  edges: [
    {
      id: "edge_source_output",
      source: "node_source",
      sourceHandle: "text",
      target: "node_output",
      targetHandle: "text",
    },
  ],
  viewport: { x: 0, y: 0, zoom: 1 },
};

const refreshedLibrary: NodeLibraryItem[] = [
  {
    type: "greeting_source",
    displayName: "Greeting Source v2",
    description: "Create the latest greeting text.",
    category: "Hello Runtime",
    outputs: [{ id: "message", label: "Message", dataType: "hello/text" }],
    fields: [
      {
        key: "name",
        label: "Name",
        kind: "text",
        defaultValue: "World",
      },
      {
        key: "punctuation",
        label: "Punctuation",
        kind: "text",
        defaultValue: "!",
      },
    ],
  },
];

describe("runtime library migration", () => {
  it("updates node template snapshots and marks removed fields and edges as invalid", () => {
    const migrated = migrateGraphWithNodeLibrary(existingGraph, refreshedLibrary);
    const sourceNode = migrated.nodes.find((node) => node.id === "node_source");

    expect(sourceNode?.data.label).toBe("Greeting Source v2");
    expect(sourceNode?.data.description).toBe("Create the latest greeting text.");
    expect(sourceNode?.data.category).toBe("Hello Runtime");
    expect(sourceNode?.data.outputs).toEqual([{ id: "message", label: "Message", dataType: "hello/text" }]);
    expect(sourceNode?.data.values).toMatchObject({
      name: "Codex",
      punctuation: "!",
      tone: "friendly",
    });
    expect(sourceNode?.data.templateMarkers).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          code: "removedField",
        }),
        expect.objectContaining({
          code: "removedPort",
        }),
      ]),
    );
    expect(migrated.edges[0].invalidReason).toContain("text");
  });

  it("retains nodes whose template was removed and marks them invalid", () => {
    const migrated = migrateGraphWithNodeLibrary(existingGraph, refreshedLibrary);
    const outputNode = migrated.nodes.find((node) => node.id === "node_output");

    expect(outputNode?.data.templateMarkers).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          code: "missingNodeType",
        }),
      ]),
    );
  });
});
