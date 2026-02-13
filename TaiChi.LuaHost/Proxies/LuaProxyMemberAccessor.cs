using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace TaiChi.LuaHost.Proxies;

/// <summary>
/// 为 Lua 对象代理提供成员（属性/字段）访问的反射缓存与 getter/setter 编译。
/// </summary>
internal sealed class LuaProxyMemberAccessor
{
    private readonly ConcurrentDictionary<MemberCacheKey, LuaProxyResolvedMember?> _cache = new();

    /// <summary>
    /// 尝试解析成员访问器。
    /// </summary>
    /// <param name="type">目标类型。</param>
    /// <param name="memberName">成员名称。</param>
    /// <param name="member">解析到的成员信息。</param>
    public bool TryResolve(Type type, string memberName, out LuaProxyResolvedMember member)
    {
        var resolved = _cache.GetOrAdd(new MemberCacheKey(type, memberName), static key => LuaProxyResolvedMember.Create(key.Type, key.MemberName));
        if (resolved is null)
        {
            member = default!;
            return false;
        }

        member = resolved;
        return true;
    }

    /// <summary>
    /// 尝试读取成员值。
    /// </summary>
    /// <param name="target">目标实例。</param>
    /// <param name="memberName">成员名称。</param>
    /// <param name="value">读取到的值。</param>
    public bool TryGetValue(object target, string memberName, out object? value)
    {
        if (!TryResolve(target.GetType(), memberName, out var member) || !member.CanRead)
        {
            value = null;
            return false;
        }

        value = member.Get(target);
        return true;
    }

    /// <summary>
    /// 尝试写入成员值。
    /// </summary>
    /// <param name="target">目标实例。</param>
    /// <param name="memberName">成员名称。</param>
    /// <param name="value">待写入的值。</param>
    public bool TrySetValue(object target, string memberName, object? value)
    {
        if (!TryResolve(target.GetType(), memberName, out var member) || !member.CanWrite)
        {
            return false;
        }

        member.Set(target, value);
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
    /// 表示一个已解析的成员访问器。
    /// </summary>
    internal sealed class LuaProxyResolvedMember
    {
        private readonly Func<object, object?>? _getter;
        private readonly Action<object, object?>? _setter;

        private LuaProxyResolvedMember(Type memberType, Func<object, object?>? getter, Action<object, object?>? setter)
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
        /// <param name="target">目标实例。</param>
        public object? Get(object target)
        {
            if (_getter is null)
            {
                throw new InvalidOperationException("成员不支持读取。");
            }

            return _getter(target);
        }

        /// <summary>
        /// 写入成员值。
        /// </summary>
        /// <param name="target">目标实例。</param>
        /// <param name="value">值。</param>
        public void Set(object target, object? value)
        {
            if (_setter is null)
            {
                throw new InvalidOperationException("成员不支持写入。");
            }

            _setter(target, value);
        }

        /// <summary>
        /// 基于类型与成员名创建访问器。
        /// </summary>
        /// <param name="type">目标类型。</param>
        /// <param name="memberName">成员名称。</param>
        public static LuaProxyResolvedMember? Create(Type type, string memberName)
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
                return new LuaProxyResolvedMember(property.PropertyType, getter, setter);
            }

            var field = FindField(type, memberName);
            if (field != null)
            {
                var getter = CreateFieldGetter(field);
                var setter = field.IsInitOnly ? null : CreateFieldSetter(field);
                return new LuaProxyResolvedMember(field.FieldType, getter, setter);
            }

            return null;
        }

        private static PropertyInfo? FindProperty(Type type, string memberName)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

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
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

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

        private static Func<object, object?>? CreatePropertyGetter(PropertyInfo property)
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

            return target => property.GetValue(target);
        }

        private static Action<object, object?>? CreatePropertySetter(PropertyInfo property)
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

            return (target, value) => property.SetValue(target, value);
        }

        private static Func<object, object?> CreateFieldGetter(FieldInfo field)
        {
            if (field.IsPublic && IsPublicType(field.DeclaringType))
            {
                return CompileGetter(field);
            }

            return target => field.GetValue(target);
        }

        private static Action<object, object?> CreateFieldSetter(FieldInfo field)
        {
            if (field.IsPublic && IsPublicType(field.DeclaringType))
            {
                return CompileSetter(field);
            }

            return (target, value) => field.SetValue(target, value);
        }

        private static bool IsPublicType(Type? type)
        {
            return type != null && (type.IsPublic || type.IsNestedPublic);
        }

        private static Func<object, object?> CompileGetter(PropertyInfo property)
        {
            var targetParam = Expression.Parameter(typeof(object), "target");
            var typedTarget = Expression.Convert(targetParam, property.DeclaringType!);
            var access = Expression.Property(typedTarget, property);
            var box = Expression.Convert(access, typeof(object));
            return Expression.Lambda<Func<object, object?>>(box, targetParam).Compile();
        }

        private static Action<object, object?> CompileSetter(PropertyInfo property)
        {
            var targetParam = Expression.Parameter(typeof(object), "target");
            var valueParam = Expression.Parameter(typeof(object), "value");
            var typedTarget = Expression.Convert(targetParam, property.DeclaringType!);
            var typedValue = Expression.Convert(valueParam, property.PropertyType);
            var assign = Expression.Assign(Expression.Property(typedTarget, property), typedValue);
            return Expression.Lambda<Action<object, object?>>(assign, targetParam, valueParam).Compile();
        }

        private static Func<object, object?> CompileGetter(FieldInfo field)
        {
            var targetParam = Expression.Parameter(typeof(object), "target");
            var typedTarget = Expression.Convert(targetParam, field.DeclaringType!);
            var access = Expression.Field(typedTarget, field);
            var box = Expression.Convert(access, typeof(object));
            return Expression.Lambda<Func<object, object?>>(box, targetParam).Compile();
        }

        private static Action<object, object?> CompileSetter(FieldInfo field)
        {
            var targetParam = Expression.Parameter(typeof(object), "target");
            var valueParam = Expression.Parameter(typeof(object), "value");
            var typedTarget = Expression.Convert(targetParam, field.DeclaringType!);
            var typedValue = Expression.Convert(valueParam, field.FieldType);
            var assign = Expression.Assign(Expression.Field(typedTarget, field), typedValue);
            return Expression.Lambda<Action<object, object?>>(assign, targetParam, valueParam).Compile();
        }
    }
}
