import { describe, expect, it } from "vitest";

import { resolveContextMenuPosition } from "@/components/editor/context-menu-position";

describe("context menu position", () => {
  it("opens to the right and below the cursor when there is enough room", () => {
    expect(
      resolveContextMenuPosition(
        { x: 240, y: 180 },
        { width: 320, height: 260 },
        { width: 1024, height: 768 },
        16,
      ),
    ).toEqual({
      x: 240,
      y: 180,
    });
  });

  it("opens to the left and above the cursor when the bottom-right space is insufficient", () => {
    expect(
      resolveContextMenuPosition(
        { x: 980, y: 720 },
        { width: 320, height: 260 },
        { width: 1024, height: 768 },
        16,
      ),
    ).toEqual({
      x: 660,
      y: 460,
    });
  });

  it("clamps the menu when it is larger than the available space on both sides", () => {
    expect(
      resolveContextMenuPosition(
        { x: 64, y: 64 },
        { width: 500, height: 700 },
        { width: 420, height: 520 },
        16,
      ),
    ).toEqual({
      x: 16,
      y: 16,
    });
  });
});
