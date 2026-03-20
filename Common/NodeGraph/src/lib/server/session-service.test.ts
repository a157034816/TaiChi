import { beforeEach, describe, expect, it, vi } from "vitest";

import { completeSession, createSession, getSession } from "@/lib/server/session-service";
import { getRuntimeStore } from "@/lib/server/store";

const sessionInput = {
  domain: "erp-workflow",
  clientName: "TaiChi ERP",
  nodeLibraryEndpoint: "https://client.example.com/library",
  completionWebhook: "https://client.example.com/completed",
  graph: {
    name: "审批流程",
    nodes: [],
    edges: [],
    viewport: { x: 0, y: 0, zoom: 1 },
  },
};

describe("session service", () => {
  beforeEach(() => {
    const store = getRuntimeStore();
    store.domains.clear();
    store.sessions.clear();
    vi.restoreAllMocks();
  });

  it("creates a private editor session and posts the completion webhook", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(
        new Response(
          JSON.stringify({
            nodes: [
              {
                type: "start",
                labelKey: "nodes.start.label",
                descriptionKey: "nodes.start.description",
                categoryKey: "categories.control",
              },
            ],
            i18n: {
              defaultLocale: "en",
              locales: {
                en: {
                  "categories.control": "Control",
                  "nodes.start.description": "Entry node",
                  "nodes.start.label": "Start",
                  "ports.next": "Next",
                },
                "zh-CN": {
                  "categories.control": "控制",
                  "nodes.start.description": "入口节点",
                  "nodes.start.label": "开始",
                  "ports.next": "下一步",
                },
              },
            },
          }),
        ),
      )
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
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

    const completionResult = await completeSession(created.sessionId, {
      ...sessionInput.graph,
      nodes: [
        {
          id: "node_1",
          type: "default",
          position: { x: 0, y: 0 },
          data: {
            label: "Start",
            labelKey: "nodes.start.label",
            nodeType: "start",
            outputs: [{ id: "next", labelKey: "ports.next" }],
          },
        },
      ],
      edges: [
        {
          id: "edge_1",
          source: "node_1",
          sourceHandle: "next",
          target: "node_2",
          targetHandle: "request",
        },
      ],
    });

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(completionResult.delivered).toBe(true);
    expect(getSession(created.sessionId)?.status).toBe("completed");
    const webhookRequest = fetchMock.mock.calls[1][1];
    const webhookPayload = JSON.parse(String(webhookRequest?.body));
    expect(webhookPayload.graph.edges[0]).toMatchObject({
      sourceHandle: "next",
      targetHandle: "request",
    });
    expect(webhookPayload.graph.nodes[0].data).toMatchObject({
      label: "Start",
      labelKey: "nodes.start.label",
    });
  });
});
