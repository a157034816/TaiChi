import { completeSessionRequestSchema } from "@/lib/nodegraph/schema";
import { jsonError, jsonResponse } from "@/lib/server/http";
import { completeSession } from "@/lib/server/session-service";

export async function POST(
  request: Request,
  context: { params: Promise<{ sessionId: string }> },
) {
  try {
    const { sessionId } = await context.params;
    const body = await request.json();
    const input = completeSessionRequestSchema.parse(body);
    return jsonResponse(await completeSession(sessionId, input.graph));
  } catch (error) {
    return jsonError(error);
  }
}
