# CentralService SDK E2E 测试项清单

## 1. 文档目的

这份文档用于尽数列出当前仓库里 `CentralService` 与各语言 SDK 的 E2E 测试项，说明：

- 统一 E2E 场景一共有多少项
- 每个场景具体覆盖什么行为
- 每个场景被哪些语言 / 运行时变体覆盖
- 对应的入口脚本、示例程序和特殊限制是什么

本文档基于以下实际入口整理：

- 根级统一 E2E 脚本：`TaiChi/SDK/CentralService/scripts/e2e.py`
- 根级统一构建 / E2E / 打包脚本：`TaiChi/SDK/CentralService/scripts/sdk.py`
- 各语言示例入口：
  - `.NET`：`TaiChi/SDK/CentralService/dotnet/examples/Shared/CentralServiceE2ERunner.cs`
  - `net40`：`TaiChi/SDK/CentralService/dotnet/net40/examples/CentralService.Net40E2e/Program.cs`
  - `JavaScript`：`TaiChi/SDK/CentralService/javascript/examples/e2e.js`
  - `Python`：`TaiChi/SDK/CentralService/python/examples/e2e.py`
  - `Go`：`TaiChi/SDK/CentralService/go/examples/e2e/main.go`
  - `Java`：`TaiChi/SDK/CentralService/java/examples/E2EMain.java`
  - `Rust`：`TaiChi/SDK/CentralService/rust/e2e/src/main.rs`

## 2. 总览

### 2.1 统一场景数

当前统一定义的 E2E 场景共 **8 项**：

1. `smoke`
2. `service_fanout`
3. `transport_failover`
4. `business_no_failover`
5. `max_attempts`
6. `circuit_open`
7. `circuit_recovery`
8. `half_open_reopen`

### 2.2 语言 / 运行时覆盖单元

当前 E2E 语言入口分为两层：

- 脚本层默认语言组：
  - `dotnet`
  - `javascript`
  - `python`
  - `rust`
  - `java`
  - `go`
- 其中 `dotnet` 组会在脚本内部继续展开为 4 个运行时变体：
  - `dotnet`
  - `dotnet10`
  - `dotnetcore20`
  - `net40`

因此，当前实际执行单元为：

- 现代 `.NET` 变体 3 个：`dotnet` / `dotnet10` / `dotnetcore20`
- 特殊 `.NET Framework` 变体 1 个：`net40`
- 其他语言 5 个：`javascript` / `python` / `go` / `java` / `rust`

总计 **9 个语言 / 运行时执行单元**。

### 2.3 当前理论总测试条目数

如果按“场景 × 语言/运行时执行单元”来计数：

- `dotnet` / `dotnet10` / `dotnetcore20`：每个支持 8 项，共 `3 × 8 = 24`
- `javascript` / `python` / `go` / `java` / `rust`：每个支持 8 项，共 `5 × 8 = 40`
- `net40`：仅支持 `smoke`，共 `1`

理论总 E2E 执行条目数为 **65 项**。

## 3. 场景清单

| 场景名 | 目的 | 关键验证点 | 典型测试拓扑 |
|---|---|---|---|
| `smoke` | 最基础的端到端冒烟验证 | `register`、`heartbeat(ws)`、`list`、`discover roundrobin`、`discover weighted`、`discover best`、`network evaluate/get/all`、`deregister` 全链路可用 | 1 个真实 CentralService |
| `service_fanout` | 验证“服务端注册到所有中心”的业务语义 | 同一 `serviceId` 被注册到所有中心；各中心都能 `list/discover` 到共享实例；最终对所有中心执行清理 | 2 个真实 CentralService |
| `transport_failover` | 验证“仅在传输层异常时切备用” | 主中心发生传输异常后，客户端切到备用中心并成功发现服务 | 主中心为 fault proxy，备用为真实 CentralService |
| `business_no_failover` | 验证“业务失败不切备用” | 主中心返回业务失败时，客户端停留在首端点，不切到备用中心 | 2 个真实 CentralService，其中主中心没有目标服务 |
| `max_attempts` | 验证“单中心最大尝试次数” | 主中心在达到 `maxAttempts` 后才切备用；脚本会校验 fault proxy 请求次数 | 主中心为 fault proxy，备用为真实 CentralService |
| `circuit_open` | 验证“失败达到阈值后进入熔断打开状态” | 第一次调用消耗主中心允许尝试次数并触发熔断；后续调用应跳过主中心直接走备用 | 主中心为 fault proxy，备用为真实 CentralService |
| `circuit_recovery` | 验证“熔断时长后进入半开并恢复” | 熔断窗口过去后重新尝试主中心；达到恢复阈值后完全恢复 | 主中心为 fault proxy，随后恢复为可代理；备用为真实 CentralService |
| `half_open_reopen` | 验证“半开探测失败后再次熔断” | 半开阶段重新探测主中心；若再次失败，应重新熔断并继续切备用 | 主中心为 fault proxy，备用为真实 CentralService |

## 4. 各场景更具体的覆盖内容

### 4.1 `smoke`

覆盖的动作最完整，用来验证 SDK 基础能力是否整体可用：

- 服务注册成功
- WebSocket 心跳通道可用（或按注册配置禁用心跳）
- 服务列表查询成功
- 三种发现策略可用：
  - 轮询发现
  - 加权发现
  - 最佳实例发现
- 网络评分相关接口可用：
  - `EvaluateNetwork`
  - `GetNetwork`
  - `GetNetworkAll`
- 最终注销成功

### 4.2 `service_fanout`

这是这次多中心需求里最关键的“服务侧注册策略”验证项，主要覆盖：

- 同一业务实例对多个中心执行注册
- 所有中心看到的是同一个 `serviceId`
- 所有中心都能列出这个实例
- 所有中心都能用发现接口发现这个实例
- 后续对所有中心都能完成清理

### 4.3 `transport_failover`

覆盖“发现请求按优先级顺序访问中心服务，遇到传输异常才切备用”：

- 主中心优先被访问
- 主中心发生的是传输层故障，不是业务错误
- SDK 在主中心不可用时切换到备用中心
- 备用中心返回正确服务实例

### 4.4 `business_no_failover`

覆盖“业务失败不应被误判为可切换的传输失败”：

- 首端点返回业务失败
- 错误上下文仍指向首端点
- 不应自动切到备用中心

### 4.5 `max_attempts`

覆盖“单中心最大尝试次数”：

- 主中心允许在单端点内重试多次
- 达到 `maxAttempts` 后才切换到备用中心
- 脚本会额外校验 fault proxy 的请求计数，确保次数符合预期

### 4.6 `circuit_open`

覆盖“熔断器打开”：

- 主中心在连续失败达到阈值后进入 open 状态
- 下一次请求不应再打到主中心
- 应直接通过备用中心返回结果

### 4.7 `circuit_recovery`

覆盖“熔断后自动进入半开并恢复”：

- 熔断时长过去后进入半开
- 半开状态重新尝试主中心
- 达到恢复阈值后恢复为正常

### 4.8 `half_open_reopen`

覆盖“半开探测后再次失败”：

- 熔断窗口过去后进入半开
- 半开探测再次失败
- 熔断器重新打开
- 之后仍应继续使用备用中心

## 5. 语言 / 运行时覆盖矩阵

说明：

- `√` = 该语言 / 运行时实现了该场景
- `-` = 不支持或脚本层显式跳过

| 场景 | dotnet | dotnet10 | dotnetcore20 | net40 | javascript | python | go | java | rust |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| `smoke` | √ | √ | √ | √ | √ | √ | √ | √ | √ |
| `service_fanout` | √ | √ | √ | - | √ | √ | √ | √ | √ |
| `transport_failover` | √ | √ | √ | - | √ | √ | √ | √ | √ |
| `business_no_failover` | √ | √ | √ | - | √ | √ | √ | √ | √ |
| `max_attempts` | √ | √ | √ | - | √ | √ | √ | √ | √ |
| `circuit_open` | √ | √ | √ | - | √ | √ | √ | √ | √ |
| `circuit_recovery` | √ | √ | √ | - | √ | √ | √ | √ | √ |
| `half_open_reopen` | √ | √ | √ | - | √ | √ | √ | √ | √ |

## 6. 各语言入口清单

### 6.1 脚本入口

- 统一 E2E 入口：
  - `TaiChi/SDK/CentralService/scripts/e2e.py`
- 统一包装入口：
  - `TaiChi/SDK/CentralService/scripts/sdk.py -E2E`

### 6.2 `.NET`

- 共享 E2E 逻辑：
  - `TaiChi/SDK/CentralService/dotnet/examples/Shared/CentralServiceE2ERunner.cs`
- 运行时变体入口：
  - `TaiChi/SDK/CentralService/dotnet/examples/CentralService.DotNetE2e/Program.cs`
  - `TaiChi/SDK/CentralService/dotnet/examples/CentralService.DotNet10E2e/Program.cs`
  - `TaiChi/SDK/CentralService/dotnet/examples/CentralService.DotNetCore20E2e/Program.cs`
  - `TaiChi/SDK/CentralService/dotnet/net40/examples/CentralService.Net40E2e/Program.cs`

### 6.3 JavaScript

- 入口：
  - `TaiChi/SDK/CentralService/javascript/examples/e2e.js`

### 6.4 Python

- 入口：
  - `TaiChi/SDK/CentralService/python/examples/e2e.py`

### 6.5 Go

- 入口：
  - `TaiChi/SDK/CentralService/go/examples/e2e/main.go`

### 6.6 Java

- 入口：
  - `TaiChi/SDK/CentralService/java/examples/E2EMain.java`

### 6.7 Rust

- 入口：
  - `TaiChi/SDK/CentralService/rust/e2e/src/main.rs`

## 7. 特殊限制与注意事项

### 7.1 `net40` 特殊限制

`net40` 当前只支持 `smoke`。

脚本层对 `net40` 的处理是：

- 当场景不是 `smoke` 时直接跳过
- 不参与 `service_fanout / transport_failover / business_no_failover / max_attempts / circuit_open / circuit_recovery / half_open_reopen`

### 7.2 `dotnet` 语言组的展开方式

`scripts/e2e.py` 中的 `dotnet` 并不是单一运行时，而是脚本内再拆成：

- `dotnet`
- `dotnet10`
- `dotnetcore20`
- `net40`

因此在统计 E2E 覆盖时，建议把它们视为 4 个独立执行单元。

### 7.3 当前脚本使用的统一环境变量

E2E 统一通过脚本注入这些核心变量：

- `CENTRAL_SERVICE_E2E_SCENARIO`
- `CENTRAL_SERVICE_ENDPOINTS_JSON`
- `CENTRAL_SERVICE_E2E_SERVICE_NAME`
- `CENTRAL_SERVICE_E2E_SERVICE_PORT`
- `CENTRAL_SERVICE_E2E_SERVICE_ID`
- `CENTRAL_SERVICE_E2E_EXPECTED_FIRST_ID`
- `CENTRAL_SERVICE_E2E_EXPECTED_SECOND_ID`
- `CENTRAL_SERVICE_E2E_EXPECTED_THIRD_ID`
- `CENTRAL_SERVICE_TIMEOUT_MS`
- `CENTRAL_SERVICE_E2E_TIMEOUT_MS`
- `CENTRAL_SERVICE_BREAK_WAIT_SECONDS`
- `CENTRAL_SERVICE_E2E_BREAK_WAIT_SECONDS`

## 8. 推荐查看顺序

如果只是想快速理解目前有哪些 E2E，建议按这个顺序看：

1. 先看 `TaiChi/SDK/CentralService/scripts/e2e.py`
2. 再看 `.NET` 共享逻辑 `dotnet/examples/Shared/CentralServiceE2ERunner.cs`
3. 然后对照各语言示例入口
4. 最后看本表的覆盖矩阵，判断某个场景是否已被某个语言实现

## 9. 一句话结论

当前 CentralService SDK 的统一 E2E 场景共有 **8 项**，覆盖 **9 个语言 / 运行时执行单元**，理论总执行条目数为 **65 项**；其中 `net40` 仅保留 `smoke`，其余语言 / 现代 `.NET` 变体均覆盖全部 8 项。
