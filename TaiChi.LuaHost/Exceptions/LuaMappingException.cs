using System;

namespace TaiChi.LuaHost.Exceptions;

/// <summary>
/// 表示 Lua 映射过程中发生的异常。
/// </summary>
public sealed class LuaMappingException : Exception
{
    /// <summary>
    /// 初始化异常实例。
    /// </summary>
    /// <param name="message">异常描述。</param>
    public LuaMappingException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// 初始化异常实例并附带内部异常。
    /// </summary>
    /// <param name="message">异常描述。</param>
    /// <param name="innerException">底层异常。</param>
    public LuaMappingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
