using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Lua;
using TaiChi.LuaHost.Exceptions;

namespace TaiChi.LuaHost.Proxies;

/// <summary>
/// 提供 LuaValue 与 .NET 类型之间的最小必要转换能力，包含代理壳解包逻辑。
/// </summary>
internal static class LuaProxyValueConverter
{
    private static readonly ConcurrentDictionary<Type, Func<object, LuaValue>?> ObjectToLuaValueConverters = new();
    private static readonly ConcurrentDictionary<Type, Func<LuaValue, object?>> LuaValueReaders = new();

    /// <summary>
    /// 将 <see cref="LuaValue"/> 转换为指定 .NET 类型。
    /// </summary>
    /// <param name="state">当前 LuaState。</param>
    /// <param name="value">Lua 值。</param>
    /// <param name="destinationType">目标 .NET 类型。</param>
    public static object? ConvertToDotNet(LuaState state, LuaValue value, Type destinationType)
    {
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (destinationType is null)
        {
            throw new ArgumentNullException(nameof(destinationType));
        }

        if (destinationType == typeof(LuaValue))
        {
            return value;
        }

        var nonNullableType = Nullable.GetUnderlyingType(destinationType) ?? destinationType;

        if (value.Type == LuaValueType.Nil)
        {
            return nonNullableType.IsValueType ? Activator.CreateInstance(nonNullableType) : null;
        }

        if (nonNullableType == typeof(LuaTable))
        {
            return value.Read<LuaTable>();
        }

        if (nonNullableType == typeof(LuaFunction))
        {
            return value.Read<LuaFunction>();
        }

        if (nonNullableType == typeof(object))
        {
            return ConvertToObject(state, value);
        }

        if (value.Type == LuaValueType.Table)
        {
            var table = value.Read<LuaTable>();
            if (LuaProxyTableFactory.TryUnwrapProxyTable(table, out var target))
            {
                if (nonNullableType.IsInstanceOfType(target))
                {
                    return target;
                }
            }
        }

        if (nonNullableType.IsEnum)
        {
            return ConvertToEnum(value, nonNullableType);
        }

        try
        {
            return ReadWithCache(value, nonNullableType);
        }
        catch (Exception ex)
        {
            throw new LuaMappingException($"无法将 Lua 值（{value.Type}）转换为 {nonNullableType.Name}。", ex);
        }
    }

    /// <summary>
    /// 尝试将任意 .NET 对象转换为 LuaValue。
    /// </summary>
    /// <param name="value">.NET 对象。</param>
    /// <param name="luaValue">输出 LuaValue。</param>
    public static bool TryConvertToLuaValue(object value, out LuaValue luaValue)
    {
        if (value is LuaValue direct)
        {
            luaValue = direct;
            return true;
        }

        switch (value)
        {
            case string str:
                luaValue = str;
                return true;
            case bool boolean:
                luaValue = boolean;
                return true;
            case int number:
                luaValue = number;
                return true;
            case long longValue:
                luaValue = longValue;
                return true;
            case double doubleValue:
                luaValue = doubleValue;
                return true;
            case float floatValue:
                luaValue = floatValue;
                return true;
            case LuaTable table:
                luaValue = table;
                return true;
            case LuaFunction function:
                luaValue = function;
                return true;

        }

        var type = value.GetType();

        if (type.IsEnum)
        {
            luaValue = value.ToString() ?? string.Empty;
            return true;
        }

        if (TryConvertNumeric(value, out luaValue))
        {
            return true;
        }

        var converter = ObjectToLuaValueConverters.GetOrAdd(type, static t => BuildObjectToLuaValueConverter(t));
        if (converter is null)
        {
            luaValue = LuaValue.Nil;
            return false;
        }

        luaValue = converter(value);
        return true;
    }

    private static object? ConvertToObject(LuaState state, LuaValue value)
    {
        if (value.Type == LuaValueType.Table)
        {
            var table = value.Read<LuaTable>();
            if (LuaProxyTableFactory.TryUnwrapProxyTable(table, out var target))
            {
                return target;
            }

            return table;
        }

        return value.Type switch
        {
            LuaValueType.Boolean => value.Read<bool>(),
            LuaValueType.String => value.Read<string>(),
            LuaValueType.Number => value.Read<double>(),
            LuaValueType.Function => value.Read<LuaFunction>(),
            LuaValueType.UserData => value.Read<object?>(),
            LuaValueType.Thread => value.Read<object?>(),
            _ => value
        };
    }

    private static object ConvertToEnum(LuaValue value, Type enumType)
    {
        if (value.Type == LuaValueType.String)
        {
            var name = value.Read<string>();
            return Enum.Parse(enumType, name, ignoreCase: true);
        }

        if (value.Type == LuaValueType.Number)
        {
            var number = value.Read<double>();
            var underlying = Enum.GetUnderlyingType(enumType);
            var converted = System.Convert.ChangeType(number, underlying, CultureInfo.InvariantCulture);
            return Enum.ToObject(enumType, converted ?? throw new InvalidOperationException());
        }

        throw new LuaMappingException($"无法将 Lua 值（{value.Type}）转换为枚举 {enumType.Name}。");
    }

    private static object? ReadWithCache(LuaValue value, Type destinationType)
    {
        var reader = LuaValueReaders.GetOrAdd(destinationType, static t => BuildLuaValueReader(t));
        return reader(value);
    }

    private static Func<LuaValue, object?> BuildLuaValueReader(Type destinationType)
    {
        var readMethod = typeof(LuaValue).GetMethod(nameof(LuaValue.Read), BindingFlags.Public | BindingFlags.Instance, binder: null, types: Type.EmptyTypes, modifiers: null);
        if (readMethod is null || !readMethod.IsGenericMethodDefinition)
        {
            throw new InvalidOperationException("未找到 LuaValue.Read<T>() 方法。");
        }

        var generic = readMethod.MakeGenericMethod(destinationType);
        var valueParam = Expression.Parameter(typeof(LuaValue), "value");
        var call = Expression.Call(valueParam, generic);
        var box = Expression.Convert(call, typeof(object));
        return Expression.Lambda<Func<LuaValue, object?>>(box, valueParam).Compile();
    }

    private static Func<object, LuaValue>? BuildObjectToLuaValueConverter(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
        var method = type.GetMethod("op_Implicit", flags, binder: null, types: new[] { type }, modifiers: null);
        if (method is null || method.ReturnType != typeof(LuaValue))
        {
            return null;
        }

        var objParam = Expression.Parameter(typeof(object), "obj");
        var cast = Expression.Convert(objParam, type);
        var call = Expression.Call(method, cast);
        return Expression.Lambda<Func<object, LuaValue>>(call, objParam).Compile();
    }

    private static bool TryConvertNumeric(object value, out LuaValue luaValue)
    {
        switch (Type.GetTypeCode(value.GetType()))
        {
            case TypeCode.Byte:
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Int32:
            case TypeCode.UInt32:
            case TypeCode.Int64:
            case TypeCode.UInt64:
                luaValue = System.Convert.ToInt64(value, CultureInfo.InvariantCulture);
                return true;
            case TypeCode.Single:
            case TypeCode.Double:
            case TypeCode.Decimal:
                luaValue = System.Convert.ToDouble(value, CultureInfo.InvariantCulture);
                return true;
            default:
                luaValue = LuaValue.Nil;
                return false;
        }
    }

    /// <summary>
    /// 将 LuaFunctionExecutionContext.GetArgument&lt;object?&gt; 得到的“原始对象”转换为目标 .NET 类型。
    /// </summary>
    /// <param name="state">当前 LuaState。</param>
    /// <param name="value">Lua 参数读取到的对象（string/double/bool/LuaTable/LuaFunction/null 等）。</param>
    /// <param name="destinationType">目标 .NET 类型。</param>
    public static object? ConvertFromLuaObject(LuaState state, object? value, Type destinationType)
    {
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (destinationType is null)
        {
            throw new ArgumentNullException(nameof(destinationType));
        }

        if (destinationType == typeof(LuaValue))
        {
            return LuaProxyTableFactory.WrapValue(state, value);
        }

        var nonNullableType = Nullable.GetUnderlyingType(destinationType) ?? destinationType;

        if (nonNullableType == typeof(object))
        {
            return ConvertLuaObjectToObject(value);
        }

        if (value is null)
        {
            return nonNullableType.IsValueType ? Activator.CreateInstance(nonNullableType) : null;
        }

        if (value is LuaTable table && nonNullableType != typeof(LuaTable))
        {
            if (LuaProxyTableFactory.TryUnwrapProxyTable(table, out var unwrapped) && nonNullableType.IsInstanceOfType(unwrapped))
            {
                return unwrapped;
            }
        }

        if (nonNullableType.IsInstanceOfType(value))
        {
            return value;
        }

        if (nonNullableType.IsEnum)
        {
            return ConvertRawToEnum(value, nonNullableType);
        }

        try
        {
            return System.Convert.ChangeType(value, nonNullableType, CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            throw new LuaMappingException($"无法将 Lua 参数转换为 {nonNullableType.Name}。", ex);
        }
    }

    private static object? ConvertLuaObjectToObject(object? value)
    {
        if (value is LuaTable table && LuaProxyTableFactory.TryUnwrapProxyTable(table, out var target))
        {
            return target;
        }

        return value;
    }

    private static object ConvertRawToEnum(object value, Type enumType)
    {
        if (value is string name)
        {
            return Enum.Parse(enumType, name, ignoreCase: true);
        }

        var underlying = Enum.GetUnderlyingType(enumType);
        var converted = System.Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
        return Enum.ToObject(enumType, converted ?? throw new InvalidOperationException());
    }
}
