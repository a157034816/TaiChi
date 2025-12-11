using System;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Lua;
using TaiChi.LuaHost.Attributes;
using TaiChi.LuaHost.Exceptions;
using TaiChi.LuaHost.Options;

namespace TaiChi.LuaHost.Contexts;

/// <summary>
/// 对 <see cref="LuaFunctionExecutionContext"/> 的轻量封装，提供类型安全的参数访问与统一返回方法。
/// </summary>
public sealed class LuaCallContext
{
    private readonly string _functionName;
    private readonly LuaModuleReturnValueHandler? _returnValueHandler;

    /// <summary>
    /// 初始化上下文实例。
    /// </summary>
    /// <param name="executionContext">Lua 调用上下文。</param>
    /// <param name="cancellationToken">取消标记。</param>
    /// <param name="functionName">当前处理的函数名称。</param>
    /// <param name="returnValueHandler">可选的自定义返回值处理器。</param>
    public LuaCallContext(
        LuaFunctionExecutionContext executionContext,
        CancellationToken cancellationToken,
        string functionName,
        LuaModuleReturnValueHandler? returnValueHandler = null)
    {
        if (functionName is null)
        {
            throw new ArgumentNullException(nameof(functionName));
        }

        ExecutionContext = executionContext;
        CancellationToken = cancellationToken;
        _functionName = functionName;
        _returnValueHandler = returnValueHandler;
    }

    /// <summary>
    /// 获取底层 Lua 上下文。
    /// </summary>
    public LuaFunctionExecutionContext ExecutionContext { get; }

    /// <summary>
    /// 获取取消标记。
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// 判断指定索引的参数是否存在。
    /// </summary>
    public bool HasArgument(int index) => ExecutionContext.HasArgument(index);

    /// <summary>
    /// 获取必填参数。
    /// </summary>
    /// <typeparam name="T">参数类型。</typeparam>
    /// <param name="index">参数索引。</param>
    /// <param name="parameterName">参数名称，用于异常提示。</param>
    public T Require<T>(int index, string? parameterName = null)
    {
        if (!HasArgument(index))
        {
            throw new LuaMappingException(FormatMissingArgumentMessage(index, parameterName));
        }

        return ExecutionContext.GetArgument<T>(index);
    }

    /// <summary>
    /// 获取可选参数。
    /// </summary>
    /// <typeparam name="T">参数类型。</typeparam>
    /// <param name="index">参数索引。</param>
    /// <param name="defaultValue">缺省值。</param>
    public T? Get<T>(int index, T? defaultValue = default)
    {
        return HasArgument(index) ? ExecutionContext.GetArgument<T>(index) : defaultValue;
    }

    /// <summary>
    /// 返回空结果。
    /// </summary>
    public int Return() => ExecutionContext.Return();

    /// <summary>
    /// 返回单一结果。
    /// </summary>
    public int Return(object? value)
    {
        return value is null ? ExecutionContext.Return() : ReturnWithBestEffort(value);
    }

    /// <summary>
    /// 根据方法签名解析实参。
    /// </summary>
    /// <param name="targetType">目标类型。</param>
    /// <param name="argumentIndex">Lua 实参索引。</param>
    /// <param name="parameter">方法参数信息。</param>
    internal object? ResolveArgument(Type targetType, int argumentIndex, ParameterInfo parameter, string? alias = null)
    {
        if (targetType is null)
        {
            throw new ArgumentNullException(nameof(targetType));
        }

        if (parameter is null)
        {
            throw new ArgumentNullException(nameof(parameter));
        }

        var argAttribute = parameter.GetCustomAttribute<LuaArgAttribute>();
        var parameterName = alias ?? argAttribute?.Name ?? parameter.Name;
        if (!HasArgument(argumentIndex))
        {
            if (TryResolveAttributeDefault(targetType, parameter, parameterName, argAttribute, out var defaultValue))
            {
                return defaultValue;
            }

            if (TryResolveParameterDefault(targetType, parameter, parameterName, out defaultValue))
            {
                return defaultValue;
            }

            if (argAttribute?.Optional == true)
            {
                return AllowsNull(targetType) ? null : GetDefaultValue(targetType);
            }

            if (AllowsNull(targetType) || argAttribute?.AllowNull == true)
            {
                return null;
            }

            throw new LuaMappingException(FormatMissingArgumentMessage(argumentIndex, parameterName));
        }

        var rawValue = ExecutionContext.GetArgument<object?>(argumentIndex);
        return ConvertValueCore(rawValue, targetType, parameter, parameterName, argAttribute?.AllowNull == true);
    }

    /// <summary>
    /// 将同步结果写入 Lua 栈。
    /// </summary>
    /// <param name="value">返回值。</param>
    internal ValueTask<int> ReturnValue(object? value)
    {
        return value is null
            ? new ValueTask<int>(Return())
            : new ValueTask<int>(Return(value));
    }

    private static bool AllowsNull(Type type)
    {
        if (!type.IsValueType)
        {
            return true;
        }

        return Nullable.GetUnderlyingType(type) != null;
    }

    private bool TryResolveAttributeDefault(Type targetType, ParameterInfo parameter, string? parameterName, LuaArgAttribute? attribute, out object? value)
    {
        if (attribute is null)
        {
            value = null;
            return false;
        }

        if (!attribute.TryResolveDefaultValue(this, parameter, targetType, out var rawValue))
        {
            value = null;
            return false;
        }

        value = ConvertValueCore(rawValue, targetType, parameter, parameterName, attribute.AllowNull);
        return true;
    }

    private bool TryResolveParameterDefault(Type targetType, ParameterInfo parameter, string? parameterName, out object? value)
    {
        if (!parameter.HasDefaultValue)
        {
            value = null;
            return false;
        }

        var rawDefault = parameter.DefaultValue;
        if (rawDefault == DBNull.Value || rawDefault == Type.Missing)
        {
            rawDefault = AllowsNull(targetType) ? null : GetDefaultValue(targetType);
        }

        value = ConvertValueCore(rawDefault, targetType, parameter, parameterName, allowNullOverride: true);
        return true;
    }

    private object? ConvertValueCore(object? value, Type targetType, ParameterInfo parameter, string? parameterName, bool allowNullOverride = false)
    {
        var allowsNull = AllowsNull(targetType);
        var destinationType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (value is null)
        {
            if (allowNullOverride)
            {
                return allowsNull ? null : GetDefaultValue(destinationType);
            }

            if (allowsNull)
            {
                return null;
            }

            var displayName = parameterName ?? parameter.Name;
            throw new LuaMappingException($"Lua 函数 {_functionName} 的参数 {displayName} 不能为空。");
        }

        if (destinationType.IsInstanceOfType(value))
        {
            return value;
        }

        try
        {
            if (destinationType.IsEnum)
            {
                if (value is string enumName)
                {
                    return Enum.Parse(destinationType, enumName, ignoreCase: true);
                }

                var numericValue = Convert.ChangeType(value, Enum.GetUnderlyingType(destinationType), CultureInfo.InvariantCulture);
                return Enum.ToObject(destinationType, numericValue ?? throw new InvalidOperationException());
            }

            return Convert.ChangeType(value, destinationType, CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            var displayName = parameterName ?? parameter.Name;
            throw new LuaMappingException($"无法将参数 {displayName} 转换为 {destinationType.Name}。", ex);
        }
    }

    private static object? GetDefaultValue(Type type)
    {
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    private int ReturnWithBestEffort(object value)
    {
        return value switch
        {
            string str => ExecutionContext.Return(str),
            bool boolean => ExecutionContext.Return(boolean),
            int number => ExecutionContext.Return(number),
            long longValue => ExecutionContext.Return(longValue),
            double doubleValue => ExecutionContext.Return(doubleValue),
            float floatValue => ExecutionContext.Return(floatValue),
            LuaTable table => ExecutionContext.Return(table),
            LuaFunction function => ExecutionContext.Return(function),
            _ => TryReturnCustomOrFallback(value)
        };
    }

    private int TryReturnCustomOrFallback(object value)
    {
        if (_returnValueHandler != null && _returnValueHandler(this, value, out var handledCount))
        {
            return handledCount;
        }

        return InvokeReturnWithReflection(value);
    }

    private int InvokeReturnWithReflection(object value)
    {
        var method = typeof(LuaFunctionExecutionContext).GetMethod("Return", new[] { value.GetType() });
        if (method != null)
        {
            return (int)method.Invoke(ExecutionContext, new[] { value });
        }

        throw new LuaMappingException($"Lua 函数 {_functionName} 暂不支持返回类型 {value.GetType().Name}。");
    }

    private string FormatMissingArgumentMessage(int index, string? parameterName)
    {
        var ordinal = (index + 1).ToString(CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(parameterName)
            ? $"Lua 函数 {_functionName} 缺少第 {ordinal} 个参数。"
            : $"Lua 函数 {_functionName} 缺少参数 {parameterName}（位置 {ordinal}）。";
    }
}
