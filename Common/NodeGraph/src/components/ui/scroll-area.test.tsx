// @vitest-environment jsdom

import { act } from "react";
import { createRoot } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";

import { ScrollArea } from "@/components/ui/scroll-area";

/**
 * JSDOM does not implement ResizeObserver, but our scroll container only needs
 * a minimal stub for mount-time smoke coverage.
 */
class ResizeObserverStub {
  observe() {}

  unobserve() {}

  disconnect() {}
}

describe("ScrollArea", () => {
  afterEach(() => {
    document.body.innerHTML = "";
    vi.restoreAllMocks();
  });

  it("mounts without triggering a maximum update depth error", async () => {
    vi.stubGlobal("ResizeObserver", ResizeObserverStub);

    const container = document.createElement("div");
    document.body.appendChild(container);

    const root = createRoot(container);
    const consoleErrors: string[] = [];

    vi.spyOn(console, "error").mockImplementation((...args) => {
      consoleErrors.push(args.map((value) => String(value)).join(" "));
    });

    await act(async () => {
      root.render(
        <ScrollArea className="h-24">
          <div style={{ minHeight: "12rem" }}>Scrollable content</div>
        </ScrollArea>,
      );
    });

    await act(async () => {
      await Promise.resolve();
    });

    expect(consoleErrors.join("\n")).not.toContain("Maximum update depth exceeded");

    await act(async () => {
      root.unmount();
    });
  });
});
