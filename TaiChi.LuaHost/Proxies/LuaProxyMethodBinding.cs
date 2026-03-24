using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Lua;
using TaiChi.LuaHost.Attributes;
using TaiChi.LuaHost.Exceptions;

namespace TaiChi.LuaHost.Proxies;

/// <summary>
/// 负责将 Lua 侧方法调用绑定到目标对象的实例方法。
/// </summary>
internal sealed class LuaProxyMethodBinding
{
    private readonly LuaState _state;
    private readonly object _target;
    private readonly Type _targetType;
    private readonly IReadOnlyDictionary<string, MethodInfo[]> _methodsByName;
    private readonly IReadOnlyDictionary<string, MethodInfo[]> _methodsByAlias;
    private readonly ConcurrentDictionary<string, LuaFunction> _functionCache = new(StringComparer.Ordinal);

    /// <summary>
    /// 初始化绑定器。
    /// </summary>
    /// <param name="state">当前 LuaState。</param>
    /// <param name="target">目标对象。</param>
    public LuaProxyMethodBinding(LuaState state, object target)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _targetType = target.GetType();
        _methodsByName = BuildMethodMapByName(_targetType);
        _methodsByAlias = BuildMethodMapByAlias(_targetType);
    }

    /// <summary>
    /// 尝试为指定名称解析可调用的 LuaFunction。
    /// </summary>
    /// <param name="memberName">Lua 侧访问的成员名（方法名或别名）。</param>
    /// <param name="function">输出 LuaFunction。</param>
    public bool TryGetFunction(string memberName, out LuaFunction function)
    {
        if (string.IsNullOrWhiteSpace(memberName))
        {
            function = default!;
            return false;
        }

        if (_functionCache.TryGetValue(memberName, out function!))
        {
            return true;
        }

        var resolved = ResolveMethods(memberName);
        if (resolved is null)
        {
            function = default!;
            return false;
        }

        function = _functionCache.GetOrAdd(memberName, _ => CreateFunction(memberName, resolved));
        return true;
    }

    private MethodInfo[]? ResolveMethods(string memberName)
    {
        if (_methodsByAlias.TryGetValue(memberName, out var aliasMethods))
        {
            return aliasMethods;
        }

        if (_methodsByName.TryGetValue(memberName, out var methods))
        {
            return methods;
        }

        return null;
    }

    private LuaFunction CreateFunction(string memberName, MethodInfo[] methods)
    {
        if (methods.Length == 1)
        {
            return CreateBoundInstanceMethodFunction(methods[0], memberName);
        }

        if (_methodsByAlias.ContainsKey(memberName))
        {
            return new LuaFunction((_, _) => throw new LuaMappingException($"方法别名 {memberName} 存在冲突：发现 {methods.Length} 个候选重载，请确保别名唯一。"));
        }

        return new LuaFunction((_, _) => throw new LuaMappingException($"方法 {memberName} 存在多个重载，Lua 对象代理不支持自动选择重载，请为每个重载标注 [LuaOverloadPreferred(\"...\")] 并通过别名调用。"));
    }

    private LuaFunction CreateBoundInstanceMethodFunction(MethodInfo method, string luaName)
    {
        return new LuaFunction(async (context, token) =>
        {
            try
            {
                return await InvokeInstanceMethodAsync(context, token, method, luaName).ConfigureAwait(false);
            }
            catch (LuaMappingException)
            {
                throw;
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw new LuaMappingException($"调用实例方法 {_targetType.Name}.{method.Name} 失败。", ex.InnerException);
            }
            catch (Exception ex)
            {
                throw new LuaMappingException($"调用实例方法 {_targetType.Name}.{method.Name} 失败。", ex);
            }
        });
    }

    private async ValueTask<int> InvokeInstanceMethodAsync(LuaFunctionExecutionContext context, CancellationToken token, MethodInfo method, string luaName)
    {
        EnsureSelfProvided(context, luaName);

        var args = BindArguments(method, context, token);
        var result = method.Invoke(_target, args);
        return await CompleteInvocationAsync(context, result).ConfigureAwait(false);
    }

    private static void EnsureSelfProvided(LuaFunctionExecutionContext context, string luaName)
    {
        if (!context.HasArgument(0))
        {
            throw new LuaMappingException($"调用实例方法 {luaName} 时必须传入 self。请使用 ':' 调用或使用 '.' 且显式传入 self（例如 obj.{luaName}(obj, ...)）。");
        }

        try
        {
            _ = context.GetArgument<LuaTable>(0);
        }
        catch (Exception ex)
        {
            throw new LuaMappingException($"调用实例方法 {luaName} 时缺少正确的 self。请使用 ':' 调用或使用 '.' 且显式传入 self（例如 obj.{luaName}(obj, ...)）。", ex);
        }
    }

    private object?[] BindArguments(MethodInfo method, LuaFunctionExecutionContext context, CancellationToken token)
    {
        var parameters = method.GetParameters();
        if (parameters.Length == 0)
        {
            return Array.Empty<object?>();
        }

        var values = new object?[parameters.Length];
        var luaIndex = 1;

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
                values[i] = BindParamArray(parameter, context, ref luaIndex);
                continue;
            }

            if (context.HasArgument(luaIndex))
            {
                var rawValue = context.GetArgument<object?>(luaIndex);
                values[i] = LuaProxyValueConverter.ConvertFromLuaObject(_state, rawValue, parameter.ParameterType);
                luaIndex++;
                continue;
            }

            if (TryGetDefaultValue(parameter, out var defaultValue))
            {
                values[i] = defaultValue;
                continue;
            }

            throw new LuaMappingException($"调用 {_targetType.Name}.{method.Name} 时缺少参数 {parameter.Name}。 ");
        }

        return values;
    }

    private object BindParamArray(ParameterInfo parameter, LuaFunctionExecutionContext context, ref int luaIndex)
    {
        var elementType = parameter.ParameterType.GetElementType() ?? typeof(object);
        var values = new List<object?>();

        while (context.HasArgument(luaIndex))
        {
            var rawValue = context.GetArgument<object?>(luaIndex);
            values.Add(LuaProxyValueConverter.ConvertFromLuaObject(_state, rawValue, elementType));
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

    private async ValueTask<int> CompleteInvocationAsync(LuaFunctionExecutionContext context, object? result)
    {
        if (result is null)
        {
            return context.Return();
        }

        switch (result)
        {
            case Task task:
                await task.ConfigureAwait(false);
                return await CompleteTaskResultAsync(context, task).ConfigureAwait(false);
            case ValueTask valueTask:
                await valueTask.ConfigureAwait(false);
                return context.Return();
        }

        if (IsValueTaskStruct(result.GetType()))
        {
            var asTaskMethod = result.GetType().GetMethod("AsTask", BindingFlags.Public | BindingFlags.Instance);
            if (asTaskMethod != null)
            {
                var task = (Task)asTaskMethod.Invoke(result, Array.Empty<object?>())!;
                await task.ConfigureAwait(false);
                return await CompleteTaskResultAsync(context, task).ConfigureAwait(false);
            }
        }

        var luaValue = LuaProxyTableFactory.WrapValue(_state, result);
        return LuaProxyReturnHelper.Return(context, luaValue);
    }

    private ValueTask<int> CompleteTaskResultAsync(LuaFunctionExecutionContext context, Task task)
    {
        var type = task.GetType();
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var resultProperty = type.GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);
            var value = resultProperty?.GetValue(task);
            if (value is null)
            {
                return new ValueTask<int>(context.Return());
            }

            var luaValue = LuaProxyTableFactory.WrapValue(_state, value);
            return new ValueTask<int>(LuaProxyReturnHelper.Return(context, luaValue));
        }

        return new ValueTask<int>(context.Return());
    }

    private static bool IsValueTaskStruct(Type type)
    {
        return type == typeof(ValueTask) ||
               (type.IsValueType && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueTask<>));
    }

    private static IReadOnlyDictionary<string, MethodInfo[]> BuildMethodMapByName(Type type)
    {
        var map = new Dictionary<string, List<MethodInfo>>(StringComparer.Ordinal);
        foreach (var method in EnumerateEligibleInstanceMethods(type))
        {
            if (!map.TryGetValue(method.Name, out var list))
            {
                list = new List<MethodInfo>();
                map[method.Name] = list;
            }

            list.Add(method);
        }

        return map.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, MethodInfo[]> BuildMethodMapByAlias(Type type)
    {
        var map = new Dictionary<string, List<MethodInfo>>(StringComparer.Ordinal);
        foreach (var method in EnumerateEligibleInstanceMethods(type))
        {
            var attr = method.GetCustomAttribute<LuaOverloadPreferredAttribute>();
            if (attr is null)
            {
                continue;
            }

            if (!map.TryGetValue(attr.Alias, out var list))
            {
                list = new List<MethodInfo>();
                map[attr.Alias] = list;
            }

            list.Add(method);
        }

        return map.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
    }

    private static IEnumerable<MethodInfo> EnumerateEligibleInstanceMethods(Type type)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        for (var current = type; current != null; current = current.BaseType)
        {
            foreach (var method in current.GetMethods(flags))
            {
                if (method.IsSpecialName || method.DeclaringType == typeof(object))
                {
                    continue;
                }

                if (method.ContainsGenericParameters || method.IsGenericMethodDefinition)
                {
                    continue;
                }

                if (method.GetParameters().Any(p => p.ParameterType.IsByRef || p.IsOut))
                {
                    continue;
                }

                yield return method;
            }
        }
    }
}
