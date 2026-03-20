import type { Connection, HandleType } from "@xyflow/react";

import type { NodeGraphEdge, NodeGraphNodeData, NodeLibraryItem, NodePortDefinition } from "@/lib/nodegraph/types";

type PortOwner = Pick<NodeLibraryItem, "inputs" | "outputs"> | Pick<NodeGraphNodeData, "inputs" | "outputs">;

interface PendingConnectionCompatibilityInput {
  handleType: HandleType;
  startPort: NodePortDefinition | null;
}

export interface PendingConnectionDraft {
  nodeId: string;
  handleId: string | null;
  handleType: HandleType;
}

function normalizeHandleId(handleId?: string | null) {
  return handleId ?? "__default_handle__";
}

function normalizeMatchToken(value?: string | null) {
  const normalized = value?.trim().toLowerCase();
  return normalized?.length ? normalized : null;
}

function getPortsForHandleType(owner: PortOwner, handleType: HandleType) {
  return handleType === "source" ? owner.outputs ?? [] : owner.inputs ?? [];
}

function getOppositePorts(owner: PortOwner, handleType: HandleType) {
  return handleType === "source" ? owner.inputs ?? [] : owner.outputs ?? [];
}

export function getPortForHandle(owner: PortOwner, handleType: HandleType, handleId?: string | null) {
  const ports = getPortsForHandleType(owner, handleType);

  if (!ports.length) {
    return null;
  }

  if (handleId == null) {
    return ports[0] ?? null;
  }

  return ports.find((port) => port.id === handleId) ?? null;
}

export function findCompatibleOppositePort(
  owner: PortOwner,
  { handleType, startPort }: PendingConnectionCompatibilityInput,
) {
  const oppositePorts = getOppositePorts(owner, handleType);

  if (!oppositePorts.length) {
    return null;
  }

  if (!startPort) {
    return oppositePorts[0] ?? null;
  }

  const startDataType = normalizeMatchToken(startPort.dataType);

  if (startDataType) {
    const typedMatch = oppositePorts.find((port) => normalizeMatchToken(port.dataType) === startDataType);

    if (typedMatch) {
      return typedMatch;
    }
  }

  const startHandleToken = normalizeMatchToken(startPort.id);

  if (startHandleToken) {
    const fallbackIdMatch = oppositePorts.find((port) => {
      const candidateDataType = normalizeMatchToken(port.dataType);

      return (!startDataType || !candidateDataType) && normalizeMatchToken(port.id) === startHandleToken;
    });

    if (fallbackIdMatch) {
      return fallbackIdMatch;
    }
  }

  if (startDataType) {
    const untypedPort = oppositePorts.find((port) => normalizeMatchToken(port.dataType) === null);
    return untypedPort ?? null;
  }

  return oppositePorts[0] ?? null;
}

export function getCompatibleNodeLibraryItems(
  items: NodeLibraryItem[],
  pendingConnection: Pick<PendingConnectionDraft, "handleType">,
  startPort: NodePortDefinition | null,
) {
  return items.filter((item) =>
    Boolean(
      findCompatibleOppositePort(item, {
        handleType: pendingConnection.handleType,
        startPort,
      }),
    ),
  );
}

export function buildConnectionForInsertedNode(params: {
  existingNodeId: string;
  existingHandleId: string | null;
  existingHandleType: HandleType;
  existingPort: NodePortDefinition | null;
  insertedNodeId: string;
  insertedNodeData: Pick<NodeGraphNodeData, "inputs" | "outputs">;
}) {
  const matchedPort = findCompatibleOppositePort(params.insertedNodeData, {
    handleType: params.existingHandleType,
    startPort: params.existingPort,
  });

  if (!matchedPort) {
    return null;
  }

  if (params.existingHandleType === "source") {
    return {
      source: params.existingNodeId,
      sourceHandle: params.existingHandleId,
      target: params.insertedNodeId,
      targetHandle: matchedPort.id,
    } satisfies Connection;
  }

  return {
    source: params.insertedNodeId,
    sourceHandle: matchedPort.id,
    target: params.existingNodeId,
    targetHandle: params.existingHandleId,
  } satisfies Connection;
}

export function removeConflictingInputEdges(
  edges: NodeGraphEdge[],
  connection: Pick<Connection, "target" | "targetHandle">,
) {
  if (!connection.target) {
    return edges;
  }

  const targetHandle = normalizeHandleId(connection.targetHandle);

  return edges.filter(
    (edge) =>
      edge.target !== connection.target ||
      normalizeHandleId(edge.targetHandle) !== targetHandle,
  );
}
