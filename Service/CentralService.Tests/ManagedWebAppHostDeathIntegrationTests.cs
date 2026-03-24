using System.Diagnostics;
using System.Text;

namespace CentralService.Tests;

public sealed class ManagedWebAppHostDeathIntegrationTests
{
    [Fact]
    public async Task ManagedWebAppSupervisor_ShouldTerminateManagedSite_WhenHostProcessDiesUnexpectedly()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var servicePort = LoopbackHttpServer.GetUnusedPort();
        var managedPort = LoopbackHttpServer.GetUnusedPort();
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"central-service-hostdeath-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        using var httpClient = new HttpClient();

        Process? hostProcess = null;
        int? managedSitePid = null;
        int? managedChildPid = null;

        try
        {
            var prepareScriptPath = Path.Combine(tempDirectory, "prepare-site.ps1");
            var siteScriptPath = Path.Combine(tempDirectory, "managed-site-with-child.ps1");
            var managedSitePidPath = Path.Combine(tempDirectory, "managed-site.pid");
            var managedChildPidPath = Path.Combine(tempDirectory, "managed-site-child.pid");
            var dbPath = Path.Combine(tempDirectory, "central-service-admin.db");

            await File.WriteAllTextAsync(prepareScriptPath, BuildPrepareSiteScript(), Encoding.UTF8);
            await File.WriteAllTextAsync(siteScriptPath, BuildManagedSiteScriptWithChild(), Encoding.UTF8);

            hostProcess = StartCentralServiceHost(
                servicePort,
                managedPort,
                dbPath,
                tempDirectory,
                prepareScriptPath,
                siteScriptPath,
                managedSitePidPath,
                managedChildPidPath,
                stdout,
                stderr);

            await WaitUntilHttpOkAsync(httpClient, $"http://127.0.0.1:{servicePort}/health", TimeSpan.FromSeconds(30));
            await WaitUntilHttpOkAsync(httpClient, $"http://127.0.0.1:{managedPort}/", TimeSpan.FromSeconds(30));

            managedSitePid = await WaitUntilPidFileAsync(managedSitePidPath, TimeSpan.FromSeconds(30));
            managedChildPid = await WaitUntilPidFileAsync(managedChildPidPath, TimeSpan.FromSeconds(30));

            Assert.True(IsProcessAlive(managedSitePid.Value));
            Assert.True(IsProcessAlive(managedChildPid.Value));

            hostProcess.Kill();
            await hostProcess.WaitForExitAsync();

            await WaitUntilAsync(
                () => !IsProcessAlive(managedSitePid.Value) && !IsProcessAlive(managedChildPid.Value),
                TimeSpan.FromSeconds(20),
                $"宿主异常退出后，托管站点或其子进程仍然存活。stdout:{Environment.NewLine}{stdout}{Environment.NewLine}stderr:{Environment.NewLine}{stderr}");
        }
        finally
        {
            TryKillProcess(hostProcess, entireProcessTree: true);

            if (managedSitePid is { } sitePid)
            {
                TryKillProcess(sitePid);
            }

            if (managedChildPid is { } childPid)
            {
                TryKillProcess(childPid);
            }

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

    private static Process StartCentralServiceHost(
        int servicePort,
        int managedPort,
        string dbPath,
        string workingDirectory,
        string prepareScriptPath,
        string siteScriptPath,
        string managedSitePidPath,
        string managedChildPidPath,
        StringBuilder stdout,
        StringBuilder stderr)
    {
        var assemblyPath = typeof(CentralService.Program).Assembly.Location;
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = Path.GetDirectoryName(assemblyPath) ?? AppContext.BaseDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{servicePort}";
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";

        startInfo.ArgumentList.Add(assemblyPath);
        AddCommandLineSetting(startInfo, "CentralServiceAdminDb:DatabaseType", "Sqlite");
        AddCommandLineSetting(startInfo, "CentralServiceAdminDb:ConnectionString", $"Data Source={dbPath}");
        AddCommandLineSetting(startInfo, "CentralServiceAdminSeed:Enabled", "true");
        AddCommandLineSetting(startInfo, "CentralServiceAdminSeed:AdminUsername", "admin");
        AddCommandLineSetting(startInfo, "CentralServiceAdminSeed:AdminPassword", "admin123!");
        AddCommandLineSetting(startInfo, "ManagedWebApps:Definitions:0:Enabled", "false");
        AddCommandLineSetting(startInfo, "ManagedWebApps:Definitions:1:Name", "central-service-admin-site");
        AddCommandLineSetting(startInfo, "ManagedWebApps:Definitions:1:Enabled", "true");
        AddCommandLineSetting(startInfo, "ManagedWebApps:Definitions:1:WorkingDirectory", workingDirectory);
        AddCommandLineSetting(startInfo, "ManagedWebApps:Definitions:1:Command", "powershell");
        AddCommandLineSetting(startInfo, "ManagedWebApps:Definitions:1:Arguments:0", "-NoLogo");
        AddCommandLineSetting(startInfo, "ManagedWebApps:Definitions:1:Arguments:1", "-NonInteractive");
        AddCommandLineSetting(startInfo, "ManagedWebApps:Definitions:1:Arguments:2", "-ExecutionPolicy");
        AddCommandLineSetting(startInfo, "ManagedWebApps:Definitions:1:Arguments:3", "Bypass");
        AddCommandLineSetting(startInfo, "ManagedWebApps:Definitions:1:Arguments:4", "-File");
        AddCommandLineSetting(startInfo, "ManagedWebApps:Definitions:1:Arguments:5", siteScriptPath);
        AddCommandLineSetting(startInfo, "ManagedWebApps:Definitions:1:Arguments:6", "-Port");
        AddCommandLineSetting(startInfo, "ManagedWebApps:Definitions:1:Arguments:7", managedPort.ToString());
        AddCommandLineSetting(startInfo, "ManagedWebApps:Definitions:1:Arguments:8", "-PidFile");
        AddCommandLineSetting(startInfo, "ManagedWebApps:Definitions:1:Arguments:9", managedSitePidPath);
        AddCommandLineSetting(startInfo, "ManagedWebApps:Definitions:1:Arguments:10", "-ChildPidFile");
        AddCommandLineSetting(startInfo, "ManagedWebApps:Definitions:1:Arguments:11", managedChildPidPath);
        AddCommandLineSetting(startInfo, "ManagedWebApps:Definitions:1:HealthCheckUrl", $"http://127.0.0.1:{managedPort}/");
        AddCommandLineSetting(startInfo, "ManagedWebApps:Definitions:1:StartupTimeoutSeconds", "20");
        AddCommandLineSetting(startInfo, "ManagedWebApps:Definitions:1:HealthCheckTimeoutSeconds", "2");
        AddCommandLineSetting(startInfo, "ManagedWebApps:Definitions:1:MaxConsecutiveHealthFailures", "2");
        AddCommandLineSetting(startInfo, "ManagedWebApps:Definitions:1:RestartDelaySeconds", "1");
        AddCommandLineSetting(startInfo, "ManagedWebApps:Definitions:1:RequiredPaths:0", ".next/BUILD_ID");
        AddCommandLineSetting(startInfo, "ManagedWebApps:Definitions:1:PrepareCommand", "powershell");
        AddCommandLineSetting(startInfo, "ManagedWebApps:Definitions:1:PrepareArguments:0", "-NoLogo");
        AddCommandLineSetting(startInfo, "ManagedWebApps:Definitions:1:PrepareArguments:1", "-NonInteractive");
        AddCommandLineSetting(startInfo, "ManagedWebApps:Definitions:1:PrepareArguments:2", "-ExecutionPolicy");
        AddCommandLineSetting(startInfo, "ManagedWebApps:Definitions:1:PrepareArguments:3", "Bypass");
        AddCommandLineSetting(startInfo, "ManagedWebApps:Definitions:1:PrepareArguments:4", "-File");
        AddCommandLineSetting(startInfo, "ManagedWebApps:Definitions:1:PrepareArguments:5", prepareScriptPath);
        AddCommandLineSetting(startInfo, "ManagedWebApps:PollIntervalSeconds", "1");

        var process = new Process
        {
            StartInfo = startInfo,
        };
        process.OutputDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                stdout.AppendLine(args.Data);
            }
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                stderr.AppendLine(args.Data);
            }
        };

        Assert.True(process.Start());
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private static void AddCommandLineSetting(ProcessStartInfo startInfo, string key, string value)
    {
        startInfo.ArgumentList.Add($"--{key}");
        startInfo.ArgumentList.Add(value);
    }

    private static async Task WaitUntilHttpOkAsync(HttpClient httpClient, string url, TimeSpan timeout)
    {
        await WaitUntilAsync(async () =>
        {
            using var response = await httpClient.GetAsync(url);
            return response.IsSuccessStatusCode;
        }, timeout, $"等待 HTTP 就绪超时: {url}");
    }

    private static async Task<int> WaitUntilPidFileAsync(string path, TimeSpan timeout)
    {
        var pid = 0;
        await WaitUntilAsync(() =>
        {
            if (!File.Exists(path))
            {
                return false;
            }

            var content = File.ReadAllText(path).Trim();
            return int.TryParse(content, out pid) && pid > 0;
        }, timeout, $"等待 PID 文件超时: {path}");
        return pid;
    }

    private static bool IsProcessAlive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static async Task WaitUntilAsync(
        Func<bool> condition,
        TimeSpan timeout,
        string failureMessage)
    {
        await WaitUntilAsync(() => Task.FromResult(condition()), timeout, failureMessage);
    }

    private static async Task WaitUntilAsync(
        Func<Task<bool>> condition,
        TimeSpan timeout,
        string failureMessage)
    {
        var deadline = DateTime.UtcNow.Add(timeout);
        Exception? lastError = null;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (await condition())
                {
                    return;
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
            throw new Xunit.Sdk.XunitException($"{failureMessage}{Environment.NewLine}{lastError}");
        }

        throw new Xunit.Sdk.XunitException(failureMessage);
    }

    private static void TryKillProcess(Process? process, bool entireProcessTree)
    {
        if (process == null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree);
                process.WaitForExit(5000);
            }
        }
        catch
        {
            // ignore
        }
        finally
        {
            process.Dispose();
        }
    }

    private static void TryKillProcess(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch
        {
            // ignore
        }
    }

    private static string BuildPrepareSiteScript()
    {
        return """
$buildDirectory = Join-Path $PSScriptRoot '.next'
New-Item -ItemType Directory -Path $buildDirectory -Force | Out-Null
Set-Content -Path (Join-Path $buildDirectory 'BUILD_ID') -Value 'test-build' -Encoding UTF8
""";
    }

    private static string BuildManagedSiteScriptWithChild()
    {
        return """
param(
    [int]$Port,
    [string]$PidFile,
    [string]$ChildPidFile
)

$childStartInfo = [System.Diagnostics.ProcessStartInfo]::new()
$childStartInfo.FileName = 'powershell'
$childStartInfo.UseShellExecute = $false
$childStartInfo.CreateNoWindow = $true
$childStartInfo.ArgumentList.Add('-NoLogo')
$childStartInfo.ArgumentList.Add('-NonInteractive')
$childStartInfo.ArgumentList.Add('-ExecutionPolicy')
$childStartInfo.ArgumentList.Add('Bypass')
$childStartInfo.ArgumentList.Add('-Command')
$childStartInfo.ArgumentList.Add('while ($true) { Start-Sleep -Seconds 1 }')

$child = [System.Diagnostics.Process]::Start($childStartInfo)
Set-Content -Path $PidFile -Value $PID -Encoding UTF8
Set-Content -Path $ChildPidFile -Value $child.Id -Encoding UTF8

$listener = [System.Net.HttpListener]::new()
$listener.Prefixes.Add("http://127.0.0.1:$Port/")
$listener.Start()

try {
    while ($listener.IsListening) {
        $context = $listener.GetContext()
        $buffer = [System.Text.Encoding]::UTF8.GetBytes('ok')
        $context.Response.StatusCode = 200
        $context.Response.ContentType = 'text/plain; charset=utf-8'
        $context.Response.OutputStream.Write($buffer, 0, $buffer.Length)
        $context.Response.OutputStream.Close()
        $context.Response.Close()
    }
}
finally {
    try {
        if ($listener.IsListening) {
            $listener.Stop()
        }
    }
    catch {
    }

    $listener.Close()

    try {
        if (-not $child.HasExited) {
            $child.Kill()
            $child.WaitForExit()
        }
    }
    catch {
    }
}
""";
    }
}
