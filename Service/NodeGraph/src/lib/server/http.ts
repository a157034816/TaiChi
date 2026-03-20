import { HttpError } from "@/lib/server/errors";

export function jsonResponse(payload: unknown, init?: ResponseInit) {
  return Response.json(payload, init);
}

export function jsonError(error: unknown) {
  if (error instanceof HttpError) {
    return jsonResponse(
      {
        error: error.message,
      },
      { status: error.status },
    );
  }

  return jsonResponse(
    {
      error: "NodeGraph encountered an unexpected error.",
    },
    { status: 500 },
  );
}
