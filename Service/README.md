# Service 目录说明（中心服务重构）

本目录用于承载“服务端/中心服务相关”的独立项目，避免与根目录下的历史结构混杂。

## 目录结构

- `CentralService/`：中心服务（.NET 10，纯 WebAPI）
  - 运行时 API：服务自注册/心跳/注销、服务发现与网络评估（保持既有 `/api/Service/*` 契约）
  - 管理端 API：Cookie 登录、RBAC、审计、配置版本/发布回滚、监控汇总（`/api/admin/*`）
- `../SDK/CentralService/`：多语言中心服务 SDK，统一提供 service/client 两类接入；`.NET` 入口位于 `dotnet/src/CentralService.Service/` 与 `dotnet/src/CentralService.Client/`
- `central-service-admin-site/`：中心服务管理站点（Next.js + Tailwind CSS + shadcn/ui）

## 运行后端（CentralService）

默认启动地址（见 `TaiChi/Service/CentralService/Properties/launchSettings.json`）：

- HTTP：`http://localhost:15700`
- HTTPS：`https://localhost:25700`（同时也会监听 HTTP 15700）

启动命令：

```powershell
cd TaiChi/Service/CentralService
dotnet run --launch-profile http
```

### CentralService 托管 admin site（默认启用）

`TaiChi/Service/CentralService/appsettings.json` 默认启用了 `ManagedWebApps`，中心服务启动后会自动拉起并守护 `central-service-admin-site`：

- 托管站点：`central-service-admin-site`
- 工作目录：`TaiChi/Service/central-service-admin-site`
- 启动命令：`npm run start -- --hostname 0.0.0.0 --port 3000`
- 注入环境变量：`CENTRAL_SERVICE_BASE_URL=http://127.0.0.1:15700`
- 健康检查：`http://127.0.0.1:3000/`

这意味着偏生产部署场景下，启动 `CentralService` 时会优先检查前端是否具备生产产物，再决定直接拉起站点还是先补执行构建。

在 Windows 上，`CentralService` 还会把托管站点进程绑定到宿主 `Job Object`。这意味着如果中心服务因为崩溃、被强制结束等不可抗力直接死亡，`central-service-admin-site` 及其派生子进程也会被操作系统一并终止，不会继续以“孤儿进程”形式残留。

前置条件：

- 首次部署前在 `TaiChi/Service/central-service-admin-site` 执行 `npm install`
- 确保 `3000` 端口未被其他进程占用

默认情况下：

- 如果检测到 `.next/BUILD_ID` 已存在，中心服务会直接执行 `npm run start`
- 如果检测到 `.next/BUILD_ID` 缺失，中心服务会先执行 `npm run build`，成功后再执行 `npm run start`

仍然建议在发布流程中预先执行 `npm run build`，这样首启更快，也更便于提前暴露构建问题。

如果你要单独调试前端 `dev server`，建议暂时关闭默认托管：

```powershell
dotnet run --launch-profile http -- --ManagedWebApps:Definitions:0:Enabled=false
```

### 后台管理数据库（默认 SQLite）

默认位置：`TaiChi/Service/CentralService/data/central-service-admin.db`（可通过配置 `CentralServiceAdminDb:ConnectionString` 覆盖）。

### 首次管理员账号

两种方式（二选一）：

1) **开发环境本机初始化**（默认启用）：调用 `POST /api/auth/bootstrap`（仅允许本机回环地址、且“无任何用户”时可用）。
2) **配置种子账号**：设置配置项 `CentralServiceAdminSeed:Enabled=true`，并提供 `CentralServiceAdminSeed:AdminUsername`、`CentralServiceAdminSeed:AdminPassword`。

补充说明：

- 当 `ASPNETCORE_ENVIRONMENT` 不是 `Development` 时，`/api/auth/bootstrap` 默认关闭
- 此时登录页会显示状态说明并禁用「首次初始化」按钮
- 若你确实需要在非 Development 环境开放该入口，必须显式配置 `CentralServiceAuth:Bootstrap:Enabled=true`

## 单独运行前端（central-service-admin-site）

前端通过 Next.js `rewrites` 代理到后端，推荐同源访问以避免 CORS / SameSite 复杂度。只有在你需要单独调试站点时，才建议手动启动前端。

```powershell
cd TaiChi/Service/central-service-admin-site
copy .env.example .env.local
npm install
npm run dev
```

关键环境变量：

- `CENTRAL_SERVICE_BASE_URL`：后端服务地址（例如 `http://localhost:15700`）

更多说明见：`TaiChi/Service/central-service-admin-site/README.md`

## 监控与健康

- `GET /health`：基础健康检查
- `GET /api/admin/monitoring/summary`：关键指标汇总（服务数量/故障数/网络评分/后台任务状态）
- `GET /api/admin/monitoring/health`：HealthChecks 详情（需登录且具备 `centralservice.monitoring.read` 权限）
- `BackgroundTasks` 中的 `ManagedWebApp:central-service-admin-site`：表示前端托管进程当前健康状态
