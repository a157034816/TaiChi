using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace TaiChi.LuaHost.Proxies;

/// <summary>
/// 为 Lua 静态类型代理提供静态成员（属性/字段）访问的反射缓存与 getter/setter 编译。
/// </summary>
internal sealed class LuaStaticProxyMemberAccessor
{
    private readonly ConcurrentDictionary<MemberCacheKey, LuaStaticResolvedMember?> _cache = new();

    /// <summary>
    /// 尝试解析静态成员访问器。
    /// </summary>
    /// <param name="type">目标类型。</param>
    /// <param name="memberName">成员名称。</param>
    /// <param name="member">解析到的成员信息。</param>
    public bool TryResolve(Type type, string memberName, out LuaStaticResolvedMember member)
    {
        var resolved = _cache.GetOrAdd(new MemberCacheKey(type, memberName), static key => LuaStaticResolvedMember.Create(key.Type, key.MemberName));
        if (resolved is null)
        {
            member = default!;
            return false;
        }

        member = resolved;
        return true;
    }

    /// <summary>
    /// 尝试读取静态成员值。
    /// </summary>
    /// <param name="type">目标类型。</param>
    /// <param name="memberName">成员名称。</param>
    /// <param name="value">读取到的值。</param>
    public bool TryGetValue(Type type, string memberName, out object? value)
    {
        if (!TryResolve(type, memberName, out var member) || !member.CanRead)
        {
            value = null;
            return false;
        }

        value = member.Get();
        return true;
    }

    /// <summary>
    /// 尝试写入静态成员值。
    /// </summary>
    /// <param name="type">目标类型。</param>
    /// <param name="memberName">成员名称。</param>
    /// <param name="value">待写入的值。</param>
    public bool TrySetValue(Type type, string memberName, object? value)
    {
        if (!TryResolve(type, memberName, out var member) || !member.CanWrite)
        {
            return false;
        }

        member.Set(value);
        return true;
    }

    private readonly struct MemberCacheKey : IEquatable<MemberCacheKey>
    {
        public MemberCacheKey(Type type, string memberName)
        {
            Type = type;
            MemberName = memberName;
        }

        public Type Type { get; }

        public string MemberName { get; }

        public bool Equals(MemberCacheKey other)
        {
            return ReferenceEquals(Type, other.Type) && string.Equals(MemberName, other.MemberName, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is MemberCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Type, StringComparer.Ordinal.GetHashCode(MemberName));
        }
    }

    /// <summary>
    /// 表示一个已解析的静态成员访问器。
    /// </summary>
    internal sealed class LuaStaticResolvedMember
    {
        private readonly Func<object?>? _getter;
        private readonly Action<object?>? _setter;

        private LuaStaticResolvedMember(Type memberType, Func<object?>? getter, Action<object?>? setter)
        {
            MemberType = memberType;
            _getter = getter;
            _setter = setter;
        }

        /// <summary>
        /// 获取成员类型。
        /// </summary>
        public Type MemberType { get; }

        /// <summary>
        /// 获取是否可读。
        /// </summary>
        public bool CanRead => _getter != null;

        /// <summary>
        /// 获取是否可写。
        /// </summary>
        public bool CanWrite => _setter != null;

        /// <summary>
        /// 读取成员值。
        /// </summary>
        public object? Get()
        {
            if (_getter is null)
            {
                throw new InvalidOperationException("成员不支持读取。");
            }

            return _getter();
        }

        /// <summary>
        /// 写入成员值。
        /// </summary>
        /// <param name="value">值。</param>
        public void Set(object? value)
        {
            if (_setter is null)
            {
                throw new InvalidOperationException("成员不支持写入。");
            }

            _setter(value);
        }

        /// <summary>
        /// 基于类型与成员名创建访问器。
        /// </summary>
        /// <param name="type">目标类型。</param>
        /// <param name="memberName">成员名称。</param>
        public static LuaStaticResolvedMember? Create(Type type, string memberName)
        {
            if (type is null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (string.IsNullOrWhiteSpace(memberName))
            {
                return null;
            }

            var property = FindProperty(type, memberName);
            if (property != null)
            {
                if (property.GetIndexParameters().Length > 0)
                {
                    return null;
                }

                var getter = CreatePropertyGetter(property);
                var setter = CreatePropertySetter(property);
                if (getter is null && setter is null)
                {
                    return null;
                }

                return new LuaStaticResolvedMember(property.PropertyType, getter, setter);
            }

            var field = FindField(type, memberName);
            if (field != null)
            {
                var getter = CreateFieldGetter(field);
                var setter = field.IsInitOnly ? null : CreateFieldSetter(field);
                return new LuaStaticResolvedMember(field.FieldType, getter, setter);
            }

            return null;
        }

        private static PropertyInfo? FindProperty(Type type, string memberName)
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            for (var current = type; current != null; current = current.BaseType)
            {
                var property = current.GetProperty(memberName, flags);
                if (property != null)
                {
                    return property;
                }
            }

            return null;
        }

        private static FieldInfo? FindField(Type type, string memberName)
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            for (var current = type; current != null; current = current.BaseType)
            {
                var field = current.GetField(memberName, flags);
                if (field != null)
                {
                    return field;
                }
            }

            return null;
        }

        private static Func<object?>? CreatePropertyGetter(PropertyInfo property)
        {
            var getter = property.GetGetMethod(nonPublic: true);
            if (getter is null)
            {
                return null;
            }

            if (getter.IsPublic && IsPublicType(property.DeclaringType))
            {
                return CompileGetter(property);
            }

            return () => property.GetValue(null);
        }

        private static Action<object?>? CreatePropertySetter(PropertyInfo property)
        {
            var setter = property.GetSetMethod(nonPublic: true);
            if (setter is null)
            {
                return null;
            }

            if (setter.IsPublic && IsPublicType(property.DeclaringType))
            {
                return CompileSetter(property);
            }

            return value => property.SetValue(null, value);
        }

        private static Func<object?> CreateFieldGetter(FieldInfo field)
        {
            if (field.IsPublic && IsPublicType(field.DeclaringType))
            {
                return CompileGetter(field);
            }

            return () => field.GetValue(null);
        }

        private static Action<object?> CreateFieldSetter(FieldInfo field)
        {
            if (field.IsPublic && IsPublicType(field.DeclaringType))
            {
                return CompileSetter(field);
            }

            return value => field.SetValue(null, value);
        }

        private static bool IsPublicType(Type? type)
        {
            return type != null && (type.IsPublic || type.IsNestedPublic);
        }

        private static Func<object?> CompileGetter(PropertyInfo property)
        {
            var access = Expression.Property(instance: null, property);
            var box = Expression.Convert(access, typeof(object));
            return Expression.Lambda<Func<object?>>(box).Compile();
        }

        private static Action<object?> CompileSetter(PropertyInfo property)
        {
            var valueParam = Expression.Parameter(typeof(object), "value");
            var typedValue = Expression.Convert(valueParam, property.PropertyType);
            var assign = Expression.Assign(Expression.Property(instance: null, property), typedValue);
            return Expression.Lambda<Action<object?>>(assign, valueParam).Compile();
        }

        private static Func<object?> CompileGetter(FieldInfo field)
        {
            var access = Expression.Field(expression: null, field);
            var box = Expression.Convert(access, typeof(object));
            return Expression.Lambda<Func<object?>>(box).Compile();
        }

        private static Action<object?> CompileSetter(FieldInfo field)
        {
            var valueParam = Expression.Parameter(typeof(object), "value");
            var typedValue = Expression.Convert(valueParam, field.FieldType);
            var assign = Expression.Assign(Expression.Field(expression: null, field), typedValue);
            return Expression.Lambda<Action<object?>>(assign, valueParam).Compile();
        }
    }
}
