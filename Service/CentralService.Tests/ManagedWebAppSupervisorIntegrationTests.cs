using System.Net.Http.Json;
using System.Text;
using CentralService.Admin.Models;
using CentralService.Services;
using CentralService.Service.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CentralService.Tests;

/// <summary>
/// 托管 WebApp 监督器（ManagedWebAppSupervisor）集成测试：
/// 验证按配置启动外部站点，并在监控接口中呈现健康状态。
/// </summary>
/// <remarks>
/// 该测试依赖 Windows 平台与 PowerShell，因此在非 Windows 环境会直接跳过。
/// </remarks>
public sealed class ManagedWebAppSupervisorIntegrationTests
{
    /// <summary>
    /// 验证按配置启动托管站点后：
    /// 1. 后台任务可在 summary 中查询到且为 Healthy；
    /// 2. health 接口中包含对应检查项；
    /// 3. Prepare 脚本能够生成 required path（用于就绪判定）。
    /// </summary>
    [Fact]
    public async Task ManagedWebAppSupervisor_StartsConfiguredSite_AndReportsHealthy()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var managedPort = LoopbackHttpServer.GetUnusedPort();
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"central-service-managed-site-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var prepareScriptPath = Path.Combine(tempDirectory, "prepare-site.ps1");
            var scriptPath = Path.Combine(tempDirectory, "managed-site.ps1");
            await File.WriteAllTextAsync(prepareScriptPath, BuildPrepareSiteScript(), Encoding.UTF8);
            await File.WriteAllTextAsync(scriptPath, BuildManagedSiteScript(), Encoding.UTF8);

            var additionalSettings = BuildManagedSiteSettings(prepareScriptPath, scriptPath, tempDirectory, managedPort);
            using var factory = new CentralServiceWebApplicationFactory(
                additionalSettings: additionalSettings,
                configureServices: services =>
                {
                    services.RemoveAll<IManagedProcessLifetimeBinder>();
                    services.AddSingleton<IManagedProcessLifetimeBinder, NoOpManagedProcessLifetimeBinder>();
                });
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true,
            });

            await LoginAsync(client, factory.AdminUsername, factory.AdminPassword);

            var summaryTask = await WaitUntilAsync(async () =>
            {
                var payload = await client.GetFromJsonAsync<ApiResponse<MonitoringSummaryResponse>>("/api/admin/monitoring/summary");
                return payload?.Data?.BackgroundTasks
                    .FirstOrDefault(x => x.TaskName == "ManagedWebApp:central-service-admin-site");
            });

            Assert.NotNull(summaryTask);
            Assert.True(summaryTask!.IsHealthy);

            var healthItem = await WaitUntilAsync(async () =>
            {
                var payload = await client.GetFromJsonAsync<ApiResponse<MonitoringHealthResponse>>("/api/admin/monitoring/health");
                return payload?.Data?.Checks
                    .FirstOrDefault(x => x.Name == "central_service_background_tasks");
            });

            Assert.NotNull(healthItem);
            Assert.Equal("Healthy", healthItem!.Status);
            Assert.True(File.Exists(Path.Combine(tempDirectory, ".next", "BUILD_ID")));
        }
        finally
        {
            try
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
            catch
            {
                // 忽略清理过程中的异常（例如文件被占用）。
            }
        }
    }

    /// <summary>
    /// 调用登录接口，建立管理员 Cookie 会话（用于访问管理端监控 API）。
    /// </summary>
    /// <param name="client">测试用 HttpClient。</param>
    /// <param name="username">用户名。</param>
    /// <param name="password">密码。</param>
    private static async Task LoginAsync(HttpClient client, string username, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password));
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// 构造托管站点配置（ManagedWebApps:Definitions）所需的设置键值对。
    /// </summary>
    /// <param name="prepareScriptPath">Prepare 脚本路径（用于生成 required paths）。</param>
    /// <param name="startScriptPath">启动脚本路径。</param>
    /// <param name="workingDirectory">站点工作目录。</param>
    /// <param name="port">站点监听端口。</param>
    /// <returns>可直接注入到 IConfiguration 的设置字典。</returns>
    private static Dictionary<string, string?> BuildManagedSiteSettings(
        string prepareScriptPath,
        string startScriptPath,
        string workingDirectory,
        int port)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ManagedWebApps:Definitions:1:Name"] = "central-service-admin-site",
            ["ManagedWebApps:Definitions:1:Enabled"] = "true",
            ["ManagedWebApps:Definitions:1:WorkingDirectory"] = workingDirectory,
            ["ManagedWebApps:Definitions:1:Command"] = "powershell",
            ["ManagedWebApps:Definitions:1:Arguments:0"] = "-NoLogo",
            ["ManagedWebApps:Definitions:1:Arguments:1"] = "-NonInteractive",
            ["ManagedWebApps:Definitions:1:Arguments:2"] = "-ExecutionPolicy",
            ["ManagedWebApps:Definitions:1:Arguments:3"] = "Bypass",
            ["ManagedWebApps:Definitions:1:Arguments:4"] = "-File",
            ["ManagedWebApps:Definitions:1:Arguments:5"] = startScriptPath,
            ["ManagedWebApps:Definitions:1:Arguments:6"] = "-Port",
            ["ManagedWebApps:Definitions:1:Arguments:7"] = port.ToString(),
            ["ManagedWebApps:Definitions:1:HealthCheckUrl"] = $"http://127.0.0.1:{port}/",
            ["ManagedWebApps:Definitions:1:StartupTimeoutSeconds"] = "20",
            ["ManagedWebApps:Definitions:1:HealthCheckTimeoutSeconds"] = "2",
            ["ManagedWebApps:Definitions:1:MaxConsecutiveHealthFailures"] = "2",
            ["ManagedWebApps:Definitions:1:RestartDelaySeconds"] = "1",
            ["ManagedWebApps:PollIntervalSeconds"] = "1",
        };

        ConfigurePowerShellScript(settings, "ManagedWebApps:Definitions:1:Prepare", prepareScriptPath);
        settings["ManagedWebApps:Definitions:1:RequiredPaths:0"] = ".next/BUILD_ID";

        return settings;
    }

    /// <summary>
    /// 在限定时间内轮询异步获取器，直到返回非 null 或超时。
    /// </summary>
    /// <typeparam name="T">引用类型返回值。</typeparam>
    /// <param name="fetcher">异步获取器。</param>
    /// <returns>获取到的对象；超时返回 null。</returns>
    private static async Task<T?> WaitUntilAsync<T>(Func<Task<T?>> fetcher)
        where T : class
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        Exception? lastError = null;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var value = await fetcher();
                if (value != null)
                {
                    return value;
                }
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            await Task.Delay(500);
        }

        if (lastError != null)
        {
            throw lastError;
        }

        return null;
    }

    /// <summary>
    /// 构造托管站点脚本：启动一个 HttpListener 并持续响应 "ok"。
    /// </summary>
    /// <returns>PowerShell 脚本文本。</returns>
    private static string BuildManagedSiteScript()
    {
        return """
param([int]$Port)

$listener = [System.Net.HttpListener]::new()
$listener.Prefixes.Add("http://127.0.0.1:$Port/")
$listener.Start()

try {
    while ($listener.IsListening) {
        $context = $listener.GetContext()
        $buffer = [System.Text.Encoding]::UTF8.GetBytes("ok")
        $context.Response.StatusCode = 200
        $context.Response.ContentType = "text/plain; charset=utf-8"
        $context.Response.OutputStream.Write($buffer, 0, $buffer.Length)
        $context.Response.OutputStream.Close()
        $context.Response.Close()
    }
}
finally {
    $listener.Stop()
    $listener.Close()
}
""";
    }

    /// <summary>
    /// 构造 Prepare 脚本：生成 <c>.next/BUILD_ID</c> 以满足 required paths 的就绪判定。
    /// </summary>
    /// <returns>PowerShell 脚本文本。</returns>
    private static string BuildPrepareSiteScript()
    {
        return """
$buildDirectory = Join-Path $PSScriptRoot '.next'
New-Item -ItemType Directory -Path $buildDirectory -Force | Out-Null
Set-Content -Path (Join-Path $buildDirectory 'BUILD_ID') -Value 'test-build' -Encoding UTF8
""";
    }

    /// <summary>
    /// 按约定的参数格式，把脚本执行信息写入配置字典。
    /// </summary>
    /// <param name="settings">配置字典。</param>
    /// <param name="prefix">键前缀（例如 "ManagedWebApps:Definitions:1:Prepare"）。</param>
    /// <param name="scriptPath">脚本路径。</param>
    private static void ConfigurePowerShellScript(
        IDictionary<string, string?> settings,
        string prefix,
        string scriptPath)
    {
        settings[$"{prefix}Command"] = "powershell";
        settings[$"{prefix}Arguments:0"] = "-NoLogo";
        settings[$"{prefix}Arguments:1"] = "-NonInteractive";
        settings[$"{prefix}Arguments:2"] = "-ExecutionPolicy";
        settings[$"{prefix}Arguments:3"] = "Bypass";
        settings[$"{prefix}Arguments:4"] = "-File";
        settings[$"{prefix}Arguments:5"] = scriptPath;
    }
}
