using System.Threading.Tasks;
using TaiChi.LuaHost;
using Xunit;

namespace TaiChi.LuaHost.Tests;

/// <summary>
/// 覆盖 Lua 对象代理在实例方法绑定（:/. self 语义、params/可选参数、Task/ValueTask）上的核心行为。
/// </summary>
public sealed class LuaObjectProxyMethodBindingTests
{
    /// <summary>
    /// 使用 ':' 调用实例方法应自动传入 self。
    /// </summary>
    [Fact]
    public async Task Method_Should_Be_Callable_With_Colon()
    {
        using var host = new LuaScriptHost();
        var target = new ProxyTestPerson("A");
        host.SetGlobalProxy("p", target);

        var r1 = await host.ExecuteAsync("return p:Echo('x')");
        Assert.Equal("A:x", r1.ReadFirstOrDefault<string>());
    }

    /// <summary>
    /// 使用 '.' 调用实例方法时，必须显式传入 self。
    /// </summary>
    [Fact]
    public async Task Method_Should_Be_Callable_With_Dot_And_Explicit_Self()
    {
        using var host = new LuaScriptHost();
        var target = new ProxyTestPerson("A");
        host.SetGlobalProxy("p", target);

        var r1 = await host.ExecuteAsync("return p.Echo(p, 'y')");
        Assert.Equal("A:y", r1.ReadFirstOrDefault<string>());
    }

    /// <summary>
    /// 使用 '.' 调用实例方法但省略 self 时应抛异常。
    /// </summary>
    [Fact]
    public async Task Method_Should_Throw_When_Dot_Missing_Self()
    {
        using var host = new LuaScriptHost();
        var target = new ProxyTestPerson("A");
        host.SetGlobalProxy("p", target);

        var ex = await Assert.ThrowsAsync<LuaScriptHostException>(() => host.ExecuteAsync("return p.Echo('y')"));
        Assert.Contains("self", ex.ToString());
    }

    /// <summary>
    /// params 与可选参数应按 Lua 实参绑定到 .NET 方法参数。
    /// </summary>
    [Fact]
    public async Task Method_Should_Bind_Params_And_Default_Values()
    {
        using var host = new LuaScriptHost();
        var target = new ProxyTestPerson("A");
        host.SetGlobalProxy("p", target);

        var r1 = await host.ExecuteAsync("return p:Sum(1, 2, 3), p:AddOptional(3)");
        Assert.Equal(6d, r1[0].Read<double>());
        Assert.Equal(5d, r1[1].Read<double>());
    }

    /// <summary>
    /// Task/ValueTask 返回值应被等待并将结果返回给 Lua。
    /// </summary>
    [Fact]
    public async Task Method_Should_Await_Task_And_ValueTask()
    {
        using var host = new LuaScriptHost();
        var target = new ProxyTestPerson("A");
        host.SetGlobalProxy("p", target);

        var r1 = await host.ExecuteAsync("return p:AsyncAdd(1, 2), p:ValueTaskAdd(2, 3)");
        Assert.Equal(3d, r1[0].Read<double>());
        Assert.Equal(5d, r1[1].Read<double>());
    }
}
