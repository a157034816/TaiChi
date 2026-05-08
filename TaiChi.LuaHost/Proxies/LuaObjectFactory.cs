using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Lua;
using TaiChi.LuaHost.Exceptions;

namespace TaiChi.LuaHost.Proxies;

/// <summary>
/// 负责根据 Lua 调用上下文选择并调用目标类型的构造器，并将构造结果包装成 Lua 代理壳。
/// </summary>
internal static class LuaObjectFactory
{
    /// <summary>
    /// 调用 <paramref name="type"/> 的构造器创建实例并返回 Lua 代理壳表。
    /// </summary>
    /// <param name="state">当前 LuaState。</param>
    /// <param name="type">目标 .NET 类型。</param>
    /// <param name="context">Lua 函数调用上下文。</param>
    /// <param name="token">取消标记（仅用于参数中存在 <see cref="CancellationToken"/> 的情况）。</param>
    /// <param name="firstArgIndex">Lua 参数中视为构造器实参的起始下标（之前位置应为类型名等元参数）。</param>
    /// <exception cref="LuaMappingException">在无可用构造器、参数无法转换或多个候选都可调用时抛出。</exception>
    public static LuaTable Create(LuaState state, Type type, LuaFunctionExecutionContext context, CancellationToken token, int firstArgIndex)
    {
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (type is null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        if (firstArgIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(firstArgIndex));
        }

        var luaArgCount = CountLuaArguments(context, firstArgIndex);
        var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Where(ctor => !ctor.GetParameters().Any(p => p.ParameterType.IsByRef || p.IsOut))
            .ToArray();

        if (constructors.Length == 0)
        {
            throw new LuaMappingException($"类型 {type.FullName ?? type.Name} 没有可用的公共构造器。");
        }

        var matches = new List<(ConstructorInfo Ctor, object?[] Args)>();
        var rejectionReasons = new List<string>();

        foreach (var ctor in constructors.OrderByDescending(c => c.GetParameters().Length))
        {
            if (!IsArgumentCountCompatible(ctor, luaArgCount))
            {
                rejectionReasons.Add($"  · {DescribeConstructor(ctor)} —— 参数个数不匹配（需要 {DescribeArity(ctor)}，实际 {luaArgCount}）");
                continue;
            }

            try
            {
                var args = BindArguments(state, ctor, context, token, firstArgIndex);
                matches.Add((ctor, args));
            }
            catch (Exception ex)
            {
                rejectionReasons.Add($"  · {DescribeConstructor(ctor)} —— {ex.Message}");
            }
        }

        if (matches.Count == 0)
        {
            var detail = rejectionReasons.Count == 0
                ? string.Empty
                : Environment.NewLine + string.Join(Environment.NewLine, rejectionReasons);
            throw new LuaMappingException(
                $"找不到能接受 {luaArgCount} 个参数的 {type.FullName ?? type.Name} 构造器。{detail}");
        }

        if (matches.Count > 1)
        {
            var items = string.Join(Environment.NewLine, matches.Select(m => $"  · {DescribeConstructor(m.Ctor)}"));
            throw new LuaMappingException(
                $"调用 {type.FullName ?? type.Name} 构造器存在歧义：发现 {matches.Count} 个均可绑定的候选。{Environment.NewLine}{items}");
        }

        var (selectedCtor, boundArgs) = matches[0];
        object instance;
        try
        {
            instance = selectedCtor.Invoke(boundArgs);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw new LuaMappingException($"调用 {type.FullName ?? type.Name} 构造器失败。", ex.InnerException);
        }

        return LuaProxyTableFactory.Wrap(state, instance);
    }

    private static int CountLuaArguments(LuaFunctionExecutionContext context, int firstArgIndex)
    {
        var count = 0;
        while (context.HasArgument(firstArgIndex + count))
        {
            count++;
        }

        return count;
    }

    private static bool IsArgumentCountCompatible(ConstructorInfo ctor, int luaArgCount)
    {
        var parameters = ctor.GetParameters();
        // 排除特殊注入参数（CancellationToken）后再统计真实可见的形参数。
        var visible = parameters.Where(p => p.ParameterType != typeof(CancellationToken)).ToArray();

        var requiredCount = visible.Count(p => !p.HasDefaultValue && !IsParamArray(p));
        var hasParamArray = visible.Length > 0 && IsParamArray(visible[^1]);
        var maxCount = hasParamArray ? int.MaxValue : visible.Length;

        return luaArgCount >= requiredCount && luaArgCount <= maxCount;
    }

    private static object?[] BindArguments(LuaState state, ConstructorInfo ctor, LuaFunctionExecutionContext context, CancellationToken token, int firstArgIndex)
    {
        var parameters = ctor.GetParameters();
        if (parameters.Length == 0)
        {
            return Array.Empty<object?>();
        }

        var values = new object?[parameters.Length];
        var luaIndex = firstArgIndex;

        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];

            if (parameter.ParameterType == typeof(CancellationToken))
            {
                values[i] = token;
                continue;
            }

            if (IsParamArray(parameter))
            {
                values[i] = BindParamArray(state, parameter, context, ref luaIndex);
                continue;
            }

            if (context.HasArgument(luaIndex))
            {
                var rawValue = context.GetArgument<object?>(luaIndex);
                values[i] = LuaProxyValueConverter.ConvertFromLuaObject(state, rawValue, parameter.ParameterType);
                luaIndex++;
                continue;
            }

            if (TryGetDefaultValue(parameter, out var defaultValue))
            {
                values[i] = defaultValue;
                continue;
            }

            throw new LuaMappingException($"调用构造器时缺少参数 {parameter.Name}。");
        }

        return values;
    }

    private static object BindParamArray(LuaState state, ParameterInfo parameter, LuaFunctionExecutionContext context, ref int luaIndex)
    {
        var elementType = parameter.ParameterType.GetElementType() ?? typeof(object);
        var values = new List<object?>();

        while (context.HasArgument(luaIndex))
        {
            var rawValue = context.GetArgument<object?>(luaIndex);
            values.Add(LuaProxyValueConverter.ConvertFromLuaObject(state, rawValue, elementType));
            luaIndex++;
        }

        var array = Array.CreateInstance(elementType, values.Count);
        for (var i = 0; i < values.Count; i++)
        {
            array.SetValue(values[i], i);
        }

        return array;
    }

    private static bool IsParamArray(ParameterInfo parameter)
    {
        return parameter.GetCustomAttribute<ParamArrayAttribute>() != null;
    }

    private static bool TryGetDefaultValue(ParameterInfo parameter, out object? value)
    {
        if (!parameter.HasDefaultValue)
        {
            value = null;
            return false;
        }

        var rawDefault = parameter.DefaultValue;
        if (rawDefault == DBNull.Value || rawDefault == Type.Missing)
        {
            var nonNullable = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
            rawDefault = nonNullable.IsValueType ? Activator.CreateInstance(nonNullable) : null;
        }

        value = rawDefault;
        return true;
    }

    private static string DescribeConstructor(ConstructorInfo ctor)
    {
        var sig = string.Join(", ", ctor.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
        return $"{ctor.DeclaringType?.Name}({sig})";
    }

    private static string DescribeArity(ConstructorInfo ctor)
    {
        var visible = ctor.GetParameters().Where(p => p.ParameterType != typeof(CancellationToken)).ToArray();
        var required = visible.Count(p => !p.HasDefaultValue && !IsParamArray(p));
        var hasParams = visible.Length > 0 && IsParamArray(visible[^1]);
        if (hasParams)
        {
            return $"{required}+";
        }

        return required == visible.Length ? required.ToString() : $"{required}~{visible.Length}";
    }
}
