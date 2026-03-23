import { beforeEach, describe, expect, it, vi } from "vitest";

import type { CreateSessionRequest, RuntimeRegistrationRequest } from "@/lib/nodegraph/types";
import {
  completeSession,
  createSession,
  getEditorPayload,
  getFieldOptions,
  getSession,
  refreshSessionRuntimeLibrary,
} from "@/lib/server/session-service";
import { registerRuntime } from "@/lib/server/runtime-service";
import { getRuntimeStore } from "@/lib/server/store";

const runtimeInput = {
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
        outputs: [{ id: "text", label: "Text", dataType: "hello/text" }],
      },
      {
        type: "console_output",
        displayName: "Console Output",
        description: "Write the message to the host console.",
        category: "Hello World",
        inputs: [{ id: "text", label: "Text", dataType: "hello/text" }],
        fields: [
          {
            key: "channel",
            label: "Channel",
            kind: "select",
            optionsEndpoint: "https://client.example.com/nodegraph/runtime/options/channel",
          },
        ],
      },
    ],
    typeMappings: [
      {
        canonicalId: "hello/text",
        type: "GreetingText",
      },
    ],
  },
} satisfies RuntimeRegistrationRequest;

const sessionInput = {
  runtimeId: runtimeInput.runtimeId,
  completionWebhook: "https://client.example.com/completed",
  graph: {
    name: "Hello World",
    nodes: [],
    edges: [],
    viewport: { x: 0, y: 0, zoom: 1 },
  },
  metadata: {
    ticketId: "HELLO-1",
  },
} satisfies CreateSessionRequest;

describe("session service", () => {
  beforeEach(() => {
    const store = getRuntimeStore();
    store.runtimes.clear();
    store.sessions.clear();
    vi.restoreAllMocks();
  });

  it("creates a private editor session from a registered runtime and posts the completion webhook", async () => {
    registerRuntime(runtimeInput);

    const fetchMock = vi.fn().mockResolvedValueOnce(new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetchMock);

    const created = await createSession(
      new Request("http://localhost/api/sdk/sessions", {
        headers: {
          "x-forwarded-for": "10.0.0.25",
        },
      }),
      sessionInput,
    );

    expect(created.accessType).toBe("private");
    expect(created.editorUrl).toContain("127.0.0.1");
    expect(created.runtimeId).toBe(runtimeInput.runtimeId);

    const editorPayload = getEditorPayload(created.sessionId);
    expect(editorPayload.runtime.runtimeId).toBe(runtimeInput.runtimeId);
    expect(editorPayload.nodeLibrary[0]).toMatchObject({
      displayName: "Greeting Source",
      type: "greeting_source",
    });

    const completionResult = await completeSession(created.sessionId, {
      ...sessionInput.graph,
      nodes: [
        {
          id: "node_1",
          type: "default",
          position: { x: 0, y: 0 },
          data: {
            label: "Greeting Source",
            description: "Create the base greeting text.",
            category: "Hello World",
            nodeType: "greeting_source",
            outputs: [{ id: "text", label: "Text", dataType: "hello/text" }],
          },
        },
      ],
      edges: [],
    });

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(completionResult.delivered).toBe(true);
    expect(getSession(created.sessionId)?.status).toBe("completed");
    const webhookRequest = fetchMock.mock.calls[0][1];
    const webhookPayload = JSON.parse(String(webhookRequest?.body));
    expect(webhookPayload.graph.nodes[0].data).toMatchObject({
      label: "Greeting Source",
      category: "Hello World",
    });
  });

  it("proxies select field options through the configured runtime endpoint", async () => {
    registerRuntime(runtimeInput);

    const fetchMock = vi.fn().mockResolvedValueOnce(
      new Response(
        JSON.stringify({
          options: [
            { value: "stdout", label: "stdout" },
            { value: "debug", label: "debug" },
          ],
        }),
      ),
    );
    vi.stubGlobal("fetch", fetchMock);

    const created = await createSession(
      new Request("http://localhost/api/sdk/sessions", {
        headers: {
          "x-forwarded-for": "10.0.0.25",
        },
      }),
      sessionInput,
    );

    const result = await getFieldOptions(created.sessionId, {
      fieldKey: "channel",
      locale: "en",
      nodeType: "console_output",
    });

    expect(result).toEqual({
      options: [
        { value: "stdout", label: "stdout" },
        { value: "debug", label: "debug" },
      ],
    });
    expect(fetchMock).toHaveBeenCalledTimes(1);
    const requestUrl = new URL(String(fetchMock.mock.calls[0][0]));
    expect(requestUrl.origin + requestUrl.pathname).toBe("https://client.example.com/nodegraph/runtime/options/channel");
    expect(requestUrl.searchParams.get("domain")).toBe("hello-world");
    expect(requestUrl.searchParams.get("fieldKey")).toBe("channel");
    expect(requestUrl.searchParams.get("locale")).toBe("en");
    expect(requestUrl.searchParams.get("nodeType")).toBe("console_output");
  });

  it("rejects session creation when the runtime is missing or expired", async () => {
    await expect(
      createSession(
        new Request("http://localhost/api/sdk/sessions", {
          headers: {
            "x-forwarded-for": "10.0.0.25",
          },
        }),
        sessionInput,
      ),
    ).rejects.toMatchObject({
      message: expect.stringContaining("runtime"),
      status: 404,
    });
  });

  it("refreshes the current session library and returns a migrated graph", async () => {
    registerRuntime(runtimeInput);

    const created = await createSession(
      new Request("http://localhost/api/sdk/sessions", {
        headers: {
          "x-forwarded-for": "10.0.0.25",
        },
      }),
      {
        ...sessionInput,
        graph: {
          ...sessionInput.graph,
          nodes: [
            {
              id: "node_source",
              type: "default",
              position: { x: 0, y: 0 },
              data: {
                label: "Greeting Source",
                description: "Create the base greeting text.",
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
        },
      },
    );

    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            libraryVersion: "hello-world@2",
            library: {
              nodes: [
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
              ],
            },
          }),
        ),
      ),
    );

    const refreshed = await refreshSessionRuntimeLibrary(created.sessionId);

    expect(refreshed.runtime.libraryVersion).toBe("hello-world@2");
    expect(refreshed.migratedGraph?.nodes[0].data).toMatchObject({
      label: "Greeting Source v2",
      category: "Hello Runtime",
    });
    expect(refreshed.migratedGraph?.nodes[0].data.values).toMatchObject({
      name: "Codex",
      punctuation: "!",
      tone: "friendly",
    });
    expect(refreshed.migratedGraph?.nodes[1].data.templateMarkers).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          code: "missingNodeType",
        }),
      ]),
    );
    expect(refreshed.migratedGraph?.edges[0].invalidReason).toContain("text");
    expect(getSession(created.sessionId)?.graph.edges[0].invalidReason).toContain("text");
  });
});
