using System.Threading.Tasks;
using TaiChi.LuaHost;
using Xunit;

namespace TaiChi.LuaHost.Tests;

/// <summary>
/// 覆盖 Lua 静态类型代理（static 表）在成员读写、静态方法调用、重载别名与自动注册上的核心行为。
/// </summary>
public sealed class LuaStaticTypeProxyTests
{
    /// <summary>
    /// 显式注册后，static 代理应支持静态属性/字段读写（包含 nonpublic），并支持引用类型递归包装与代理解包写入。
    /// </summary>
    [Fact]
    public async Task Static_Proxy_Should_Get_And_Set_Static_Members()
    {
        StaticTestTarget.Value = 1;
        StaticTestTarget.Count = 2;
        StaticTestTarget.Person = new ProxyTestPerson("P");

        using var host = new LuaScriptHost();
        host.RegisterStaticType(typeof(StaticTestTarget));

        var r1 = await host.ExecuteAsync(
            "return static.StaticTestTarget.Value, static.StaticTestTarget.Count, static.StaticTestTarget.PrivateName, static.StaticTestTarget.PrivateNumber, static.StaticTestTarget.Person.Name");

        Assert.Equal(1d, r1[0].Read<double>());
        Assert.Equal(2d, r1[1].Read<double>());
        Assert.Equal("private", r1[2].Read<string>());
        Assert.Equal(7d, r1[3].Read<double>());
        Assert.Equal("P", r1[4].Read<string>());

        var child = new ProxyTestPerson("C");
        host.SetGlobalProxy("p", child);

        var r2 = await host.ExecuteAsync(
            "static.StaticTestTarget.Value = 3; static.StaticTestTarget.Count = 4; " +
            "static.StaticTestTarget.PrivateName = 'X'; static.StaticTestTarget.PrivateNumber = 9; " +
            "static.StaticTestTarget.Person = p; return static.StaticTestTarget.Value, static.StaticTestTarget.Count, static.StaticTestTarget.Person.Name");

        Assert.Equal(3d, r2[0].Read<double>());
        Assert.Equal(4d, r2[1].Read<double>());
        Assert.Equal("C", r2[2].Read<string>());

        Assert.Equal(3, StaticTestTarget.Value);
        Assert.Equal(4, StaticTestTarget.Count);
        Assert.Equal("X", StaticTestTarget.ReadPrivateName());
        Assert.Equal(9, StaticTestTarget.ReadPrivateNumber());
        Assert.Same(child, StaticTestTarget.Person);
    }

    /// <summary>
    /// 静态方法应可用 '.' 调用，但使用 ':' 调用必须抛异常。
    /// </summary>
    [Fact]
    public async Task Static_Method_Should_Be_Callable_With_Dot_But_Not_Colon()
    {
        using var host = new LuaScriptHost();
        host.RegisterStaticType(typeof(StaticTestTarget));

        var r1 = await host.ExecuteAsync("return static.StaticTestTarget.Add(1, 2)");
        Assert.Equal(3d, r1.ReadFirstOrDefault<double>());

        var ex = await Assert.ThrowsAsync<LuaScriptHostException>(() => host.ExecuteAsync("return static.StaticTestTarget:Add(1, 2)"));
        Assert.Contains("不支持", ex.ToString());
        Assert.Contains("':'", ex.ToString());
    }

    /// <summary>
    /// 当同名静态方法存在多个重载时，直接通过原名访问应抛异常。
    /// </summary>
    [Fact]
    public async Task Calling_Overloaded_Static_Method_By_Original_Name_Should_Throw()
    {
        using var host = new LuaScriptHost();
        host.RegisterStaticType(typeof(StaticOverloadTarget));

        var ex = await Assert.ThrowsAsync<LuaScriptHostException>(() => host.ExecuteAsync("return static.StaticOverloadTarget.Foo(1)"));
        Assert.Contains("多个重载", ex.ToString());
    }

    /// <summary>
    /// 标注 LuaOverloadPreferred 的静态方法应可通过别名调用。
    /// </summary>
    [Fact]
    public async Task Calling_Static_Overloads_By_Alias_Should_Work()
    {
        using var host = new LuaScriptHost();
        host.RegisterStaticType(typeof(StaticOverloadTarget));

        var r1 = await host.ExecuteAsync("return static.StaticOverloadTarget.Foo_1(7), static.StaticOverloadTarget.Foo_2(1, 2)");
        Assert.Equal(7d, r1[0].Read<double>());
        Assert.Equal(3d, r1[1].Read<double>());
    }

    /// <summary>
    /// 当同一个别名对应多个候选静态方法时应抛异常。
    /// </summary>
    [Fact]
    public async Task Static_Alias_Conflict_Should_Throw()
    {
        using var host = new LuaScriptHost();
        host.RegisterStaticType(typeof(StaticAliasConflictTarget));

        var ex = await Assert.ThrowsAsync<LuaScriptHostException>(() => host.ExecuteAsync("return static.StaticAliasConflictTarget.X(1)"));
        Assert.Contains("别名 X", ex.ToString());
        Assert.Contains("冲突", ex.ToString());
    }

    /// <summary>
    /// 当未显式注册时，访问 static.Xxx 应在开启自动注册时按已加载程序集解析同名 static class。
    /// </summary>
    [Fact]
    public async Task Static_AutoRegister_Should_Work_For_Unique_Match()
    {
        LuaHostAutoStaticUnique.Value = 11;

        using var host = new LuaScriptHost();
        var r1 = await host.ExecuteAsync("return static.LuaHostAutoStaticUnique.Value");
        Assert.Equal(11d, r1.ReadFirstOrDefault<double>());
    }

    /// <summary>
    /// 当存在多个同名 static class 时，自动注册应抛 LuaMappingException 并列出候选。
    /// </summary>
    [Fact]
    public async Task Static_AutoRegister_Should_Throw_When_Name_Conflicts()
    {
        using var host = new LuaScriptHost();

        var ex = await Assert.ThrowsAsync<LuaScriptHostException>(() => host.ExecuteAsync("return static.ConflictStatic"));
        Assert.Contains("发生冲突", ex.ToString());
        Assert.Contains("StaticA.ConflictStatic", ex.ToString());
        Assert.Contains("StaticB.ConflictStatic", ex.ToString());
    }

    /// <summary>
    /// 关闭自动注册后，访问未显式注册的 static.Xxx 必须返回 nil 且不会抛同名冲突异常。
    /// </summary>
    [Fact]
    public async Task Static_AutoRegister_Can_Be_Disabled()
    {
        LuaHostAutoStaticUnique.Value = 12;

        using var host = new LuaScriptHost(new LuaScriptHostOptions { EnableStaticAutoRegister = false });

        var r1 = await host.ExecuteAsync("return static.LuaHostAutoStaticUnique == nil, static.ConflictStatic == nil");
        Assert.True(r1[0].Read<bool>());
        Assert.True(r1[1].Read<bool>());

        host.RegisterStaticType(typeof(LuaHostAutoStaticUnique));

        var r2 = await host.ExecuteAsync("return static.LuaHostAutoStaticUnique.Value");
        Assert.Equal(12d, r2.ReadFirstOrDefault<double>());
    }
}

