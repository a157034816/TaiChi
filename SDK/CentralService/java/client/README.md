# CentralService Java Client

## 定位

- 面向需要读取服务列表、执行服务发现、查询网络评估结果的服务消费方。
- 不负责服务自注册、心跳与注销；这部分能力位于兄弟包 `../service/README.md`。

## 包名

- Java package：`erp.centralservice.client`
- 主要入口类型：`CentralServiceDiscoveryClient`
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
import erp.centralservice.client.CentralServiceDiscoveryClient;
import erp.centralservice.client.CentralServiceSdkOptions;
import erp.centralservice.client.models.ServiceInfo;
import erp.centralservice.client.models.ServiceListResponse;
import erp.centralservice.client.models.ServiceNetworkStatus;

CentralServiceDiscoveryClient client =
    new CentralServiceDiscoveryClient(new CentralServiceSdkOptions("http://127.0.0.1:5000"));

ServiceListResponse listed = client.list("SdkE2E");
ServiceInfo rr = client.discoverRoundRobin("SdkE2E");
ServiceInfo weighted = client.discoverWeighted("SdkE2E");
ServiceInfo best = client.discoverBest("SdkE2E");
ServiceNetworkStatus network = client.getNetwork(rr.id);
ServiceNetworkStatus refreshed = client.evaluateNetwork(rr.id);
ServiceNetworkStatus[] all = client.getNetworkAll();
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
jar cf ".\dist\centralservice-java-client.jar" -C ".\build\classes" .
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
- `../service/README.md`
