using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Lua;
using TaiChi.LuaHost.Exceptions;

namespace TaiChi.LuaHost.Proxies;

/// <summary>
/// 负责为任意 .NET 类型创建可在 Lua 中使用的“静态类型代理壳”（LuaTable）。
/// </summary>
public static class LuaStaticProxyTableFactory
{
    internal const string ProxyFieldName = "$__tai_chi_static_proxy";
    internal const string StaticRootGlobalName = "static";

    private static readonly LuaStaticProxyMemberAccessor MemberAccessor = new();
    private static readonly ConditionalWeakTable<LuaState, LuaStaticProxyStateCache> StateCaches = new();
    private static readonly ConditionalWeakTable<LuaTable, LuaStaticTypeProxy> ProxyByTable = new();
    private static readonly Action<LuaTable, LuaTable> SetMetatableAction = BuildSetMetatableAction();

    /// <summary>
    /// 创建或复用某个类型在指定 LuaState 下的静态代理壳。
    /// </summary>
    /// <param name="state">目标 LuaState。</param>
    /// <param name="type">目标类型。</param>
    public static LuaTable Wrap(LuaState state, Type type)
    {
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (type is null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        var cache = StateCaches.GetValue(state, static _ => new LuaStaticProxyStateCache(CreateTypeProxyMetatable()));
        lock (cache.SyncRoot)
        {
            if (cache.TableByType.TryGetValue(type, out var cached))
            {
                return cached;
            }

            var table = new LuaTable();
            var proxy = new LuaStaticTypeProxy(state, table, type, MemberAccessor);

            table[ProxyFieldName] = proxy;
            SetMetatableAction(table, cache.TypeProxyMetatable);

            cache.TableByType[type] = table;
            ProxyByTable.Add(table, proxy);

            return table;
        }
    }

    /// <summary>
    /// 确保指定 LuaState 的全局环境已初始化 <c>static</c> 根表，并返回该根表。
    /// </summary>
    /// <param name="state">目标 LuaState。</param>
    /// <param name="options">宿主配置。</param>
    internal static LuaTable EnsureStaticRoot(LuaState state, LuaScriptHostOptions options)
    {
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        var cache = StateCaches.GetValue(state, static _ => new LuaStaticProxyStateCache(CreateTypeProxyMetatable()));
        if (cache.StaticRootTable is null)
        {
            lock (cache.SyncRoot)
            {
                cache.StaticRootTable ??= CreateStaticRootTable(state, options);
            }
        }

        state.Environment[StaticRootGlobalName] = cache.StaticRootTable;
        return cache.StaticRootTable;
    }

    private sealed class LuaStaticProxyStateCache
    {
        public LuaStaticProxyStateCache(LuaTable typeProxyMetatable)
        {
            TypeProxyMetatable = typeProxyMetatable;
        }

        public object SyncRoot { get; } = new();

        public Dictionary<Type, LuaTable> TableByType { get; } = new();

        public LuaTable TypeProxyMetatable { get; }

        public LuaTable? StaticRootTable { get; set; }
    }

    private static LuaTable CreateTypeProxyMetatable()
    {
        var metatable = new LuaTable();
        metatable["__index"] = new LuaFunction(IndexAsync);
        metatable["__newindex"] = new LuaFunction(NewIndexAsync);
        return metatable;
    }

    private static LuaTable CreateStaticRootTable(LuaState state, LuaScriptHostOptions options)
    {
        var rootTable = new LuaTable();

        var metatable = new LuaTable();
        metatable["__index"] = new LuaFunction((context, token) => RootIndexAsync(state, options, context, token));
        SetMetatableAction(rootTable, metatable);

        return rootTable;
    }

    private static ValueTask<int> RootIndexAsync(LuaState state, LuaScriptHostOptions options, LuaFunctionExecutionContext context, CancellationToken token)
    {
        _ = token;

        if (!options.EnableStaticAutoRegister)
        {
            return new ValueTask<int>(context.Return());
        }

        var rootTable = context.GetArgument<LuaTable>(0);
        if (!context.HasArgument(1))
        {
            return new ValueTask<int>(context.Return());
        }

        string alias;
        try
        {
            alias = context.GetArgument<string>(1);
        }
        catch
        {
            return new ValueTask<int>(context.Return());
        }

        if (string.IsNullOrWhiteSpace(alias))
        {
            return new ValueTask<int>(context.Return());
        }

        var candidates = ResolveStaticTypesByName(alias);
        if (candidates.Count == 0)
        {
            return new ValueTask<int>(context.Return());
        }

        if (candidates.Count > 1)
        {
            throw BuildStaticAutoRegisterConflictException(alias, candidates);
        }

        var proxyTable = Wrap(state, candidates[0]);
        rootTable[alias] = proxyTable;
        return new ValueTask<int>(context.Return(proxyTable));
    }

    private static IReadOnlyList<Type> ResolveStaticTypesByName(string typeName)
    {
        var matches = new List<Type>();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var type in SafeGetTypes(assembly))
            {
                if (type is null)
                {
                    continue;
                }

                if (!type.IsClass || !type.IsAbstract || !type.IsSealed)
                {
                    continue;
                }

                if (!string.Equals(type.Name, typeName, StringComparison.Ordinal))
                {
                    continue;
                }

                matches.Add(type);
            }
        }

        return matches;
    }

    private static IEnumerable<Type?> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types;
        }
        catch
        {
            return Array.Empty<Type?>();
        }
    }

    private static LuaMappingException BuildStaticAutoRegisterConflictException(string alias, IReadOnlyList<Type> candidates)
    {
        var items = string.Join(Environment.NewLine, candidates.Select(t => $"- {t.FullName} | {t.Assembly.GetName().Name}"));
        var message = $"static.{alias} 自动解析发生冲突：发现 {candidates.Count} 个同名 static class。{Environment.NewLine}" +
                      $"请使用 LuaScriptHost.RegisterStaticType(Type, alias) 显式注册别名以消除歧义。{Environment.NewLine}" +
                      $"候选类型：{Environment.NewLine}{items}";
        return new LuaMappingException(message);
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

    private static bool TryGetProxy(LuaTable table, out LuaStaticTypeProxy proxy)
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
                proxy = candidate.Read<LuaStaticTypeProxy>();
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
        if (metatableProperty !=null && metatableProperty.CanWrite && metatableProperty.PropertyType == luaValueType)
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

