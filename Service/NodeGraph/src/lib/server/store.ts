import type { ActiveDebugSession, NodeGraphSession, RuntimeRegistryEntry } from "@/lib/nodegraph/types";

interface RuntimeStore {
  runtimes: Map<string, RuntimeRegistryEntry>;
  sessions: Map<string, NodeGraphSession>;
  debugSessions: Map<string, ActiveDebugSession>;
}

declare global {
  var __nodeGraphStore: RuntimeStore | undefined;
}

export function getRuntimeStore(): RuntimeStore {
  if (!globalThis.__nodeGraphStore) {
    globalThis.__nodeGraphStore = {
      runtimes: new Map(),
      sessions: new Map(),
      debugSessions: new Map(),
    };
  }

  return globalThis.__nodeGraphStore;
}
