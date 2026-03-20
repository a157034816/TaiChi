# CentralService Java Service

## 定位

- 面向需要向中心服务注册自身、发送心跳、主动注销实例的服务提供方。
- 仅覆盖 `register`、`heartbeat`、`deregister` 三类能力；服务发现与网络评估请使用兄弟包 `../client/README.md`。

## 包名

- Java package：`erp.centralservice.service`
- 主要入口类型：`CentralServiceServiceClient`
- 配置类型：`CentralServiceSdkOptions`
- 源码目录：`src/main/java`

## 环境要求

- `JDK 8+`
- 可访问 CentralService HTTP API，例如 `http://127.0.0.1:5000`
- 无第三方依赖，全部基于 JDK 标准库实现

## 安装/引用

- 当前包以源码目录形式维护，可直接把 `src/main/java` 加入你的工程 source set。
- 如果要以产物引用，先在当前目录编译，再把生成的 JAR 加入项目 classpath。

```powershell
$files = Get-ChildItem -Recurse -Filter *.java -Path "src/main/java" | ForEach-Object { $_.FullName }
Remove-Item -Recurse -Force ".\build" -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path ".\build\classes" -Force | Out-Null
javac -encoding UTF-8 -source 8 -target 8 -d ".\build\classes" $files
```

## 快速示例

```java
import erp.centralservice.service.CentralServiceSdkOptions;
import erp.centralservice.service.CentralServiceServiceClient;
import erp.centralservice.service.models.ServiceRegistrationRequest;
import erp.centralservice.service.models.ServiceRegistrationResponse;

import java.util.HashMap;
import java.util.Map;

CentralServiceSdkOptions options = new CentralServiceSdkOptions("http://127.0.0.1:5000");
CentralServiceServiceClient client = new CentralServiceServiceClient(options);

ServiceRegistrationRequest request = new ServiceRegistrationRequest();
request.id = "";
request.name = "SdkE2E";
request.host = "127.0.0.1";
request.port = 18083;
request.serviceType = "Web";
request.healthCheckUrl = "/health";
request.healthCheckPort = 0;
request.healthCheckType = "Http";
request.weight = 1;

Map<String, String> metadata = new HashMap<String, String>();
metadata.put("sdk", "java");
request.metadata = metadata;

ServiceRegistrationResponse response = client.register(request);
client.heartbeat(response.id);
client.deregister(response.id);
```

## 构建

```powershell
$files = Get-ChildItem -Recurse -Filter *.java -Path "src/main/java" | ForEach-Object { $_.FullName }
Remove-Item -Recurse -Force ".\build" -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path ".\build\classes" -Force | Out-Null
javac -encoding UTF-8 -source 8 -target 8 -d ".\build\classes" $files
```

## 打包

```powershell
New-Item -ItemType Directory -Path ".\dist" -Force | Out-Null
jar cf ".\dist\centralservice-java-service.jar" -C ".\build\classes" .
```

## 验证

- 当前包的单包验证以编译通过为准。
- 如果要验证完整的“注册 → 心跳 → 发现 → 网络评估 → 注销”链路，请切到 `../` 目录并运行端到端示例。

```powershell
$serviceFiles = Get-ChildItem -Recurse -Filter *.java -Path "service/src/main/java" | ForEach-Object { $_.FullName }
$clientFiles = Get-ChildItem -Recurse -Filter *.java -Path "client/src/main/java" | ForEach-Object { $_.FullName }
$exampleFiles = Get-ChildItem -Filter *.java -Path "examples" | ForEach-Object { $_.FullName }

Remove-Item -Recurse -Force ".\build" -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path ".\build\service",".\build\client",".\build\e2e" -Force | Out-Null

javac -encoding UTF-8 -source 8 -target 8 -d ".\build\service" $serviceFiles
javac -encoding UTF-8 -source 8 -target 8 -d ".\build\client" $clientFiles
javac -encoding UTF-8 -source 8 -target 8 -cp ".\build\service;.\build\client" -d ".\build\e2e" $exampleFiles

java -cp ".\build\service;.\build\client;.\build\e2e" E2EMain
```

## 相关链接

- `../README.md`
- `../examples/E2EMain.java`
- `../client/README.md`
