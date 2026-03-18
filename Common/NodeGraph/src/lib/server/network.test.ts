import { describe, expect, it } from "vitest";

import { extractClientIp, isPrivateIp, resolveAccessType } from "@/lib/server/network";

describe("network utilities", () => {
  it("extracts the forwarded IP and marks private networks correctly", () => {
    const request = new Request("http://localhost/api", {
      headers: {
        "x-forwarded-for": "10.10.1.5, 203.0.113.8",
      },
    });

    expect(extractClientIp(request)).toBe("10.10.1.5");
    expect(resolveAccessType(request)).toBe("private");
  });

  it("treats public IPv4 addresses as public access", () => {
    expect(isPrivateIp("203.0.113.42")).toBe(false);
  });

  it("recognizes loopback and unique local addresses as private", () => {
    expect(isPrivateIp("127.0.0.1")).toBe(true);
    expect(isPrivateIp("fd12::1")).toBe(true);
  });
});
