import { beforeEach, describe, expect, it, vi } from "vitest";

import { completeSession, createSession, getFieldOptions, getSession } from "@/lib/server/session-service";
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

  it("proxies select field options through the configured node template endpoint", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(
        new Response(
          JSON.stringify({
            nodes: [
              {
                type: "approval",
                labelKey: "nodes.approval.label",
                descriptionKey: "nodes.approval.description",
                categoryKey: "categories.control",
                fields: [
                  {
                    key: "priority",
                    labelKey: "fields.priority.label",
                    kind: "select",
                    optionsEndpoint: "https://client.example.com/options/priorities",
                  },
                ],
              },
            ],
            i18n: {
              defaultLocale: "en",
              locales: {
                en: {
                  "categories.control": "Control",
                  "fields.priority.label": "Priority",
                  "nodes.approval.description": "Review node",
                  "nodes.approval.label": "Approval",
                },
                "zh-CN": {
                  "categories.control": "控制",
                  "fields.priority.label": "优先级",
                  "nodes.approval.description": "审核节点",
                  "nodes.approval.label": "审批",
                },
              },
            },
          }),
        ),
      )
      .mockResolvedValueOnce(
        new Response(
          JSON.stringify({
            options: [
              { value: "low", label: "Low" },
              { value: "high", label: "High" },
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
      fieldKey: "priority",
      locale: "en",
      nodeType: "approval",
    });

    expect(result).toEqual({
      options: [
        { value: "low", label: "Low" },
        { value: "high", label: "High" },
      ],
    });
    expect(fetchMock).toHaveBeenCalledTimes(2);
    const requestUrl = new URL(String(fetchMock.mock.calls[1][0]));
    expect(requestUrl.origin + requestUrl.pathname).toBe("https://client.example.com/options/priorities");
    expect(requestUrl.searchParams.get("domain")).toBe("erp-workflow");
    expect(requestUrl.searchParams.get("fieldKey")).toBe("priority");
    expect(requestUrl.searchParams.get("locale")).toBe("en");
    expect(requestUrl.searchParams.get("nodeType")).toBe("approval");
  });
});
