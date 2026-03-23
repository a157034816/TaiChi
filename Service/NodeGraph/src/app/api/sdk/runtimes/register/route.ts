import { runtimeRegistrationRequestSchema } from "@/lib/nodegraph/schema";
import { jsonError, jsonResponse } from "@/lib/server/http";
import { registerRuntime } from "@/lib/server/runtime-service";

export async function POST(request: Request) {
  try {
    const body = await request.json();
    const input = runtimeRegistrationRequestSchema.parse(body);
    const payload = registerRuntime(input);
    return jsonResponse(payload, { status: 201 });
  } catch (error) {
    return jsonError(error);
  }
}
