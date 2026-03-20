# erp-centralservice-client

## 定位

`erp-centralservice-client` 是 CentralService SDK 的 Python 客户端接入包，对应仓库目录 `TaiChi/SDK/CentralService/python/client`，用于封装服务列表、服务发现以及网络状态查询与评估能力。

## 包名 / 模块名

- 分发包名：`erp-centralservice-client`
- 导入模块名：`erp_centralservice_client`
- 主要导出：`CentralServiceDiscoveryClient`、`CentralServiceError`、`calculate_network_score`、`default_base_url`

## 环境要求

- 需要 `Python 3.10+`。
- 需要可访问的 CentralService 根地址；示例默认使用 `http://127.0.0.1:5000`。
- 如需从环境变量读取地址，约定变量名为 `CENTRAL_SERVICE_BASEURL`。

## 安装

在仓库根目录执行本地安装：

```bash
python -X utf8 -m pip install "./TaiChi/SDK/CentralService/python/client"
```

若已完成打包，也可安装 `dist` 目录中的分发文件：

```bash
python -X utf8 -m pip install "./TaiChi/SDK/CentralService/dist/python/client/<package-file>"
```

## 快速示例

```python
from erp_centralservice_client import (
    CentralServiceDiscoveryClient,
    CentralServiceError,
    calculate_network_score,
    default_base_url,
)

client = CentralServiceDiscoveryClient(default_base_url())

try:
    service_list = client.list("demo-service")
    selected = client.discover_best("demo-service")
    network = client.get_network(selected["id"])

    print("services:", len(service_list["services"]))
    print("score:", calculate_network_score(network))
except CentralServiceError as exc:
    print(exc)
```

## 构建

仓库统一构建命令：

```powershell
python -X utf8 "TaiChi/SDK/CentralService/scripts/sdk.py" -Build -Languages python -SdkKinds client
```

## 打包

仓库统一打包命令：

```powershell
python -X utf8 "TaiChi/SDK/CentralService/scripts/sdk.py" -Pack -Languages python -SdkKinds client
```

## 验证

在 `TaiChi/SDK/CentralService/python/client` 目录执行：

```bash
python -X utf8 -c "from erp_centralservice_client import CentralServiceDiscoveryClient, CentralServiceError, calculate_network_score, default_base_url; print(CentralServiceDiscoveryClient.__name__, CentralServiceError.__name__, callable(default_base_url), callable(calculate_network_score))"
```

如需做源码级快速检查，也可执行：

```bash
python -X utf8 -m compileall "erp_centralservice_client"
```

## 相关链接

- 根说明：[`../../README.md`](../../README.md)
- 客户端契约：[`../../contract/discovery-api.md`](../../contract/discovery-api.md)
- 统一脚本：[`../../scripts/sdk.py`](../../scripts/sdk.py)
