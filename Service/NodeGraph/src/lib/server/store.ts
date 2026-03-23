import type { NodeGraphSession, RuntimeRegistryEntry } from "@/lib/nodegraph/types";

interface RuntimeStore {
  runtimes: Map<string, RuntimeRegistryEntry>;
  sessions: Map<string, NodeGraphSession>;
}

declare global {
  var __nodeGraphStore: RuntimeStore | undefined;
}

export function getRuntimeStore(): RuntimeStore {
  if (!globalThis.__nodeGraphStore) {
    globalThis.__nodeGraphStore = {
      runtimes: new Map(),
      sessions: new Map(),
    };
  }

  return globalThis.__nodeGraphStore;
}
