using System;
using System.Collections.Generic;
using Lua;

namespace TaiChi.LuaHost;

/// <summary>
/// 表示 Lua 执行返回的值集合，提供便捷的类型读取方法。
/// </summary>
public readonly struct LuaScriptExecutionResult
{
    private static readonly LuaValue[] EmptyValues = Array.Empty<LuaValue>();
    private readonly IReadOnlyList<LuaValue> _values;

    /// <summary>
    /// 使用执行结果构建实例。
    /// </summary>
    /// <param name="values">Lua 返回的值集合。</param>
    public LuaScriptExecutionResult(IReadOnlyList<LuaValue>? values)
    {
        _values = values ?? EmptyValues;
    }

    /// <summary>
    /// 获取结果中的值数量。
    /// </summary>
    public int Count => _values.Count;

    /// <summary>
    /// 以索引方式访问指定位置的值。
    /// </summary>
    /// <param name="index">值的索引。</param>
    public LuaValue this[int index] => _values[index];

    /// <summary>
    /// 获取原始的值列表。
    /// </summary>
    public IReadOnlyList<LuaValue> Values => _values;

    /// <summary>
    /// 尝试读取第一个值并转为指定类型。
    /// </summary>
    /// <typeparam name="T">目标类型。</typeparam>
    /// <param name="value">若转换成功则返回值。</param>
    /// <returns>若存在值且转换成功则为 true。</returns>
    public bool TryReadFirst<T>(out T value)
    {
        if (_values.Count == 0)
        {
            value = default!;
            return false;
        }

        return _values[0].TryRead(out value!);
    }

    /// <summary>
    /// 读取第一个值，若不存在则返回默认值。
    /// </summary>
    /// <typeparam name="T">目标类型。</typeparam>
    /// <param name="defaultValue">当无值或转换失败时返回的默认值。</param>
    public T ReadFirstOrDefault<T>(T defaultValue = default!)
    {
        return TryReadFirst<T>(out var value) ? value : defaultValue;
    }
}
