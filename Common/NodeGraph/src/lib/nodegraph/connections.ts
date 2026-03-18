import type { Connection } from "@xyflow/react";

import type { NodeGraphEdge } from "@/lib/nodegraph/types";

function normalizeHandleId(handleId?: string | null) {
  return handleId ?? "__default_handle__";
}

export function isInputHandleOccupied(
  edges: NodeGraphEdge[],
  connection: Pick<Connection, "target" | "targetHandle">,
) {
  if (!connection.target) {
    return false;
  }

  const targetHandle = normalizeHandleId(connection.targetHandle);

  return edges.some(
    (edge) =>
      edge.target === connection.target &&
      normalizeHandleId(edge.targetHandle) === targetHandle,
  );
}
