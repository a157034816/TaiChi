using System;
using System.Collections;
using System.Data;
using System.Threading.Tasks;
using Lua;
using TaiChi.LuaHost;
using TaiChi.LuaHost.Proxies;
using Xunit;

namespace TaiChi.LuaHost.Tests;

/// <summary>
/// 覆盖 Lua 对象代理在成员读写、递归包装与数据结构适配（DataRow/IDictionary）上的核心行为。
/// </summary>
public sealed class LuaObjectProxyMemberAccessTests
{
    /// <summary>
    /// 同一 LuaState 内对同一 target 的 Wrap 必须复用同一个代理壳。
    /// </summary>
    [Fact]
    public void Wrap_Should_Reuse_Table_For_Same_Target_In_Same_State()
    {
        using var host = new LuaScriptHost();
        var target = new ProxyTestPerson("A");

        var first = LuaProxyTableFactory.Wrap(host.State, target);
        var second = LuaProxyTableFactory.Wrap(host.State, target);

        Assert.Same(first, second);
    }

    /// <summary>
    /// 代理壳应支持读写公开属性与公开字段。
    /// </summary>
    [Fact]
    public async Task Proxy_Should_Get_And_Set_Public_Members()
    {
        using var host = new LuaScriptHost();
        var target = new ProxyTestPerson("A") { Count = 1 };
        host.SetGlobalProxy("p", target);

        var r1 = await host.ExecuteAsync("return p.Name, p.Count");
        Assert.Equal("A", r1[0].Read<string>());
        Assert.Equal(LuaValueType.Number, r1[1].Type);
        Assert.Equal(1d, r1[1].Read<double>());

        var r2 = await host.ExecuteAsync("p.Name = 'B'; p.Count = 3; return p.Name, p.Count");
        Assert.Equal("B", r2[0].Read<string>());
        Assert.Equal(3d, r2[1].Read<double>());
        Assert.Equal("B", target.Name);
        Assert.Equal(3, target.Count);
    }

    /// <summary>
    /// 代理壳应支持读写 nonpublic 属性/字段。
    /// </summary>
    [Fact]
    public async Task Proxy_Should_Get_And_Set_NonPublic_Members()
    {
        using var host = new LuaScriptHost();
        var target = new ProxyTestPerson("A");
        host.SetGlobalProxy("p", target);

        var r1 = await host.ExecuteAsync("return p.PrivateName, p.PrivateNumber");
        Assert.Equal("private:A", r1[0].Read<string>());
        Assert.Equal(7d, r1[1].Read<double>());

        await host.ExecuteAsync("p.PrivateName = 'X'; p.PrivateNumber = 9");
        Assert.Equal("X", target.ReadPrivateName());
        Assert.Equal(9, target.ReadPrivateNumber());
    }

    /// <summary>
    /// 代理壳应支持 DataRow 列的读写。
    /// </summary>
    [Fact]
    public async Task Proxy_Should_Read_And_Write_DataRow_Columns()
    {
        var table = new DataTable();
        table.Columns.Add("Name", typeof(object));
        table.Columns.Add("Count", typeof(object));

        var row = table.NewRow();
        row["Name"] = "A";
        row["Count"] = 1;
        table.Rows.Add(row);

        using var host = new LuaScriptHost();
        host.SetGlobalProxy("p", row);

        var r1 = await host.ExecuteAsync("return p.Name, p.Count");
        Assert.Equal("A", r1[0].Read<string>());
        Assert.Equal(1d, r1[1].Read<double>());

        await host.ExecuteAsync("p.Name = 'B'; p.Count = 3");
        Assert.Equal("B", row["Name"]);
        Assert.Equal(3d, Convert.ToDouble(row["Count"]));

        await host.ExecuteAsync("p.Name = nil");
        Assert.Equal(DBNull.Value, row["Name"]);
    }

    /// <summary>
    /// 代理壳应支持 DataRowView 列的读写。
    /// </summary>
    [Fact]
    public async Task Proxy_Should_Read_And_Write_DataRowView_Columns()
    {
        var table = new DataTable();
        table.Columns.Add("Name", typeof(object));

        var row = table.NewRow();
        row["Name"] = "A";
        table.Rows.Add(row);

        var view = table.DefaultView[0];

        using var host = new LuaScriptHost();
        host.SetGlobalProxy("p", view);

        var r1 = await host.ExecuteAsync("return p.Name");
        Assert.Equal("A", r1.ReadFirstOrDefault<string>());

        await host.ExecuteAsync("p.Name = 'B'");
        Assert.Equal("B", view["Name"]);
    }

    /// <summary>
    /// 代理壳应支持 legacy IDictionary 的字符串键读写。
    /// </summary>
    [Fact]
    public async Task Proxy_Should_Read_And_Write_Dictionary_Keys()
    {
        var dict = new Hashtable
        {
            ["Name"] = "A"
        };

        using var host = new LuaScriptHost();
        host.SetGlobalProxy("p", dict);

        var r1 = await host.ExecuteAsync("return p.Name");
        Assert.Equal("A", r1.ReadFirstOrDefault<string>());

        await host.ExecuteAsync("p.Name = 'B'; p.Count = 3");
        Assert.Equal("B", dict["Name"]);
        Assert.Equal(3d, Convert.ToDouble(dict["Count"]));
    }

    /// <summary>
    /// 当成员返回引用类型时应递归包装为代理壳；写入代理壳时应自动解包为真实对象。
    /// </summary>
    [Fact]
    public async Task Proxy_Should_Wrap_ReferenceType_And_Unwrap_On_Set()
    {
        var parent = new ProxyTestPerson("P");
        var child1 = new ProxyTestPerson("C1");
        parent.Child = child1;

        var child2 = new ProxyTestPerson("C2");

        using var host = new LuaScriptHost();
        host.SetGlobalProxy("p", parent);
        host.SetGlobalProxy("c", child2);

        var r1 = await host.ExecuteAsync("return p.Child.Name");
        Assert.Equal("C1", r1.ReadFirstOrDefault<string>());

        var r2 = await host.ExecuteAsync("return p.Child == p.Child");
        Assert.True(r2.ReadFirstOrDefault<bool>());

        await host.ExecuteAsync("p.Child = c");
        Assert.Same(child2, parent.Child);

        var r3 = await host.ExecuteAsync("return p.Child.Name");
        Assert.Equal("C2", r3.ReadFirstOrDefault<string>());
    }
}
