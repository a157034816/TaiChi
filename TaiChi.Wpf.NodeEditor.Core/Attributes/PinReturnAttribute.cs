namespace TaiChi.Wpf.NodeEditor.Core.Attributes;

/// <summary>
/// 用于标记属性作为返回值引脚的特性。
/// 此特性专门用于基于类的节点，标记属性作为输出引脚。
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class PinReturnAttribute : Attribute
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
    /// 构造函数
    /// </summary>
    /// <param name="name">引脚的显示名称</param>
    /// <exception cref="ArgumentException">当name为空或空白字符串时抛出</exception>
    public PinReturnAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or whitespace", nameof(name));
        Name = name;
    }
}
