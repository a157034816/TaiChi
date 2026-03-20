# 中心服务管理站点（central-service-admin-site）

中心服务管理站点，配套后端 `TaiChi/Service/CentralService`：
- 前端：Next.js（App Router）+ Tailwind CSS + shadcn/ui
- 鉴权：通过 Next.js `rewrites` 代理到后端（同源），后端使用 Cookie Authentication

## 环境要求

- Node.js 22+（建议与仓库一致）
- npm 10+

## 环境变量

创建 `.env.local`（可参考 `.env.example`）：

- `CENTRAL_SERVICE_BASE_URL`：后端 CentralService 基址（用于 Next.js 代理）
  - 默认：`http://localhost:15700`（对应 `TaiChi/Service/CentralService/Properties/launchSettings.json`）
- `AUTH_COOKIE_NAME`：鉴权 Cookie 名称（需与后端一致）
  - 默认：`CentralServiceAdmin.Auth`

## 本地开发

### 偏生产部署模式（推荐）

默认情况下，`TaiChi/Service/CentralService/appsettings.json` 已启用由中心服务托管本前端站点。部署时推荐按下面顺序执行：

1) 安装依赖并构建前端

```bash
cd TaiChi/Service/central-service-admin-site
npm install
npm run build
```

2) 直接启动中心服务

```bash
cd TaiChi/Service/CentralService
dotnet run --launch-profile http
```

启动后，`CentralService` 会自动执行：

- 工作目录：`TaiChi/Service/central-service-admin-site`
- 命令：`npm run start -- --hostname 0.0.0.0 --port 3000`
- 环境变量：`CENTRAL_SERVICE_BASE_URL=http://127.0.0.1:15700`
- 启动前检查：若缺少 `.next/BUILD_ID`，会先执行 `npm run build`

此时打开 `http://localhost:3000` 即可访问管理站点；如果前端进程异常退出，中心服务会继续尝试拉起并将状态上报到 `/api/admin/monitoring/summary`。首次启动若触发自动构建，耗时会明显长于直接启动已构建产物。

在 Windows 上，如果 `CentralService` 自身因为崩溃或被强制结束而来不及执行正常停机逻辑，系统也会通过 `Job Object` 自动回收本前端站点及其派生子进程，避免管理站点残留为孤儿进程。

### 本地前端联调模式

只有在你需要热更新调试页面时，才建议改为手动启动前端 `dev server`。

1) 启动后端（CentralService），并关闭默认托管

```bash
cd TaiChi/Service/CentralService
dotnet run --launch-profile http -- --ManagedWebApps:Definitions:0:Enabled=false
```

2) 启动前端（本项目）

```bash
cd TaiChi/Service/central-service-admin-site
npm install
npm run dev
```

打开 `http://localhost:3000`。

### 首次初始化（本机）

当后端数据库中**没有任何用户**时：
- 在登录页输入用户名/密码
- 点击「首次初始化」

后端将调用 `POST /api/auth/bootstrap` 创建首个管理员账号（仅允许本机访问）。

如果登录页提示首次初始化未启用，说明当前 `CentralService` 并不是以 `Development` 环境运行，或者后端显式关闭了 bootstrap。此时有两种做法：

- 推荐：配置 `CentralServiceAdminSeed:Enabled=true` 与种子管理员账号
- 按需启用：设置 `CentralServiceAuth:Bootstrap:Enabled=true`

## 生产构建

```bash
npm run build
npm start
```

如果不是手动托管，而是交给 `CentralService` 管理，则构建完成后通常不需要再单独执行 `npm start`；在未构建时，中心服务也会在启动链路中自动补执行一次 `npm run build`。

## 代理策略（rewrites）

`next.config.ts` 已将以下路径代理到 `CENTRAL_SERVICE_BASE_URL`：
- `/api/auth/*`
- `/api/admin/*`
- `/api/Service/*`
- `/api/ServiceDiscovery/*`
- `/health`

浏览器侧始终请求同源的 `http://localhost:3000/...`，避免 CORS 与 SameSite 的复杂度。

## 安全提示

- 不要在仓库中硬编码管理员密码/Token/SigningKey。
- 生产建议通过反向代理实现同源，或严格配置 CORS 白名单。
- 若使用中心服务托管模式，请确保至少已完成 `npm install`；未预先 `build` 时，中心服务会尝试自动执行 `npm run build`，但发布流程中仍建议提前构建以缩短首启时间并提前暴露构建失败。
