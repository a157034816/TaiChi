using System;
using Lua;
using TaiChi.LuaHost.Exceptions;

namespace TaiChi.LuaHost.Proxies;

/// <summary>
/// 表示 Lua 侧可用的静态类型代理，负责将静态成员读写与静态方法调用映射到真实 .NET 类型。
/// </summary>
[LuaObject]
public partial class LuaStaticTypeProxy
{
    private readonly LuaState _state;
    private readonly Type _targetType;
    private readonly LuaStaticProxyMemberAccessor _memberAccessor;
    private readonly LuaStaticProxyMethodBinding _methodBinding;

    /// <summary>
    /// 初始化静态类型代理对象。
    /// </summary>
    /// <param name="state">当前 LuaState。</param>
    /// <param name="proxyTable">当前静态代理壳表。</param>
    /// <param name="targetType">被代理的目标类型。</param>
    /// <param name="memberAccessor">成员访问器缓存。</param>
    internal LuaStaticTypeProxy(LuaState state, LuaTable proxyTable, Type targetType, LuaStaticProxyMemberAccessor memberAccessor)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _targetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
        _memberAccessor = memberAccessor ?? throw new ArgumentNullException(nameof(memberAccessor));
        _methodBinding = new LuaStaticProxyMethodBinding(state, targetType, proxyTable);
    }

    /// <summary>
    /// 读取静态成员值；若不存在对应静态成员，则尝试按方法名返回可调用的函数。
    /// </summary>
    /// <param name="memberName">成员名称。</param>
    public LuaValue Get(string memberName)
    {
        if (string.IsNullOrWhiteSpace(memberName))
        {
            return LuaValue.Nil;
        }

        if (_memberAccessor.TryGetValue(_targetType, memberName, out var memberValue))
        {
            return LuaProxyTableFactory.WrapValue(_state, memberValue);
        }

        if (_methodBinding.TryGetFunction(memberName, out var function))
        {
            return function;
        }

        return LuaValue.Nil;
    }

    /// <summary>
    /// 写入静态成员值（以 LuaFunctionExecutionContext.GetArgument&lt;object?&gt; 得到的原始对象为输入）。
    /// </summary>
    /// <param name="memberName">成员名称。</param>
    /// <param name="value">Lua 参数读取到的对象。</param>
    internal void SetRaw(string memberName, object? value)
    {
        if (string.IsNullOrWhiteSpace(memberName))
        {
            throw new LuaMappingException("成员名不能为空。");
        }

        if (_memberAccessor.TryResolve(_targetType, memberName, out var member) && member.CanWrite)
        {
            var converted = LuaProxyValueConverter.ConvertFromLuaObject(_state, value, member.MemberType);
            if (!_memberAccessor.TrySetValue(_targetType, memberName, converted))
            {
                throw new LuaMappingException($"写入成员 {memberName} 失败。");
            }

            return;
        }

        throw new LuaMappingException($"无法写入成员 {memberName}：未找到可写静态属性/字段。");
    }
}

