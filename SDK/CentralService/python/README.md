# CentralService Python SDK

返回根说明：[`../README.md`](../README.md)

## 项目定位

当前目录拆分为两个独立 Python 包项目：

- `service`：包名 `erp-centralservice-service`，模块名 `erp_centralservice_service`
- `client`：包名 `erp-centralservice-client`，模块名 `erp_centralservice_client`

根级 `pyproject.toml` 仅作为双项目工作区元信息，声明 `service` 与 `client` 两个项目，不提供聚合 SDK 源码入口。

## 目录结构

```text
python/
  README.md
  pyproject.toml
  scripts/
    build_dist.py
  service/
    pyproject.toml
    README.md
    erp_centralservice_service/
  client/
    pyproject.toml
    README.md
    erp_centralservice_client/
  examples/
    e2e.py
```

## 依赖

- Python `>=3.10`
- 打包依赖：`setuptools>=61`、`wheel`
- 可访问的 CentralService 实例，默认 `http://127.0.0.1:5000`

## 快速开始

### 验证 service / client 包可编译

在 `TaiChi/SDK/CentralService/python/` 下执行：

```powershell
Push-Location "service"
python -X utf8 -m compileall "erp_centralservice_service"
Pop-Location

Push-Location "client"
python -X utf8 -m compileall "erp_centralservice_client"
Pop-Location
```

### 运行端到端示例

```powershell
$env:CENTRAL_SERVICE_BASEURL = "http://127.0.0.1:5000"
$env:CENTRAL_SERVICE_TIMEOUT_MS = "800"
python -X utf8 "examples/e2e.py"

> 如需多中心联调，可改用 `CENTRAL_SERVICE_ENDPOINTS_JSON` 传入端点数组，并通过 `CENTRAL_SERVICE_E2E_SCENARIO` 选择 `smoke / service_fanout / transport_failover / business_no_failover / max_attempts / circuit_open / circuit_recovery / half_open_reopen`。
```

## 构建 / 打包 / 验证

### 本地验证

默认使用 `compileall` 作为轻量级构建验证，与统一脚本行为保持一致。

### 推荐：使用根级统一脚本

```powershell
python -X utf8 "../scripts/sdk.py" -Build -Languages "python"
python -X utf8 "../scripts/sdk.py" -E2E -Languages "python"
python -X utf8 "../scripts/sdk.py" -Pack -Languages "python"
```

### 需要单独打包时

根级统一脚本会调用：

- `scripts/build_dist.py --project-root python/service --project-name erp-centralservice-service`
- `scripts/build_dist.py --project-root python/client --project-name erp-centralservice-client`

统一打包后输出到 `../dist/python/service/` 与 `../dist/python/client/`。

## service / client 子项目

- 服务端包说明：[`service/README.md`](./service/README.md)
- 客户端包说明：[`client/README.md`](./client/README.md)

契约边界、统一入口与产物说明请参考根说明：[`../README.md`](../README.md)。
