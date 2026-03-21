import { beforeEach, describe, expect, it, vi } from "vitest";

import { GET } from "@/app/api/editor/sessions/[sessionId]/field-options/route";

const { getFieldOptionsMock } = vi.hoisted(() => ({
  getFieldOptionsMock: vi.fn(),
}));

vi.mock("@/lib/server/session-service", () => ({
  getFieldOptions: getFieldOptionsMock,
}));

describe("field options route", () => {
  beforeEach(() => {
    getFieldOptionsMock.mockReset();
  });

  it("returns proxied field options for the current session", async () => {
    getFieldOptionsMock.mockResolvedValue({
      options: [
        { value: "low", label: "Low" },
        { value: "high", label: "High" },
      ],
    });

    const response = await GET(
      new Request(
        "http://localhost/api/editor/sessions/ngs_test/field-options?nodeType=approval&fieldKey=priority&locale=en",
      ),
      {
        params: Promise.resolve({ sessionId: "ngs_test" }),
      },
    );

    expect(response.status).toBe(200);
    await expect(response.json()).resolves.toEqual({
      options: [
        { value: "low", label: "Low" },
        { value: "high", label: "High" },
      ],
    });
    expect(getFieldOptionsMock).toHaveBeenCalledWith("ngs_test", {
      fieldKey: "priority",
      locale: "en",
      nodeType: "approval",
    });
  });
});
