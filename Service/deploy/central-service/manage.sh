#!/usr/bin/env sh
set -eu

# 统一管理中心服务离线发布包的启动、停止、重启、状态与日志查看。

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
ENV_FILE="$SCRIPT_DIR/.env"
ENV_TEMPLATE="$SCRIPT_DIR/.env.example"
COMPOSE_FILE="$SCRIPT_DIR/compose.release.yml"

print_usage() {
    # 输出脚本支持的命令列表；help/无参数走标准输出，非法命令走标准错误。
    output_mode=${1:-stderr}

    if [ "$output_mode" = "stdout" ]; then
        cat <<'EOF'
用法:
  ./manage.sh
  ./manage.sh help
  ./manage.sh start
  ./manage.sh stop
  ./manage.sh restart [service]
  ./manage.sh status
  ./manage.sh logs [service]

说明:
  不带参数时默认显示本帮助。
  service 为可选服务名，例如: caddy、central-service、admin-site。
EOF
    else
        cat >&2 <<'EOF'
用法:
  ./manage.sh
  ./manage.sh help
  ./manage.sh start
  ./manage.sh stop
  ./manage.sh restart [service]
  ./manage.sh status
  ./manage.sh logs [service]

说明:
  不带参数时默认显示本帮助。
  service 为可选服务名，例如: caddy、central-service、admin-site。
EOF
    fi
}

ensure_env_file() {
    # 启动/停止前确保部署配置文件存在；若不存在则从模板生成并提示补齐。
    if [ -f "$ENV_FILE" ]; then
        return 0
    fi

    cp "$ENV_TEMPLATE" "$ENV_FILE"
    echo "已生成 $ENV_FILE，请先编辑域名、邮箱、管理员密码和代理密钥后再重试。" >&2
    exit 1
}

run_compose() {
    # 统一封装 compose 调用，避免每个分支重复拼接 env-file 与 compose 文件路径。
    docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" "$@"
}

COMMAND=${1:-help}
SERVICE_NAME=${2:-}

case "$COMMAND" in
    help)
        print_usage stdout
        ;;
    start)
        ensure_env_file
        run_compose up -d
        ;;
    stop)
        ensure_env_file
        run_compose stop
        ;;
    restart)
        ensure_env_file
        run_compose restart ${SERVICE_NAME:+"$SERVICE_NAME"}
        ;;
    status)
        ensure_env_file
        run_compose ps
        ;;
    logs)
        ensure_env_file
        if [ -n "$SERVICE_NAME" ]; then
            run_compose logs -f "$SERVICE_NAME"
        else
            run_compose logs -f
        fi
        ;;
    *)
        print_usage
        exit 1
        ;;
esac
