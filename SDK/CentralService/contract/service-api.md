# CentralService Service API 契约

> 适用范围：`service` SDK（服务端接入）
>
> 以实际服务端实现为准：`TaiChi/Service/CentralService/Controllers/ServiceController.cs`

## 基本约定

- BaseUrl：例如 `http://127.0.0.1:5000`
- JSON：默认 `camelCase`
- 成功响应：统一返回 `ApiResponse<T>`
- 常见失败响应：
  - `ApiResponse<object>` 风格业务错误
  - `ProblemDetails`
  - `ValidationProblemDetails`

## 接口范围

### POST `/api/Service/register`

注册服务实例。

请求：`ServiceRegistrationRequest`

响应：`ApiResponse<ServiceRegistrationResponse>`

示例文件：

- `contract/examples/service.register.request.json`
- `contract/examples/service.register.response.json`

### GET `/api/Service/heartbeat/ws?serviceId={serviceId}`（WebSocket）

服务心跳 WebSocket 通道（由周边服务主动连接）。

- 中心 -> 服务：Text `"heartbeat"`
- 服务 -> 中心：Text `"heartbeat_ok"`

中心服务会按注册时的 `heartbeatIntervalSeconds` 周期发送心跳请求；当该值为 `0` 时不会发送心跳请求。

### DELETE `/api/Service/deregister/{id}`

注销服务实例。

响应：`ApiResponse<object>`

## 错误响应样例

- `contract/examples/error.apiResponse.json`
- `contract/examples/error.problemDetails.json`
- `contract/examples/error.validationProblemDetails.json`

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

### ServiceRegistrationRequest

- `id` string，可选；为空时由服务端生成
- `name` string
- `host` string
- `localIp` string，可选，服务实例的局域网 IP；未传时服务端回退为 `host`
- `operatorIp` string，可选，服务实例的运营商 IP
- `publicIp` string，可选，服务实例的公网 IP
- `port` int
- `serviceType` string，常见值：`Web` / `Socket`
- `healthCheckUrl` string
- `healthCheckPort` int
- `heartbeatIntervalSeconds` int，可选；中心服务通过 WebSocket 向该服务发送心跳请求的频率（秒），为 `0` 时表示不发送
- `weight` int
- `metadata` object，`map<string, string>`

### ServiceRegistrationResponse

- `id` string
- `registerTimestamp` long，Unix 毫秒时间戳

### ServiceInfo

`register` 成功后的服务元数据结构与服务发现接口保持一致，SDK 侧可直接复用该模型。

- `id` string
- `name` string
- `host` string
- `localIp` string，可选，服务实例的局域网 IP
- `operatorIp` string，可选，服务实例的运营商 IP
- `publicIp` string，可选，服务实例的公网 IP
- `port` int
- `url` string，可选，由服务端派生
- `serviceType` string
- `status` int
- `healthCheckUrl` string
- `healthCheckPort` int
- `heartbeatIntervalSeconds` int，可选；为 `0` 时表示不发送心跳请求
- `registerTime` string
- `lastHeartbeatTime` string
- `weight` int
- `metadata` object
- `isLocalNetwork` bool
