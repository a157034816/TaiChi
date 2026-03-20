using CentralService.Client;
using CentralService.Client.Errors;
using CentralService.Client.Models;

// 用法:
// 1) 启动中心服务（确保 /api/ServiceAccess/resolve 与 /api/ServiceAccess/report 可访问）
// 2) 设置环境变量 CENTRAL_SERVICE_BASE_URL 或直接修改 baseUrl
// 3) 运行本示例

var baseUrl = Environment.GetEnvironmentVariable("CENTRAL_SERVICE_BASE_URL");
if (string.IsNullOrWhiteSpace(baseUrl))
{
    baseUrl = "http://127.0.0.1:15700";
}

var options = new CentralServiceSdkOptions(baseUrl)
{
    ClientIdentity = new CentralServiceClientIdentity
    {
        ClientName = "access-sample",
        LocalIp = "127.0.0.1",
        OperatorIp = "10.0.0.1",
        PublicIp = "110.0.0.1",
    }
};

using var client = new CentralServiceDiscoveryClient(options);

try
{
    // 通过中心服务 resolve -> 执行业务回调 -> report。
    // 这里用一个“模拟连接”的回调：如果服务端口是偶数就当作成功，否则当作网络失败。
    var result = await client.AccessAsync(
        "AccessService",
        context =>
        {
            if (context?.Service == null)
            {
                return Task.FromResult(ServiceAccessCallbackResult<string>.FromFailure(
                    ServiceAccessFailureKind.Unknown,
                    "resolve 未返回服务实例"));
            }

            var port = context.Service.Port;
            if (port % 2 == 0)
            {
                return Task.FromResult(ServiceAccessCallbackResult<string>.FromSuccess(
                    $"连接成功: {context.Service.Name} ({context.Service.Url})"));
            }

            return Task.FromResult(ServiceAccessCallbackResult<string>.FromFailure(
                ServiceAccessFailureKind.Transport,
                $"模拟连接失败: {context.Service.Name} ({context.Service.Url})"));
        });

    Console.WriteLine(result);
}
catch (CentralServiceAccessException ex)
{
    Console.WriteLine($"访问失败: service={ex.ServiceName} kind={ex.FailureKind} message={ex.Message}");
}
catch (Exception ex)
{
    Console.WriteLine($"异常: {ex}");
}

