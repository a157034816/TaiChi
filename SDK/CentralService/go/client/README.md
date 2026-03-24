# CentralService Go Client

## 定位

- 面向服务消费方的 Go SDK，用于读取服务列表、执行服务发现、查询与刷新网络评估结果。
- 服务注册、心跳与注销不在当前包内，请使用 `../service/README.md`。

## 包名

- Module path：`ensoai.local/centralservice-client`
- Package name：`centralservice`
- 主要入口类型：`Options`、`DiscoveryClient`、`ServiceListResponse`、`ServiceInfo`、`ServiceNetworkStatus`

## 环境要求

- `Go 1.20+`
- 可访问 CentralService HTTP API，例如 `http://127.0.0.1:5000`
- 适合私有仓库、`replace` 或 `go work` 场景

## 安装/引用

- 当前 module path 为 `ensoai.local/centralservice-client`，适合在内部仓库或本地工作区中使用。
- 如果你的业务工程与 SDK 不在同一个仓库，建议在消费方 `go.mod` 中显式加入 `replace`。

```go
require ensoai.local/centralservice-client v0.0.0

replace ensoai.local/centralservice-client => ../client
```

```go
import (
    clientsdk "ensoai.local/centralservice-client"
)
```

## 快速示例

```go
package main

import (
    "context"

    clientsdk "ensoai.local/centralservice-client"
)

func main() {
    opts := clientsdk.NewOptions("http://127.0.0.1:5000")
    client := clientsdk.NewDiscoveryClient(opts)
    ctx := context.Background()

    listed, err := client.List(ctx, "SdkE2E")
    if err != nil {
        panic(err)
    }
    _ = listed

    rr, err := client.DiscoverRoundRobin(ctx, "SdkE2E")
    if err != nil {
        panic(err)
    }
    _, _ = client.DiscoverWeighted(ctx, "SdkE2E")
    _, _ = client.DiscoverBest(ctx, "SdkE2E")
    _, _ = client.GetNetwork(ctx, rr.Id)
    _, _ = client.EvaluateNetwork(ctx, rr.Id)
    _, _ = client.GetNetworkAll(ctx)
}
```

## 构建

当前目录的最小构建验证：

```powershell
go build ./...
```

如需与仓库统一口径保持一致，建议在仓库根目录执行：

```powershell
python -X utf8 "TaiChi/SDK/CentralService/scripts/sdk.py" -Build -Languages go -SdkKinds client
```

## 打包

Go SDK 的发布口径以根级统一脚本为准，产物会输出到 `../../dist/go/client/`：

```powershell
python -X utf8 "TaiChi/SDK/CentralService/scripts/sdk.py" -Pack -Languages go -SdkKinds client
```

## 验证

- 当前包的快速验证以 `go build ./...` 为准。
- 完整联调可参考上级目录示例 `../examples/e2e/main.go`。

```powershell
go build ./...
```

## 相关链接

- [`../README.md`](../README.md)
- [`../examples/e2e/main.go`](../examples/e2e/main.go)
- [`../service/README.md`](../service/README.md)
