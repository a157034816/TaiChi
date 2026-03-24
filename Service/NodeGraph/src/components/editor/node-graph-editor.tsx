"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Background,
  BackgroundVariant,
  Controls,
  MiniMap,
  ReactFlow,
} from "@xyflow/react";
import { AlertTriangle, CheckCircle2, Crosshair, Network, RefreshCcw, Save, Waypoints } from "lucide-react";

import { CanvasContextMenu } from "@/components/editor/canvas-context-menu";
import { EditorI18nProvider } from "@/components/editor/editor-i18n-context";
import { NodeInspectorPanel } from "@/components/editor/node-inspector-panel";
import { NodeLibraryPanel } from "@/components/editor/node-library-panel";
import { TypeColorsProvider } from "@/components/editor/type-colors";
import { editorNodeTypes, useNodeGraphCanvas } from "@/components/editor/use-node-graph-canvas";
import {
  DEFAULT_EDITOR_PREFERENCES,
  persistEditorPreferences,
  readEditorPreferences,
} from "@/lib/nodegraph/editor-preferences";
import { createI18nRuntime, getAvailableLocaleCodes, serializeNodeData } from "@/lib/nodegraph/localization";
import type { EditorSessionPayload, NodeGraphDocument } from "@/lib/nodegraph/types";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";

interface NodeGraphEditorProps {
  payload: EditorSessionPayload;
}

/**
 * 承载完整节点编辑工作台，并负责处理节点库刷新后的即时回灌。
 */
export function NodeGraphEditor({ payload }: NodeGraphEditorProps) {
  const [editorPayload, setEditorPayload] = useState(payload);
  const [graphName, setGraphName] = useState(payload.session.graph.name);
  const [graphDescription, setGraphDescription] = useState(payload.session.graph.description ?? "");
  const [searchTerm, setSearchTerm] = useState("");
  const [saveState, setSaveState] = useState<"idle" | "saving" | "saved" | "error">("idle");
  const [refreshState, setRefreshState] = useState<"idle" | "refreshing" | "refreshed" | "error">("idle");
  const [debugState, setDebugState] = useState<"idle" | "starting" | "error">("idle");
  const [preferences, setPreferences] = useState(DEFAULT_EDITOR_PREFERENCES);
  const availableLocales = useMemo(() => getAvailableLocaleCodes(), []);
  const i18n = useMemo(
    () =>
      createI18nRuntime({
        locale: preferences.locale,
      }),
    [preferences.locale],
  );
  const {
    addNode,
    addNodeAtMenuPosition,
    canvasEdges,
    contextMenuItems,
    contextMenuRef,
    contextMenuMeta,
    contextMenuState,
    connectionLineType,
    copyCurrentSelection,
    cutCurrentSelection,
    deleteCurrentSelection,
    defaultEdgeOptions,
    edges,
    focusLabel,
    handleConnect,
    handleConnectEnd,
    handleConnectStart,
    handleDelete,
    handleEdgeClick,
    handleEdgeContextMenu,
    handleNodeClick,
    handleNodeContextMenu,
    handlePaneClick,
    handlePaneContextMenu,
    handleSelectionChange,
    handleSelectionContextMenu,
    nodes,
    onEdgesChange,
    onNodesChange,
    pasteClipboardAtMenuPosition,
    selectedEdge,
    selectedNode,
    selectedTemplate,
    selectionTypeLabel,
    setReactFlowInstance,
    updateSelectedNode,
  } = useNodeGraphCanvas(editorPayload, preferences.edgeStyle, i18n);

  const typeColors = useMemo(
    () =>
      new Map(
        (editorPayload.typeMappings ?? [])
          .filter((mapping) => Boolean(mapping.color))
          .map((mapping) => [mapping.canonicalId, String(mapping.color)] as const),
      ),
    [editorPayload.typeMappings],
  );
  const deleteKeyCode = useMemo(() => ["Delete", "Backspace"], []);
  const fitViewOptions = useMemo(() => ({ padding: 0.18 }), []);
  const invalidNodeCount = useMemo(
    () => nodes.filter((node) => (node.data.templateMarkers?.length ?? 0) > 0).length,
    [nodes],
  );
  const invalidEdgeCount = useMemo(
    () => edges.filter((edge) => Boolean(edge.invalidReason)).length,
    [edges],
  );
  const invalidGraphCount = invalidNodeCount + invalidEdgeCount;
  const handleCanvasEdgeClick = useCallback(
    (_event: unknown, edge: { id: string }) => {
      handleEdgeClick(edge.id);
    },
    [handleEdgeClick],
  );
  const handleCanvasNodeClick = useCallback(
    (_event: unknown, node: { id: string }) => {
      handleNodeClick(node.id);
    },
    [handleNodeClick],
  );
  const miniMapNodeColor = useCallback(
    (node: { data?: { templateMarkers?: unknown[] }; style?: { borderColor?: string } }) =>
      node.data?.templateMarkers?.length ? "#ef4444" : String(node.style?.borderColor ?? "#ff9d1c"),
    [],
  );

  useEffect(() => {
    setEditorPayload(payload);
    setGraphName(payload.session.graph.name);
    setGraphDescription(payload.session.graph.description ?? "");
  }, [payload]);

  useEffect(() => {
    if (typeof window === "undefined") {
      return;
    }

    const frameId = window.requestAnimationFrame(() => {
      const nextPreferences = readEditorPreferences(window.localStorage, availableLocales);

      setPreferences((currentPreferences) =>
        currentPreferences.locale === nextPreferences.locale &&
        currentPreferences.edgeStyle === nextPreferences.edgeStyle
          ? currentPreferences
          : nextPreferences,
      );
    });

    return () => {
      window.cancelAnimationFrame(frameId);
    };
  }, [availableLocales]);

  useEffect(() => {
    if (typeof window === "undefined") {
      return;
    }

    persistEditorPreferences(window.localStorage, preferences);
    document.documentElement.lang = preferences.locale;
  }, [preferences]);

  function buildCurrentGraphSnapshot(): NodeGraphDocument {
    return {
      ...editorPayload.session.graph,
      name: graphName,
      description: graphDescription,
      nodes: nodes.map((node) => ({
        ...node,
        data: serializeNodeData(node.data, i18n),
      })),
      edges,
    };
  }

  async function saveGraph() {
    setSaveState("saving");
    const nextGraph = buildCurrentGraphSnapshot();

    try {
      const response = await fetch(`/api/editor/sessions/${editorPayload.session.sessionId}/complete`, {
        method: "POST",
        headers: {
          "content-type": "application/json",
        },
        body: JSON.stringify({
          graph: nextGraph,
        }),
      });

      if (!response.ok) {
        setSaveState("error");
        return;
      }

      setEditorPayload((currentPayload) => ({
        ...currentPayload,
        session: {
          ...currentPayload.session,
          graph: nextGraph,
          updatedAt: new Date().toISOString(),
        },
      }));
      setSaveState("saved");
    } catch {
      setSaveState("error");
    }
  }

  async function refreshLibrary() {
    setRefreshState("refreshing");

    try {
      const response = await fetch(`/api/editor/sessions/${editorPayload.session.sessionId}/library/refresh`, {
        method: "POST",
        headers: {
          "content-type": "application/json",
        },
        body: JSON.stringify({
          graph: buildCurrentGraphSnapshot(),
        }),
      });

      if (!response.ok) {
        setRefreshState("error");
        return;
      }

      const refreshed = (await response.json()) as {
        runtime: EditorSessionPayload["runtime"];
        nodeLibrary: EditorSessionPayload["nodeLibrary"];
        typeMappings?: EditorSessionPayload["typeMappings"];
        migratedGraph?: NodeGraphDocument;
      };
      const nextGraph = refreshed.migratedGraph ?? buildCurrentGraphSnapshot();

      setEditorPayload((currentPayload) => ({
        ...currentPayload,
        runtime: refreshed.runtime,
        nodeLibrary: refreshed.nodeLibrary,
        typeMappings: refreshed.typeMappings ?? currentPayload.typeMappings,
        session: {
          ...currentPayload.session,
          graph: nextGraph,
          updatedAt: new Date().toISOString(),
        },
      }));
      setGraphName(nextGraph.name);
      setGraphDescription(nextGraph.description ?? "");
      setRefreshState("refreshed");
    } catch {
      setRefreshState("error");
    }
  }

  async function openDebugger() {
    setDebugState("starting");

    try {
      const response = await fetch(`/api/editor/sessions/${editorPayload.session.sessionId}/debug`, {
        method: "POST",
        headers: {
          "content-type": "application/json",
        },
        body: JSON.stringify({
          graph: buildCurrentGraphSnapshot(),
          breakpoints: [],
        }),
      });

      if (!response.ok) {
        setDebugState("error");
        return;
      }

      window.open(`/editor/${editorPayload.session.sessionId}/debug`, "_blank", "noopener,noreferrer");
      setDebugState("idle");
    } catch {
      setDebugState("error");
    }
  }

  return (
    <EditorI18nProvider value={i18n}>
      <TypeColorsProvider value={typeColors}>
        <div className="editor-workbench">
          <div className="grid min-h-screen gap-4 p-4 xl:grid-cols-[20rem_minmax(0,1fr)_22rem]">
            <div className="xl:sticky xl:top-4 xl:h-[calc(100vh-2rem)]">
              <NodeLibraryPanel
                items={editorPayload.nodeLibrary}
                onAddNode={addNode}
                onSearchTermChange={setSearchTerm}
                searchTerm={searchTerm}
              />
            </div>

            <div className="flex min-h-[72vh] flex-col gap-4">
              <Card className="editor-panel overflow-hidden">
                <CardContent className="flex flex-col gap-6 p-6 xl:flex-row xl:items-start xl:justify-between">
                  <div className="space-y-4">
                    <div className="flex flex-wrap gap-2">
                      <Badge className="editor-chip">{editorPayload.session.domain}</Badge>
                      <Badge className="border-white/10 bg-black/30 text-[#d4deef]" variant="outline">
                        {i18n.text(`editor.access.${editorPayload.session.accessType}`)} {i18n.text("editor.header.accessUrlSuffix")}
                      </Badge>
                      <Badge className="border-white/10 bg-black/30 text-[#d4deef]" variant="outline">
                        {i18n.text("editor.header.libraryNodes", { count: editorPayload.nodeLibrary.length })}
                      </Badge>
                      <Badge
                        className="border-white/10 bg-black/30 text-[#d4deef]"
                        data-testid="runtime-library-version"
                        variant="outline"
                      >
                        {i18n.text("editor.runtime.libraryVersion", { version: editorPayload.runtime.libraryVersion })}
                      </Badge>
                    </div>

                    <div className="space-y-3">
                      <p className="editor-kicker">{i18n.text("editor.header.activeGraphKicker")}</p>
                      <h1 className="display-font text-4xl font-semibold tracking-[0.08em] text-white uppercase sm:text-5xl">
                        {graphName}
                      </h1>
                      <p className="max-w-3xl text-sm leading-7 text-muted-foreground sm:text-base">
                        {graphDescription?.trim() || i18n.text("editor.header.fallbackDescription")}
                      </p>
                      <p className="text-xs uppercase tracking-[0.3em] text-[#92a3bc]">
                        {i18n.text("editor.header.sessionLabel", { sessionId: editorPayload.session.sessionId })}
                      </p>
                    </div>
                  </div>

                  <div className="flex flex-col gap-4 xl:min-w-[20rem] xl:items-end">
                    <div className="grid gap-3 sm:grid-cols-3 xl:w-full">
                      <div className="graph-stage__stat">
                        <span className="graph-stage__stat-label">{i18n.text("editor.stats.nodes")}</span>
                        <span className="graph-stage__stat-value">{nodes.length}</span>
                      </div>
                      <div className="graph-stage__stat">
                        <span className="graph-stage__stat-label">{i18n.text("editor.stats.links")}</span>
                        <span className="graph-stage__stat-value">{edges.length}</span>
                      </div>
                      <div className="graph-stage__stat">
                        <span className="graph-stage__stat-label">{i18n.text("editor.stats.focus")}</span>
                        <span className="graph-stage__stat-value truncate">{focusLabel}</span>
                      </div>
                    </div>

                    {invalidGraphCount ? (
                      <div
                        className="inline-flex items-center gap-2 rounded-2xl border border-rose-400/20 bg-rose-500/10 px-4 py-3 text-sm text-rose-100"
                        data-testid="invalid-graph-alert"
                        role="alert"
                      >
                        <AlertTriangle className="size-4" />
                        {i18n.text("editor.runtime.invalidGraphSummary", { count: invalidGraphCount })}
                      </div>
                    ) : null}

                    <div className="flex flex-wrap items-center justify-end gap-3">
                      {refreshState === "refreshed" ? (
                        <span className="inline-flex items-center gap-2 rounded-full border border-sky-400/20 bg-sky-500/10 px-4 py-2 text-sm text-sky-100">
                          <CheckCircle2 className="size-4" />
                          {i18n.text("editor.runtime.libraryRefreshed")}
                        </span>
                      ) : null}

                      {refreshState === "error" ? (
                        <span className="rounded-full border border-rose-400/20 bg-rose-500/12 px-4 py-2 text-sm text-rose-200">
                          {i18n.text("editor.runtime.libraryRefreshFailed")}
                        </span>
                      ) : null}

                      {debugState === "error" ? (
                        <span className="rounded-full border border-rose-400/20 bg-rose-500/12 px-4 py-2 text-sm text-rose-200">
                          {i18n.text("editor.runtime.debugStartFailed")}
                        </span>
                      ) : null}

                      {saveState === "saved" ? (
                        <span
                          className="inline-flex items-center gap-2 rounded-full border border-emerald-400/20 bg-emerald-500/10 px-4 py-2 text-sm text-emerald-200"
                          role="status"
                        >
                          <CheckCircle2 className="size-4" />
                          {i18n.text("editor.save.delivered")}
                        </span>
                      ) : null}

                      {saveState === "error" ? (
                        <span
                          className="rounded-full border border-rose-400/20 bg-rose-500/12 px-4 py-2 text-sm text-rose-200"
                          role="alert"
                        >
                          {i18n.text("editor.save.failed")}
                        </span>
                      ) : null}

                      <Button
                        className="h-12 rounded-2xl border border-white/10 bg-white/5 px-6 text-white hover:bg-white/10"
                        data-testid="refresh-library-button"
                        onClick={refreshLibrary}
                        size="lg"
                        variant="outline"
                      >
                        <RefreshCcw className="size-4" />
                        {refreshState === "refreshing"
                          ? i18n.text("editor.runtime.refreshingLibrary")
                          : i18n.text("editor.runtime.refreshLibrary")}
                      </Button>

                      {editorPayload.runtime.capabilities.canDebug ? (
                        <Button
                          className="h-12 rounded-2xl border border-sky-300/20 bg-sky-500/10 px-6 text-sky-100 hover:bg-sky-500/20"
                          data-testid="open-debugger-button"
                          onClick={openDebugger}
                          size="lg"
                          variant="outline"
                        >
                          <Crosshair className="size-4" />
                          {debugState === "starting"
                            ? i18n.text("editor.runtime.startingDebugger")
                            : i18n.text("editor.runtime.openDebugger")}
                        </Button>
                      ) : null}

                      <Button
                        className="h-12 rounded-2xl border border-amber-300/10 bg-[linear-gradient(135deg,#ff9d1c,#ffb44c)] px-6 text-[#1d1305] shadow-[0_18px_38px_rgba(255,157,28,0.2)] hover:bg-[linear-gradient(135deg,#ffad38,#ffc261)]"
                        onClick={saveGraph}
                        size="lg"
                      >
                        <Save className="size-4" />
                        {saveState === "saving"
                          ? i18n.text("editor.save.submitting")
                          : i18n.text("editor.save.completeEditing")}
                      </Button>
                    </div>
                  </div>
                </CardContent>
              </Card>

              <Card className="editor-panel min-h-[60vh] flex-1 overflow-hidden">
                <CardContent className="editor-grid graph-stage h-full min-h-[68vh] rounded-[1.5rem] p-0">
                  <div className="graph-stage__hud">
                    <div>
                      <p className="editor-kicker">{i18n.text("editor.canvas.kicker")}</p>
                      <h2 className="graph-stage__title">{i18n.text("editor.canvas.title")}</h2>
                      <p className="graph-stage__subtitle">{i18n.text("editor.canvas.subtitle")}</p>
                    </div>
                    <div className="graph-stage__stats">
                      <span className="graph-stage__badge">
                        <Waypoints className="size-4" />
                        {i18n.text("editor.stats.activeLinks", { count: edges.length })}
                      </span>
                      <span className="graph-stage__badge">
                        <Crosshair className="size-4" />
                        {selectionTypeLabel}
                      </span>
                    </div>
                  </div>

                  <ReactFlow
                    className="graph-flow"
                    colorMode="dark"
                    connectionLineStyle={defaultEdgeOptions.style}
                    connectionLineType={connectionLineType}
                    defaultEdgeOptions={defaultEdgeOptions}
                    deleteKeyCode={deleteKeyCode}
                    edges={canvasEdges}
                    elevateEdgesOnSelect
                    fitView
                    fitViewOptions={fitViewOptions}
                    nodeTypes={editorNodeTypes}
                    nodes={nodes}
                    onConnect={handleConnect}
                    onConnectEnd={handleConnectEnd}
                    onConnectStart={handleConnectStart}
                    onDelete={handleDelete}
                    onEdgeClick={handleCanvasEdgeClick}
                    onEdgeContextMenu={handleEdgeContextMenu}
                    onEdgesChange={onEdgesChange}
                    onInit={setReactFlowInstance}
                    onNodeClick={handleCanvasNodeClick}
                    onNodeContextMenu={handleNodeContextMenu}
                    onNodesChange={onNodesChange}
                    onPaneClick={handlePaneClick}
                    onPaneContextMenu={handlePaneContextMenu}
                    onSelectionChange={handleSelectionChange}
                    onSelectionContextMenu={handleSelectionContextMenu}
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
                    <MiniMap
                      className="graph-minimap"
                      pannable
                      zoomable
                      nodeColor={miniMapNodeColor}
                    />
                  </ReactFlow>

                  {contextMenuState && contextMenuMeta ? (
                    <CanvasContextMenu
                      ref={contextMenuRef}
                      canCopy={contextMenuMeta.canCopy}
                      canCut={contextMenuMeta.canCut}
                      canDelete={contextMenuMeta.canDelete}
                      canPaste={contextMenuMeta.canPaste}
                      copyLabel={contextMenuMeta.copyLabel}
                      cutLabel={contextMenuMeta.cutLabel}
                      deleteLabel={contextMenuMeta.deleteLabel}
                      emptyStateMessage={contextMenuMeta.emptyStateMessage}
                      isConnectionCreation={contextMenuMeta.mode === "connection"}
                      items={contextMenuItems}
                      libraryLabel={contextMenuMeta.libraryLabel}
                      onAddNode={addNodeAtMenuPosition}
                      onCopy={copyCurrentSelection}
                      onCut={cutCurrentSelection}
                      onDelete={deleteCurrentSelection}
                      onPaste={pasteClipboardAtMenuPosition}
                      position={contextMenuState.position}
                      showLibrary={contextMenuState.showLibrary}
                    />
                  ) : null}
                </CardContent>
              </Card>

              <Card className="editor-panel">
                <CardContent className="flex flex-wrap items-center gap-3 p-5 text-sm leading-7 text-muted-foreground">
                  <Network className="size-4 text-primary" />
                  {i18n.text("editor.footer.summary")}
                </CardContent>
              </Card>
            </div>

            <div className="xl:sticky xl:top-4 xl:h-[calc(100vh-2rem)]">
              <NodeInspectorPanel
                edge={selectedEdge}
                edgeStyle={preferences.edgeStyle}
                graphDescription={graphDescription}
                graphName={graphName}
                locale={preferences.locale}
                node={selectedNode}
                nodes={nodes}
                onEdgeStyleChange={(nextEdgeStyle) =>
                  setPreferences((current) => ({
                    ...current,
                    edgeStyle: nextEdgeStyle,
                  }))
                }
                onGraphDescriptionChange={setGraphDescription}
                onGraphNameChange={setGraphName}
                onLocaleChange={(nextLocale) =>
                  setPreferences((current) => ({
                    ...current,
                    locale: nextLocale,
                  }))
                }
                onNodeFieldChange={(field, value) =>
                  updateSelectedNode((node) => ({
                    ...node,
                    data: {
                      ...node.data,
                      [field]: value,
                      ...(field === "label"
                        ? {
                            label: value,
                            labelOverride: value,
                          }
                        : {
                            description: value,
                            descriptionOverride: value,
                          }),
                    },
                  }))
                }
                onNodeValueChange={(key, value) =>
                  updateSelectedNode((node) => ({
                    ...node,
                    data: {
                      ...node.data,
                      values: {
                        ...(node.data.values ?? {}),
                        [key]: value,
                      },
                    },
                  }))
                }
                sessionId={editorPayload.session.sessionId}
                template={selectedTemplate}
              />
            </div>
          </div>
        </div>
      </TypeColorsProvider>
    </EditorI18nProvider>
  );
}
