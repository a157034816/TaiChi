using System;

namespace TaiChi.LuaHost;

/// <summary>
/// 表示 Lua 脚本宿主运行时出现的异常。
/// </summary>
public sealed class LuaScriptHostException : Exception
{
    /// <summary>
    /// 使用消息初始化异常。
    /// </summary>
    /// <param name="message">异常描述。</param>
    public LuaScriptHostException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// 使用消息和内部异常初始化实例。
    /// </summary>
    /// <param name="message">异常描述。</param>
    /// <param name="innerException">底层异常。</param>
    public LuaScriptHostException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
