using System.Net.Http.Json;
using System.Text;
using CentralService.Admin.Models;
using CentralService.Services;
using CentralService.Service.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CentralService.Tests;

public sealed class ManagedWebAppSupervisorIntegrationTests
{
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
                // ignore
            }
        }
    }

    private static async Task LoginAsync(HttpClient client, string username, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password));
        response.EnsureSuccessStatusCode();
    }

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

    private static string BuildPrepareSiteScript()
    {
        return """
$buildDirectory = Join-Path $PSScriptRoot '.next'
New-Item -ItemType Directory -Path $buildDirectory -Force | Out-Null
Set-Content -Path (Join-Path $buildDirectory 'BUILD_ID') -Value 'test-build' -Encoding UTF8
""";
    }

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
