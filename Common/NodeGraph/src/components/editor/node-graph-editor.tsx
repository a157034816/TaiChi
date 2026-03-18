"use client";

import { useCallback, useState } from "react";
import {
  addEdge,
  Background,
  BackgroundVariant,
  type Connection,
  ConnectionLineType,
  Controls,
  MarkerType,
  MiniMap,
  ReactFlow,
  useEdgesState,
  useNodesState,
} from "@xyflow/react";
import { CheckCircle2, Crosshair, Network, Save, Waypoints } from "lucide-react";

import { BlueprintNode } from "@/components/editor/blueprint-node";
import { NodeInspectorPanel } from "@/components/editor/node-inspector-panel";
import { NodeLibraryPanel } from "@/components/editor/node-library-panel";
import { removeConflictingInputEdges } from "@/lib/nodegraph/connections";
import { buildNodeStyle, createNodeFromLibrary, normalizeNodeDataPorts } from "@/lib/nodegraph/factories";
import {
  getCanvasFocusLabel,
  getCanvasTypeLabel,
  resolveCanvasSelection,
  type CanvasSelection,
} from "@/lib/nodegraph/selection";
import type { EditorSessionPayload, NodeGraphEdge, NodeGraphNode, NodeLibraryItem } from "@/lib/nodegraph/types";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";

interface NodeGraphEditorProps {
  payload: EditorSessionPayload;
}

const editorNodeTypes = {
  default: BlueprintNode,
};

const EDGE_STROKE = "rgba(159, 179, 217, 0.94)";

const defaultEdgeOptions = {
  type: "smoothstep",
  animated: false,
  markerEnd: {
    type: MarkerType.ArrowClosed,
    color: "#9fb3d9",
    width: 18,
    height: 18,
  },
  style: {
    stroke: EDGE_STROKE,
    strokeWidth: 3,
  },
} as const;

function getNextNodePosition(currentCount: number) {
  return {
    x: 120 + (currentCount % 3) * 360,
    y: 140 + Math.floor(currentCount / 3) * 230,
  };
}

function getNodeTemplate(nodeLibrary: NodeLibraryItem[], nodeType: string) {
  return nodeLibrary.find((item) => item.type === nodeType);
}

function prepareCanvasNode(node: NodeGraphNode, nodeLibrary: NodeLibraryItem[]): NodeGraphNode {
  const template = getNodeTemplate(nodeLibrary, node.data.nodeType);

  return {
    ...node,
    type: "default",
    data: normalizeNodeDataPorts(node.data, template),
    style: {
      ...(node.style ?? {}),
      ...buildNodeStyle(node.data.appearance),
    },
  };
}

function prepareCanvasEdge(edge: NodeGraphEdge): NodeGraphEdge {
  return {
    ...edge,
    type: "smoothstep",
    animated: false,
    markerEnd: edge.markerEnd ?? defaultEdgeOptions.markerEnd,
    style: {
      stroke: EDGE_STROKE,
      strokeWidth: 3,
      ...(edge.style ?? {}),
    },
  };
}

function createCanvasEdge(connection: Connection): NodeGraphEdge {
  return prepareCanvasEdge({
    ...connection,
    id: `edge_${crypto.randomUUID()}`,
  });
}

export function NodeGraphEditor({ payload }: NodeGraphEditorProps) {
  const [graphName, setGraphName] = useState(payload.session.graph.name);
  const [graphDescription, setGraphDescription] = useState(payload.session.graph.description ?? "");
  const [searchTerm, setSearchTerm] = useState("");
  const [saveState, setSaveState] = useState<"idle" | "saving" | "saved" | "error">("idle");
  const [nodes, setNodes, onNodesChange] = useNodesState<NodeGraphNode>(
    payload.session.graph.nodes.map((node) => prepareCanvasNode(node, payload.nodeLibrary)),
  );
  const [edges, setEdges, onEdgesChange] = useEdgesState<NodeGraphEdge>(
    payload.session.graph.edges.map(prepareCanvasEdge),
  );
  const [selectedItem, setSelectedItem] = useState<CanvasSelection>(null);

  const selectedNode =
    selectedItem?.type === "node"
      ? (nodes.find((node) => node.id === selectedItem.id) as NodeGraphNode | undefined) ?? null
      : null;
  const selectedEdge =
    selectedItem?.type === "edge"
      ? (edges.find((edge) => edge.id === selectedItem.id) as NodeGraphEdge | undefined) ?? null
      : null;
  const selectedTemplate = selectedNode ? getNodeTemplate(payload.nodeLibrary, selectedNode.data.nodeType) ?? null : null;
  const focusLabel = getCanvasFocusLabel(selectedItem, nodes, edges);
  const selectionTypeLabel = getCanvasTypeLabel(selectedItem, nodes, edges);
  const handleSelectionChange = useCallback(
    ({ nodes: selectedNodes, edges: selectedEdges }: { nodes: NodeGraphNode[]; edges: NodeGraphEdge[] }) => {
      setSelectedItem(
        resolveCanvasSelection({
          nodes: selectedNodes,
          edges: selectedEdges,
        }),
      );
    },
    [],
  );

  function addNode(item: EditorSessionPayload["nodeLibrary"][number]) {
    setNodes((currentNodes) => [
      ...currentNodes,
      createNodeFromLibrary(item, getNextNodePosition(currentNodes.length)),
    ]);
  }

  function updateSelectedNode(mutator: (node: NodeGraphNode) => NodeGraphNode) {
    if (!selectedNode) {
      return;
    }

    setNodes((currentNodes) =>
      currentNodes.map((node) => (node.id === selectedNode.id ? mutator(node as NodeGraphNode) : node)),
    );
  }

  async function saveGraph() {
    setSaveState("saving");

    try {
      const response = await fetch(`/api/editor/sessions/${payload.session.sessionId}/complete`, {
        method: "POST",
        headers: {
          "content-type": "application/json",
        },
        body: JSON.stringify({
          graph: {
            ...payload.session.graph,
            name: graphName,
            description: graphDescription,
            nodes,
            edges,
          },
        }),
      });

      setSaveState(response.ok ? "saved" : "error");
    } catch {
      setSaveState("error");
    }
  }

  return (
    <div className="editor-workbench">
      <div className="grid min-h-screen gap-4 p-4 xl:grid-cols-[20rem_minmax(0,1fr)_22rem]">
        <div className="xl:sticky xl:top-4 xl:h-[calc(100vh-2rem)]">
          <NodeLibraryPanel
            items={payload.nodeLibrary}
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
                  <Badge className="editor-chip">{payload.session.domain}</Badge>
                  <Badge className="border-white/10 bg-black/30 text-[#d4deef]" variant="outline">
                    {payload.session.accessType} URL
                  </Badge>
                  <Badge className="border-white/10 bg-black/30 text-[#d4deef]" variant="outline">
                    {payload.nodeLibrary.length} library nodes
                  </Badge>
                </div>

                <div className="space-y-3">
                  <p className="editor-kicker">Active graph</p>
                  <h1 className="display-font text-4xl font-semibold tracking-[0.08em] text-white uppercase sm:text-5xl">
                    {graphName}
                  </h1>
                  <p className="max-w-3xl text-sm leading-7 text-muted-foreground sm:text-base">
                    {graphDescription?.trim() ||
                      "Build and review the session graph here, then push the final document back through the completion webhook."}
                  </p>
                  <p className="text-xs uppercase tracking-[0.3em] text-[#92a3bc]">
                    Session {payload.session.sessionId}
                  </p>
                </div>
              </div>

              <div className="flex flex-col gap-4 xl:min-w-[20rem] xl:items-end">
                <div className="grid gap-3 sm:grid-cols-3 xl:w-full">
                  <div className="graph-stage__stat">
                    <span className="graph-stage__stat-label">Nodes</span>
                    <span className="graph-stage__stat-value">{nodes.length}</span>
                  </div>
                  <div className="graph-stage__stat">
                    <span className="graph-stage__stat-label">Links</span>
                    <span className="graph-stage__stat-value">{edges.length}</span>
                  </div>
                  <div className="graph-stage__stat">
                    <span className="graph-stage__stat-label">Focus</span>
                    <span className="graph-stage__stat-value truncate">
                      {focusLabel}
                    </span>
                  </div>
                </div>

                <div className="flex flex-wrap items-center justify-end gap-3">
                  {saveState === "saved" ? (
                    <span
                      className="inline-flex items-center gap-2 rounded-full border border-emerald-400/20 bg-emerald-500/10 px-4 py-2 text-sm text-emerald-200"
                      role="status"
                    >
                      <CheckCircle2 className="size-4" />
                      Webhook delivered
                    </span>
                  ) : null}

                  {saveState === "error" ? (
                    <span
                      className="rounded-full border border-rose-400/20 bg-rose-500/12 px-4 py-2 text-sm text-rose-200"
                      role="alert"
                    >
                      The completion webhook failed. Adjust the data and try again.
                    </span>
                  ) : null}

                  <Button
                    className="h-12 rounded-2xl border border-amber-300/10 bg-[linear-gradient(135deg,#ff9d1c,#ffb44c)] px-6 text-[#1d1305] shadow-[0_18px_38px_rgba(255,157,28,0.2)] hover:bg-[linear-gradient(135deg,#ffad38,#ffc261)]"
                    onClick={saveGraph}
                    size="lg"
                  >
                    <Save className="size-4" />
                    {saveState === "saving" ? "Submitting..." : "Complete editing"}
                  </Button>
                </div>
              </div>
            </CardContent>
          </Card>

          <Card className="editor-panel min-h-[60vh] flex-1 overflow-hidden">
            <CardContent className="editor-grid graph-stage h-full min-h-[68vh] rounded-[1.5rem] p-0">
              <div className="graph-stage__hud">
                <div>
                  <p className="editor-kicker">Blueprint workspace</p>
                  <h2 className="graph-stage__title">Node canvas</h2>
                  <p className="graph-stage__subtitle">
                    Drag to position nodes, connect outputs into inputs, then edit graph and node data from the inspector dock.
                  </p>
                </div>
                <div className="graph-stage__stats">
                  <span className="graph-stage__badge">
                    <Waypoints className="size-4" />
                    {edges.length} active links
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
                connectionLineType={ConnectionLineType.SmoothStep}
                defaultEdgeOptions={defaultEdgeOptions}
                deleteKeyCode={["Delete", "Backspace"]}
                edges={edges}
                elevateEdgesOnSelect
                fitView
                fitViewOptions={{ padding: 0.18 }}
                nodeTypes={editorNodeTypes}
                nodes={nodes}
                onConnect={(connection: Connection) =>
                  setEdges((currentEdges) => {
                    return addEdge(
                      createCanvasEdge(connection),
                      removeConflictingInputEdges(currentEdges, connection),
                    );
                  })
                }
                onEdgeClick={(_, edge) => setSelectedItem({ type: "edge", id: edge.id })}
                onEdgesChange={onEdgesChange}
                onNodeClick={(_, node) => setSelectedItem({ type: "node", id: node.id })}
                onNodesChange={onNodesChange}
                onPaneClick={() => setSelectedItem(null)}
                onSelectionChange={handleSelectionChange}
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
                  nodeColor={(node) =>
                    String((node.style as { borderColor?: string } | undefined)?.borderColor ?? "#ff9d1c")
                  }
                />
              </ReactFlow>
            </CardContent>
          </Card>

          <Card className="editor-panel">
            <CardContent className="flex flex-wrap items-center gap-3 p-5 text-sm leading-7 text-muted-foreground">
              <Network className="size-4 text-primary" />
              This refit keeps the existing session workflow intact: add nodes from the library,
              wire them on the canvas, inspect field values on the right, then submit the finished graph.
            </CardContent>
          </Card>
        </div>

        <div className="xl:sticky xl:top-4 xl:h-[calc(100vh-2rem)]">
          <NodeInspectorPanel
            edge={selectedEdge}
            graphDescription={graphDescription}
            graphName={graphName}
            node={selectedNode}
            nodes={nodes}
            onGraphDescriptionChange={setGraphDescription}
            onGraphNameChange={setGraphName}
            onNodeFieldChange={(field, value) =>
              updateSelectedNode((node) => ({
                ...node,
                data: {
                  ...node.data,
                  [field]: value,
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
            template={selectedTemplate}
          />
        </div>
      </div>
    </div>
  );
}
