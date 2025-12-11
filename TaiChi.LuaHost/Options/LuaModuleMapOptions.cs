using System;
using System.Collections.Generic;
using System.Reflection;
using TaiChi.LuaHost.Contexts;
using TaiChi.LuaHost.Naming;

namespace TaiChi.LuaHost.Options;

/// <summary>
/// 自定义 MapObject/MapStaticClass 行为的配置。
/// </summary>
public sealed class LuaModuleMapOptions
{
    private readonly Dictionary<Type, object?> _services = new();
    private ILuaNamingStrategy _namingStrategy = SnakeCaseNamingStrategy.Instance;

    /// <summary>
    /// 是否包含未显式标记 <see cref="Attributes.LuaModuleMethodAttribute"/> 的方法。
    /// </summary>
    public bool IncludeImplicitMembers { get; set; }

    /// <summary>
    /// 导出名称策略，默认使用 <see cref="SnakeCaseNamingStrategy"/>。
    /// </summary>
    public ILuaNamingStrategy NamingStrategy
    {
        get => _namingStrategy;
        set => _namingStrategy = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// 自定义参数注入器集合。
    /// </summary>
    public IList<LuaModuleParameterResolver> ParameterResolvers { get; } = new List<LuaModuleParameterResolver>();

    /// <summary>
    /// 自定义返回类型处理器，返回 true 表示已经写入 Lua 栈。
    /// </summary>
    public LuaModuleReturnValueHandler? ReturnValueHandler { get; set; }

    /// <summary>
    /// 额外的服务解析器。
    /// </summary>
    public LuaModuleServiceResolver? ServiceResolver { get; set; }

    /// <summary>
    /// 方法过滤器，返回 true 时才会导出该方法。
    /// </summary>
    public Func<MethodInfo, bool>? MethodFilter { get; set; }

    internal static LuaModuleMapOptions Create(Action<LuaModuleMapOptions>? configure)
    {
        var options = new LuaModuleMapOptions();
        configure?.Invoke(options);
        return options;
    }

    internal bool TryResolveService(Type serviceType, out object? value)
    {
        if (serviceType is null)
        {
            throw new ArgumentNullException(nameof(serviceType));
        }

        if (_services.TryGetValue(serviceType, out value))
        {
            return true;
        }

        if (ServiceResolver != null && ServiceResolver(serviceType, out value))
        {
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// 向服务表中注册实例。
    /// </summary>
    public LuaModuleMapOptions AddService(Type serviceType, object? instance)
    {
        if (serviceType is null)
        {
            throw new ArgumentNullException(nameof(serviceType));
        }

        _services[serviceType] = instance;
        return this;
    }

    /// <summary>
    /// 向服务表中注册实例。
    /// </summary>
    public LuaModuleMapOptions AddService<TService>(TService instance)
    {
        return AddService(typeof(TService), instance);
    }
}

/// <summary>
/// 自定义参数解析委托。
/// </summary>
/// <param name="parameter">方法参数。</param>
/// <param name="context">当前调用上下文。</param>
/// <param name="value">解析到的结果。</param>
/// <returns>返回 true 表示解析成功。</returns>
public delegate bool LuaModuleParameterResolver(ParameterInfo parameter, LuaCallContext context, out object? value);

/// <summary>
/// 服务解析委托。
/// </summary>
/// <param name="serviceType">目标服务类型。</param>
/// <param name="instance">解析到的实例。</param>
/// <returns>解析成功返回 true。</returns>
public delegate bool LuaModuleServiceResolver(Type serviceType, out object? instance);

/// <summary>
/// 自定义返回类型处理委托。
/// </summary>
/// <param name="context">当前调用上下文。</param>
/// <param name="value">即将返回的对象。</param>
/// <param name="results">已经写入 Lua 栈的返回数量。</param>
/// <returns>返回 true 表示处理完成。</returns>
public delegate bool LuaModuleReturnValueHandler(LuaCallContext context, object value, out int results);
