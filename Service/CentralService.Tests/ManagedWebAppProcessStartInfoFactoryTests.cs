using System.IO;
using CentralService.Services;

namespace CentralService.Tests;

public sealed class ManagedWebAppProcessStartInfoFactoryTests
{
    [Fact]
    public void CreateForTest_CmdExtension_ShouldWrapWithCmdExe()
    {
        var comspec = @"C:\Windows\System32\cmd.exe";

        var psi = ManagedWebAppProcessStartInfoFactory.CreateForTest(
            command: "npm.cmd",
            arguments: new[] { "run", "start" },
            pathEnv: null,
            fileExists: _ => false,
            comspec: comspec);

        Assert.Equal(comspec, psi.FileName);
        Assert.False(psi.UseShellExecute);
        Assert.True(psi.RedirectStandardOutput);
        Assert.True(psi.RedirectStandardError);
        Assert.True(psi.CreateNoWindow);
        Assert.Equal(new[] { "/d", "/s", "/c", "npm.cmd", "run", "start" }, psi.ArgumentList);
    }

    [Fact]
    public void CreateForTest_NoExtensionAndExeExists_ShouldNotWrap()
    {
        var fakeBin = @"C:\fakebin";

        var psi = ManagedWebAppProcessStartInfoFactory.CreateForTest(
            command: "powershell",
            arguments: new[] { "-File", "site.ps1" },
            pathEnv: fakeBin,
            fileExists: path => string.Equals(path, Path.Combine(fakeBin, "powershell.exe"), StringComparison.OrdinalIgnoreCase),
            comspec: @"C:\Windows\System32\cmd.exe");

        Assert.Equal("powershell", psi.FileName);
        Assert.Equal(new[] { "-File", "site.ps1" }, psi.ArgumentList);
    }

    [Fact]
    public void CreateForTest_NoExtensionAndExeMissing_ShouldWrap()
    {
        var psi = ManagedWebAppProcessStartInfoFactory.CreateForTest(
            command: "npm",
            arguments: new[] { "run", "start" },
            pathEnv: @"C:\fakebin",
            fileExists: _ => false,
            comspec: @"C:\Windows\System32\cmd.exe");

        Assert.EndsWith("cmd.exe", psi.FileName, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new[] { "/d", "/s", "/c", "npm", "run", "start" }, psi.ArgumentList);
    }

    [Fact]
    public void CreateForTest_Ps1Command_ShouldThrow()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ManagedWebAppProcessStartInfoFactory.CreateForTest(
                command: "site.ps1",
                arguments: Array.Empty<string>(),
                pathEnv: null,
                fileExists: _ => false,
                comspec: @"C:\Windows\System32\cmd.exe"));
    }
}
