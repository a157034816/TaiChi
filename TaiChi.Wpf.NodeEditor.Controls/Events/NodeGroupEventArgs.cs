using System.Windows;
using TaiChi.Wpf.NodeEditor.Core.Models;

namespace TaiChi.Wpf.NodeEditor.Controls.Events;

/// <summary>
/// 分组变更类型
/// </summary>
public enum NodeGroupChangeAction
{
    Created,
    Deleted,
    Updated,
    NameChanged,
    BoundsChanged,
    NodeAdded,
    NodeRemoved,
    SelectionChanged
}

/// <summary>
/// 分组相关的路由事件参数
/// </summary>
public class NodeGroupEventArgs : RoutedEventArgs
{
    /// <summary>
    /// 发生变更的分组
    /// </summary>
    public NodeGroup Group { get; init; }

    /// <summary>
    /// 变更涉及的节点（当 Action 为 NodeAdded/NodeRemoved 时）
    /// </summary>
    public Node? Node { get; init; }

    /// <summary>
    /// 父分组（用于 Create/Delete 或层级调整时提供上下文）
    /// </summary>
    public NodeGroup? ParentGroup { get; init; }

    /// <summary>
    /// 变更动作
    /// </summary>
    public NodeGroupChangeAction Action { get; init; }

    /// <summary>
    /// 旧名称（当名称改变时有效）
    /// </summary>
    public string? OldName { get; init; }

    /// <summary>
    /// 新名称（当名称改变时有效）
    /// </summary>
    public string? NewName { get; init; }

    /// <summary>
    /// 旧边界（当边界改变时有效）
    /// </summary>
    public NodeEditorRect? OldBounds { get; init; }

    /// <summary>
    /// 新边界（当边界改变时有效）
    /// </summary>
    public NodeEditorRect? NewBounds { get; init; }

    public NodeGroupEventArgs(RoutedEvent routedEvent, object source, NodeGroup group, NodeGroupChangeAction action)
        : base(routedEvent, source)
    {
        Group = group;
        Action = action;
    }
}

