# erp-centralservice-service

## 定位

`erp-centralservice-service` 是 CentralService SDK 的 Python 服务端接入包，对应仓库目录 `TaiChi/SDK/CentralService/python/service`，用于封装服务注册、心跳续约与注销流程。

## 包名 / 模块名

- 分发包名：`erp-centralservice-service`
- 导入模块名：`erp_centralservice_service`
- 主要导出：`CentralServiceServiceClient`、`CentralServiceError`、`default_base_url`

## 环境要求

- 需要 `Python 3.10+`。
- 需要可访问的 CentralService 根地址；示例默认使用 `http://127.0.0.1:5000`。
- 如需从环境变量读取地址，约定变量名为 `CENTRAL_SERVICE_BASEURL`。

## 安装

在仓库根目录执行本地安装：

```bash
python -X utf8 -m pip install "./TaiChi/SDK/CentralService/python/service"
```

若已完成打包，也可安装 `dist` 目录中的分发文件：

```bash
python -X utf8 -m pip install "./TaiChi/SDK/CentralService/dist/python/service/<package-file>"
```

## 快速示例

```python
from erp_centralservice_service import (
    CentralServiceError,
    CentralServiceServiceClient,
    default_base_url,
)

client = CentralServiceServiceClient(default_base_url())

try:
    registered = client.register(
        {
            "name": "demo-service",
            "host": "127.0.0.1",
            "port": 8080,
            "serviceType": "Web",
            "healthCheckUrl": "/health",
            "healthCheckPort": 0,
            "healthCheckType": "Http",
            "weight": 100,
            "metadata": {"sdk": "python"},
        }
    )

    client.heartbeat(registered["id"])
    client.deregister(registered["id"])
except CentralServiceError as exc:
    print(exc)
```

## 构建

仓库统一构建命令：

```powershell
python -X utf8 "TaiChi/SDK/CentralService/scripts/sdk.py" -Build -Languages python -SdkKinds service
```

## 打包

仓库统一打包命令：

```powershell
python -X utf8 "TaiChi/SDK/CentralService/scripts/sdk.py" -Pack -Languages python -SdkKinds service
```

## 验证

在 `TaiChi/SDK/CentralService/python/service` 目录执行：

```bash
python -X utf8 -c "from erp_centralservice_service import CentralServiceServiceClient, CentralServiceError, default_base_url; print(CentralServiceServiceClient.__name__, CentralServiceError.__name__, callable(default_base_url))"
```

如需做源码级快速检查，也可执行：

```bash
python -X utf8 -m compileall "erp_centralservice_service"
```

## 相关链接

- 根说明：[`../../README.md`](../../README.md)
- 服务端契约：[`../../contract/service-api.md`](../../contract/service-api.md)
- 统一脚本：[`../../scripts/sdk.py`](../../scripts/sdk.py)
