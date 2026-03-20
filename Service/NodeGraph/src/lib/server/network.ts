import type { AccessType } from "@/lib/nodegraph/types";

function normalizeIp(ipAddress: string | null | undefined) {
  if (!ipAddress) {
    return undefined;
  }

  const candidate = ipAddress.split(",")[0]?.trim();
  if (!candidate) {
    return undefined;
  }

  return candidate.replace(/^::ffff:/, "");
}

export function extractClientIp(request: Request) {
  return (
    normalizeIp(request.headers.get("x-forwarded-for")) ??
    normalizeIp(request.headers.get("x-real-ip")) ??
    undefined
  );
}

export function isPrivateIp(ipAddress: string | undefined) {
  if (!ipAddress) {
    return false;
  }

  if (ipAddress === "127.0.0.1" || ipAddress === "::1" || ipAddress === "localhost") {
    return true;
  }

  if (/^10\./.test(ipAddress)) {
    return true;
  }

  if (/^192\.168\./.test(ipAddress)) {
    return true;
  }

  const match172 = ipAddress.match(/^172\.(\d{1,3})\./);
  if (match172) {
    const secondOctet = Number(match172[1]);
    return secondOctet >= 16 && secondOctet <= 31;
  }

  return ipAddress.startsWith("fc") || ipAddress.startsWith("fd");
}

export function resolveAccessType(request: Request): AccessType {
  return isPrivateIp(extractClientIp(request)) ? "private" : "public";
}

export function resolveBaseUrl(request: Request, publicBaseUrl: string, privateBaseUrl: string) {
  return resolveAccessType(request) === "private" ? privateBaseUrl : publicBaseUrl;
}
