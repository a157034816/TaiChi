import assert from "node:assert/strict";
import test from "node:test";

import nextConfig from "../next.config.ts";

/**
 * 验证管理站点已经切换为根路径部署，避免继续依赖旧的 `/centralservice`
 * `basePath`，从而与 `centralservice.y-bf.lol` 的根路径发布方案保持一致。
 */
test("next config does not declare the legacy /centralservice basePath", () => {
  assert.equal(
    (nextConfig as { basePath?: string }).basePath,
    undefined,
    "根路径部署时不应再声明 /centralservice basePath"
  );
});

/**
 * 验证浏览器侧仍通过同源 `/api/*` 与 `/health` 访问后端，而不是把请求改写到
 * 旧的 `/centralservice/...` 子路径。
 */
test("next rewrites keep same-origin root API paths", async () => {
  const rewrites = await nextConfig.rewrites?.();
  assert.ok(Array.isArray(rewrites), "rewrites 应返回路由规则数组");

  const sources = rewrites.map((rewrite) => rewrite.source);
  assert.deepEqual(sources, [
    "/api/auth/:path*",
    "/api/admin/:path*",
    "/api/Service/:path*",
    "/api/ServiceDiscovery/:path*",
    "/health",
  ]);

  for (const rewrite of rewrites) {
    assert.equal(
      rewrite.destination.includes("/centralservice"),
      false,
      "根路径部署时不应再把请求改写到 /centralservice 子路径"
    );
  }
});
