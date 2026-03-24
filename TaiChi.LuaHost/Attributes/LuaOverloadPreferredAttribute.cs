using System;

namespace TaiChi.LuaHost.Attributes;

/// <summary>
/// 为同名方法的特定重载声明 Lua 侧调用别名，用于避免“多个重载”时的歧义。
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class LuaOverloadPreferredAttribute : Attribute
{
    /// <summary>
    /// 初始化特性。
    /// </summary>
    /// <param name="alias">Lua 侧使用的别名。</param>
    public LuaOverloadPreferredAttribute(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            throw new ArgumentException("别名不能为空。", nameof(alias));
        }

        Alias = alias.Trim();
    }

    /// <summary>
    /// 获取 Lua 侧调用的别名。
    /// </summary>
    public string Alias { get; }
}
