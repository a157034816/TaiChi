import { jsonResponse } from "@/lib/server/http";

export async function GET() {
  return jsonResponse({
    status: "ok",
    service: "NodeGraph",
    timestamp: new Date().toISOString(),
  });
}
