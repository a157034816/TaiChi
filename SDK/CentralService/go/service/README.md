# CentralService Go Service

## 定位

- 面向服务提供方的 Go SDK，用于向中心服务注册实例并主动注销；心跳检测使用 WebSocket 通道。
- 发现、列表查询与网络评估能力不在当前包中，请使用 `../client/README.md`。

## 包名

- Module path：`ensoai.local/centralservice-service`
- Package name：`centralservice`
- 主要入口类型：`Options`、`ServiceClient`、`ServiceRegistrationRequest`

## 环境要求

- `Go 1.20+`
- 可访问 CentralService HTTP API，例如 `http://127.0.0.1:5000`
- 适合私有仓库、`replace` 或 `go work` 场景

## 安装/引用

- 当前 module path 为 `ensoai.local/centralservice-service`，适合在内部仓库或本地工作区中使用。
- 如果你的业务工程与 SDK 不在同一个仓库，建议在消费方 `go.mod` 中显式加入 `replace`。

```go
require ensoai.local/centralservice-service v0.0.0

replace ensoai.local/centralservice-service => ../service
```

```go
import (
    servicesdk "ensoai.local/centralservice-service"
)
```

## 快速示例

```go
package main

import (
    "context"

    servicesdk "ensoai.local/centralservice-service"
)

func main() {
    opts := servicesdk.NewOptions("http://127.0.0.1:5000")
    svc := servicesdk.NewServiceClient(opts)
    ctx := context.Background()

    reg, err := svc.Register(ctx, servicesdk.ServiceRegistrationRequest{
        Id:              "",
        Name:            "SdkE2E",
        Host:            "127.0.0.1",
        Port:            18084,
        ServiceType:     "Web",
        HealthCheckUrl:  "/health",
        HealthCheckPort: 0,
        HeartbeatIntervalSeconds: 0,
        Weight:          1,
        Metadata:        map[string]string{"sdk": "go"},
    })
    if err != nil {
        panic(err)
    }

    if err := svc.Deregister(ctx, reg.Id); err != nil {
        panic(err)
    }
}
```

## 构建

当前目录的最小构建验证：

```powershell
go build ./...
```

如需与仓库统一口径保持一致，建议在仓库根目录执行：

```powershell
python -X utf8 "TaiChi/SDK/CentralService/scripts/sdk.py" -Build -Languages go -SdkKinds service
```

## 打包

Go SDK 的发布口径以根级统一脚本为准，产物会输出到 `../../dist/go/service/`：

```powershell
python -X utf8 "TaiChi/SDK/CentralService/scripts/sdk.py" -Pack -Languages go -SdkKinds service
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
- [`../client/README.md`](../client/README.md)
