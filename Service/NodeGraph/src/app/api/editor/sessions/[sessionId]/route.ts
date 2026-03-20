import { jsonError, jsonResponse } from "@/lib/server/http";
import { getEditorPayload } from "@/lib/server/session-service";

export async function GET(
  _request: Request,
  context: { params: Promise<{ sessionId: string }> },
) {
  try {
    const { sessionId } = await context.params;
    return jsonResponse(getEditorPayload(sessionId));
  } catch (error) {
    return jsonError(error);
  }
}
