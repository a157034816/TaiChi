#!/usr/bin/env sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
IMAGE_ARCHIVE="$SCRIPT_DIR/central-service-images.tar"
ENV_FILE="$SCRIPT_DIR/.env"
ENV_TEMPLATE="$SCRIPT_DIR/.env.example"
COMPOSE_FILE="$SCRIPT_DIR/compose.release.yml"

if [ ! -f "$IMAGE_ARCHIVE" ]; then
    echo "缺少镜像归档: $IMAGE_ARCHIVE" >&2
    exit 1
fi

if [ ! -f "$ENV_FILE" ]; then
    cp "$ENV_TEMPLATE" "$ENV_FILE"
    echo "已生成 $ENV_FILE，请先编辑 CENTRAL_DOMAIN、API_DOMAIN、邮箱、管理员密码和代理密钥后再重新执行。" >&2
    exit 1
fi

docker load -i "$IMAGE_ARCHIVE"
docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" up -d --force-recreate
