# CentralService Java SDK

返回根说明：[`../README.md`](../README.md)

## 项目定位

Java 目录按双项目拆分：

- `service`：面向服务提供方，负责注册、心跳、注销
- `client`：面向服务消费方，负责列表、发现、网络状态查询/评估

当前实现面向 JDK 8，无第三方依赖；`examples/E2EMain.java` 展示 service 与 client 联合接入。

## 目录结构

```text
java/
  README.md
  service/
    README.md
    src/main/java/
  client/
    README.md
    src/main/java/
  examples/
    E2EMain.java
```

## 依赖

- JDK 8
- `javac`、`java`、`jar`
- 可访问的 CentralService 实例，默认 `http://127.0.0.1:5000`

## 快速开始

在 `TaiChi/SDK/CentralService/java/` 下执行：

```powershell
$env:CENTRAL_SERVICE_BASEURL = "http://127.0.0.1:5000"
$env:CENTRAL_SERVICE_TIMEOUT_MS = "800"

$serviceFiles = Get-ChildItem -Recurse -Filter *.java -Path "service/src/main/java" | ForEach-Object { $_.FullName }
$clientFiles = Get-ChildItem -Recurse -Filter *.java -Path "client/src/main/java" | ForEach-Object { $_.FullName }
$exampleFiles = Get-ChildItem -Filter *.java -Path "examples" | ForEach-Object { $_.FullName }

Remove-Item -Recurse -Force ".\\build" -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path ".\\build\\service",".\\build\\client",".\\build\\e2e" -Force | Out-Null

javac -encoding UTF-8 -source 8 -target 8 -d ".\\build\\service" $serviceFiles
javac -encoding UTF-8 -source 8 -target 8 -d ".\\build\\client" $clientFiles
javac -encoding UTF-8 -source 8 -target 8 -cp ".\\build\\service;.\\build\\client" -d ".\\build\\e2e" $exampleFiles

java -cp ".\\build\\service;.\\build\\client;.\\build\\e2e" E2EMain
```

如需多中心联调，可改用 `CENTRAL_SERVICE_ENDPOINTS_JSON` 传入端点数组，并通过
`CENTRAL_SERVICE_E2E_SCENARIO` 选择 `smoke / service_fanout / transport_failover / business_no_failover / max_attempts / circuit_open / circuit_recovery / half_open_reopen`。

```powershell
$env:CENTRAL_SERVICE_ENDPOINTS_JSON = '[{"baseUrl":"http://127.0.0.1:5001","priority":0,"maxAttempts":2,"circuitBreaker":{"failureThreshold":1,"breakDurationMinutes":1,"recoveryThreshold":1}},{"baseUrl":"http://127.0.0.1:5002","priority":10}]'
$env:CENTRAL_SERVICE_E2E_SCENARIO = "transport_failover"
$env:CENTRAL_SERVICE_TIMEOUT_MS = "800"
$env:CENTRAL_SERVICE_BREAK_WAIT_SECONDS = "2"

java -cp ".\\build\\service;.\\build\\client;.\\build\\e2e" E2EMain
```

- `CENTRAL_SERVICE_TIMEOUT_MS` 会同时应用到 discovery client 与 service client。
- `CENTRAL_SERVICE_BREAK_WAIT_SECONDS` / `CENTRAL_SERVICE_E2E_BREAK_WAIT_SECONDS` 可用于缩短 `circuit_recovery`、`half_open_reopen` 的半开等待。

## 构建 / 打包 / 验证

### 推荐：使用根级统一脚本

```powershell
python -X utf8 "../scripts/sdk.py" -Build -Languages "java"
python -X utf8 "../scripts/sdk.py" -E2E -Languages "java"
python -X utf8 "../scripts/sdk.py" -Pack -Languages "java"
```

### 打包产物

统一打包后输出到 `../dist/java/`：

- `service/erp-centralservice-service-java-{version}.jar`
- `client/erp-centralservice-client-java-{version}.jar`

### 验证建议

- 本地先按“快速开始”编译 service / client / example
- 联调时执行 `java -cp ".\\build\\service;.\\build\\client;.\\build\\e2e" E2EMain`
- 需要与统一流程保持一致时使用根级 `sdk.py`

## service / client 子项目

- 服务端项目说明：[`service/README.md`](./service/README.md)
- 客户端项目说明：[`client/README.md`](./client/README.md)

更多契约与跨语言入口说明请参考根说明：[`../README.md`](../README.md)。
