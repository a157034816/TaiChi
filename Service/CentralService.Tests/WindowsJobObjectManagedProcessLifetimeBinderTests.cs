using System.Diagnostics;

namespace CentralService.Tests;

public sealed class WindowsJobObjectManagedProcessLifetimeBinderTests
{
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

    private static async Task WaitForProcessExitAsync(Process process, TimeSpan timeout)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        await process.WaitForExitAsync(timeoutCts.Token);
    }
}
