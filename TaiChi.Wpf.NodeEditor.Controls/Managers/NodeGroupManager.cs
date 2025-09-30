using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using TaiChi.Wpf.NodeEditor.Core.Models;

namespace TaiChi.Wpf.NodeEditor.Controls.Managers;

/// <summary>
/// 分组管理器：负责创建/删除分组、节点添加/移除、边界计算与验证。
/// 注意：本管理器位于 Controls 层，但仅依赖 Core 层的 Node/NodeGroup 数据模型，不直接耦合 UI 控件。
/// </summary>
public class NodeGroupManager
{
    private readonly ObservableCollection<NodeGroup> _groups;

    /// <summary>
    /// 提供节点尺寸的回调（用于边界计算）。
    /// 若未提供，则使用 <see cref="DefaultNodeSize"/> 作为近似尺寸。
    /// </summary>
    public Func<Node, Size>? MeasureNode { get; init; }

    /// <summary>
    /// 默认节点尺寸（用于无法测量时的近似）。
    /// </summary>
    public Size DefaultNodeSize { get; init; } = new Size(120, 60);

    /// <summary>
    /// 受管的分组集合。
    /// </summary>
    public ReadOnlyObservableCollection<NodeGroup> Groups { get; }

    // 显式提供无参构造，确保可被 XAML 解析器无障碍创建
    public NodeGroupManager() : this(null) { }

    public NodeGroupManager(ObservableCollection<NodeGroup>? groups)
    {
        _groups = groups ?? new ObservableCollection<NodeGroup>();
        Groups = new ReadOnlyObservableCollection<NodeGroup>(_groups);
    }

    /// <summary>
    /// 约束或扩展：给定期望的节点位置，若超出分组边界则按策略约束或扩展分组边界。
    /// 返回最终可用的位置（逻辑坐标）。
    /// </summary>
    public NodeEditorPoint ConstrainOrExpandNodePosition(Node node, NodeGroup group, NodeEditorPoint desiredPosition, bool dynamicExpand = true, double padding = 8)
    {
        if (node == null || group == null)
            return desiredPosition;

        var size = MeasureNode?.Invoke(node) ?? DefaultNodeSize;
        var desiredRect = new NodeEditorRect(desiredPosition.X, desiredPosition.Y, size.Width, size.Height);
        var g = group.Bounds;

        // 若节点矩形已完全在分组边界内，直接返回
        if (Contains(g, desiredRect))
            return desiredPosition;

        if (dynamicExpand)
        {
            // 扩展分组边界以容纳节点（含 padding）
            var minX = Math.Min(g.X, desiredRect.X - padding);
            var minY = Math.Min(g.Y, desiredRect.Y - padding);
            var maxX = Math.Max(g.X + g.Width, desiredRect.X + desiredRect.Width + padding);
            var maxY = Math.Max(g.Y + g.Height, desiredRect.Y + desiredRect.Height + padding);

            group.Bounds = new NodeEditorRect(minX, minY, Math.Max(0, maxX - minX), Math.Max(0, maxY - minY));
            return desiredPosition;
        }
        else
        {
            // 约束在边界内（不扩展时）
            var clampedX = Math.Max(g.X, Math.Min(desiredRect.X, g.X + g.Width - desiredRect.Width));
            var clampedY = Math.Max(g.Y, Math.Min(desiredRect.Y, g.Y + g.Height - desiredRect.Height));
            return new NodeEditorPoint(clampedX, clampedY);
        }
    }

    /// <summary>
    /// 新建分组。
    /// </summary>
    public NodeGroup CreateGroup(string name, NodeEditorRect bounds, NodeGroup? parent = null)
    {
        var group = new NodeGroup
        {
            Name = name,
            Bounds = bounds
        };

        if (parent != null)
        {
            parent.AddChild(group);
        }
        else
        {
            _groups.Add(group);
        }

        return group;
    }

    /// <summary>
    /// 根据一批节点自动创建分组，边界会包裹这些节点并留出 padding。
    /// </summary>
    public NodeGroup CreateGroupFromNodes(string name, IEnumerable<Node> nodes, double padding = 16, NodeGroup? parent = null)
    {
        var nodeList = nodes?.Where(n => n != null).Distinct().ToList() ?? new List<Node>();
        var bounds = CalculateBoundsForNodes(nodeList, padding);
        var group = CreateGroup(name, bounds, parent);

        foreach (var node in nodeList)
        {
            AddNodeToGroup(node, group, adjustBounds: false);
        }

        // 最后统一调整边界（避免频繁重复计算）
        UpdateGroupBoundsToFit(group, padding);
        return group;
    }

    /// <summary>
    /// 删除分组（递归移除所有子分组）。节点将被移出该分组。
    /// </summary>
    public void DeleteGroup(NodeGroup group)
    {
        if (group == null) return;

        // 递归删除子分组
        foreach (var child in group.Children.ToList())
        {
            DeleteGroup(child);
        }

        // 移出所有节点
        foreach (var node in group.Nodes.ToList())
        {
            RemoveNodeFromGroup(node, group);
        }

        // 从父级或根集合移除
        if (group.Parent != null)
        {
            group.Parent.RemoveChild(group);
        }
        else
        {
            _groups.Remove(group);
        }
    }

    /// <summary>
    /// 将节点添加到分组。
    /// </summary>
    public bool AddNodeToGroup(Node node, NodeGroup group, bool adjustBounds = true)
    {
        if (node == null || group == null) return false;

        // 设置双向关系由 Node.Group 属性维护
        node.Group = group;

        // 可选的边界自动扩展
        if (adjustBounds)
        {
            ExpandBoundsToIncludeNode(group, node, padding: 8);
        }

        return true;
    }

    /// <summary>
    /// 从分组移除节点。
    /// </summary>
    public bool RemoveNodeFromGroup(Node node, NodeGroup group)
    {
        if (node == null || group == null) return false;
        if (node.Group != group) return false;

        node.Group = null;
        return true;
    }

    /// <summary>
    /// 将分组整体移动指定偏移；可选是否级联移动子分组和节点（仅更新节点 Position）。
    /// </summary>
    public void MoveGroup(NodeGroup group, double dx, double dy, bool cascadeNodes = false, bool cascadeChildren = true)
    {
        if (group == null) return;

        group.Bounds = new NodeEditorRect(group.Bounds.X + dx, group.Bounds.Y + dy, group.Bounds.Width, group.Bounds.Height);

        if (cascadeNodes)
        {
            foreach (var node in group.Nodes)
            {
                node.Position = new NodeEditorPoint(node.Position.X + dx, node.Position.Y + dy);
            }
        }

        if (cascadeChildren)
        {
            foreach (var child in group.Children)
            {
                MoveGroup(child, dx, dy, cascadeNodes: cascadeNodes, cascadeChildren: true);
            }
        }
    }

    /// <summary>
    /// 根据组内节点重新计算并设置分组边界。
    /// </summary>
    public void UpdateGroupBoundsToFit(NodeGroup group, double padding = 16)
    {
        if (group == null) return;

        var allNodes = group.GetAllNodesRecursive().ToList();
        if (allNodes.Count == 0) return;

        var rect = CalculateBoundsForNodes(allNodes, padding);
        group.Bounds = rect;
    }

    /// <summary>
    /// 校验节点是否在分组边界内（含 padding 容差）。
    /// </summary>
    public bool ValidateNodeInsideBounds(NodeGroup group, Node node, double tolerance = 0)
    {
        if (group == null || node == null) return false;
        var nodeRect = GetNodeRect(node);
        var g = group.Bounds;
        var expanded = new NodeEditorRect(g.X - tolerance, g.Y - tolerance, g.Width + 2 * tolerance, g.Height + 2 * tolerance);
        return RectsIntersect(expanded, nodeRect) && Contains(expanded, nodeRect);
    }

    /// <summary>
    /// 使分组边界扩展以完全包含指定节点（带 padding）。
    /// </summary>
    public void ExpandBoundsToIncludeNode(NodeGroup group, Node node, double padding = 8)
    {
        if (group == null || node == null) return;
        var g = group.Bounds;
        var n = GetNodeRect(node);

        var minX = Math.Min(g.X, n.X - padding);
        var minY = Math.Min(g.Y, n.Y - padding);
        var maxX = Math.Max(g.X + g.Width, n.X + n.Width + padding);
        var maxY = Math.Max(g.Y + g.Height, n.Y + n.Height + padding);

        group.Bounds = new NodeEditorRect(minX, minY, Math.Max(0, maxX - minX), Math.Max(0, maxY - minY));
    }

    /// <summary>
    /// 计算一批节点（含近似尺寸）的包围矩形。
    /// </summary>
    public NodeEditorRect CalculateBoundsForNodes(IEnumerable<Node> nodes, double padding = 16)
    {
        var list = nodes?.Where(n => n != null).ToList() ?? new List<Node>();
        if (list.Count == 0)
        {
            return new NodeEditorRect(0, 0, 0, 0);
        }

        double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;

        foreach (var node in list)
        {
            var r = GetNodeRect(node);
            minX = Math.Min(minX, r.X);
            minY = Math.Min(minY, r.Y);
            maxX = Math.Max(maxX, r.X + r.Width);
            maxY = Math.Max(maxY, r.Y + r.Height);
        }

        if (double.IsInfinity(minX) || double.IsInfinity(minY) || double.IsInfinity(maxX) || double.IsInfinity(maxY))
        {
            return new NodeEditorRect(0, 0, 0, 0);
        }

        minX -= padding; minY -= padding; maxX += padding; maxY += padding;

        return new NodeEditorRect(minX, minY, Math.Max(0, maxX - minX), Math.Max(0, maxY - minY));
    }

    /// <summary>
    /// 将节点映射为矩形（通过回调或默认尺寸）。
    /// </summary>
    private NodeEditorRect GetNodeRect(Node node)
    {
        var size = MeasureNode?.Invoke(node) ?? DefaultNodeSize;
        return new NodeEditorRect(node.Position.X, node.Position.Y, size.Width, size.Height);
    }

    #region 几何工具

    private static bool RectsIntersect(NodeEditorRect a, NodeEditorRect b)
    {
        return a.IntersectsWith(b);
    }

    private static bool Contains(NodeEditorRect outer, NodeEditorRect inner)
    {
        return inner.X >= outer.X && inner.Y >= outer.Y &&
               inner.X + inner.Width <= outer.X + outer.Width &&
               inner.Y + inner.Height <= outer.Y + outer.Height;
    }

    #endregion
}
