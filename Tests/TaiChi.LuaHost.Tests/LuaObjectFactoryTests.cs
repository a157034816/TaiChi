using System.Collections.Generic;
using System.Threading.Tasks;
using TaiChi.LuaHost;
using Xunit;

namespace TaiChi.LuaHost.Tests;

/// <summary>
/// 覆盖 Lua 全局工厂函数（默认 <c>create</c>）在类型解析、构造器选择、参数转换、结果代理化与异常路径上的核心行为。
/// </summary>
public sealed class LuaObjectFactoryTests
{
    /// <summary>
    /// 显式登记后，Lua 端按不同参数个数调用应分别命中对应构造器并填充字段。
    /// </summary>
    [Fact]
    public async Task Create_Should_Build_Instance_With_Multiple_Arg_Counts()
    {
        using var host = new LuaScriptHost();
        host.RegisterFactoryType<FactoryTarget>();

        var r0 = await host.ExecuteAsync("local x = create('FactoryTarget'); return x.Origin, x.Name, x.Value");
        Assert.Equal("()", r0[0].Read<string>());
        Assert.Equal(string.Empty, r0[1].Read<string>());
        Assert.Equal(0d, r0[2].Read<double>());

        var r1 = await host.ExecuteAsync("local x = create('FactoryTarget', 'hello'); return x.Origin, x.Name");
        Assert.Equal("(string)", r1[0].Read<string>());
        Assert.Equal("hello", r1[1].Read<string>());

        var r2 = await host.ExecuteAsync("local x = create('FactoryTarget', 'hello', 42); return x.Origin, x.Name, x.Value");
        Assert.Equal("(string,int)", r2[0].Read<string>());
        Assert.Equal("hello", r2[1].Read<string>());
        Assert.Equal(42d, r2[2].Read<double>());
    }

    /// <summary>
    /// 同时支持以 <c>Type.Name</c>、<c>Type.FullName</c>、自定义别名三种方式调用工厂函数。
    /// </summary>
    [Fact]
    public async Task Create_Should_Accept_FullName_Name_And_Alias()
    {
        using var host = new LuaScriptHost();
        host.RegisterFactoryType<FactoryTarget>("FT");

        var script = "return " +
            "create('FactoryTarget', 'a').Name, " +
            $"create('{typeof(FactoryTarget).FullName}', 'b').Name, " +
            "create('FT', 'c').Name";
        var values = await host.ExecuteAsync(script);

        Assert.Equal("a", values[0].Read<string>());
        Assert.Equal("b", values[1].Read<string>());
        Assert.Equal("c", values[2].Read<string>());
    }

    /// <summary>
    /// 在 <see cref="LuaScriptHostOptions.EnableFactoryAutoResolve"/> 启用时，未登记的唯一类型也能被解析。
    /// </summary>
    [Fact]
    public async Task Create_Should_Auto_Resolve_When_Not_Registered_And_Unique()
    {
        using var host = new LuaScriptHost();

        var values = await host.ExecuteAsync("local x = create('FactoryTarget', 'auto'); return x.Origin, x.Name");

        Assert.Equal("(string)", values[0].Read<string>());
        Assert.Equal("auto", values[1].Read<string>());
    }

    /// <summary>
    /// 关闭自动解析后，未登记的类型应直接抛异常，避免意外的反射扫描。
    /// </summary>
    [Fact]
    public async Task Create_Should_Throw_When_Auto_Resolve_Disabled_And_Type_Unregistered()
    {
        using var host = new LuaScriptHost(new LuaScriptHostOptions { EnableFactoryAutoResolve = false });

        var ex = await Assert.ThrowsAsync<LuaScriptHostException>(() => host.ExecuteAsync("create('FactoryTarget')"));
        Assert.Contains("无法解析类型", ex.ToString());
    }

    /// <summary>
    /// 全程序集自动解析遇到同名类型时必须抛异常，引导用户使用别名或 FullName 区分。
    /// </summary>
    [Fact]
    public async Task Create_Should_Throw_On_Ambiguous_Type_Name()
    {
        using var host = new LuaScriptHost();

        var ex = await Assert.ThrowsAsync<LuaScriptHostException>(() => host.ExecuteAsync("create('FactoryConflictTarget')"));
        Assert.Contains("冲突", ex.ToString());
        Assert.Contains("FactoryConflictTarget", ex.ToString());
    }

    /// <summary>
    /// 当类型仅有一个构造器但参数个数不匹配时，必须抛带「参数个数」诊断的异常。
    /// </summary>
    [Fact]
    public async Task Create_Should_Throw_When_No_Constructor_Matches()
    {
        using var host = new LuaScriptHost();
        host.RegisterFactoryType<FactorySingleCtorTarget>();

        var ex = await Assert.ThrowsAsync<LuaScriptHostException>(() => host.ExecuteAsync("create('FactorySingleCtorTarget')"));
        Assert.Contains("构造器", ex.ToString());
        Assert.Contains("参数", ex.ToString());
    }

    /// <summary>
    /// 工厂函数返回的应是真正的代理壳，可以直接读取/调用 .NET 实例成员。
    /// </summary>
    [Fact]
    public async Task Create_Should_Wrap_Result_As_Proxy_And_Allow_Member_Access()
    {
        using var host = new LuaScriptHost();
        host.RegisterFactoryType<ProxyTestPerson>();

        var values = await host.ExecuteAsync("local p = create('ProxyTestPerson', 'Lily'); return p.Name, p:Echo('hi')");

        Assert.Equal("Lily", values[0].Read<string>());
        Assert.Equal("Lily:hi", values[1].Read<string>());
    }

    /// <summary>
    /// LuaTable 形态的代理参数（指向已被代理的 .NET 实例）应被解包并匹配到引用类型构造器。
    /// </summary>
    [Fact]
    public async Task Create_Should_Unwrap_Proxy_Argument_Into_Constructor()
    {
        using var host = new LuaScriptHost();
        host.RegisterFactoryType<FactoryTarget>();
        host.SetGlobalProxy("owner", new ProxyTestPerson("Owner"));

        var values = await host.ExecuteAsync("local x = create('FactoryTarget', owner); return x.Origin, x.Name");

        Assert.Equal("(ProxyTestPerson)", values[0].Read<string>());
        Assert.Equal("Owner", values[1].Read<string>());
    }

    /// <summary>
    /// 通过 backtick 全名表达开放泛型实例化，验证 <see cref="System.Type.GetType(string)"/> 路径可用。
    /// </summary>
    [Fact]
    public async Task Create_Should_Support_Generic_Backtick_Syntax()
    {
        using var host = new LuaScriptHost();

        var values = await host.ExecuteAsync(
            "local list = create('System.Collections.Generic.List`1[[System.String, System.Private.CoreLib]]'); return list.Count");

        Assert.Equal(0d, values[0].Read<double>());
    }

    /// <summary>
    /// 通过 <see cref="LuaScriptHostOptions.FactoryFunctionName"/> 自定义名称后，原 <c>create</c> 不再是函数。
    /// </summary>
    [Fact]
    public async Task Custom_Function_Name_Should_Replace_Default()
    {
        using var host = new LuaScriptHost(new LuaScriptHostOptions { FactoryFunctionName = "make" });
        host.RegisterFactoryType<FactoryTarget>();

        var values = await host.ExecuteAsync("local x = make('FactoryTarget', 'm'); return x.Name, type(create)");

        Assert.Equal("m", values[0].Read<string>());
        Assert.Equal("nil", values[1].Read<string>());
    }

    /// <summary>
    /// 第一个参数缺失或类型不正确时抛异常。
    /// </summary>
    [Fact]
    public async Task Create_Should_Throw_When_Type_Name_Missing_Or_Invalid()
    {
        using var host = new LuaScriptHost();

        var missing = await Assert.ThrowsAsync<LuaScriptHostException>(() => host.ExecuteAsync("create()"));
        Assert.Contains("至少", missing.ToString());

        var blank = await Assert.ThrowsAsync<LuaScriptHostException>(() => host.ExecuteAsync("create('   ')"));
        Assert.Contains("不能为空白", blank.ToString());
    }
}
