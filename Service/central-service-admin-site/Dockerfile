#
# 多阶段构建说明：
# - deps：仅安装依赖，用于提升缓存命中与复用 node_modules
# - build：编译 Next.js（standalone 输出）
# - runtime：仅拷贝运行所需文件，避免把源码/构建缓存带入最终镜像
#
# 重要：当前构建环境可能无法稳定访问 Docker Hub（例如 auth.docker.io）。
# 因此这里默认使用 `mcr.microsoft.com/devcontainers/javascript-node` 作为基础镜像，
# 以确保 `Service/deploy/central-service/export-release.ps1` 在离线/受限网络环境下可用。
# 为了进一步降低部署体积，runtime 阶段使用更小的 `mcr.microsoft.com/dotnet/runtime-deps` 作为基础镜像，
# 并从 build 阶段复制 Node.js 可执行文件，从而同时满足：
# - 不依赖 Docker Hub（只依赖 MCR）
# - 运行时镜像更小（显著降低“DISK USAGE”）

FROM mcr.microsoft.com/devcontainers/javascript-node:1-22-bookworm AS deps
WORKDIR /app

# 禁用 Next.js 遥测，避免在 CI / 构建环境产生无关输出。
ENV NEXT_TELEMETRY_DISABLED=1

COPY package.json package-lock.json ./
RUN npm ci

FROM mcr.microsoft.com/devcontainers/javascript-node:1-22-bookworm AS build
WORKDIR /app

ENV NEXT_TELEMETRY_DISABLED=1

ARG CENTRAL_SERVICE_BASE_URL=http://centralservice:15700
ENV CENTRAL_SERVICE_BASE_URL=${CENTRAL_SERVICE_BASE_URL}

COPY --from=deps /app/node_modules ./node_modules
COPY . .

RUN npm run build

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS runtime
WORKDIR /app

# 从构建镜像复制 Node.js 运行时，避免依赖 Docker Hub 的 `node:*` 基础镜像。
COPY --from=build /usr/local/bin/node /usr/local/bin/node

ARG CENTRAL_SERVICE_BASE_URL=http://centralservice:15700
ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1
ENV HOSTNAME=0.0.0.0
ENV PORT=3000
ENV CENTRAL_SERVICE_BASE_URL=${CENTRAL_SERVICE_BASE_URL}

USER 1000:1000

COPY --from=build --chown=1000:1000 /app/.next/standalone ./
COPY --from=build --chown=1000:1000 /app/.next/static ./.next/static
COPY --from=build --chown=1000:1000 /app/public ./public

EXPOSE 3000
CMD ["/usr/local/bin/node", "server.js"]
