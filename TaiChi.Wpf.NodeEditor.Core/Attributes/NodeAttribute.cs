namespace TaiChi.Wpf.NodeEditor.Core.Attributes;

/// <summary>
/// 用于标记一个类或方法是节点的特性。
/// 在节点库模式中，此特性用于标记公共方法为节点。
/// 保持对旧的基于类的节点的向后兼容性。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class NodeAttribute : Attribute
{
    /// <summary>
    /// 节点的显示名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 节点所属的类别，用于在工具箱中分组（仅用于基于类的节点）
    /// </summary>
    public string Category { get; set; } = "Default";

    /// <summary>
    /// 定义节点的具体路径（用于基于方法的节点）。
    /// 如果设置，其优先级高于 NodeLibraryAttribute 的 Path。
    /// 如果未设置，将继承 NodeLibraryAttribute 的 Path。
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// 节点的描述信息
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name">节点的显示名称</param>
    public NodeAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or whitespace", nameof(name));
        Name = name;
    }
}
