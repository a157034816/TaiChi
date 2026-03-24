FROM mcr.microsoft.com/devcontainers/javascript-node:1-22-bookworm AS deps
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

FROM mcr.microsoft.com/devcontainers/javascript-node:1-22-bookworm AS build
WORKDIR /app

ARG CENTRAL_SERVICE_BASE_URL=http://centralservice:15700
ENV CENTRAL_SERVICE_BASE_URL=${CENTRAL_SERVICE_BASE_URL}

COPY --from=deps /app/node_modules ./node_modules
COPY . .

RUN npm run build

FROM mcr.microsoft.com/devcontainers/javascript-node:1-22-bookworm AS runtime
WORKDIR /app

ARG CENTRAL_SERVICE_BASE_URL=http://centralservice:15700
ENV NODE_ENV=production
ENV HOSTNAME=0.0.0.0
ENV PORT=3000
ENV CENTRAL_SERVICE_BASE_URL=${CENTRAL_SERVICE_BASE_URL}

COPY --from=build /app/.next/standalone ./
COPY --from=build /app/.next/static ./.next/static
COPY --from=build /app/public ./public

EXPOSE 3000
CMD ["node", "server.js"]
