import { describe, expect, it } from "vitest";

import {
  consumePaneClickSuppression,
  shouldOpenConnectionCreationMenu,
} from "@/components/editor/connection-menu-interactions";

describe("connection menu interactions", () => {
  it("opens the connection creation menu only for a blank-canvas drop without a target handle", () => {
    expect(
      shouldOpenConnectionCreationMenu({
        hasPendingConnection: true,
        hasClientPosition: true,
        hasTargetNode: false,
        hasTargetHandle: false,
        droppedOnBlankCanvas: true,
      }),
    ).toBe(true);

    expect(
      shouldOpenConnectionCreationMenu({
        hasPendingConnection: true,
        hasClientPosition: true,
        hasTargetNode: true,
        hasTargetHandle: true,
        droppedOnBlankCanvas: false,
      }),
    ).toBe(false);
  });

  it("does not open the menu when the pointer position or pending connection is missing", () => {
    expect(
      shouldOpenConnectionCreationMenu({
        hasPendingConnection: false,
        hasClientPosition: true,
        hasTargetNode: false,
        hasTargetHandle: false,
        droppedOnBlankCanvas: true,
      }),
    ).toBe(false);

    expect(
      shouldOpenConnectionCreationMenu({
        hasPendingConnection: true,
        hasClientPosition: false,
        hasTargetNode: false,
        hasTargetHandle: false,
        droppedOnBlankCanvas: true,
      }),
    ).toBe(false);
  });

  it("consumes pane click suppression after a single ignored click", () => {
    expect(consumePaneClickSuppression(true)).toEqual({
      shouldIgnorePaneClick: true,
      nextShouldSuppressPaneClick: false,
    });

    expect(consumePaneClickSuppression(false)).toEqual({
      shouldIgnorePaneClick: false,
      nextShouldSuppressPaneClick: false,
    });
  });
});
