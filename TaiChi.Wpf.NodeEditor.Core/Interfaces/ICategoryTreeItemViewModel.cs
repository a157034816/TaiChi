using TaiChi.Wpf.NodeEditor.Core.Registry;

namespace TaiChi.Wpf.NodeEditor.Core.Interfaces;

/// <summary>
/// 树形视图中分类节点的接口定义
/// </summary>
public interface ICategoryTreeItemViewModel : ITreeItemViewModel
{
    /// <summary>
    /// 该分类下的节点总数
    /// </summary>
    int NodeCount { get; }

    /// <summary>
    /// 可见的节点数量（用于搜索过滤后显示）
    /// </summary>
    int VisibleNodeCount { get; }

    /// <summary>
    /// 添加新的节点
    /// </summary>
    /// <param name="nodeMetadata">节点元数据</param>
    void AddNode(NodeMetadata nodeMetadata);

    /// <summary>
    /// 移除节点
    /// </summary>
    /// <param name="nodeMetadata">节点元数据</param>
    void RemoveNode(NodeMetadata nodeMetadata);

    /// <summary>
    /// 清空所有节点
    /// </summary>
    void ClearNodes();
}