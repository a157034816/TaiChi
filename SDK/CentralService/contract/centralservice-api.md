# CentralService API 契约（兼容入口）

本目录的契约文档已按职责拆分为两个独立文件：

- `service` 项目：`contract/service-api.md`
- `client` 项目：`contract/discovery-api.md`

## 接口归属

### Service

- `POST /api/Service/register`
- `POST /api/Service/heartbeat`
- `DELETE /api/Service/deregister/{id}`

### Client

- `GET /api/Service/list`
- `GET /api/ServiceDiscovery/discover/roundrobin/{serviceName}`
- `GET /api/ServiceDiscovery/discover/weighted/{serviceName}`
- `GET /api/ServiceDiscovery/discover/best/{serviceName}`
- `GET /api/ServiceDiscovery/network/all`
- `GET /api/ServiceDiscovery/network/{serviceId}`
- `POST /api/ServiceDiscovery/network/evaluate/{serviceId}`

## 实现基准

- `TaiChi/Service/CentralService/Controllers/ServiceController.cs`
- `TaiChi/Service/CentralService/Controllers/ServiceDiscoveryController.cs`

如需查看完整字段、错误响应与示例，请直接阅读拆分后的两份契约文档。
