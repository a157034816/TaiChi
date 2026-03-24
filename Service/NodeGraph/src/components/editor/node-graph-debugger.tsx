"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { Background, BackgroundVariant, Controls, MiniMap, ReactFlow } from "@xyflow/react";
import { Activity, Crosshair, Pause, Play, RotateCcw, SquareTerminal } from "lucide-react";

import { DebuggerNodeProvider } from "@/components/editor/debugger-node-context";
import { EditorI18nProvider } from "@/components/editor/editor-i18n-context";
import { TypeColorsProvider } from "@/components/editor/type-colors";
import { editorNodeTypes } from "@/components/editor/use-node-graph-canvas";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { ScrollArea } from "@/components/ui/scroll-area";
import { createI18nRuntime } from "@/lib/nodegraph/localization";
import type { DebugSessionPayload, EditorSessionPayload, NodeGraphNode } from "@/lib/nodegraph/types";

interface NodeGraphDebuggerProps {
  payload: EditorSessionPayload;
}

function getSnapshotNodeId(snapshot: DebugSessionPayload["snapshot"]) {
  const candidate = snapshot.lastEvent?.nodeId;
  return typeof candidate === "string" ? candidate : null;
}

function buildDebugNodes(debugSession: DebugSessionPayload, selectedNodeId: string | null): NodeGraphNode[] {
  const pendingNodeId = debugSession.snapshot.pendingNodeId;
  const failedNodeId = debugSession.snapshot.pauseReason === "error" ? debugSession.snapshot.pendingNodeId : null;
  const lastEventNodeId = getSnapshotNodeId(debugSession.snapshot);

  return debugSession.graph.nodes.map((node) => {
    const isPending = pendingNodeId === node.id;
    const isFailed = failedNodeId === node.id;
    const isLastEvent = lastEventNodeId === node.id;
    const hasBreakpoint = debugSession.breakpoints.includes(node.id);
    const baseAppearance = node.data.appearance ?? {};
    const borderColor = isFailed
      ? "#ff6b6b"
      : isPending
        ? "#ff9d1c"
        : isLastEvent
          ? "#57c7ff"
          : hasBreakpoint
            ? "#f97316"
            : baseAppearance.borderColor;

    return {
      ...node,
      selected: selectedNodeId === node.id,
      data: {
        ...node.data,
        appearance: {
          ...baseAppearance,
          borderColor,
        },
      },
    };
  });
}

/**
 * 承载宿主调试会话的只读可视化调试页。
 */
export function NodeGraphDebugger({ payload }: NodeGraphDebuggerProps) {
  const i18n = useMemo(() => createI18nRuntime({ locale: "zh-CN" }), []);
  const [debugSession, setDebugSession] = useState<DebugSessionPayload | null>(null);
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const [state, setState] = useState<"loading" | "ready" | "error">("loading");
  const [actionState, setActionState] = useState<"idle" | "running">("idle");
  const typeColors = useMemo(
    () =>
      new Map(
        (payload.typeMappings ?? [])
          .filter((mapping) => Boolean(mapping.color))
          .map((mapping) => [mapping.canonicalId, String(mapping.color)] as const),
      ),
    [payload.typeMappings],
  );

  const loadDebugSession = useCallback(async () => {
    setState("loading");

    try {
      const response = await fetch(`/api/editor/sessions/${payload.session.sessionId}/debug`);
      if (response.status === 404) {
        const createdResponse = await fetch(`/api/editor/sessions/${payload.session.sessionId}/debug`, {
          method: "POST",
          headers: {
            "content-type": "application/json",
          },
          body: JSON.stringify({
            graph: payload.session.graph,
            breakpoints: [],
          }),
        });

        if (!createdResponse.ok) {
          throw new Error("Failed to create the debug session.");
        }

        setDebugSession((await createdResponse.json()) as DebugSessionPayload);
        setState("ready");
        return;
      }

      if (!response.ok) {
        throw new Error("Failed to load the debug session.");
      }

      setDebugSession((await response.json()) as DebugSessionPayload);
      setState("ready");
    } catch {
      setState("error");
    }
  }, [payload.session.graph, payload.session.sessionId]);

  useEffect(() => {
    void loadDebugSession();
  }, [loadDebugSession]);

  useEffect(() => {
    if (!debugSession) {
      return;
    }

    setSelectedNodeId((current) => current ?? debugSession.snapshot.pendingNodeId ?? debugSession.graph.nodes[0]?.id ?? null);
  }, [debugSession]);

  const updateDebugSession = useCallback(
    async (path: string, init: RequestInit) => {
      setActionState("running");

      try {
        const response = await fetch(`/api/editor/sessions/${payload.session.sessionId}/debug${path}`, init);
        if (!response.ok) {
          throw new Error("Debugger action failed.");
        }

        setDebugSession((await response.json()) as DebugSessionPayload);
        setState("ready");
      } catch {
        setState("error");
      } finally {
        setActionState("idle");
      }
    },
    [payload.session.sessionId],
  );

  const toggleBreakpoint = useCallback(
    async (nodeId: string) => {
      if (!debugSession) {
        return;
      }

      const nextBreakpoints = debugSession.breakpoints.includes(nodeId)
        ? debugSession.breakpoints.filter((entry) => entry !== nodeId)
        : [...debugSession.breakpoints, nodeId];

      await updateDebugSession("/breakpoints", {
        method: "PUT",
        headers: {
          "content-type": "application/json",
        },
        body: JSON.stringify({
          breakpoints: nextBreakpoints,
        }),
      });
    },
    [debugSession, updateDebugSession],
  );

  const debugNodes = useMemo(
    () => (debugSession ? buildDebugNodes(debugSession, selectedNodeId) : payload.session.graph.nodes),
    [debugSession, payload.session.graph.nodes, selectedNodeId],
  );
  const debugEdges = debugSession?.graph.edges ?? payload.session.graph.edges;
  const selectedNode = useMemo(
    () => debugSession?.graph.nodes.find((node) => node.id === selectedNodeId) ?? null,
    [debugSession, selectedNodeId],
  );
  const breakpointSet = useMemo(() => new Set(debugSession?.breakpoints ?? []), [debugSession]);
  const debuggerContextValue = useMemo(
    () => ({
      breakpoints: breakpointSet,
      pendingNodeId: debugSession?.snapshot.pendingNodeId ?? null,
      lastEventNodeId: debugSession ? getSnapshotNodeId(debugSession.snapshot) : null,
      failedNodeId:
        debugSession?.snapshot.pauseReason === "error" ? (debugSession.snapshot.pendingNodeId ?? null) : null,
      onToggleBreakpoint: (nodeId: string) => {
        void toggleBreakpoint(nodeId);
      },
    }),
    [breakpointSet, debugSession, toggleBreakpoint],
  );

  return (
    <EditorI18nProvider value={i18n}>
      <TypeColorsProvider value={typeColors}>
        <DebuggerNodeProvider value={debuggerContextValue}>
          <div className="editor-workbench">
            <div className="grid min-h-screen gap-4 p-4 xl:grid-cols-[minmax(0,1fr)_24rem]">
              <div className="flex min-h-[72vh] flex-col gap-4">
                <Card className="editor-panel overflow-hidden">
                  <CardContent className="flex flex-col gap-6 p-6 xl:flex-row xl:items-start xl:justify-between">
                    <div className="space-y-4">
                      <div className="flex flex-wrap gap-2">
                        <Badge className="editor-chip">{payload.session.domain}</Badge>
                        <Badge className="border-white/10 bg-black/30 text-[#d4deef]" variant="outline">
                          {i18n.text("editor.debugger.header.badge")}
                        </Badge>
                      </div>
                      <div className="space-y-3">
                        <p className="editor-kicker">{i18n.text("editor.debugger.header.kicker")}</p>
                        <h1 className="display-font text-4xl font-semibold tracking-[0.08em] text-white uppercase sm:text-5xl">
                          {payload.session.graph.name}
                        </h1>
                        <p className="max-w-3xl text-sm leading-7 text-muted-foreground sm:text-base">
                          {payload.session.graph.description?.trim() || i18n.text("editor.debugger.header.description")}
                        </p>
                      </div>
                    </div>

                    <div className="flex flex-wrap items-center justify-end gap-3">
                      <Button
                        className="h-12 rounded-2xl border border-white/10 bg-white/5 px-6 text-white hover:bg-white/10"
                        data-testid="debug-step-button"
                        onClick={() =>
                          void updateDebugSession("/step", {
                            method: "POST",
                          })
                        }
                        size="lg"
                        variant="outline"
                      >
                        <Pause className="size-4" />
                        {i18n.text("editor.debugger.step")}
                      </Button>
                      <Button
                        className="h-12 rounded-2xl border border-sky-300/20 bg-sky-500/10 px-6 text-sky-100 hover:bg-sky-500/20"
                        data-testid="debug-continue-button"
                        onClick={() =>
                          void updateDebugSession("/continue", {
                            method: "POST",
                          })
                        }
                        size="lg"
                        variant="outline"
                      >
                        <Play className="size-4" />
                        {i18n.text("editor.debugger.continue")}
                      </Button>
                      <Button
                        className="h-12 rounded-2xl border border-white/10 bg-white/5 px-6 text-white hover:bg-white/10"
                        onClick={() =>
                          void updateDebugSession("", {
                            method: "POST",
                            headers: {
                              "content-type": "application/json",
                            },
                            body: JSON.stringify({
                              graph: debugSession?.graph ?? payload.session.graph,
                              breakpoints: debugSession?.breakpoints ?? [],
                            }),
                          })
                        }
                        size="lg"
                        variant="outline"
                      >
                        <RotateCcw className="size-4" />
                        {i18n.text("editor.debugger.restart")}
                      </Button>
                    </div>
                  </CardContent>
                </Card>

                <Card className="editor-panel min-h-[60vh] flex-1 overflow-hidden">
                  <CardContent className="editor-grid graph-stage h-full min-h-[68vh] rounded-[1.5rem] p-0">
                    <div className="graph-stage__hud">
                      <div>
                        <p className="editor-kicker">{i18n.text("editor.debugger.canvas.kicker")}</p>
                        <h2 className="graph-stage__title">{i18n.text("editor.debugger.canvas.title")}</h2>
                        <p className="graph-stage__subtitle">{i18n.text("editor.debugger.canvas.subtitle")}</p>
                      </div>
                      <div className="graph-stage__stats">
                        <span className="graph-stage__badge">
                          <Activity className="size-4" />
                          {debugSession?.snapshot.status ?? i18n.text("editor.debugger.status.loading")}
                        </span>
                        <span className="graph-stage__badge">
                          <Crosshair className="size-4" />
                          {debugSession?.snapshot.pendingNodeId ?? i18n.text("editor.debugger.pending.empty")}
                        </span>
                      </div>
                    </div>

                    <ReactFlow
                      className="graph-flow"
                      colorMode="dark"
                      edges={debugEdges}
                      fitView
                      fitViewOptions={{ padding: 0.18 }}
                      nodeTypes={editorNodeTypes}
                      nodes={debugNodes}
                      nodesConnectable={false}
                      nodesDraggable={false}
                      onNodeClick={(_event, node) => setSelectedNodeId(node.id)}
                      proOptions={{ hideAttribution: true }}
                    >
                      <Background
                        color="rgba(125, 142, 173, 0.08)"
                        gap={24}
                        id="minor-grid"
                        variant={BackgroundVariant.Lines}
                      />
                      <Background
                        color="rgba(255, 157, 28, 0.09)"
                        gap={120}
                        id="major-grid"
                        lineWidth={1.4}
                        variant={BackgroundVariant.Lines}
                      />
                      <Controls className="graph-controls" position="bottom-left" />
                      <MiniMap className="graph-minimap" pannable zoomable />
                    </ReactFlow>
                  </CardContent>
                </Card>
              </div>

              <div className="xl:sticky xl:top-4 xl:h-[calc(100vh-2rem)]">
                <Card className="editor-panel flex h-full min-h-[32rem] flex-col overflow-hidden">
                  <CardHeader className="space-y-3 border-b border-white/6 pb-5">
                    <p className="editor-kicker">{i18n.text("editor.debugger.panel.kicker")}</p>
                    <CardTitle className="display-font flex items-center gap-3 text-3xl tracking-[0.08em] text-white uppercase">
                      <span className="editor-icon-shell">
                        <SquareTerminal className="size-5 text-primary" />
                      </span>
                      {i18n.text("editor.debugger.panel.title")}
                    </CardTitle>
                  </CardHeader>

                  <CardContent className="min-h-0 flex-1 p-5">
                    <ScrollArea className="h-full pr-2">
                      <div className="space-y-4">
                        <div className="rounded-[1.25rem] border border-white/8 bg-black/25 p-4 text-sm text-[#edf3ff]">
                          <p className="text-xs uppercase tracking-[0.24em] text-[#8fa0bc]">
                            {i18n.text("editor.debugger.status.label")}
                          </p>
                          <p className="mt-2">{debugSession?.snapshot.status ?? i18n.text("editor.debugger.status.loading")}</p>
                        </div>

                        <div className="rounded-[1.25rem] border border-white/8 bg-black/25 p-4 text-sm text-[#edf3ff]">
                          <p className="text-xs uppercase tracking-[0.24em] text-[#8fa0bc]">
                            {i18n.text("editor.debugger.pending.label")}
                          </p>
                          <p className="mt-2">{debugSession?.snapshot.pendingNodeId ?? i18n.text("editor.debugger.pending.empty")}</p>
                        </div>

                        <div className="rounded-[1.25rem] border border-white/8 bg-black/25 p-4 text-sm text-[#edf3ff]">
                          <p className="text-xs uppercase tracking-[0.24em] text-[#8fa0bc]">
                            {i18n.text("editor.debugger.breakpoints.label")}
                          </p>
                          <p className="mt-2">
                            {debugSession?.breakpoints.length
                              ? debugSession.breakpoints.join(", ")
                              : i18n.text("editor.debugger.breakpoints.empty")}
                          </p>
                        </div>

                        {selectedNode ? (
                          <div className="rounded-[1.25rem] border border-white/8 bg-black/25 p-4 text-sm text-[#edf3ff]">
                            <p className="text-xs uppercase tracking-[0.24em] text-[#8fa0bc]">
                              {i18n.text("editor.debugger.selection.label")}
                            </p>
                            <p className="mt-2">{selectedNode.id}</p>
                            <p className="mt-2 text-[#8fa0bc]">{selectedNode.data.nodeType}</p>
                          </div>
                        ) : null}

                        <div className="rounded-[1.25rem] border border-white/8 bg-black/25 p-4 text-sm text-[#edf3ff]">
                          <p className="text-xs uppercase tracking-[0.24em] text-[#8fa0bc]">
                            {i18n.text("editor.debugger.results.label")}
                          </p>
                          <pre className="mt-2 overflow-x-auto rounded-[1rem] bg-[#0a1018] p-3 text-xs leading-6 text-[#d6e6ff]">
                            {JSON.stringify(debugSession?.snapshot.results ?? {}, null, 2)}
                          </pre>
                        </div>

                        <div className="rounded-[1.25rem] border border-white/8 bg-black/25 p-4 text-sm text-[#edf3ff]">
                          <p className="text-xs uppercase tracking-[0.24em] text-[#8fa0bc]">
                            {i18n.text("editor.debugger.events.label")}
                          </p>
                          <pre className="mt-2 overflow-x-auto rounded-[1rem] bg-[#0a1018] p-3 text-xs leading-6 text-[#d6e6ff]">
                            {JSON.stringify(debugSession?.snapshot.events ?? [], null, 2)}
                          </pre>
                        </div>

                        {state === "error" ? (
                          <div className="rounded-[1.25rem] border border-rose-400/20 bg-rose-500/12 p-4 text-sm text-rose-100">
                            {i18n.text("editor.debugger.status.error")}
                          </div>
                        ) : null}

                        {actionState === "running" ? (
                          <div className="rounded-[1.25rem] border border-sky-400/20 bg-sky-500/12 p-4 text-sm text-sky-100">
                            {i18n.text("editor.debugger.status.running")}
                          </div>
                        ) : null}
                      </div>
                    </ScrollArea>
                  </CardContent>
                </Card>
              </div>
            </div>
          </div>
        </DebuggerNodeProvider>
      </TypeColorsProvider>
    </EditorI18nProvider>
  );
}
