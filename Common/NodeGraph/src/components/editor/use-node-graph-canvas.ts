"use client";

import { useCallback, useEffect, useLayoutEffect, useRef, useState, type MouseEvent as ReactMouseEvent } from "react";
import { addEdge, type Connection, MarkerType, type ReactFlowInstance, useEdgesState, useNodesState } from "@xyflow/react";

import { BlueprintNode } from "@/components/editor/blueprint-node";
import { resolveContextMenuPosition } from "@/components/editor/context-menu-position";
import { removeConflictingInputEdges } from "@/lib/nodegraph/connections";
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
import type { EditorSessionPayload, NodeGraphEdge, NodeGraphNode, NodeLibraryItem } from "@/lib/nodegraph/types";

interface CanvasContextMenuState {
  anchorPosition: NodeGraphNode["position"];
  position: NodeGraphNode["position"];
  flowPosition: NodeGraphNode["position"];
  selection: CanvasSelectionSnapshot;
  showLibrary: boolean;
  targetEdgeId?: string;
}

const EDGE_STROKE = "rgba(159, 179, 217, 0.94)";
const CONTEXT_MENU_MARGIN = 16;

export const editorNodeTypes = {
  default: BlueprintNode,
};

export const defaultEdgeOptions = {
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

function getCopyLabel(selection: CanvasSelectionSnapshot) {
  if (selection.nodeIds.length > 1) {
    return "Copy selected nodes";
  }

  return "Copy node";
}

function getCutLabel(selection: CanvasSelectionSnapshot) {
  if (selection.nodeIds.length > 1) {
    return "Cut selected nodes";
  }

  return "Cut node";
}

function getDeleteLabel(selection: CanvasSelectionSnapshot, targetEdgeId?: string) {
  if (targetEdgeId && !selection.nodeIds.length) {
    return "Delete edge";
  }

  if (selection.nodeIds.length > 1) {
    return "Delete selected nodes";
  }

  if (selection.nodeIds.length === 1) {
    return "Delete node";
  }

  if (selection.edgeIds.length > 1) {
    return "Delete selected edges";
  }

  return "Delete selection";
}

export function useNodeGraphCanvas(payload: EditorSessionPayload) {
  const reactFlowRef = useRef<ReactFlowInstance<NodeGraphNode, NodeGraphEdge> | null>(null);
  const contextMenuRef = useRef<HTMLDivElement | null>(null);
  const [clipboard, setClipboard] = useState<NodeClipboardPayload | null>(null);
  const [pasteCount, setPasteCount] = useState(0);
  const [nodes, setNodes, onNodesChange] = useNodesState<NodeGraphNode>(
    payload.session.graph.nodes.map((node) => prepareCanvasNode(node, payload.nodeLibrary)),
  );
  const [edges, setEdges, onEdgesChange] = useEdgesState<NodeGraphEdge>(
    payload.session.graph.edges.map(prepareCanvasEdge),
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
  const focusLabel = getCanvasFocusLabel(selectedItem, nodes, edges);
  const selectionTypeLabel = getCanvasTypeLabel(selectedItem, nodes, edges);

  const handleSelectionChange = useCallback(
    ({ nodes: selectedNodes, edges: selectedEdges }: { nodes: NodeGraphNode[]; edges: NodeGraphEdge[] }) => {
      const nextSelection = createCanvasSelectionSnapshot({
        nodes: selectedNodes,
        edges: selectedEdges,
      });
      setSelectionSnapshot(nextSelection);
      setSelectedItem(
        resolveCanvasSelection({
          nodes: selectedNodes,
          edges: selectedEdges,
        }),
      );
    },
    [],
  );

  useEffect(() => {
    if (!contextMenuState) {
      return;
    }

    function handlePointerDown(event: PointerEvent) {
      if (event.target instanceof Element && event.target.closest("[data-canvas-context-menu='true']")) {
        return;
      }

      setContextMenuState(null);
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        setContextMenuState(null);
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
      createNodeFromLibrary(item, getNextNodePosition(currentNodes.length)),
    ]);
  }

  function addNodeAtMenuPosition(item: NodeLibraryItem) {
    if (!contextMenuState) {
      return;
    }

    const nextNode = createNodeFromLibrary(item, contextMenuState.flowPosition);
    const nextSelection = createNodeSelectionSnapshot(nextNode.id);

    setNodes((currentNodes) => [
      ...currentNodes.map((node) => (node.selected ? { ...node, selected: false } : node)),
      {
        ...nextNode,
        selected: true,
      },
    ]);
    setEdges((currentEdges) => currentEdges.map((edge) => (edge.selected ? { ...edge, selected: false } : edge)));
    setSelectionSnapshot(nextSelection);
    setSelectedItem({ type: "node", id: nextNode.id });
    setContextMenuState(null);
  }

  function updateSelectedNode(mutator: (node: NodeGraphNode) => NodeGraphNode) {
    if (!selectedNode) {
      return;
    }

    setNodes((currentNodes) =>
      currentNodes.map((node) => (node.id === selectedNode.id ? mutator(node as NodeGraphNode) : node)),
    );
  }

  function openContextMenu(
    event: MouseEvent | ReactMouseEvent,
    selection: CanvasSelectionSnapshot,
    options?: { showLibrary?: boolean; targetEdgeId?: string },
  ) {
    const reactFlow = reactFlowRef.current;

    event.preventDefault();
    event.stopPropagation();

    if (!reactFlow) {
      return;
    }

    const anchorPosition = {
      x: event.clientX,
      y: event.clientY,
    };

    setContextMenuState({
      anchorPosition,
      position: anchorPosition,
      flowPosition: reactFlow.screenToFlowPosition({
        x: event.clientX,
        y: event.clientY,
      }),
      selection,
      showLibrary: options?.showLibrary ?? true,
      targetEdgeId: options?.targetEdgeId,
    });
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
    setEdges((currentEdges) =>
      addEdge(createCanvasEdge(connection), removeConflictingInputEdges(currentEdges, connection)),
    );
  }

  function handleNodeClick(nodeId: string) {
    setSelectedItem({ type: "node", id: nodeId });
  }

  function handleEdgeClick(edgeId: string) {
    setSelectedItem({ type: "edge", id: edgeId });
  }

  function handlePaneClick() {
    setContextMenuState(null);
    setSelectedItem(null);
  }

  function handleDelete() {
    setSelectionSnapshot(createEmptyCanvasSelectionSnapshot());
    setSelectedItem(null);
    setContextMenuState(null);
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
    setContextMenuState(null);
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
    setContextMenuState(null);
  }

  const contextMenuMeta = contextMenuState
    ? {
        canCopy: contextMenuState.selection.nodeIds.length > 0,
        canCut: contextMenuState.selection.nodeIds.length > 0,
        canDelete: contextMenuState.selection.nodeIds.length > 0 || contextMenuState.selection.edgeIds.length > 0,
        canPaste: Boolean(clipboard),
        copyLabel: getCopyLabel(contextMenuState.selection),
        cutLabel: getCutLabel(contextMenuState.selection),
        deleteLabel: getDeleteLabel(contextMenuState.selection, contextMenuState.targetEdgeId),
      }
    : null;

  return {
    addNode,
    addNodeAtMenuPosition,
    contextMenuRef,
    contextMenuMeta,
    contextMenuState,
    copyCurrentSelection,
    cutCurrentSelection,
    defaultEdgeOptions,
    deleteCurrentSelection,
    edges,
    focusLabel,
    handleConnect,
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
