using System;
using Lua;
using TaiChi.LuaHost.Builders;
using TaiChi.LuaHost.Exceptions;

namespace TaiChi.LuaHost.Helpers;

/// <summary>
/// 为 Lua 模块提供统一构建与注册入口的帮助类。
/// </summary>
public static class LuaModuleHelper
{
    /// <summary>
    /// 将模块注册到 Lua 环境。
    /// </summary>
    /// <param name="state">目标 LuaState。</param>
    /// <param name="globalName">注册到全局表的名称。</param>
    /// <param name="buildAction">构建模块的委托。</param>
    public static void RegisterModule(LuaState state, string globalName, Action<LuaModuleBuilder> buildAction)
    {
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var env = state.Environment ?? throw new LuaMappingException("Lua 环境尚未初始化。");
        var table = CreateTable(buildAction);
        var name = string.IsNullOrWhiteSpace(globalName) ? throw new ArgumentException("注册名称不能为空。", nameof(globalName)) : globalName;
        env[name] = table;
    }

    /// <summary>
    /// 创建模块对应的 LuaTable，方便单元测试或特殊调用场景。
    /// </summary>
    /// <param name="buildAction">构建模块的委托。</param>
    public static LuaTable CreateTable(Action<LuaModuleBuilder> buildAction)
    {
        if (buildAction is null)
        {
            throw new ArgumentNullException(nameof(buildAction));
        }

        var builder = new LuaModuleBuilder();
        buildAction(builder);
        return builder.Build();
    }
}
