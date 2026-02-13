using System.Threading.Tasks;
using TaiChi.LuaHost;
using Xunit;

namespace TaiChi.LuaHost.Tests;

/// <summary>
/// 覆盖 Lua 对象代理在“多重载即异常”与“重载别名调用”上的核心行为。
/// </summary>
public sealed class LuaObjectProxyOverloadAliasTests
{
    /// <summary>
    /// 当同名方法存在多个重载时，直接通过原名访问应抛异常。
    /// </summary>
    [Fact]
    public async Task Calling_Overloaded_Method_By_Original_Name_Should_Throw()
    {
        using var host = new LuaScriptHost();
        host.SetGlobalProxy("p", new OverloadTarget());

        var ex = await Assert.ThrowsAsync<LuaScriptHostException>(() => host.ExecuteAsync("return p:Foo(1)"));
        Assert.Contains("多个重载", ex.ToString());
    }

    /// <summary>
    /// 标注 LuaOverloadPreferred 的方法应可通过别名调用。
    /// </summary>
    [Fact]
    public async Task Calling_Overloads_By_Alias_Should_Work()
    {
        using var host = new LuaScriptHost();
        host.SetGlobalProxy("p", new OverloadTarget());

        var r1 = await host.ExecuteAsync("return p:Foo_1(7), p:Foo_2(1, 2)");
        Assert.Equal(7d, r1[0].Read<double>());
        Assert.Equal(3d, r1[1].Read<double>());
    }

    /// <summary>
    /// 当同一个别名对应多个候选方法时应抛异常。
    /// </summary>
    [Fact]
    public async Task Alias_Conflict_Should_Throw()
    {
        using var host = new LuaScriptHost();
        host.SetGlobalProxy("p", new AliasConflictTarget());

        var ex = await Assert.ThrowsAsync<LuaScriptHostException>(() => host.ExecuteAsync("return p:X(1)"));
        Assert.Contains("别名 X", ex.ToString());
        Assert.Contains("冲突", ex.ToString());
    }
}
