import { beforeEach, describe, expect, it, vi } from "vitest";

import { ensureDomain } from "@/lib/server/domain-service";
import { getRuntimeStore } from "@/lib/server/store";

const workflowRequestType = "workflow/request";

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

  it("fetches and stores the node library for a first-time domain", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          nodes: [
            {
              type: "start",
              label: "Start",
              description: "Entry node",
              category: "control",
              inputs: [],
              outputs: [{ id: "next", label: "Next", dataType: workflowRequestType }],
            },
          ],
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
      { id: "next", label: "Next", dataType: workflowRequestType },
    ]);
    expect(result.entry.typeMappings).toEqual([
      {
        canonicalId: workflowRequestType,
        type: "WorkflowRequest",
      },
    ]);
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(getRuntimeStore().domains.get("erp-workflow")).toBeDefined();
  });

  it("reuses the in-memory cache when the endpoints are unchanged", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify([
            {
              type: "start",
              label: "Start",
              description: "Entry node",
              category: "control",
            },
          ]),
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
                label: "Approval",
                description: "Manual approval step",
                category: "workflow",
                inputs: [{ id: "request", label: "Request", dataType: workflowRequestType }],
              },
            ],
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

  it("rejects typed ports whose canonical ids are missing from typeMappings", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            nodes: [
              {
                type: "approval",
                label: "Approval",
                description: "Manual approval step",
                category: "workflow",
                inputs: [{ id: "request", label: "Request", dataType: workflowRequestType }],
              },
            ],
            typeMappings: [
              {
                canonicalId: "workflow/review-task",
                type: "ReviewTask",
              },
            ],
          }),
        ),
      ),
    );

    await expect(ensureDomain(createInput())).rejects.toMatchObject({
      message: expect.stringContaining(`canonicalId "${workflowRequestType}"`),
      status: 502,
    });
  });
});
