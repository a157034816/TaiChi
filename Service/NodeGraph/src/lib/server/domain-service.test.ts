import { beforeEach, describe, expect, it, vi } from "vitest";

import { ensureDomain } from "@/lib/server/domain-service";
import { getRuntimeStore } from "@/lib/server/store";

const workflowRequestType = "workflow/request";
const defaultTypeColor = "#64748B";
const libraryI18n = {
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
} as const;

const createInput = () => ({
  domain: "erp-workflow",
  clientName: "TaiChi ERP",
  nodeLibraryEndpoint: "https://client.example.com/library",
  completionWebhook: "https://client.example.com/completed",
  metadata: {
    ticketId: "WF-1001",
  },
});

describe("domain service", () => {
  beforeEach(() => {
    const store = getRuntimeStore();
    store.domains.clear();
    store.sessions.clear();
    vi.restoreAllMocks();
  });

  it("fetches and stores the node library plus i18n catalog for a first-time domain", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          nodes: [
            {
              type: "start",
              labelKey: "nodes.start.label",
              descriptionKey: "nodes.start.description",
              categoryKey: "categories.control",
              inputs: [],
              outputs: [{ id: "next", labelKey: "ports.next", dataType: workflowRequestType }],
            },
          ],
          i18n: libraryI18n,
          typeMappings: [
            {
              canonicalId: workflowRequestType,
              type: "WorkflowRequest",
            },
          ],
        }),
      ),
    );
    vi.stubGlobal("fetch", fetchMock);

    const result = await ensureDomain(createInput());

    expect(result.domainCached).toBe(false);
    expect(result.entry.nodeLibrary).toHaveLength(1);
    expect(result.entry.nodeLibrary[0].outputs).toEqual([
      { id: "next", labelKey: "ports.next", dataType: workflowRequestType },
    ]);
    expect(result.entry.i18n).toEqual(libraryI18n);
    expect(result.entry.typeMappings).toEqual([
      {
        canonicalId: workflowRequestType,
        type: "WorkflowRequest",
        color: defaultTypeColor,
      },
    ]);
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(getRuntimeStore().domains.get("erp-workflow")).toBeDefined();
  });

  it("reuses the in-memory cache when the endpoints are unchanged", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
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
          i18n: libraryI18n,
        }),
      ),
    );
    vi.stubGlobal("fetch", fetchMock);

    await ensureDomain(createInput());
    const cachedResult = await ensureDomain(createInput());

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(cachedResult.domainCached).toBe(true);
  });

  it("rejects conflicting current-sdk type mappings", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            nodes: [
              {
                type: "approval",
                labelKey: "nodes.approval.label",
                descriptionKey: "nodes.approval.description",
                categoryKey: "categories.workflow",
                inputs: [{ id: "request", labelKey: "ports.request", dataType: workflowRequestType }],
              },
            ],
            i18n: {
              defaultLocale: "en",
              locales: {
                en: {
                  "categories.workflow": "Workflow",
                  "nodes.approval.description": "Manual approval step",
                  "nodes.approval.label": "Approval",
                  "ports.request": "Request",
                },
              },
            },
            typeMappings: [
              {
                canonicalId: workflowRequestType,
                type: "SharedType",
              },
              {
                canonicalId: "workflow/review-task",
                type: "SharedType",
              },
            ],
          }),
        ),
      ),
    );

    await expect(ensureDomain(createInput())).rejects.toMatchObject({
      message: expect.stringContaining('type "SharedType"'),
      status: 502,
    });
  });

  it("rejects node-library payloads whose translation keys are missing from the default locale", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            nodes: [
              {
                type: "start",
                labelKey: "nodes.start.label",
                descriptionKey: "nodes.start.description",
                categoryKey: "categories.control",
                outputs: [{ id: "next", labelKey: "ports.next", dataType: workflowRequestType }],
              },
            ],
            i18n: {
              defaultLocale: "en",
              locales: {
                en: {
                  "categories.control": "Control",
                  "nodes.start.description": "Entry node",
                  "nodes.start.label": "Start",
                },
              },
            },
            typeMappings: [
              {
                canonicalId: workflowRequestType,
                type: "WorkflowRequest",
              },
            ],
          }),
        ),
      ),
    );

    await expect(ensureDomain(createInput())).rejects.toThrow(/ports\.next/);
  });

  it("rejects invalid type mapping colors", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            nodes: [
              {
                type: "start",
                labelKey: "nodes.start.label",
                descriptionKey: "nodes.start.description",
                categoryKey: "categories.control",
                outputs: [{ id: "next", labelKey: "ports.next", dataType: workflowRequestType }],
              },
            ],
            i18n: libraryI18n,
            typeMappings: [
              {
                canonicalId: workflowRequestType,
                type: "WorkflowRequest",
                color: "red",
              },
            ],
          }),
        ),
      ),
    );

    await expect(ensureDomain(createInput())).rejects.toMatchObject({
      message: expect.stringContaining("node library payload is invalid"),
      status: 502,
    });
  });
});
