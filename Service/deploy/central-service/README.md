# CentralService（中心服务）Docker Compose 一键部署

本目录提供中心服务家族的 **Docker Compose 一键部署**（前后端拆分容器 + Caddy 统一反代 + Let’s Encrypt 自动签证书）。

部署结果：

- 管理站点：`https://centralservice.y-bf.lol/`
- 后端 API（HTTPS）：`https://api.y-bf.lol`
- 后端直连端口（HTTP）：`http://<服务器IP>:15700`（用于外部服务注册/心跳等直连场景）

## 前置条件

1) DNS 解析

- `centralservice.y-bf.lol` 指向服务器公网 IP
- `api.y-bf.lol` 指向服务器公网 IP

2) 端口开放

- `80`、`443`：用于 Caddy + Let’s Encrypt
- `15700`：如果你需要外部服务通过宿主机端口直连注册，则需开放；否则可删掉 compose 里的端口映射

3) Docker 与 Compose

- 安装 Docker（Linux 服务器建议 Docker Engine + Compose v2）

## 一键启动

在本目录执行：

```bash
cp .env.example .env
```

编辑 `.env`，至少修改：

- `LETSENCRYPT_EMAIL`
- `CENTRAL_ADMIN_PASSWORD`
- `PROXY_SECRET`

启动：

```bash
docker compose up -d --build
```

## 离线发布到服务器

如果你的服务器不方便直接拉源码构建，可在本机导出离线发布包：

```powershell
cd TaiChi/Service/deploy/central-service
powershell -ExecutionPolicy Bypass -File .\export-release.ps1
```

默认会先按当前源码重建 `centralservice` 与 `admin-site` 镜像，再导出离线包，避免把旧标签镜像带到服务器。

导出后，`release/` 目录会包含：

- `central-service-images.tar`：后端 / 前端 / Caddy 镜像归档
- `central-service-images.sha256`：镜像归档校验值
- `compose.release.yml`：服务器使用的镜像版 Compose
- `Caddyfile`、`.env.example`、`deploy.sh`、`manage.sh`、`README.md`

将整个 `release/` 目录上传到服务器后执行：

```bash
cd /path/to/release
chmod +x ./deploy.sh
chmod +x ./manage.sh
./deploy.sh
```

首次执行会自动生成 `.env` 并提示你补齐 `CENTRAL_DOMAIN`、`API_DOMAIN`、邮箱、管理员密码与代理密钥；编辑完成后再次执行 `./deploy.sh` 即可完成镜像导入与启动。

部署完成后，可使用一键管理脚本：

```bash
./manage.sh
./manage.sh help
./manage.sh start
./manage.sh stop
./manage.sh restart
./manage.sh status
./manage.sh logs
./manage.sh logs caddy
```

其中 `./manage.sh` 在不带参数时会直接显示帮助信息，方便服务器上快速查看可用命令。

## 验证

```bash
curl -I https://api.y-bf.lol/health
curl -I https://centralservice.y-bf.lol/
```

若希望验证宿主机直连（HTTP，不走 TLS）：

```bash
curl -I http://127.0.0.1:15700/health
```

## 数据持久化

后端数据目录挂载到本目录的 `./data`（含 SQLite 与 service-circuit 文件）：

- `./data/central-service-admin.db`（默认）
- `./data/service-circuit.toml`
- `./data/service-circuit.services.json`

备份只需打包 `./data`。

## 安全说明（重要）

- 由于你选择发布 `15700:15700` 到公网，任何人都可以直连该端口访问后端 API（HTTP）。
- 本部署通过 “代理密钥模式” 保护 `X-Forwarded-*` 头：只有来自 Caddy 且携带 `PROXY_SECRET` 的请求才会被后端采信转发信息，避免公网直连伪造头影响 Scheme/IP 判定。
- 若你不需要公网直连 15700，强烈建议移除 `ports: ["15700:15700"]` 并仅通过 `api.y-bf.lol` 或 `centralservice.y-bf.lol` 访问。
