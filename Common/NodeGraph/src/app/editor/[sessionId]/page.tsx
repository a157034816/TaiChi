import { notFound } from "next/navigation";

import { NodeGraphEditor } from "@/components/editor/node-graph-editor";
import { getEditorPayload } from "@/lib/server/session-service";

export default async function EditorPage({
  params,
}: {
  params: Promise<{ sessionId: string }>;
}) {
  const { sessionId } = await params;
  let payload;

  try {
    payload = getEditorPayload(sessionId);
  } catch {
    notFound();
  }

  return <NodeGraphEditor payload={payload} />;
}
