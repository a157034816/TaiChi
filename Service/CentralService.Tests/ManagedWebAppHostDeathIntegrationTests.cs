using System.Diagnostics;
using System.Text;

namespace CentralService.Tests;

/// <summary>
/// 宿主进程异常退出的集成测试：验证 CentralService 宿主被强制终止后，托管站点及其子进程不会遗留在系统中。
/// </summary>
/// <remarks>
/// 该测试需要 Windows + PowerShell，并会启动独立 dotnet 进程与托管脚本进程。
/// 非 Windows 环境直接跳过。
/// </remarks>
public sealed class ManagedWebAppHostDeathIntegrationTests
{
    /// <summary>
    /// 验证当 CentralService 宿主进程被 Kill 时，托管站点进程及其子进程也应被正确终止。
    /// </summary>
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
                // 忽略清理过程中的异常（例如文件被占用）。
            }
        }
    }

    /// <summary>
    /// 启动一个独立的 CentralService 宿主进程，并通过命令行配置托管站点脚本与测试用数据库。
    /// </summary>
    /// <param name="servicePort">宿主监听端口。</param>
    /// <param name="managedPort">托管站点监听端口。</param>
    /// <param name="dbPath">测试用 Sqlite 数据库路径。</param>
    /// <param name="workingDirectory">托管站点工作目录。</param>
    /// <param name="prepareScriptPath">Prepare 脚本路径。</param>
    /// <param name="siteScriptPath">托管站点脚本路径。</param>
    /// <param name="managedSitePidPath">站点 PID 文件路径。</param>
    /// <param name="managedChildPidPath">站点子进程 PID 文件路径。</param>
    /// <param name="stdout">收集标准输出。</param>
    /// <param name="stderr">收集标准错误。</param>
    /// <returns>宿主进程实例。</returns>
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

        // 该集成测试会以“外部进程”的方式启动中心服务。
        // 在当前沙箱环境中，默认启用的 Windows EventLog 日志提供器会因为权限不足而抛异常，导致宿主无法正常启动。
        // 这里显式关闭 EventLog 输出，避免日志链路异常影响功能验证。
        AddCommandLineSetting(startInfo, "Logging:EventLog:LogLevel:Default", "None");
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

    /// <summary>
    /// 向 <see cref="ProcessStartInfo.ArgumentList"/> 追加一个 <c>--key value</c> 形式的命令行配置项。
    /// </summary>
    /// <param name="startInfo">进程启动信息。</param>
    /// <param name="key">配置键。</param>
    /// <param name="value">配置值。</param>
    private static void AddCommandLineSetting(ProcessStartInfo startInfo, string key, string value)
    {
        startInfo.ArgumentList.Add($"--{key}");
        startInfo.ArgumentList.Add(value);
    }

    /// <summary>
    /// 在限定时间内等待指定 URL 返回 2xx（用于等待宿主/托管站点就绪）。
    /// </summary>
    /// <param name="httpClient">HTTP 客户端。</param>
    /// <param name="url">待探测 URL。</param>
    /// <param name="timeout">超时时间。</param>
    private static async Task WaitUntilHttpOkAsync(HttpClient httpClient, string url, TimeSpan timeout)
    {
        await WaitUntilAsync(async () =>
        {
            using var response = await httpClient.GetAsync(url);
            return response.IsSuccessStatusCode;
        }, timeout, $"等待 HTTP 就绪超时: {url}");
    }

    /// <summary>
    /// 在限定时间内等待 PID 文件写入有效的进程号。
    /// </summary>
    /// <param name="path">PID 文件路径。</param>
    /// <param name="timeout">超时时间。</param>
    /// <returns>读取到的 PID。</returns>
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

    /// <summary>
    /// 判断指定 PID 的进程是否仍存活。
    /// </summary>
    /// <param name="pid">进程号。</param>
    /// <returns>存活返回 true；不存在或已退出返回 false。</returns>
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

    /// <summary>
    /// 在限定时间内轮询条件，满足则返回，否则抛出带失败消息的异常。
    /// </summary>
    /// <param name="condition">同步条件。</param>
    /// <param name="timeout">超时时间。</param>
    /// <param name="failureMessage">超时后的失败消息。</param>
    private static async Task WaitUntilAsync(
        Func<bool> condition,
        TimeSpan timeout,
        string failureMessage)
    {
        await WaitUntilAsync(() => Task.FromResult(condition()), timeout, failureMessage);
    }

    /// <summary>
    /// 在限定时间内轮询条件，满足则返回，否则抛出带失败消息的异常。
    /// </summary>
    /// <param name="condition">异步条件。</param>
    /// <param name="timeout">超时时间。</param>
    /// <param name="failureMessage">超时后的失败消息。</param>
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

    /// <summary>
    /// 尝试终止指定进程。
    /// </summary>
    /// <param name="process">进程实例。</param>
    /// <param name="entireProcessTree">是否同时终止进程树。</param>
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
            // 忽略清理过程中的异常。
        }
        finally
        {
            process.Dispose();
        }
    }

    /// <summary>
    /// 按 PID 尝试终止进程树（用于兜底清理）。
    /// </summary>
    /// <param name="pid">进程号。</param>
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
            // 忽略清理过程中的异常。
        }
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
    /// 构造托管站点脚本：启动 HttpListener 并额外拉起一个子进程，便于验证“宿主退出后子进程也被回收”。
    /// </summary>
    /// <returns>PowerShell 脚本文本。</returns>
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

$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $Port)
$listener.Start()

try {
    while ($true) {
        $client = $listener.AcceptTcpClient()

        try {
            $stream = $client.GetStream()
            $stream.ReadTimeout = 1000

            # 尝试读取并丢弃请求头，避免某些客户端在未发送/未读取时出现异常行为。
            $buffer = New-Object byte[] 4096
            try { $null = $stream.Read($buffer, 0, $buffer.Length) } catch { }

            $body = "ok"
            $bodyBytes = [System.Text.Encoding]::UTF8.GetBytes($body)
            $headers = "HTTP/1.1 200 OK`r`n" +
                       "Content-Type: text/plain; charset=utf-8`r`n" +
                       "Content-Length: $($bodyBytes.Length)`r`n" +
                       "Connection: close`r`n`r`n"
            $headerBytes = [System.Text.Encoding]::UTF8.GetBytes($headers)
            $stream.Write($headerBytes, 0, $headerBytes.Length)
            $stream.Write($bodyBytes, 0, $bodyBytes.Length)
        }
        finally {
            $client.Close()
        }
    }
}
finally {
    try {
        $listener.Stop()
    }
    catch {
    }

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
