# CentralService Discovery API 契约

> 适用范围：`discovery` SDK（客户端接入）
>
> 以实际服务端实现为准：`TaiChi/Service/CentralService/Controllers/ServiceController.cs` 与 `TaiChi/Service/CentralService/Controllers/ServiceDiscoveryController.cs`

## 基本约定

- BaseUrl：例如 `http://127.0.0.1:5000`
- JSON：默认 `camelCase`
- 响应差异：
  - `GET /api/Service/list`：成功时返回 `ApiResponse<ServiceListResponse>`
  - `/api/ServiceDiscovery/*`：成功时直接返回模型（`ServiceInfo` / `ServiceNetworkStatus` / 数组）
  - `ServiceDiscoveryController` 失败时常返回纯字符串
  - `/api/Service/list` 模型绑定失败时可能返回 `ValidationProblemDetails`

## 接口范围

### GET `/api/Service/list?name={name?}`

返回当前已注册服务列表。

响应：`ApiResponse<ServiceListResponse>`

### GET `/api/ServiceDiscovery/discover/roundrobin/{serviceName}`

按轮询策略选择服务。

响应：`ServiceInfo`

失败时可能返回纯字符串，例如：

- 服务名称不能为空
- 未找到类型为 X 的服务

### GET `/api/ServiceDiscovery/discover/weighted/{serviceName}`

按权重策略选择服务。

响应：`ServiceInfo`

### GET `/api/ServiceDiscovery/discover/best/{serviceName}`

按综合评分选择最佳服务。

响应：`ServiceInfo`

### GET `/api/ServiceDiscovery/network/all`

返回全部服务网络状态。

响应：`ServiceNetworkStatus[]`

### GET `/api/ServiceDiscovery/network/{serviceId}`

返回指定服务网络状态。

响应：`ServiceNetworkStatus`

### POST `/api/ServiceDiscovery/network/evaluate/{serviceId}`

触发一次网络状态评估并返回最新结果。

响应：`ServiceNetworkStatus`

失败时可能返回纯字符串。

## 错误响应样例

- `contract/examples/error.apiResponse.json`
- `contract/examples/error.problemDetails.json`
- `contract/examples/error.validationProblemDetails.json`
- `contract/examples/error.plainText.txt`

## 模型

### ApiResponse<T>

```json
{
  "success": true,
  "errorCode": null,
  "errorMessage": null,
  "data": {}
}
```

### ServiceListResponse

- `services` `ServiceInfo[]`

### ServiceInfo

时间字段建议在 SDK 中按字符串承载，避免跨语言日期解析差异。

- `id` string
- `name` string
- `host` string
- `localIp` string，可选，服务实例的局域网 IP
- `operatorIp` string，可选，服务实例的运营商 IP
- `publicIp` string，可选，服务实例的公网 IP
- `port` int
- `url` string，可选
- `serviceType` string
- `status` int（常见值：0 / 1 / 2）
- `healthCheckUrl` string
- `healthCheckPort` int
- `heartbeatIntervalSeconds` int，可选；为 `0` 时表示不发送心跳请求
- `registerTime` string
- `lastHeartbeatTime` string
- `weight` int
- `metadata` object
- `isLocalNetwork` bool

发现规则补充：

- 服务存在 `publicIp` 时，发现接口返回的入口 `host` 为 `publicIp`
- 服务不存在 `publicIp` 时，仅当请求方 IP 与服务 `operatorIp` 相同才返回该实例，且入口 `host` 为 `localIp`

### ServiceNetworkStatus

- `serviceId` string
- `responseTime` long
- `packetLoss` double
- `lastCheckTime` string
- `consecutiveSuccesses` int
- `consecutiveFailures` int
- `isAvailable` bool

## 评分算法约定

SDK 侧 `calculateScore()` 应与服务端保持一致：

- 不可用：返回 `0`
- 响应时间评分：
  - `<= 50ms => 50`
  - `>= 1000ms => 0`
  - 中间区间线性映射到 `0..50`
- 丢包率评分：
  - `<= 0 => 50`
  - `>= 50 => 0`
  - 中间区间线性映射到 `0..50`
- 总分：两者相加，范围 `0..100`
