namespace TaiChi.Wpf.NodeEditor.Core.Attributes;

/// <summary>
/// 用于标记一个类为节点库容器的特性。
/// 节点库允许在一个普通的C#类中定义多个节点方法，提高代码聚合度和开发便捷性。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class NodeLibraryAttribute : Attribute
{
    /// <summary>
    /// 定义库中所有节点的默认根路径，用于在工具箱中分组。
    /// 如果单个节点的 NodeAttribute.Path 未设置，将使用此路径。
    /// </summary>
    public string Path { get; set; } = "Default";

    /// <summary>
    /// 节点库的描述信息
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="path">节点库的默认根路径，用于工具箱分组</param>
    public NodeLibraryAttribute(string path = "Default")
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or whitespace", nameof(path));
        
        Path = path;
    }
}