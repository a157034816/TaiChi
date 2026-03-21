using System.IO;
using CentralService.Services;

namespace CentralService.Tests;

/// <summary>
/// <see cref="ManagedWebAppProcessStartInfoFactory"/> 的单元测试：验证命令行包装规则（cmd/.exe/.ps1）。
/// </summary>
public sealed class ManagedWebAppProcessStartInfoFactoryTests
{
    /// <summary>
    /// 验证当命令以 <c>.cmd</c> 结尾时，应使用 <c>cmd.exe /c</c> 进行包装执行。
    /// </summary>
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

    /// <summary>
    /// 验证当命令无扩展名且在 PATH 中存在同名 <c>.exe</c> 时，不应再额外使用 cmd 包装。
    /// </summary>
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

    /// <summary>
    /// 验证当命令无扩展名且无法解析到 <c>.exe</c> 时，应回退到 <c>cmd.exe /c</c> 执行以兼容常见命令。
    /// </summary>
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

    /// <summary>
    /// 验证当命令为 <c>.ps1</c> 脚本时，应拒绝创建并抛出异常（避免无意中直接执行脚本文件）。
    /// </summary>
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
