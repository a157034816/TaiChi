"use client";

import { createContext, useContext } from "react";

interface DebuggerNodeContextValue {
  breakpoints: Set<string>;
  failedNodeId: string | null;
  lastEventNodeId: string | null;
  pendingNodeId: string | null;
  onToggleBreakpoint: (nodeId: string) => void;
}

const DebuggerNodeContext = createContext<DebuggerNodeContextValue | null>(null);

/**
 * 为调试页内的蓝图节点提供断点与运行态信息。
 */
export function DebuggerNodeProvider({
  children,
  value,
}: {
  children: React.ReactNode;
  value: DebuggerNodeContextValue;
}) {
  return <DebuggerNodeContext.Provider value={value}>{children}</DebuggerNodeContext.Provider>;
}

/**
 * 读取当前节点调试上下文；普通编辑页下允许为空。
 */
export function useDebuggerNodeContext() {
  return useContext(DebuggerNodeContext);
}
