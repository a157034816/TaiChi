"use client";

import { useEffect, useLayoutEffect, useMemo, useRef, useState, type MouseEvent as ReactMouseEvent } from "react";
import {
  addEdge,
  type Connection,
  type Edge,
  type FinalConnectionState,
  type OnConnectStartParams,
  MarkerType,
  type ReactFlowInstance,
  useEdgesState,
  useNodesState,
} from "@xyflow/react";

import { BlueprintNode } from "@/components/editor/blueprint-node";
import {
  consumePaneClickSuppression,
  shouldOpenConnectionCreationMenu,
} from "@/components/editor/connection-menu-interactions";
import { resolveContextMenuPosition } from "@/components/editor/context-menu-position";
import {
  buildConnectionForInsertedNode,
  getCompatibleNodeLibraryItems,
  getPortForHandle,
  type PendingConnectionDraft,
  removeConflictingInputEdges,
} from "@/lib/nodegraph/connections";
import { buildClipboardFromSelection, pasteClipboardAtPosition, type NodeClipboardPayload } from "@/lib/nodegraph/clipboard";
import { buildNodeStyle, createNodeFromLibrary, normalizeNodeDataPorts } from "@/lib/nodegraph/factories";
import {
  createCanvasSelectionSnapshot,
  createEmptyCanvasSelectionSnapshot,
  getCanvasFocusLabel,
  getCanvasTypeLabel,
  resolveCanvasSelection,
  resolveCanvasSelectionFromSnapshot,
  type CanvasSelection,
  type CanvasSelectionSnapshot,
} from "@/lib/nodegraph/selection";
import type { EditorEdgeStyle } from "@/lib/nodegraph/editor-preferences";
import { getConnectionLineType, getReactFlowEdgeType } from "@/lib/nodegraph/editor-preferences";
import type { EditorMessages } from "@/lib/nodegraph/localization";
import type {
  EditorSessionPayload,
  NodeGraphEdge,
  NodeGraphNode,
  NodeLibraryItem,
  SupportedLocale,
} from "@/lib/nodegraph/types";

interface CanvasContextMenuState {
  anchorPosition: NodeGraphNode["position"];
  position: NodeGraphNode["position"];
  flowPosition: NodeGraphNode["position"];
  selection: CanvasSelectionSnapshot;
  mode: "default" | "connection";
  pendingConnection?: PendingConnectionDraft;
  showLibrary: boolean;
  targetEdgeId?: string;
}

const EDGE_STROKE = "rgba(159, 179, 217, 0.94)";
const CONTEXT_MENU_MARGIN = 16;
const DEFAULT_TYPE_COLOR = "#64748B";

export const editorNodeTypes = {
  default: BlueprintNode,
};

function createDefaultEdgeOptions(edgeStyle: EditorEdgeStyle): Pick<Edge, "type" | "animated" | "markerEnd" | "style"> {
  return {
    type: getReactFlowEdgeType(edgeStyle),
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
  };
}

function resolveTypeColorForConnection(
  connection: {
    source: string;
    target: string;
    sourceHandle?: string | null;
    targetHandle?: string | null;
  },
  nodes: NodeGraphNode[],
  typeColors: Map<string, string>,
) {
  const sourceNode = nodes.find((node) => node.id === connection.source) ?? null;
  const sourcePort = sourceNode ? getPortForHandle(sourceNode.data, "source", connection.sourceHandle ?? null) : null;

  if (sourcePort?.dataType) {
    return typeColors.get(sourcePort.dataType) ?? DEFAULT_TYPE_COLOR;
  }

  const targetNode = nodes.find((node) => node.id === connection.target) ?? null;
  const targetPort = targetNode ? getPortForHandle(targetNode.data, "target", connection.targetHandle ?? null) : null;

  if (targetPort?.dataType) {
    return typeColors.get(targetPort.dataType) ?? DEFAULT_TYPE_COLOR;
  }

  return null;
}

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

function prepareStoredEdge(edge: NodeGraphEdge, nodes: NodeGraphNode[], typeColors: Map<string, string>): NodeGraphEdge {
  const typeStroke = resolveTypeColorForConnection(edge, nodes, typeColors);
  const baseMarkerEnd = (
    edge.markerEnd && typeof edge.markerEnd === "object"
      ? edge.markerEnd
      : createDefaultEdgeOptions("smoothstep").markerEnd
  ) as Exclude<NodeGraphEdge["markerEnd"], string | undefined>;

  return {
    ...edge,
    animated: edge.animated ?? false,
    markerEnd: {
      ...baseMarkerEnd,
      color: typeStroke ?? EDGE_STROKE,
    },
    style: {
      stroke: typeStroke ?? EDGE_STROKE,
      strokeWidth: 3,
      ...(edge.style ?? {}),
    },
  };
}

function createCanvasEdge(
  edge: NodeGraphEdge,
  nodes: NodeGraphNode[],
  typeColors: Map<string, string>,
  edgeStyle: EditorEdgeStyle,
): NodeGraphEdge {
  return {
    ...prepareStoredEdge(edge, nodes, typeColors),
    type: getReactFlowEdgeType(edgeStyle),
  };
}

function createStoredEdge(connection: Connection, nodes: NodeGraphNode[], typeColors: Map<string, string>): NodeGraphEdge {
  return prepareStoredEdge({
    ...connection,
    id: `edge_${crypto.randomUUID()}`,
  }, nodes, typeColors);
}

function createNodeSelectionSnapshot(nodeId: string): CanvasSelectionSnapshot {
  return {
    nodeIds: [nodeId],
    edgeIds: [],
  };
}

function createEdgeSelectionSnapshot(edgeId: string): CanvasSelectionSnapshot {
  return {
    nodeIds: [],
    edgeIds: [edgeId],
  };
}

function areSelectionSnapshotsEqual(
  left: CanvasSelectionSnapshot,
  right: CanvasSelectionSnapshot,
) {
  if (left.nodeIds.length !== right.nodeIds.length || left.edgeIds.length !== right.edgeIds.length) {
    return false;
  }

  return left.nodeIds.every((nodeId, index) => nodeId === right.nodeIds[index]) &&
    left.edgeIds.every((edgeId, index) => edgeId === right.edgeIds[index]);
}

function areSelectionsEqual(left: CanvasSelection, right: CanvasSelection) {
  if (left === right) {
    return true;
  }

  if (!left || !right) {
    return false;
  }

  return left.type === right.type && left.id === right.id;
}

function getCopyLabel(selection: CanvasSelectionSnapshot, messages: EditorMessages) {
  if (selection.nodeIds.length > 1) {
    return messages.contextMenu.copySelectedNodes;
  }

  return messages.contextMenu.copyNode;
}

function getCutLabel(selection: CanvasSelectionSnapshot, messages: EditorMessages) {
  if (selection.nodeIds.length > 1) {
    return messages.contextMenu.cutSelectedNodes;
  }

  return messages.contextMenu.cutNode;
}

function getDeleteLabel(selection: CanvasSelectionSnapshot, messages: EditorMessages, targetEdgeId?: string) {
  if (targetEdgeId && !selection.nodeIds.length) {
    return messages.contextMenu.deleteEdge;
  }

  if (selection.nodeIds.length > 1) {
    return messages.contextMenu.deleteSelectedNodes;
  }

  if (selection.nodeIds.length === 1) {
    return messages.contextMenu.deleteNode;
  }

  if (selection.edgeIds.length > 1) {
    return messages.contextMenu.deleteSelectedEdges;
  }

  return messages.contextMenu.deleteSelection;
}

function getClientPosition(event: MouseEvent | TouchEvent | ReactMouseEvent) {
  if ("touches" in event) {
    const touch = event.touches[0] ?? event.changedTouches[0];

    if (!touch) {
      return null;
    }

    return {
      x: touch.clientX,
      y: touch.clientY,
    };
  }

  return {
    x: event.clientX,
    y: event.clientY,
  };
}

function isBlankCanvasDropTarget(target: EventTarget | null) {
  if (!(target instanceof Element)) {
    return false;
  }

  if (target.closest("[data-canvas-context-menu='true']")) {
    return false;
  }

  if (target.closest(".react-flow__node, .react-flow__edge, .react-flow__handle")) {
    return false;
  }

  return Boolean(target.closest(".react-flow"));
}

export function useNodeGraphCanvas(
  payload: EditorSessionPayload,
  edgeStyle: EditorEdgeStyle,
  locale: SupportedLocale,
  messages: EditorMessages,
) {
  const reactFlowRef = useRef<ReactFlowInstance<NodeGraphNode, NodeGraphEdge> | null>(null);
  const contextMenuRef = useRef<HTMLDivElement | null>(null);
  const suppressNextPaneClickRef = useRef(false);
  const pendingConnectionRef = useRef<PendingConnectionDraft | null>(null);
  const [clipboard, setClipboard] = useState<NodeClipboardPayload | null>(null);
  const [pasteCount, setPasteCount] = useState(0);
  const typeColors = useMemo(
    () =>
      new Map(
        (payload.typeMappings ?? []).map((mapping) => [mapping.canonicalId, mapping.color ?? DEFAULT_TYPE_COLOR] as const),
      ),
    [payload.typeMappings],
  );
  const initialNodes = useMemo(
    () => payload.session.graph.nodes.map((node) => prepareCanvasNode(node, payload.nodeLibrary)),
    [payload.nodeLibrary, payload.session.graph.nodes],
  );
  const initialEdges = useMemo(
    () => payload.session.graph.edges.map((edge) => prepareStoredEdge(edge, initialNodes, typeColors)),
    [initialNodes, payload.session.graph.edges, typeColors],
  );
  const [nodes, setNodes, onNodesChange] = useNodesState<NodeGraphNode>(
    initialNodes,
  );
  const [edges, setEdges, onEdgesChange] = useEdgesState<NodeGraphEdge>(
    initialEdges,
  );
  const [selectedItem, setSelectedItem] = useState<CanvasSelection>(null);
  const [selectionSnapshot, setSelectionSnapshot] = useState<CanvasSelectionSnapshot>(createEmptyCanvasSelectionSnapshot);
  const [contextMenuState, setContextMenuState] = useState<CanvasContextMenuState | null>(null);

  const selectedNode =
    selectedItem?.type === "node"
      ? (nodes.find((node) => node.id === selectedItem.id) as NodeGraphNode | undefined) ?? null
      : null;
  const selectedEdge =
    selectedItem?.type === "edge"
      ? (edges.find((edge) => edge.id === selectedItem.id) as NodeGraphEdge | undefined) ?? null
      : null;
  const selectedTemplate = selectedNode ? getNodeTemplate(payload.nodeLibrary, selectedNode.data.nodeType) ?? null : null;
  const focusLabel = getCanvasFocusLabel(selectedItem, nodes, edges, messages.selection);
  const selectionTypeLabel = getCanvasTypeLabel(selectedItem, nodes, edges, messages.selection);
  const canvasEdges = useMemo(
    () => edges.map((edge) => createCanvasEdge(edge, nodes, typeColors, edgeStyle)),
    [edgeStyle, edges, nodes, typeColors],
  );
  const defaultEdgeOptions = useMemo(() => createDefaultEdgeOptions(edgeStyle), [edgeStyle]);
  const connectionLineType = getConnectionLineType(edgeStyle);
  const activeMenuPendingConnection = contextMenuState?.mode === "connection" ? contextMenuState.pendingConnection ?? null : null;
  const activeMenuPendingNode = activeMenuPendingConnection
    ? (nodes.find((node) => node.id === activeMenuPendingConnection.nodeId) as NodeGraphNode | undefined) ?? null
    : null;
  const activeMenuPendingPort =
    activeMenuPendingConnection && activeMenuPendingNode
      ? getPortForHandle(
          activeMenuPendingNode.data,
          activeMenuPendingConnection.handleType,
          activeMenuPendingConnection.handleId,
        )
      : null;
  const contextMenuItems =
    contextMenuState?.mode === "connection" && contextMenuState.pendingConnection
      ? getCompatibleNodeLibraryItems(payload.nodeLibrary, contextMenuState.pendingConnection, activeMenuPendingPort)
      : payload.nodeLibrary;

  function clearPendingConnection() {
    pendingConnectionRef.current = null;
  }

  function closeContextMenu() {
    suppressNextPaneClickRef.current = false;
    setContextMenuState(null);
  }

  function handleSelectionChange({
    nodes: selectedNodes,
    edges: selectedEdges,
  }: {
    nodes: NodeGraphNode[];
    edges: NodeGraphEdge[];
  }) {
    const nextSelection = createCanvasSelectionSnapshot({
      nodes: selectedNodes,
      edges: selectedEdges,
    });
    const nextSelectedItem = resolveCanvasSelection({
      nodes: selectedNodes,
      edges: selectedEdges,
    });

    setSelectionSnapshot((currentSelection) =>
      areSelectionSnapshotsEqual(currentSelection, nextSelection) ? currentSelection : nextSelection,
    );
    setSelectedItem((currentSelection) =>
      areSelectionsEqual(currentSelection, nextSelectedItem) ? currentSelection : nextSelectedItem,
    );
  }

  useEffect(() => {
    if (!contextMenuState) {
      return;
    }

    function handlePointerDown(event: PointerEvent) {
      if (event.target instanceof Element && event.target.closest("[data-canvas-context-menu='true']")) {
        return;
      }

      closeContextMenu();
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        closeContextMenu();
      }
    }

    document.addEventListener("pointerdown", handlePointerDown, true);
    document.addEventListener("keydown", handleKeyDown);

    return () => {
      document.removeEventListener("pointerdown", handlePointerDown, true);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [contextMenuState]);

  useLayoutEffect(() => {
    if (!contextMenuState || typeof window === "undefined") {
      return;
    }

    const menuElement = contextMenuRef.current;

    if (!menuElement) {
      return;
    }

    const currentMenuElement = menuElement;
    const { anchorPosition } = contextMenuState;

    function updateMenuPosition() {
      const { width, height } = currentMenuElement.getBoundingClientRect();
      const nextPosition = resolveContextMenuPosition(
        anchorPosition,
        { width, height },
        {
          width: window.innerWidth,
          height: window.innerHeight,
        },
        CONTEXT_MENU_MARGIN,
      );

      setContextMenuState((currentState) => {
        if (
          !currentState ||
          (currentState.position.x === nextPosition.x && currentState.position.y === nextPosition.y)
        ) {
          return currentState;
        }

        return {
          ...currentState,
          position: nextPosition,
        };
      });
    }

    updateMenuPosition();

    const resizeObserver =
      typeof ResizeObserver === "undefined" ? null : new ResizeObserver(updateMenuPosition);
    resizeObserver?.observe(currentMenuElement);
    window.addEventListener("resize", updateMenuPosition);

    return () => {
      resizeObserver?.disconnect();
      window.removeEventListener("resize", updateMenuPosition);
    };
  }, [contextMenuState]);

  function applyCanvasSelection(selection: CanvasSelectionSnapshot, primarySelection: CanvasSelection | null) {
    const selectedNodeIds = new Set(selection.nodeIds);
    const selectedEdgeIds = new Set(selection.edgeIds);

    setNodes((currentNodes) =>
      currentNodes.map((node) => {
        const isSelected = selectedNodeIds.has(node.id);
        return node.selected === isSelected ? node : { ...node, selected: isSelected };
      }),
    );
    setEdges((currentEdges) =>
      currentEdges.map((edge) => {
        const isSelected = selectedEdgeIds.has(edge.id);
        return edge.selected === isSelected ? edge : { ...edge, selected: isSelected };
      }),
    );
    setSelectionSnapshot(selection);
    setSelectedItem(primarySelection);
  }

  function addNode(item: EditorSessionPayload["nodeLibrary"][number]) {
    setNodes((currentNodes) => [
      ...currentNodes,
      createNodeFromLibrary(item, getNextNodePosition(currentNodes.length), locale),
    ]);
  }

  function addNodeAtMenuPosition(item: NodeLibraryItem) {
    if (!contextMenuState) {
      return;
    }

    const nextNode = createNodeFromLibrary(item, contextMenuState.flowPosition, locale);
    const nextSelection = createNodeSelectionSnapshot(nextNode.id);
    const nextConnection =
      contextMenuState.mode === "connection" && contextMenuState.pendingConnection && activeMenuPendingNode
        ? buildConnectionForInsertedNode({
            existingNodeId: contextMenuState.pendingConnection.nodeId,
            existingHandleId: contextMenuState.pendingConnection.handleId,
            existingHandleType: contextMenuState.pendingConnection.handleType,
            existingPort: activeMenuPendingPort,
            insertedNodeId: nextNode.id,
            insertedNodeData: nextNode.data,
          })
        : null;

    setNodes((currentNodes) => [
      ...currentNodes.map((node) => (node.selected ? { ...node, selected: false } : node)),
      {
        ...nextNode,
        selected: true,
      },
    ]);
    setEdges((currentEdges) => {
      const deselectedEdges = currentEdges.map((edge) => (edge.selected ? { ...edge, selected: false } : edge));

      if (!nextConnection) {
        return deselectedEdges;
      }

      return addEdge(
        createStoredEdge(nextConnection, nodes, typeColors),
        removeConflictingInputEdges(deselectedEdges, nextConnection),
      );
    });
    setSelectionSnapshot(nextSelection);
    setSelectedItem({ type: "node", id: nextNode.id });
    clearPendingConnection();
    closeContextMenu();
  }

  function updateSelectedNode(mutator: (node: NodeGraphNode) => NodeGraphNode) {
    if (!selectedNode) {
      return;
    }

    setNodes((currentNodes) =>
      currentNodes.map((node) => (node.id === selectedNode.id ? mutator(node as NodeGraphNode) : node)),
    );
  }

  function openContextMenuAtPosition(
    anchorPosition: NodeGraphNode["position"],
    selection: CanvasSelectionSnapshot,
    options?: {
      mode?: CanvasContextMenuState["mode"];
      pendingConnection?: PendingConnectionDraft;
      suppressNextPaneClick?: boolean;
      showLibrary?: boolean;
      targetEdgeId?: string;
    },
  ) {
    const reactFlow = reactFlowRef.current;

    if (!reactFlow) {
      return;
    }

    suppressNextPaneClickRef.current = options?.suppressNextPaneClick ?? false;
    setContextMenuState({
      anchorPosition,
      position: anchorPosition,
      flowPosition: reactFlow.screenToFlowPosition(anchorPosition),
      selection,
      mode: options?.mode ?? "default",
      pendingConnection: options?.pendingConnection,
      showLibrary: options?.showLibrary ?? true,
      targetEdgeId: options?.targetEdgeId,
    });
  }

  function openContextMenu(
    event: MouseEvent | ReactMouseEvent,
    selection: CanvasSelectionSnapshot,
    options?: {
      mode?: CanvasContextMenuState["mode"];
      pendingConnection?: PendingConnectionDraft;
      suppressNextPaneClick?: boolean;
      showLibrary?: boolean;
      targetEdgeId?: string;
    },
  ) {
    const clientPosition = getClientPosition(event);

    event.preventDefault();
    event.stopPropagation();

    if (!clientPosition) {
      return;
    }

    openContextMenuAtPosition(clientPosition, selection, options);
  }

  function handlePaneContextMenu(event: MouseEvent | ReactMouseEvent) {
    openContextMenu(event, selectionSnapshot);
  }

  function handleNodeContextMenu(event: ReactMouseEvent, node: NodeGraphNode) {
    const nextSelection = selectionSnapshot.nodeIds.includes(node.id)
      ? selectionSnapshot
      : createNodeSelectionSnapshot(node.id);

    if (!selectionSnapshot.nodeIds.includes(node.id)) {
      applyCanvasSelection(nextSelection, { type: "node", id: node.id });
    } else {
      setSelectedItem({ type: "node", id: node.id });
    }

    openContextMenu(event, nextSelection, { showLibrary: false });
  }

  function handleSelectionContextMenu(event: ReactMouseEvent, selectedNodes: NodeGraphNode[]) {
    const nextSelection: CanvasSelectionSnapshot = {
      nodeIds: selectedNodes.map((node) => node.id),
      edgeIds: selectionSnapshot.edgeIds,
    };

    setSelectionSnapshot(nextSelection);
    setSelectedItem(resolveCanvasSelectionFromSnapshot(nextSelection));
    openContextMenu(event, nextSelection);
  }

  function handleEdgeContextMenu(event: ReactMouseEvent, edge: NodeGraphEdge) {
    const nextSelection = createEdgeSelectionSnapshot(edge.id);

    applyCanvasSelection(nextSelection, { type: "edge", id: edge.id });
    openContextMenu(event, nextSelection, {
      showLibrary: false,
      targetEdgeId: edge.id,
    });
  }

  function handleConnect(connection: Connection) {
    clearPendingConnection();
    setEdges((currentEdges) =>
      addEdge(createStoredEdge(connection, nodes, typeColors), removeConflictingInputEdges(currentEdges, connection)),
    );
  }

  function handleConnectStart(_event: MouseEvent | TouchEvent, params: OnConnectStartParams) {
    if (!params.nodeId || !params.handleType) {
      clearPendingConnection();
      return;
    }

    const nextPendingConnection = {
      nodeId: params.nodeId,
      handleId: params.handleId ?? null,
      handleType: params.handleType,
    } satisfies PendingConnectionDraft;

    pendingConnectionRef.current = nextPendingConnection;
  }

  function handleConnectEnd(event: MouseEvent | TouchEvent, connectionState: FinalConnectionState) {
    const activePendingConnection = pendingConnectionRef.current;
    const clientPosition = getClientPosition(event);

    if (
      activePendingConnection &&
      clientPosition &&
      shouldOpenConnectionCreationMenu({
        hasPendingConnection: activePendingConnection != null,
        hasClientPosition: clientPosition != null,
        hasTargetNode: connectionState.toNode != null,
        hasTargetHandle: connectionState.toHandle != null,
        droppedOnBlankCanvas: isBlankCanvasDropTarget(event.target),
      })
    ) {
      openContextMenuAtPosition(clientPosition, createEmptyCanvasSelectionSnapshot(), {
        mode: "connection",
        pendingConnection: activePendingConnection,
        suppressNextPaneClick: true,
      });
    }

    clearPendingConnection();
  }

  function handleNodeClick(nodeId: string) {
    setSelectedItem({ type: "node", id: nodeId });
  }

  function handleEdgeClick(edgeId: string) {
    setSelectedItem({ type: "edge", id: edgeId });
  }

  function handlePaneClick() {
    const paneClickSuppression = consumePaneClickSuppression(suppressNextPaneClickRef.current);
    suppressNextPaneClickRef.current = paneClickSuppression.nextShouldSuppressPaneClick;
    clearPendingConnection();

    if (paneClickSuppression.shouldIgnorePaneClick) {
      return;
    }

    closeContextMenu();
    setSelectedItem(null);
  }

  function handleDelete() {
    clearPendingConnection();
    setSelectionSnapshot(createEmptyCanvasSelectionSnapshot());
    setSelectedItem(null);
    closeContextMenu();
  }

  function copyCurrentSelection() {
    if (!contextMenuState?.selection.nodeIds.length) {
      return;
    }

    const nextClipboard = buildClipboardFromSelection(nodes, edges, contextMenuState.selection.nodeIds);

    if (!nextClipboard) {
      return;
    }

    setClipboard(nextClipboard);
    setPasteCount(0);
    closeContextMenu();
  }

  async function deleteCurrentSelection() {
    const reactFlow = reactFlowRef.current;

    if (!reactFlow || !contextMenuState) {
      return;
    }

    const edgeIds = contextMenuState.targetEdgeId
      ? [contextMenuState.targetEdgeId]
      : contextMenuState.selection.edgeIds;

    await reactFlow.deleteElements({
      nodes: contextMenuState.selection.nodeIds.map((id) => ({ id })),
      edges: Array.from(new Set(edgeIds)).map((id) => ({ id })),
    });

    handleDelete();
  }

  async function cutCurrentSelection() {
    if (!contextMenuState?.selection.nodeIds.length) {
      return;
    }

    const nextClipboard = buildClipboardFromSelection(nodes, edges, contextMenuState.selection.nodeIds);

    if (!nextClipboard) {
      return;
    }

    setClipboard(nextClipboard);
    setPasteCount(0);
    await deleteCurrentSelection();
  }

  function pasteClipboardAtMenuPosition() {
    if (!contextMenuState || !clipboard) {
      return;
    }

    const pastedGraph = pasteClipboardAtPosition(clipboard, {
      position: contextMenuState.flowPosition,
      cascadeIndex: pasteCount,
    });
    const nextSelection: CanvasSelectionSnapshot = {
      nodeIds: pastedGraph.nodes.map((node) => node.id),
      edgeIds: [],
    };

    setNodes((currentNodes) => [
      ...currentNodes.map((node) => (node.selected ? { ...node, selected: false } : node)),
      ...pastedGraph.nodes.map((node) => ({
        ...node,
        selected: true,
      })),
    ]);
    setEdges((currentEdges) => [
      ...currentEdges.map((edge) => (edge.selected ? { ...edge, selected: false } : edge)),
      ...pastedGraph.edges,
    ]);
    setSelectionSnapshot(nextSelection);
    setSelectedItem(
      nextSelection.nodeIds[0]
        ? {
            type: "node",
            id: nextSelection.nodeIds[0],
          }
        : null,
    );
    setPasteCount((currentCount) => currentCount + 1);
    closeContextMenu();
  }

  const contextMenuMeta = contextMenuState
    ? {
        canCopy: contextMenuState.mode === "default" && contextMenuState.selection.nodeIds.length > 0,
        canCut: contextMenuState.mode === "default" && contextMenuState.selection.nodeIds.length > 0,
        canDelete:
          contextMenuState.mode === "default" &&
          (contextMenuState.selection.nodeIds.length > 0 || contextMenuState.selection.edgeIds.length > 0),
        canPaste: contextMenuState.mode === "default" && Boolean(clipboard),
        copyLabel: getCopyLabel(contextMenuState.selection, messages),
        cutLabel: getCutLabel(contextMenuState.selection, messages),
        deleteLabel: getDeleteLabel(contextMenuState.selection, messages, contextMenuState.targetEdgeId),
        emptyStateMessage:
          contextMenuState.mode === "connection"
            ? messages.contextMenu.noCompatibleNodes
            : messages.contextMenu.noNodesAvailable,
        libraryLabel: contextMenuState.mode === "connection"
          ? messages.contextMenu.createConnectedNode
          : messages.contextMenu.addNode,
        mode: contextMenuState.mode,
      }
    : null;

  return {
    addNode,
    addNodeAtMenuPosition,
    contextMenuItems,
    contextMenuRef,
    contextMenuMeta,
    contextMenuState,
    connectionLineType,
    copyCurrentSelection,
    cutCurrentSelection,
    canvasEdges,
    defaultEdgeOptions,
    deleteCurrentSelection,
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
    setReactFlowInstance: (instance: ReactFlowInstance<NodeGraphNode, NodeGraphEdge>) => {
      reactFlowRef.current = instance;
    },
    updateSelectedNode,
  };
}
