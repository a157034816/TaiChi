using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaiChi.LuaHost.Exceptions;

namespace TaiChi.LuaHost;

/// <summary>
/// 维护可被 Lua 工厂函数（默认 <c>create</c>）创建的 .NET 类型注册表。
/// </summary>
/// <remarks>
/// 解析顺序：白名单（自定义别名 → <see cref="System.Type.FullName"/> → <see cref="System.MemberInfo.Name"/>）
/// → <see cref="System.Type.GetType(string,bool)"/>（覆盖含程序集限定名或 backtick 泛型的全名）
/// → 全程序集扫描（按 <c>FullName</c>/<c>Name</c> 命中，多匹配抛 <see cref="LuaMappingException"/>）。
/// </remarks>
internal sealed class LuaFactoryTypeRegistry
{
    private readonly Dictionary<string, Type> _byAlias = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Type> _byFullName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Type> _byName = new(StringComparer.Ordinal);
    private readonly object _syncRoot = new();

    /// <summary>
    /// 将指定类型登记到工厂白名单。
    /// </summary>
    /// <param name="type">目标类型，不可为 <c>null</c> 且必须可实例化。</param>
    /// <param name="alias">Lua 侧使用的可选别名；为空白则不登记别名索引。</param>
    public void Register(Type type, string? alias = null)
    {
        if (type is null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        EnsureInstantiable(type);

        lock (_syncRoot)
        {
            if (!string.IsNullOrWhiteSpace(alias))
            {
                _byAlias[alias.Trim()] = type;
            }

            if (!string.IsNullOrWhiteSpace(type.FullName))
            {
                _byFullName[type.FullName!] = type;
            }

            _byName[type.Name] = type;
        }
    }

    /// <summary>
    /// 在白名单与可选的全程序集扫描中解析类型。
    /// </summary>
    /// <param name="name">Lua 侧传入的类型名（别名/Name/FullName/<see cref="Type.GetType(string)"/> 可识别的全名）。</param>
    /// <param name="enableAutoResolve">未命中白名单时是否回退到 <see cref="Type.GetType(string,bool)"/> 与全程序集扫描。</param>
    /// <param name="type">命中的类型。</param>
    /// <exception cref="LuaMappingException">当全程序集扫描发现多个同名类型时抛出。</exception>
    public bool TryResolve(string name, bool enableAutoResolve, out Type type)
    {
        type = null!;

        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var key = name.Trim();

        lock (_syncRoot)
        {
            if (_byAlias.TryGetValue(key, out var registered)
                || _byFullName.TryGetValue(key, out registered)
                || _byName.TryGetValue(key, out registered))
            {
                type = registered;
                return true;
            }
        }

        if (!enableAutoResolve)
        {
            return false;
        }

        var direct = Type.GetType(key, throwOnError: false);
        if (direct is not null && IsInstantiable(direct))
        {
            type = direct;
            return true;
        }

        var matches = ResolveFromLoadedAssemblies(key);
        if (matches.Count == 0)
        {
            return false;
        }

        if (matches.Count > 1)
        {
            throw BuildAmbiguousTypeException(key, matches);
        }

        type = matches[0];
        return true;
    }

    private static IReadOnlyList<Type> ResolveFromLoadedAssemblies(string name)
    {
        var matches = new List<Type>();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var type in SafeGetTypes(assembly))
            {
                if (type is null || !IsInstantiable(type))
                {
                    continue;
                }

                if (string.Equals(type.FullName, name, StringComparison.Ordinal)
                    || string.Equals(type.Name, name, StringComparison.Ordinal))
                {
                    matches.Add(type);
                }
            }
        }

        // 同一类型可能被多个 Type.Name/FullName 命中（理论上极少），按引用去重保险起见。
        return matches.Distinct().ToList();
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

    private static bool IsInstantiable(Type type)
    {
        if (type.IsAbstract || type.IsInterface)
        {
            return false;
        }

        if (type.IsGenericTypeDefinition)
        {
            return false;
        }

        return true;
    }

    private static void EnsureInstantiable(Type type)
    {
        if (!IsInstantiable(type))
        {
            throw new LuaMappingException($"类型 {type.FullName ?? type.Name} 不可实例化（抽象类/接口/未闭合泛型），无法登记到工厂注册表。");
        }
    }

    private static LuaMappingException BuildAmbiguousTypeException(string name, IReadOnlyList<Type> matches)
    {
        var items = string.Join(Environment.NewLine, matches.Select(t => $"- {t.FullName} | {t.Assembly.GetName().Name}"));
        var message = $"工厂函数解析类型 \"{name}\" 时发生冲突：发现 {matches.Count} 个同名候选。{Environment.NewLine}" +
                      $"请使用 LuaScriptHost.RegisterFactoryType(Type, alias) 显式登记别名以消除歧义，或改传 FullName。{Environment.NewLine}" +
                      $"候选类型：{Environment.NewLine}{items}";
        return new LuaMappingException(message);
    }
}
