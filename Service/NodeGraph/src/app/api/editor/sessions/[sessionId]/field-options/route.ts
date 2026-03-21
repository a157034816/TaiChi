import { jsonError, jsonResponse } from "@/lib/server/http";
import { HttpError } from "@/lib/server/errors";
import { getFieldOptions } from "@/lib/server/session-service";

export async function GET(
  request: Request,
  context: { params: Promise<{ sessionId: string }> },
) {
  try {
    const { sessionId } = await context.params;
    const { searchParams } = new URL(request.url);
    const nodeType = searchParams.get("nodeType");
    const fieldKey = searchParams.get("fieldKey");
    const locale = searchParams.get("locale");

    if (!nodeType || !fieldKey || !locale) {
      throw new HttpError("Missing required field options query parameters.", 400);
    }

    return jsonResponse(
      await getFieldOptions(sessionId, {
        fieldKey,
        locale,
        nodeType,
      }),
    );
  } catch (error) {
    return jsonError(error);
  }
}
