using System;
using System.Reflection;
using TaiChi.LuaHost.Contexts;

namespace TaiChi.LuaHost.Attributes;

/// <summary>
/// 配置 Lua 参数绑定的行为（别名、默认值、可空等）。
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class LuaArgAttribute : Attribute
{
    private object? _defaultValue;
    private Type? _defaultValueProviderType;

    /// <summary>
    /// 初始化特性实例。
    /// </summary>
    /// <param name="name">Lua 参数别名，便于错误提示。</param>
    public LuaArgAttribute(string? name = null)
    {
        Name = string.IsNullOrWhiteSpace(name) ? null : name;
    }

    /// <summary>
    /// 获取 Lua 参数别名。
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// 指示参数是否可以省略而返回默认值。
    /// </summary>
    public bool Optional { get; set; }

    /// <summary>
    /// 指示参数是否允许绑定为 null。
    /// </summary>
    public bool AllowNull { get; set; }

    /// <summary>
    /// 获取或设置常量默认值。
    /// </summary>
    public object? DefaultValue
    {
        get => _defaultValue;
        set
        {
            _defaultValue = value;
            HasExplicitDefaultValue = true;
        }
    }

    internal bool HasExplicitDefaultValue { get; private set; }

    /// <summary>
    /// 运行时默认值提供器类型，需实现 <see cref="ILuaArgumentDefaultValueProvider"/>。
    /// </summary>
    public Type? DefaultValueProviderType
    {
        get => _defaultValueProviderType;
        set
        {
            if (value is null)
            {
                _defaultValueProviderType = null;
                return;
            }

            if (!typeof(ILuaArgumentDefaultValueProvider).IsAssignableFrom(value))
            {
                throw new ArgumentException(
                    $"类型 {value.FullName} 必须实现 {nameof(ILuaArgumentDefaultValueProvider)}。", nameof(value));
            }

            _defaultValueProviderType = value;
        }
    }

    internal bool TryResolveDefaultValue(LuaCallContext context, ParameterInfo parameter, Type targetType, out object? value)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (parameter is null)
        {
            throw new ArgumentNullException(nameof(parameter));
        }

        if (HasExplicitDefaultValue)
        {
            value = _defaultValue;
            return true;
        }

        if (_defaultValueProviderType is not null)
        {
            var provider = (ILuaArgumentDefaultValueProvider)Activator.CreateInstance(_defaultValueProviderType)!;
            value = provider.CreateDefaultValue(context, parameter, targetType);
            return true;
        }

        value = null;
        return false;
    }
}

/// <summary>
/// 提供运行时默认值的接口。
/// </summary>
public interface ILuaArgumentDefaultValueProvider
{
    /// <summary>
    /// 返回参数的运行时默认值。
    /// </summary>
    object? CreateDefaultValue(LuaCallContext context, ParameterInfo parameter, Type targetType);
}
