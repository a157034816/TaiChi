using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Lua;
using TaiChi.LuaHost.Attributes;
using TaiChi.LuaHost.Contexts;
using TaiChi.LuaHost.Exceptions;
using TaiChi.LuaHost.Options;

namespace TaiChi.LuaHost.Builders;

/// <summary>
/// 帮助构建 Lua 模块导出函数表的建造器。
/// </summary>
public sealed class LuaModuleBuilder
{
    private readonly Dictionary<string, Func<LuaFunctionExecutionContext, CancellationToken, ValueTask<int>>> _handlers = new(StringComparer.Ordinal);

    /// <summary>
    /// 统一的自定义返回类型处理器，供通过 AddXXX 方法或未指定特定处理器的映射使用。
    /// </summary>
    public LuaModuleReturnValueHandler? ReturnValueHandler { get; set; }

    /// <summary>
    /// 注册底层处理函数。
    /// </summary>
    /// <param name="name">导出名称。</param>
    /// <param name="handler">处理委托。</param>
    public LuaModuleBuilder AddFunction(string name, Func<LuaFunctionExecutionContext, CancellationToken, ValueTask<int>> handler)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("导出名称不能为空。", nameof(name));
        }

        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        var exportName = name.Trim();
        if (_handlers.ContainsKey(exportName))
        {
            throw new LuaMappingException($"重复定义 Lua 函数 {exportName}。");
        }

        _handlers[exportName] = handler;
        return this;
    }

    /// <summary>
    /// 注册只需同步实现的函数，由 builder 统一包装异常与返回值。
    /// </summary>
    /// <param name="name">导出名称。</param>
    /// <param name="handler">同步处理逻辑。</param>
    public LuaModuleBuilder AddSyncFunction(string name, Func<LuaCallContext, object?> handler)
    {
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        return AddFunction(name, (context, token) =>
        {
            var callContext = new LuaCallContext(context, token, name, ReturnValueHandler);
            var result = handler(callContext);
            return callContext.ReturnValue(result);
        });
    }

    /// <summary>
    /// 注册异步（Task）函数。
    /// </summary>
    public LuaModuleBuilder AddAsyncFunction(string name, Func<LuaCallContext, CancellationToken, Task> handler)
    {
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        return AddFunction(name, async (context, token) =>
        {
            var callContext = new LuaCallContext(context, token, name, ReturnValueHandler);
            await handler(callContext, token).ConfigureAwait(false);
            return callContext.Return();
        });
    }

    /// <summary>
    /// 注册异步（Task）函数。
    /// </summary>
    public LuaModuleBuilder AddAsyncFunction(string name, Func<LuaCallContext, Task> handler)
    {
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        return AddAsyncFunction(name, (context, _) => handler(context));
    }

    /// <summary>
    /// 注册异步（ValueTask）函数。
    /// </summary>
    public LuaModuleBuilder AddValueTaskFunction(string name, Func<LuaCallContext, CancellationToken, ValueTask> handler)
    {
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        return AddFunction(name, async (context, token) =>
        {
            var callContext = new LuaCallContext(context, token, name, ReturnValueHandler);
            await handler(callContext, token).ConfigureAwait(false);
            return callContext.Return();
        });
    }

    /// <summary>
    /// 注册异步（ValueTask）函数。
    /// </summary>
    public LuaModuleBuilder AddValueTaskFunction(string name, Func<LuaCallContext, ValueTask> handler)
    {
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        return AddValueTaskFunction(name, (context, _) => handler(context));
    }

    /// <summary>
    /// 注册返回值为 Task 的异步函数。
    /// </summary>
    public LuaModuleBuilder AddAsyncFunction<TResult>(string name, Func<LuaCallContext, CancellationToken, Task<TResult>> handler)
    {
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        return AddFunction(name, async (context, token) =>
        {
            var callContext = new LuaCallContext(context, token, name, ReturnValueHandler);
            var result = await handler(callContext, token).ConfigureAwait(false);
            return await callContext.ReturnValue(result).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// 注册返回值为 Task 的异步函数。
    /// </summary>
    public LuaModuleBuilder AddAsyncFunction<TResult>(string name, Func<LuaCallContext, Task<TResult>> handler)
    {
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        return AddAsyncFunction(name, (context, _) => handler(context));
    }

    /// <summary>
    /// 注册返回值为 ValueTask 的异步函数。
    /// </summary>
    public LuaModuleBuilder AddValueTaskFunction<TResult>(string name, Func<LuaCallContext, CancellationToken, ValueTask<TResult>> handler)
    {
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        return AddFunction(name, async (context, token) =>
        {
            var callContext = new LuaCallContext(context, token, name, ReturnValueHandler);
            var result = await handler(callContext, token).ConfigureAwait(false);
            return await callContext.ReturnValue(result).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// 注册返回值为 ValueTask 的异步函数。
    /// </summary>
    public LuaModuleBuilder AddValueTaskFunction<TResult>(string name, Func<LuaCallContext, ValueTask<TResult>> handler)
    {
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        return AddValueTaskFunction(name, (context, _) => handler(context));
    }

    /// <summary>
    /// 扫描并注册静态类型中声明的处理方法。
    /// </summary>
    /// <typeparam name="T">静态绑定类。</typeparam>
    public LuaModuleBuilder MapStaticClass<T>(Action<LuaModuleMapOptions>? configure = null)
    {
        return MapType(typeof(T), null, BindingFlags.Public | BindingFlags.Static, configure);
    }

    /// <summary>
    /// 扫描并注册静态类型中声明的处理方法。
    /// </summary>
    /// <param name="type">静态绑定类类型。</param>
    /// <exception cref="ArgumentNullException">type 为空。</exception>
    /// <exception cref="ArgumentException">type 不是静态类。</exception>
    public LuaModuleBuilder MapStaticClass(Type type, Action<LuaModuleMapOptions>? configure = null)
    {
        return MapType(type, null, BindingFlags.Public | BindingFlags.Static, configure);
    }

    /// <summary>
    /// 扫描并注册对象实例中的处理方法。
    /// </summary>
    /// <param name="target">包含绑定方法的对象。</param>
    public LuaModuleBuilder MapObject(object target, Action<LuaModuleMapOptions>? configure = null)
    {
        if (target is null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        return MapType(target.GetType(), target, BindingFlags.Public | BindingFlags.Instance, configure);
    }

    /// <summary>
    /// 构建 Lua 模块表。
    /// </summary>
    internal LuaTable Build()
    {
        var table = new LuaTable();
        foreach (var pair in _handlers)
        {
            var handler = WrapWithExceptionSafety(pair.Key, pair.Value);
            table[pair.Key] = new LuaFunction(handler);
        }

        return table;
    }

    private LuaModuleBuilder MapType(Type type, object? target, BindingFlags flags, Action<LuaModuleMapOptions>? configure)
    {
        if (type is null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        var options = LuaModuleMapOptions.Create(configure);
        var methods = type.GetMethods(flags);
        foreach (var method in methods)
        {
            if (method.IsSpecialName || method.DeclaringType == typeof(object))
            {
                continue;
            }

            if (method.GetCustomAttribute<LuaModuleIgnoreAttribute>() != null)
            {
                continue;
            }

            var attribute = method.GetCustomAttribute<LuaModuleMethodAttribute>();
            if (attribute is null && !options.IncludeImplicitMembers)
            {
                continue;
            }

            if (options.MethodFilter != null && !options.MethodFilter(method))
            {
                continue;
            }

            var exportName = ResolveExportName(method, attribute, options);
            if (string.IsNullOrWhiteSpace(exportName))
            {
                continue;
            }

            var handler = CreateMethodHandler(exportName, method, target, options);
            AddFunction(exportName, handler);
        }

        return this;
    }

    private static string ResolveExportName(MethodInfo method, LuaModuleMethodAttribute? attribute, LuaModuleMapOptions options)
    {
        if (method is null)
        {
            throw new ArgumentNullException(nameof(method));
        }

        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (attribute is null || string.IsNullOrWhiteSpace(attribute.Name))
        {
            return options.NamingStrategy.GetName(method);
        }

        return attribute.Name!.Trim();
    }

    private Func<LuaFunctionExecutionContext, CancellationToken, ValueTask<int>> CreateMethodHandler(
        string exportName,
        MethodInfo method,
        object? target,
        LuaModuleMapOptions options)
    {
        return async (executionContext, token) =>
        {
            var callContext = new LuaCallContext(executionContext, token, exportName, options.ReturnValueHandler);
            var arguments = BindArguments(method, callContext, options);

            object? invocationResult;
            try
            {
                invocationResult = method.Invoke(target, arguments);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw new LuaMappingException($"执行 Lua 函数 {exportName} 失败。", ex.InnerException);
            }

            return await CompleteInvocationAsync(callContext, invocationResult).ConfigureAwait(false);
        };
    }

    private static object?[] BindArguments(MethodInfo method, LuaCallContext callContext, LuaModuleMapOptions options)
    {
        var parameters = method.GetParameters();
        if (parameters.Length == 0)
        {
            return Array.Empty<object?>();
        }

        var values = new object?[parameters.Length];
        var luaIndex = 0;

        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            if (TryResolveSpecialParameter(parameter, callContext, options, out var resolved))
            {
                values[i] = resolved;
                continue;
            }

            if (IsParamArray(parameter))
            {
                values[i] = BindParamArray(parameter, callContext, ref luaIndex);
                continue;
            }

            values[i] = callContext.ResolveArgument(parameter.ParameterType, luaIndex, parameter);
            luaIndex++;
        }

        return values;
    }

    private static bool TryResolveSpecialParameter(ParameterInfo parameter, LuaCallContext callContext, LuaModuleMapOptions options, out object? value)
    {
        if (parameter.ParameterType == typeof(LuaCallContext))
        {
            value = callContext;
            return true;
        }

        if (parameter.ParameterType == typeof(LuaFunctionExecutionContext))
        {
            value = callContext.ExecutionContext;
            return true;
        }

        if (parameter.ParameterType == typeof(CancellationToken))
        {
            value = callContext.CancellationToken;
            return true;
        }

        if (options.TryResolveService(parameter.ParameterType, out value))
        {
            return true;
        }

        foreach (var resolver in options.ParameterResolvers)
        {
            if (resolver(parameter, callContext, out value))
            {
                return true;
            }
        }

        value = null;
        return false;
    }

    private static bool IsParamArray(ParameterInfo parameter)
    {
        return parameter.GetCustomAttribute<ParamArrayAttribute>() != null;
    }

    private static object BindParamArray(ParameterInfo parameter, LuaCallContext callContext, ref int luaIndex)
    {
        var elementType = parameter.ParameterType.GetElementType() ?? typeof(object);
        var values = new List<object?>();
        var elementIndex = 0;

        while (callContext.HasArgument(luaIndex))
        {
            var alias = $"{parameter.Name}[{elementIndex}]";
            var item = callContext.ResolveArgument(elementType, luaIndex, parameter, alias);
            values.Add(item);
            luaIndex++;
            elementIndex++;
        }

        var result = Array.CreateInstance(elementType, values.Count);
        for (var i = 0; i < values.Count; i++)
        {
            result.SetValue(values[i], i);
        }

        return result;
    }

    private static async ValueTask<int> CompleteInvocationAsync(LuaCallContext context, object? result)
    {
        if (result is null)
        {
            return context.Return();
        }

        switch (result)
        {
            case ValueTask<int> valueTaskInt:
                return await valueTaskInt.ConfigureAwait(false);
            case ValueTask valueTask:
                await valueTask.ConfigureAwait(false);
                return context.Return();
            case Task<int> taskInt:
                return await taskInt.ConfigureAwait(false);
            case Task task:
                await task.ConfigureAwait(false);
                return await CompleteTaskResultAsync(context, task).ConfigureAwait(false);
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

        return await context.ReturnValue(result).ConfigureAwait(false);
    }

    private static async ValueTask<int> CompleteTaskResultAsync(LuaCallContext context, Task task)
    {
        var type = task.GetType();
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var resultProperty = type.GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);
            var value = resultProperty?.GetValue(task);
            return await context.ReturnValue(value).ConfigureAwait(false);
        }

        return context.Return();
    }

    private static bool IsValueTaskStruct(Type type)
    {
        return type == typeof(ValueTask) ||
               (type.IsValueType && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueTask<>));
    }

    private static Func<LuaFunctionExecutionContext, CancellationToken, ValueTask<int>> WrapWithExceptionSafety(
        string name,
        Func<LuaFunctionExecutionContext, CancellationToken, ValueTask<int>> handler)
    {
        return async (context, token) =>
        {
            try
            {
                return await handler(context, token).ConfigureAwait(false);
            }
            catch (LuaMappingException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new LuaMappingException($"执行 Lua 函数 {name} 时发生未知异常。", ex);
            }
        };
    }
}
