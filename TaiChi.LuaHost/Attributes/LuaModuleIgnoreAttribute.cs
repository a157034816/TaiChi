using System;

namespace TaiChi.LuaHost.Attributes;

/// <summary>
/// 指示跳过某个方法的 Lua 导出。
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class LuaModuleIgnoreAttribute : Attribute
{
}
