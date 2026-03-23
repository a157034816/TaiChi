import { describe, expect, it } from "vitest";

import {
  createSessionRequestSchema,
  nodeGraphDocumentSchema,
  nodeLibraryEnvelopeSchema,
  runtimeRegistrationRequestSchema,
} from "@/lib/nodegraph/schema";

const greetingTextType = "hello/text";

describe("nodegraph schema", () => {
  it("accepts raw-string node library items for SDK runtime registration", () => {
    expect(
      runtimeRegistrationRequestSchema.parse({
        runtimeId: "rt_demo_001",
        domain: "hello-world",
        clientName: "Hello World Host",
        controlBaseUrl: "https://client.example.com/nodegraph/runtime",
        libraryVersion: "hello-world@1",
        capabilities: {
          canDebug: true,
          canExecute: true,
          canProfile: true,
        },
        library: {
          nodes: [
            {
              type: "greeting_source",
              displayName: "Greeting Source",
              description: "Create the base greeting text.",
              category: "Hello World",
              outputs: [{ id: "text", label: "Text", dataType: greetingTextType }],
              fields: [
                {
                  key: "name",
                  label: "Name",
                  kind: "text",
                  defaultValue: "World",
                  placeholder: "Enter the target name",
                },
              ],
            },
          ],
          typeMappings: [
            {
              canonicalId: greetingTextType,
              type: "GreetingText",
              color: "#0ea5e9",
            },
          ],
        },
      }),
    ).toBeTruthy();
  });

  it("accepts node library envelopes that no longer provide i18n bundles", () => {
    expect(
      nodeLibraryEnvelopeSchema.parse({
        nodes: [
          {
            type: "console_output",
            displayName: "Console Output",
            description: "Write the message to the host console.",
            category: "Hello World",
            inputs: [{ id: "text", label: "Text", dataType: greetingTextType }],
            outputs: [],
          },
        ],
      }),
    ).toBeTruthy();
  });

  it("accepts saved edge handle ids for multi-port graphs", () => {
    expect(
      nodeGraphDocumentSchema.parse({
        name: "Hello flow",
        nodes: [
          {
            id: "node_greeting",
            type: "default",
            position: { x: 0, y: 0 },
            data: {
              label: "Greeting Source",
              description: "Create the base greeting text.",
              category: "Hello World",
              nodeType: "greeting_source",
              outputs: [{ id: "text", label: "Text", dataType: greetingTextType }],
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
              inputs: [{ id: "text", label: "Text", dataType: greetingTextType }],
              outputs: [],
            },
          },
        ],
        edges: [
          {
            id: "edge_greeting_output",
            source: "node_greeting",
            sourceHandle: "text",
            target: "node_output",
            targetHandle: "text",
          },
        ],
        viewport: { x: 0, y: 0, zoom: 1 },
      }),
    ).toBeTruthy();
  });

  it("rejects saved graph ports that only provide translation keys", () => {
    expect(() =>
      nodeGraphDocumentSchema.parse({
        name: "Hello flow",
        nodes: [
          {
            id: "node_greeting",
            type: "default",
            position: { x: 0, y: 0 },
            data: {
              label: "Greeting Source",
              category: "Hello World",
              nodeType: "greeting_source",
              outputs: [{ id: "text", labelKey: "ports.text", dataType: greetingTextType }],
            },
          },
        ],
        edges: [],
        viewport: { x: 0, y: 0, zoom: 1 },
      }),
    ).toThrow(/label/i);
  });

  it("rejects saved graph nodes that persist localized category objects", () => {
    expect(() =>
      nodeGraphDocumentSchema.parse({
        name: "Hello flow",
        nodes: [
          {
            id: "node_greeting",
            type: "default",
            position: { x: 0, y: 0 },
            data: {
              label: "Greeting Source",
              category: {
                en: "Hello World",
                "zh-CN": "你好世界",
              },
              nodeType: "greeting_source",
              outputs: [{ id: "text", label: "Text", dataType: greetingTextType }],
            },
          },
        ],
        edges: [],
        viewport: { x: 0, y: 0, zoom: 1 },
      }),
    ).toThrow(/category/i);
  });

  it("accepts create-session payloads that reference a runtime id instead of a node-library endpoint", () => {
    expect(
      createSessionRequestSchema.parse({
        runtimeId: "rt_demo_001",
        completionWebhook: "https://client.example.com/nodegraph/completed",
        metadata: {
          ticketId: "HELLO-1",
        },
      }),
    ).toBeTruthy();
  });

  it("rejects runtime registrations whose node library omits raw labels", () => {
    expect(() =>
      runtimeRegistrationRequestSchema.parse({
        runtimeId: "rt_demo_001",
        domain: "hello-world",
        controlBaseUrl: "https://client.example.com/nodegraph/runtime",
        libraryVersion: "hello-world@1",
        library: {
          nodes: [
            {
              type: "greeting_source",
              category: "Hello World",
            },
          ],
        },
      }),
    ).toThrow(/displayName/i);
  });

  it("rejects legacy translation-key node library payloads", () => {
    expect(() =>
      nodeLibraryEnvelopeSchema.parse({
        nodes: [
          {
            type: "greeting_source",
            labelKey: "nodes.greetingSource.label",
            categoryKey: "categories.helloWorld",
          },
        ],
      }),
    ).toThrow();
  });

  it("accepts typed field definitions with raw labels", () => {
    expect(
      nodeLibraryEnvelopeSchema.parse({
        nodes: [
          {
            type: "format_message",
            displayName: "Format Message",
            description: "Format the hello text using a template.",
            category: "Hello World",
            fields: [
              {
                key: "template",
                label: "Template",
                kind: "text",
                defaultValue: "Hello, {name}!",
                placeholder: "Use {name} as the placeholder.",
              },
              {
                key: "repeat",
                label: "Repeat Count",
                kind: "int",
                defaultValue: 1,
              },
              {
                key: "uppercase",
                label: "Uppercase",
                kind: "boolean",
                defaultValue: false,
              },
              {
                key: "previewStyle",
                label: "Preview Style",
                kind: "select",
                optionsEndpoint: "https://client.example.com/nodegraph/runtime/options/previewStyle",
                defaultValue: "console",
              },
            ],
          },
        ],
      }),
    ).toBeTruthy();
  });

  it("rejects select fields without an options endpoint", () => {
    expect(() =>
      nodeLibraryEnvelopeSchema.parse({
        nodes: [
          {
            type: "format_message",
            displayName: "Format Message",
            category: "Hello World",
            fields: [
              {
                key: "previewStyle",
                label: "Preview Style",
                kind: "select",
              },
            ],
          },
        ],
      }),
    ).toThrow(/optionsEndpoint/i);
  });
});
