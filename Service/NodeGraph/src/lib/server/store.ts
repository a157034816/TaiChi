import type { DomainRegistryEntry, NodeGraphSession } from "@/lib/nodegraph/types";

interface RuntimeStore {
  domains: Map<string, DomainRegistryEntry>;
  sessions: Map<string, NodeGraphSession>;
}

declare global {
  var __nodeGraphStore: RuntimeStore | undefined;
}

export function getRuntimeStore(): RuntimeStore {
  if (!globalThis.__nodeGraphStore) {
    globalThis.__nodeGraphStore = {
      domains: new Map(),
      sessions: new Map(),
    };
  }

  return globalThis.__nodeGraphStore;
}
