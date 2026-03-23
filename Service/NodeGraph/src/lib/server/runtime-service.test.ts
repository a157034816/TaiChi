import { beforeEach, describe, expect, it, vi } from "vitest";

import type { RuntimeRegistrationRequest } from "@/lib/nodegraph/types";
import {
  getRuntimeEntry,
  refreshRuntimeLibrary,
  registerRuntime,
} from "@/lib/server/runtime-service";
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
        fields: [
          {
            key: "name",
            label: "Name",
            kind: "text",
            defaultValue: "World",
            placeholder: "Enter the target name",
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

describe("runtime service", () => {
  beforeEach(() => {
    const store = getRuntimeStore();
    store.runtimes.clear();
    store.sessions.clear();
    vi.restoreAllMocks();
    vi.useRealTimers();
  });

  it("stores a registered runtime and reuses the cache when the version is unchanged", () => {
    const first = registerRuntime(runtimeInput);
    const second = registerRuntime(runtimeInput);

    expect(first.cached).toBe(false);
    expect(second.cached).toBe(true);
    expect(getRuntimeEntry(runtimeInput.runtimeId)?.nodeLibrary[0]).toMatchObject({
      displayName: "Greeting Source",
      type: "greeting_source",
    });
  });

  it("expires runtime cache entries after 30 minutes", () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-03-21T00:00:00.000Z"));

    registerRuntime(runtimeInput);
    expect(getRuntimeEntry(runtimeInput.runtimeId)).toBeDefined();

    vi.setSystemTime(new Date("2026-03-21T00:30:01.000Z"));
    expect(getRuntimeEntry(runtimeInput.runtimeId)).toBeUndefined();
  });

  it("refreshes a runtime library from the registered host control endpoint", async () => {
    registerRuntime(runtimeInput);

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
                  category: "Hello World",
                  outputs: [{ id: "text", label: "Text", dataType: "hello/text" }],
                },
              ],
            },
          }),
        ),
      ),
    );

    const refreshed = await refreshRuntimeLibrary(runtimeInput.runtimeId);

    expect(refreshed.libraryVersion).toBe("hello-world@2");
    expect(refreshed.nodeLibrary[0]).toMatchObject({
      displayName: "Greeting Source v2",
      type: "greeting_source",
    });
  });
});
