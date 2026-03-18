import type { Connection } from "@xyflow/react";

import type { NodeGraphEdge } from "@/lib/nodegraph/types";

function normalizeHandleId(handleId?: string | null) {
  return handleId ?? "__default_handle__";
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
