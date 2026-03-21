using System.Diagnostics;

namespace CentralService.Tests;

/// <summary>
/// Windows JobObject 生命周期绑定器测试：验证在释放绑定器时会终止已附加的外部进程。
/// </summary>
/// <remarks>
/// 该能力仅在 Windows 上可用，因此非 Windows 环境直接跳过。
/// </remarks>
public sealed class WindowsJobObjectManagedProcessLifetimeBinderTests
{
    /// <summary>
    /// 验证释放绑定器后，被附加的进程会在合理时间内退出。
    /// </summary>
    [Fact]
    public async Task Dispose_ShouldTerminateAttachedProcess()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var binder = new CentralService.Services.WindowsJobObjectManagedProcessLifetimeBinder();
        using var process = StartSleepProcess();

        binder.Attach(process);
        Assert.False(process.HasExited);

        binder.Dispose();

        await WaitForProcessExitAsync(process, TimeSpan.FromSeconds(10));
        Assert.True(process.HasExited);
    }

    /// <summary>
    /// 启动一个长时间运行的休眠进程（用于验证 JobObject 的进程回收能力）。
    /// </summary>
    /// <returns>已启动的进程。</returns>
    private static Process StartSleepProcess()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add("Start-Sleep -Seconds 300");

        var process = new Process
        {
            StartInfo = startInfo,
        };

        Assert.True(process.Start());
        return process;
    }

    /// <summary>
    /// 等待进程在超时窗口内退出。
    /// </summary>
    /// <param name="process">待等待的进程。</param>
    /// <param name="timeout">超时时间。</param>
    private static async Task WaitForProcessExitAsync(Process process, TimeSpan timeout)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        await process.WaitForExitAsync(timeoutCts.Token);
    }
}
