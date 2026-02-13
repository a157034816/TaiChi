using System;
using System.Threading.Tasks;
using TaiChi.LuaHost.Attributes;

namespace TaiChi.LuaHost.Tests;

/// <summary>
/// 用于验证 Lua 对象代理（LuaObjectProxy）成员读写、递归包装、方法绑定等行为的示例目标类型。
/// </summary>
public sealed class ProxyTestPerson
{
    /// <summary>
    /// 获取或设置名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 表示一个可读写的公开字段。
    /// </summary>
    public int Count;

    /// <summary>
    /// 获取或设置子对象，用于递归包装与代理解包写入验证。
    /// </summary>
    public ProxyTestPerson? Child { get; set; }

    /// <summary>
    /// 获取或设置一个私有属性，用于验证 nonpublic 成员读写。
    /// </summary>
    private string PrivateName { get; set; } = string.Empty;

    /// <summary>
    /// 表示一个私有字段，用于验证 nonpublic 成员读写。
    /// </summary>
    private int PrivateNumber;

    /// <summary>
    /// 初始化示例实例。
    /// </summary>
    /// <param name="name">初始 Name。</param>
    public ProxyTestPerson(string name)
    {
        Name = name;
        PrivateName = $"private:{name}";
        PrivateNumber = 7;
    }

    /// <summary>
    /// 读取私有属性值，便于在测试中断言。
    /// </summary>
    public string ReadPrivateName()
    {
        return PrivateName;
    }

    /// <summary>
    /// 读取私有字段值，便于在测试中断言。
    /// </summary>
    public int ReadPrivateNumber()
    {
        return PrivateNumber;
    }

    /// <summary>
    /// 用于验证实例方法绑定的示例方法。
    /// </summary>
    /// <param name="value">输入。</param>
    /// <returns>拼接后的字符串。</returns>
    public string Echo(string value)
    {
        return $"{Name}:{value}";
    }

    /// <summary>
    /// 用于验证可选参数绑定。
    /// </summary>
    /// <param name="a">参数 a。</param>
    /// <param name="b">参数 b（可选）。</param>
    /// <returns>两数之和。</returns>
    public int AddOptional(int a, int b = 2)
    {
        return a + b;
    }

    /// <summary>
    /// 用于验证 params 参数绑定。
    /// </summary>
    /// <param name="values">可变参数列表。</param>
    /// <returns>求和结果。</returns>
    public int Sum(params int[] values)
    {
        var total = 0;
        foreach (var v in values)
        {
            total += v;
        }

        return total;
    }

    /// <summary>
    /// 用于验证 Task 返回值会被等待。
    /// </summary>
    /// <param name="a">参数 a。</param>
    /// <param name="b">参数 b。</param>
    /// <returns>异步计算的结果。</returns>
    public Task<int> AsyncAdd(int a, int b)
    {
        return Task.FromResult(a + b);
    }

    /// <summary>
    /// 用于验证 ValueTask 返回值会被等待。
    /// </summary>
    /// <param name="a">参数 a。</param>
    /// <param name="b">参数 b。</param>
    /// <returns>异步计算的结果。</returns>
    public ValueTask<int> ValueTaskAdd(int a, int b)
    {
        return new ValueTask<int>(a + b);
    }
}

/// <summary>
/// 用于验证“同名多重载不支持自动选择”以及“重载别名调用”的示例类型。
/// </summary>
public sealed class OverloadTarget
{
    /// <summary>
    /// Foo 的单参数重载。
    /// </summary>
    /// <param name="v">输入。</param>
    /// <returns>原样返回。</returns>
    [LuaOverloadPreferred("Foo_1")]
    public int Foo(int v)
    {
        return v;
    }

    /// <summary>
    /// Foo 的双参数重载。
    /// </summary>
    /// <param name="a">参数 a。</param>
    /// <param name="b">参数 b。</param>
    /// <returns>两数之和。</returns>
    [LuaOverloadPreferred("Foo_2")]
    public int Foo(int a, int b)
    {
        return a + b;
    }
}

/// <summary>
/// 用于验证“方法别名冲突会抛异常”的示例类型。
/// </summary>
public sealed class AliasConflictTarget
{
    /// <summary>
    /// 示例方法 A，别名与 B 冲突。
    /// </summary>
    /// <param name="v">输入。</param>
    /// <returns>原样返回。</returns>
    [LuaOverloadPreferred("X")]
    public int A(int v)
    {
        return v;
    }

    /// <summary>
    /// 示例方法 B，别名与 A 冲突。
    /// </summary>
    /// <param name="v">输入。</param>
    /// <returns>返回 v + 1。</returns>
    [LuaOverloadPreferred("X")]
    public int B(int v)
    {
        return v + 1;
    }
}
