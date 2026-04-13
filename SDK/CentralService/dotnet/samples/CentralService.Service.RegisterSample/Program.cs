using CentralService.Service;
using CentralService.Service.Models;

// 用法:
// 1) 启动中心服务（确保 /api/Service/register、/api/Service/heartbeat/ws、/api/Service/deregister 可访问）
// 2) 设置环境变量 CENTRAL_SERVICE_BASE_URL 或直接修改 baseUrl
// 3) 运行本示例
//
// 可选环境变量:
// - CENTRAL_SERVICE_SERVICE_NAME
// - CENTRAL_SERVICE_SERVICE_LOCAL_IP
// - CENTRAL_SERVICE_SERVICE_OPERATOR_IP
// - CENTRAL_SERVICE_SERVICE_PUBLIC_IP
// - CENTRAL_SERVICE_SERVICE_PORT
// - CENTRAL_SERVICE_HEARTBEAT_INTERVAL_SECONDS

var baseUrl = Environment.GetEnvironmentVariable("CENTRAL_SERVICE_BASE_URL");
if (string.IsNullOrWhiteSpace(baseUrl))
{
    baseUrl = "http://127.0.0.1:15700";
}

var serviceName = Environment.GetEnvironmentVariable("CENTRAL_SERVICE_SERVICE_NAME");
if (string.IsNullOrWhiteSpace(serviceName))
{
    serviceName = "RegisterSampleService";
}

var localIp = Environment.GetEnvironmentVariable("CENTRAL_SERVICE_SERVICE_LOCAL_IP");
if (string.IsNullOrWhiteSpace(localIp))
{
    localIp = "127.0.0.1";
}

var operatorIp = Environment.GetEnvironmentVariable("CENTRAL_SERVICE_SERVICE_OPERATOR_IP");
if (string.IsNullOrWhiteSpace(operatorIp))
{
    operatorIp = "10.0.0.1";
}

var publicIp = Environment.GetEnvironmentVariable("CENTRAL_SERVICE_SERVICE_PUBLIC_IP");
if (string.IsNullOrWhiteSpace(publicIp))
{
    publicIp = "110.0.0.1";
}

var portText = Environment.GetEnvironmentVariable("CENTRAL_SERVICE_SERVICE_PORT");
var port = 18101;
if (!string.IsNullOrWhiteSpace(portText)
    && int.TryParse(portText, out var parsedPort)
    && parsedPort > 0)
{
    port = parsedPort;
}

var heartbeatIntervalSecondsText = Environment.GetEnvironmentVariable("CENTRAL_SERVICE_HEARTBEAT_INTERVAL_SECONDS");
var heartbeatIntervalSeconds = 10;
if (!string.IsNullOrWhiteSpace(heartbeatIntervalSecondsText)
    && int.TryParse(heartbeatIntervalSecondsText, out var parsedHeartbeatIntervalSeconds)
    && parsedHeartbeatIntervalSeconds >= 0)
{
    heartbeatIntervalSeconds = parsedHeartbeatIntervalSeconds;
}

var options = new CentralServiceSdkOptions(baseUrl)
{
    Timeout = TimeSpan.FromSeconds(5),
};

using var client = new CentralServiceServiceClient(options);

Console.WriteLine($"BaseUrl: {baseUrl}");
Console.WriteLine($"Registering: name={serviceName} localIp={localIp} operatorIp={operatorIp} publicIp={publicIp} port={port}");

string serviceId;
try
{
    var response = client.Register(new ServiceRegistrationRequest
    {
        Name = serviceName,
        Host = localIp,
        LocalIp = localIp,
        OperatorIp = operatorIp,
        PublicIp = publicIp,
        Port = port,
        ServiceType = "Web",
        HealthCheckUrl = "/health",
        HeartbeatIntervalSeconds = heartbeatIntervalSeconds,
        Weight = 1,
        Metadata = new Dictionary<string, string>
        {
            ["sample"] = "CentralService.Service.RegisterSample",
            ["pid"] = Environment.ProcessId.ToString(),
        },
    });

    serviceId = response.Id;
    Console.WriteLine($"Registered: id={serviceId}");
}
catch (Exception ex)
{
    Console.WriteLine($"Register failed: {ex}");
    return;
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    var heartbeatClient = new CentralServiceHeartbeatWebSocketClient(baseUrl, serviceId, ignoreSslErrors: true);
    heartbeatClient.HeartbeatRequested += atUtc =>
        Console.WriteLine($"Heartbeat requested: {DateTimeOffset.Now:O} ackAtUtc={atUtc:O} id={serviceId}");

    Console.WriteLine($"Heartbeat websocket: {CentralServiceHeartbeatWebSocketProtocol.HeartbeatWebSocketPath}?serviceId={serviceId}");
    Console.WriteLine($"HeartbeatIntervalSeconds: {heartbeatIntervalSeconds}");

    await heartbeatClient.RunAsync(cts.Token);
}
catch (OperationCanceledException)
{
    // ignore
}

try
{
    client.Deregister(serviceId);
    Console.WriteLine($"Deregister ok: id={serviceId}");
}
catch (Exception ex)
{
    Console.WriteLine($"Deregister failed: {ex.Message}");
}

