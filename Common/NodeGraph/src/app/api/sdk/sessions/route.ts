import { createSessionRequestSchema } from "@/lib/nodegraph/schema";
import { createSession } from "@/lib/server/session-service";
import { jsonError, jsonResponse } from "@/lib/server/http";

export async function POST(request: Request) {
  try {
    const body = await request.json();
    const input = createSessionRequestSchema.parse(body);
    const payload = await createSession(request, input);
    return jsonResponse(payload, { status: 201 });
  } catch (error) {
    return jsonError(error);
  }
}
