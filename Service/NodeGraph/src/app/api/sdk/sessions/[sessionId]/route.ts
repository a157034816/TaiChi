import { jsonError, jsonResponse } from "@/lib/server/http";
import { getSession } from "@/lib/server/session-service";

export async function GET(
  _request: Request,
  context: { params: Promise<{ sessionId: string }> },
) {
  try {
    const { sessionId } = await context.params;
    const session = getSession(sessionId);
    if (!session) {
      return jsonResponse({ error: "NodeGraph session was not found." }, { status: 404 });
    }

    return jsonResponse(session);
  } catch (error) {
    return jsonError(error);
  }
}
