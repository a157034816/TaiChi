using System;

namespace TaiChi.LuaHost.Attributes;

/// <summary>
/// 指示公开到 Lua 的模块方法及其导出配置。
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class LuaModuleMethodAttribute : Attribute
{
    /// <summary>
    /// 初始化特性实例。
    /// </summary>
    /// <param name="name">导出到 Lua 的函数名称，留空则按方法名推导。</param>
    public LuaModuleMethodAttribute(string? name = null)
    {
        Name = string.IsNullOrWhiteSpace(name) ? null : name;
    }

    /// <summary>
    /// 获取导出名，若为空则使用默认命名策略。
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// 获取或设置允许的最小参数个数，默认不限制。
    /// </summary>
    public int MinArgs { get; set; }

    /// <summary>
    /// 获取或设置允许的最大参数个数，null 代表不限制。
    /// </summary>
    public int? MaxArgs { get; set; }
}
