using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Lua;
using TaiChi.LuaHost.Exceptions;

namespace TaiChi.LuaHost.Proxies;

/// <summary>
/// 负责为任意 .NET 对象创建可在 Lua 中使用的“代理壳”（LuaTable），并提供值包装/解包能力。
/// </summary>
public static class LuaProxyTableFactory
{
    internal const string ProxyFieldName = "$__tai_chi_proxy";

    private static readonly LuaProxyMemberAccessor MemberAccessor = new();
    private static readonly ConditionalWeakTable<LuaState, LuaProxyStateCache> StateCaches = new();
    private static readonly ConditionalWeakTable<LuaTable, LuaObjectProxy> ProxyByTable = new();

    private static readonly Action<LuaTable, LuaTable> SetMetatableAction = BuildSetMetatableAction();

    /// <summary>
    /// 创建或复用某个对象在指定 LuaState 下的代理壳。
    /// </summary>
    /// <param name="state">目标 LuaState。</param>
    /// <param name="target">被代理的对象。</param>
    public static LuaTable Wrap(LuaState state, object target)
    {
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (target is null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        var cache = StateCaches.GetValue(state, static _ => new LuaProxyStateCache(CreateProxyMetatable()));
        if (cache.TableByTarget.TryGetValue(target, out var cached))
        {
            return cached;
        }

        var proxy = new LuaObjectProxy(state, target, MemberAccessor);
        var table = new LuaTable
        {
            [ProxyFieldName] = proxy
        };

        SetMetatableAction(table, cache.ProxyMetatable);

        cache.TableByTarget.Add(target, table);
        ProxyByTable.Add(table, proxy);

        return table;
    }

    /// <summary>
    /// 将返回值转换为 LuaValue；遇到引用类型时自动包装为代理壳。
    /// </summary>
    /// <param name="state">目标 LuaState。</param>
    /// <param name="value">待包装值。</param>
    public static LuaValue WrapValue(LuaState state, object? value)
    {
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (value is null)
        {
            return LuaValue.Nil;
        }

        if (LuaProxyValueConverter.TryConvertToLuaValue(value, out var luaValue))
        {
            return luaValue;
        }

        var type = value.GetType();
        if (!type.IsValueType)
        {
            return Wrap(state, value);
        }

        return value.ToString() ?? string.Empty;
    }

    /// <summary>
    /// 尝试从代理壳中解包得到真实对象。
    /// </summary>
    /// <param name="table">LuaTable。</param>
    /// <param name="target">解包出的真实对象。</param>
    internal static bool TryUnwrapProxyTable(LuaTable table, out object target)
    {
        if (TryGetProxy(table, out var proxy))
        {
            target = proxy.Target;
            return true;
        }

        target = null!;
        return false;
    }

    internal static bool TryGetProxy(LuaTable table, out LuaObjectProxy proxy)
    {
        if (ProxyByTable.TryGetValue(table, out proxy!))
        {
            return true;
        }

        try
        {
            var candidate = table[ProxyFieldName];
            if (candidate.Type == LuaValueType.UserData)
            {
                proxy = candidate.Read<LuaObjectProxy>();
                return true;
            }
        }
        catch
        {
            // ignore
        }

        proxy = null!;
        return false;
    }

    private sealed class LuaProxyStateCache
    {
        public LuaProxyStateCache(LuaTable proxyMetatable)
        {
            ProxyMetatable = proxyMetatable;
        }

        public ConditionalWeakTable<object, LuaTable> TableByTarget { get; } = new();

        public LuaTable ProxyMetatable { get; }
    }

    private static LuaTable CreateProxyMetatable()
    {
        var metatable = new LuaTable();
        metatable["__index"] = new LuaFunction(IndexAsync);
        metatable["__newindex"] = new LuaFunction(NewIndexAsync);
        return metatable;
    }

    private static ValueTask<int> IndexAsync(LuaFunctionExecutionContext context, CancellationToken token)
    {
        _ = token;

        var table = context.GetArgument<LuaTable>(0);
        if (!TryGetProxy(table, out var proxy))
        {
            return new ValueTask<int>(context.Return());
        }

        if (!context.HasArgument(1))
        {
            return new ValueTask<int>(context.Return());
        }

        string memberName;
        try
        {
            memberName = context.GetArgument<string>(1);
        }
        catch
        {
            return new ValueTask<int>(context.Return());
        }

        var value = proxy.Get(memberName);

        if (value.Type == LuaValueType.Function)
        {
            table[memberName] = value;
        }

        return new ValueTask<int>(LuaProxyReturnHelper.Return(context, value));
    }

    private static ValueTask<int> NewIndexAsync(LuaFunctionExecutionContext context, CancellationToken token)
    {
        _ = token;

        var table = context.GetArgument<LuaTable>(0);
        if (!TryGetProxy(table, out var proxy))
        {
            return new ValueTask<int>(context.Return());
        }

        if (!context.HasArgument(1))
        {
            return new ValueTask<int>(context.Return());
        }

        string memberName;
        try
        {
            memberName = context.GetArgument<string>(1);
        }
        catch
        {
            return new ValueTask<int>(context.Return());
        }

        var rawValue = context.HasArgument(2) ? context.GetArgument<object?>(2) : null;
        proxy.SetRaw(memberName, rawValue);
        return new ValueTask<int>(context.Return());
    }

    private static Action<LuaTable, LuaTable> BuildSetMetatableAction()
    {
        var tableType = typeof(LuaTable);
        var luaValueType = typeof(LuaValue);

        var setMethod = tableType.GetMethod("SetMetatable", BindingFlags.Public | BindingFlags.Instance, binder: null, types: new[] { tableType }, modifiers: null);
        if (setMethod != null)
        {
            return (table, metatable) => setMethod.Invoke(table, new object?[] { metatable });
        }

        setMethod = tableType.GetMethod("SetMetatable", BindingFlags.Public | BindingFlags.Instance, binder: null, types: new[] { luaValueType }, modifiers: null);
        if (setMethod != null)
        {
            return (table, metatable) => setMethod.Invoke(table, new object?[] { (LuaValue)metatable });
        }

        var metatableProperty = tableType.GetProperty("Metatable", BindingFlags.Public | BindingFlags.Instance);
        if (metatableProperty != null && metatableProperty.CanWrite && metatableProperty.PropertyType == tableType)
        {
            return (table, metatable) => metatableProperty.SetValue(table, metatable);
        }

        metatableProperty = tableType.GetProperty("MetaTable", BindingFlags.Public | BindingFlags.Instance);
        if (metatableProperty != null && metatableProperty.CanWrite && metatableProperty.PropertyType == tableType)
        {
            return (table, metatable) => metatableProperty.SetValue(table, metatable);
        }

        metatableProperty = tableType.GetProperty("Metatable", BindingFlags.Public | BindingFlags.Instance);
        if (metatableProperty != null && metatableProperty.CanWrite && metatableProperty.PropertyType == luaValueType)
        {
            return (table, metatable) => metatableProperty.SetValue(table, (LuaValue)metatable);
        }

        metatableProperty = tableType.GetProperty("MetaTable", BindingFlags.Public | BindingFlags.Instance);
        if (metatableProperty != null && metatableProperty.CanWrite && metatableProperty.PropertyType == luaValueType)
        {
            return (table, metatable) => metatableProperty.SetValue(table, (LuaValue)metatable);
        }

        return (_, _) => throw new LuaMappingException("当前 LuaCSharp 版本未暴露 LuaTable 的 Metatable 设置接口。");
    }
}
