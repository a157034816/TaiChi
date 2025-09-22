namespace TaiChi.Wpf.NodeEditor.Core.Attributes;

/// <summary>
/// 用于定义方法的参数和返回值如何映射为节点的输入/输出引脚的特性。
/// 系统将通过反射来识别它们并自动生成引脚。
/// </summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.ReturnValue | AttributeTargets.Property, AllowMultiple = false)]
public sealed class PinAttribute : Attribute
{
    /// <summary>
    /// 引脚的显示名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 引脚的描述信息
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 引脚的默认值（仅用于输入引脚）
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name">引脚的显示名称</param>
    /// <exception cref="ArgumentException">当name为空或空白字符串时抛出</exception>
    public PinAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or whitespace", nameof(name));
        Name = name;
    }
}
