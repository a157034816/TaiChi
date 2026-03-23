import { refreshRuntimeLibraryRequestSchema } from "@/lib/nodegraph/schema";
import { jsonError, jsonResponse } from "@/lib/server/http";
import { refreshSessionRuntimeLibrary } from "@/lib/server/session-service";

export async function POST(
  request: Request,
  context: { params: Promise<{ sessionId: string }> },
) {
  try {
    const { sessionId } = await context.params;
    const body = await request.json().catch(() => ({}));
    const input = refreshRuntimeLibraryRequestSchema.parse(body);
    return jsonResponse(await refreshSessionRuntimeLibrary(sessionId, input.graph));
  } catch (error) {
    return jsonError(error);
  }
}
