using System;
using System.Collections;
using System.Data;
using Lua;
using TaiChi.LuaHost.Exceptions;

namespace TaiChi.LuaHost.Proxies;

/// <summary>
/// 表示 Lua 侧可用的对象代理，负责将成员读写与实例方法调用映射到真实 .NET 对象。
/// </summary>
[LuaObject]
public partial class LuaObjectProxy
{
    private readonly LuaState _state;
    private readonly object _target;
    private readonly LuaProxyMemberAccessor _memberAccessor;
    private readonly LuaProxyMethodBinding _methodBinding;

    /// <summary>
    /// 初始化代理对象。
    /// </summary>
    /// <param name="state">当前 LuaState。</param>
    /// <param name="target">被代理的真实对象。</param>
    /// <param name="memberAccessor">成员访问器缓存。</param>
    internal LuaObjectProxy(LuaState state, object target, LuaProxyMemberAccessor memberAccessor)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _memberAccessor = memberAccessor ?? throw new ArgumentNullException(nameof(memberAccessor));
        _methodBinding = new LuaProxyMethodBinding(state, target);
    }

    /// <summary>
    /// 获取被代理的真实对象。
    /// </summary>
    internal object Target => _target;

    /// <summary>
    /// 读取成员值；若不存在对应成员，则尝试按方法名返回可调用的函数。
    /// </summary>
    /// <param name="memberName">成员名称。</param>
    public LuaValue Get(string memberName)
    {
        if (string.IsNullOrWhiteSpace(memberName))
        {
            return LuaValue.Nil;
        }

        if (TryGetDataRowValue(memberName, out var rowValue))
        {
            return LuaProxyTableFactory.WrapValue(_state, rowValue);
        }

        if (TryGetDictionaryValue(memberName, out var dictValue))
        {
            return LuaProxyTableFactory.WrapValue(_state, dictValue);
        }

        if (_memberAccessor.TryGetValue(_target, memberName, out var memberValue))
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
    /// 写入成员值。
    /// </summary>
    /// <param name="memberName">成员名称。</param>
    /// <param name="value">Lua 值。</param>
    public void Set(string memberName, LuaValue value)
    {
        SetRaw(memberName, LuaProxyValueConverter.ConvertToDotNet(_state, value, typeof(object)));
    }

    /// <summary>
    /// 写入成员值（以 LuaFunctionExecutionContext.GetArgument&lt;object?&gt; 得到的原始对象为输入）。
    /// </summary>
    /// <param name="memberName">成员名称。</param>
    /// <param name="value">Lua 参数读取到的对象。</param>
    internal void SetRaw(string memberName, object? value)
    {
        if (string.IsNullOrWhiteSpace(memberName))
        {
            throw new LuaMappingException("成员名不能为空。");
        }

        if (TrySetDataRowValue(memberName, value))
        {
            return;
        }

        if (TrySetDictionaryValue(memberName, value))
        {
            return;
        }

        if (_memberAccessor.TryResolve(_target.GetType(), memberName, out var member) && member.CanWrite)
        {
            var converted = LuaProxyValueConverter.ConvertFromLuaObject(_state, value, member.MemberType);
            if (!_memberAccessor.TrySetValue(_target, memberName, converted))
            {
                throw new LuaMappingException($"写入成员 {memberName} 失败。");
            }

            return;
        }

        throw new LuaMappingException($"无法写入成员 {memberName}：未找到可写属性/字段/列/字典键。");
    }

    private bool TryGetDataRowValue(string memberName, out object? value)
    {
        if (_target is DataRow row)
        {
            var columns = row.Table?.Columns;
            if (columns != null && columns.Contains(memberName))
            {
                var raw = row[memberName];
                value = raw == DBNull.Value ? null : raw;
                return true;
            }
        }

        if (_target is DataRowView view)
        {
            var columns = view.Row?.Table?.Columns;
            if (columns != null && columns.Contains(memberName))
            {
                var raw = view[memberName];
                value = raw == DBNull.Value ? null : raw;
                return true;
            }
        }

        value = null;
        return false;
    }

    private bool TrySetDataRowValue(string memberName, object? value)
    {
        if (_target is DataRow row)
        {
            var columns = row.Table?.Columns;
            if (columns != null && columns.Contains(memberName))
            {
                var converted = LuaProxyValueConverter.ConvertFromLuaObject(_state, value, typeof(object));
                row[memberName] = converted ?? DBNull.Value;
                return true;
            }
        }

        if (_target is DataRowView view)
        {
            var columns = view.Row?.Table?.Columns;
            if (columns != null && columns.Contains(memberName))
            {
                var converted = LuaProxyValueConverter.ConvertFromLuaObject(_state, value, typeof(object));
                view[memberName] = converted ?? DBNull.Value;
                return true;
            }
        }

        return false;
    }

    private bool TryGetDictionaryValue(string memberName, out object? value)
    {
        if (_target is IDictionary dictionary && dictionary.Contains(memberName))
        {
            var raw = dictionary[memberName];
            value = raw == DBNull.Value ? null : raw;
            return true;
        }

        value = null;
        return false;
    }

    private bool TrySetDictionaryValue(string memberName, object? value)
    {
        if (_target is IDictionary dictionary)
        {
            dictionary[memberName] = LuaProxyValueConverter.ConvertFromLuaObject(_state, value, typeof(object));
            return true;
        }

        return false;
    }
}
