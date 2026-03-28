import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone",
  async rewrites() {
    const centralServiceBaseUrl =
      process.env.CENTRAL_SERVICE_BASE_URL ?? "http://localhost:15700";

    return [
      {
        source: "/api/auth/:path*",
        destination: `${centralServiceBaseUrl}/api/auth/:path*`,
      },
      {
        source: "/api/admin/:path*",
        destination: `${centralServiceBaseUrl}/api/admin/:path*`,
      },
      {
        source: "/api/Service/:path*",
        destination: `${centralServiceBaseUrl}/api/Service/:path*`,
      },
      {
        source: "/api/ServiceDiscovery/:path*",
        destination: `${centralServiceBaseUrl}/api/ServiceDiscovery/:path*`,
      },
      {
        source: "/health",
        destination: `${centralServiceBaseUrl}/health`,
      },
    ];
  },
};

export default nextConfig;
