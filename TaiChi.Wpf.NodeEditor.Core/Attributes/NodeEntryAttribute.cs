namespace TaiChi.Wpf.NodeEditor.Core.Attributes;

/// <summary>
/// 用于标记节点具有流程入口的特性。
/// 可以应用于类或方法，表示该节点有一个执行流程的入口点。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class NodeEntryAttribute : Attribute
{
    /// <summary>
    /// 入口的显示名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 入口的描述信息
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name">入口的显示名称</param>
    /// <exception cref="ArgumentException">当name为空或空白字符串时抛出</exception>
    public NodeEntryAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or whitespace", nameof(name));
        Name = name;
    }
}
