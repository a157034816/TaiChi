# @ensoai/erp-centralservice-client

## 定位

`@ensoai/erp-centralservice-client` 是 CentralService SDK 的 JavaScript 客户端接入包，对应仓库目录 `TaiChi/SDK/CentralService/javascript/client`，用于封装服务列表、服务发现以及网络状态查询与评估能力。

## 包名 / 模块名

- npm 包名：`@ensoai/erp-centralservice-client`
- 模块入口：`require("@ensoai/erp-centralservice-client")`
- 主要导出：`CentralServiceDiscoveryClient`、`CentralServiceError`、`calculateNetworkScore`

## 环境要求

- 需要已安装 `Node.js`，并确保 `node`、`npm` 在 `PATH` 中可用。
- 需要可访问的 CentralService 根地址；示例默认使用 `http://127.0.0.1:5000`。
- 如需从环境变量读取地址，约定变量名为 `CENTRAL_SERVICE_BASEURL`。

## 安装

在仓库根目录执行本地安装：

```bash
npm install "./TaiChi/SDK/CentralService/javascript/client"
```

若已完成打包，也可安装 `dist` 目录中的归档文件：

```bash
npm install "./TaiChi/SDK/CentralService/dist/javascript/client/<package-file>.tgz"
```

## 快速示例

```js
const {
  CentralServiceDiscoveryClient,
  CentralServiceError,
  calculateNetworkScore,
} = require("@ensoai/erp-centralservice-client");

const client = new CentralServiceDiscoveryClient({
  baseUrl: process.env.CENTRAL_SERVICE_BASEURL || "http://127.0.0.1:5000",
});

async function main() {
  try {
    const serviceList = await client.list("demo-service");
    const selected = await client.discoverBest("demo-service");
    const network = await client.getNetwork(selected.id);

    console.log("services:", serviceList.services.length);
    console.log("score:", calculateNetworkScore(network));
  } catch (error) {
    if (error instanceof CentralServiceError) {
      console.error(error.message);
      return;
    }

    throw error;
  }
}

main();
```

## 构建

统一构建入口会对源码执行可加载性验证：

```powershell
python -X utf8 "TaiChi/SDK/CentralService/scripts/sdk.py" -Build -Languages javascript -SdkKinds client
```

## 打包

仓库统一打包命令：

```powershell
python -X utf8 "TaiChi/SDK/CentralService/scripts/sdk.py" -Pack -Languages javascript -SdkKinds client
```

仅在当前包目录执行归档时，也可直接运行：

```bash
npm pack
```

## 验证

在 `TaiChi/SDK/CentralService/javascript/client` 目录执行：

```bash
node -e "const sdk = require('./src/index.js'); console.log(Object.keys(sdk))"
```

预期至少包含 `CentralServiceDiscoveryClient`、`CentralServiceError` 与 `calculateNetworkScore`。

## 相关链接

- 根说明：[`../../README.md`](../../README.md)
- 客户端契约：[`../../contract/discovery-api.md`](../../contract/discovery-api.md)
- 统一脚本：[`../../scripts/sdk.py`](../../scripts/sdk.py)
