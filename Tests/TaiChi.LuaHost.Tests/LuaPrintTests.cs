using System;
using System.IO;
using System.Threading.Tasks;
using TaiChi.LuaHost;
using Xunit;

namespace TaiChi.LuaHost.Tests;

/// <summary>
/// 覆盖 Lua 全局 <c>print</c> 在中文/UTF-8 文本下的输出稳定性。
/// </summary>
public sealed class LuaPrintTests
{
    /// <summary>
    /// 打印包含中文字符的字符串时不应因标准库输出缓冲区问题抛异常。
    /// </summary>
    [Fact]
    public async Task Print_Should_Accept_Unicode_Text_Without_Throwing()
    {
        using var host = new LuaScriptHost();

        var originalOut = Console.Out;
        try
        {
            using var buffer = new StringWriter();
            Console.SetOut(buffer);

            await host.ExecuteAsync("print(string.rep('A', 5000))");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
