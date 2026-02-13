using Lua;

namespace TaiChi.LuaHost.Proxies;

/// <summary>
/// 为代理相关的 LuaFunction 回调提供统一的返回值写栈逻辑。
/// </summary>
internal static class LuaProxyReturnHelper
{
    /// <summary>
    /// 将 <see cref="LuaValue"/> 写入 Lua 返回栈。
    /// </summary>
    /// <param name="context">Lua 调用上下文。</param>
    /// <param name="value">返回值。</param>
    public static int Return(LuaFunctionExecutionContext context, LuaValue value)
    {
        return value.Type == LuaValueType.Nil ? context.Return() : context.Return(value);
    }
}
