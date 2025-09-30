using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using TaiChi.Wpf.NodeEditor.Core.Enums;

namespace TaiChi.Wpf.NodeEditor.Core.Models;

/// <summary>
/// 节点图核心数据模型（Core层）。
/// 管理节点、连接与分组，提供类别信息与关系维护/重建能力。
/// </summary>
public class NodeGraph : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private NodeGraphCategory _category = NodeGraphCategory.ControlFlow;
    private Guid? _mainNodeId;

    /// <summary>
    /// 节点图唯一标识
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 节点图名称（用于UI展示或持久化说明）
    /// </summary>
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 节点图类别（ControlFlow/DataFlow 等）
    /// </summary>
    public NodeGraphCategory Category
    {
        get => _category;
        set
        {
            if (_category != value)
            {
                _category = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 主节点的标识（根据图类别定义：
    /// - ControlFlow：起点事件节点（只有输出无输入）
    /// - DataFlow：结束节点（只有输入无输出）
    /// </summary>
    public Guid? MainNodeId
    {
        get => _mainNodeId;
        set
        {
            if (_mainNodeId != value)
            {
                _mainNodeId = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MainNode));
            }
        }
    }

    /// <summary>
    /// 主节点（运行时便捷访问，不参与序列化）
    /// </summary>
    [JsonIgnore]
    public Node? MainNode
    {
        get => MainNodeId.HasValue ? Nodes.FirstOrDefault(n => n.Id == MainNodeId.Value) : null;
        set => MainNodeId = value?.Id;
    }

    /// <summary>
    /// 节点集合
    /// </summary>
    public ObservableCollection<Node> Nodes { get; } = new();

    /// <summary>
    /// 连接集合
    /// </summary>
    public ObservableCollection<Connection> Connections { get; } = new();

    /// <summary>
    /// 分组集合（根分组；支持嵌套）
    /// </summary>
    public ObservableCollection<NodeGroup> Groups { get; } = new();

    /// <summary>
    /// 构造函数：挂接集合事件以维护关系与资源释放
    /// </summary>
    public NodeGraph()
    {
        Connections.CollectionChanged += OnConnectionsChanged;
        Groups.CollectionChanged += OnGroupsChanged;
    }

    /// <summary>
    /// 计算当前图中可作为“主节点”的候选集。
    /// ControlFlow：起点事件节点（只有输出无输入）。
    /// DataFlow：结束节点（只有输入无输出）。
    /// </summary>
    public IEnumerable<Node> GetCandidateMainNodes()
    {
        return Category switch
        {
            NodeGraphCategory.ControlFlow => GetControlFlowStartNodes(),
            NodeGraphCategory.DataFlow => GetDataFlowEndNodes(),
            _ => Enumerable.Empty<Node>()
        };
    }

    /// <summary>
    /// ControlFlow 候选：只有输出无输入的节点。
    /// </summary>
    public IEnumerable<Node> GetControlFlowStartNodes()
    {
        return Nodes.Where(n =>
            // 无流程输入引脚
            !n.InputPins.Any(p => p.IsFlowPin) &&
            // 至少有一个流程输出引脚
            n.OutputPins.Any(p => p.IsFlowPin));
    }

    /// <summary>
    /// DataFlow 候选：只有输入无输出的节点。
    /// </summary>
    public IEnumerable<Node> GetDataFlowEndNodes()
    {
        return Nodes.Where(n =>
            // 至少一个数据输入引脚
            n.InputPins.Any(p => !p.IsFlowPin) &&
            // 没有数据输出引脚
            !n.OutputPins.Any(p => !p.IsFlowPin));
    }

    /// <summary>
    /// 新增节点到图中（不分组）
    /// </summary>
    public void AddNode(Node node)
    {
        if (node == null) return;
        if (!Nodes.Contains(node))
        {
            Nodes.Add(node);
        }
    }

    /// <summary>
    /// 从图中移除节点，同时断开该节点相关连接
    /// </summary>
    public bool RemoveNode(Node node)
    {
        if (node == null) return false;

        // 断开相关连接
        var related = Connections.Where(c => c.SourcePin?.ParentNode == node || c.TargetPin?.ParentNode == node).ToList();
        foreach (var c in related)
        {
            RemoveConnection(c);
        }

        // 从所属分组移除
        node.Group?.RemoveNode(node);

        return Nodes.Remove(node);
    }

    /// <summary>
    /// 创建并添加连接（辅助方法）。
    /// </summary>
    public Connection? Connect(Pin source, Pin target)
    {
        if (source == null || target == null) return null;
        if (source.ParentNode == null || target.ParentNode == null) return null;

        // 确保两个节点均在当前图中
        if (!Nodes.Contains(source.ParentNode)) AddNode(source.ParentNode);
        if (!Nodes.Contains(target.ParentNode)) AddNode(target.ParentNode);

        if (!source.CanConnectTo(target)) return null;

        var conn = new Connection(source, target);
        // Connection 构造内会完成初次数据传递及事件绑定
        AddConnection(conn);
        return conn;
    }

    /// <summary>
    /// 将已有连接加入图中
    /// </summary>
    public void AddConnection(Connection connection)
    {
        if (connection == null) return;
        if (!Connections.Contains(connection))
        {
            Connections.Add(connection);
        }
    }

    /// <summary>
    /// 从图中移除连接，并断开其两端引脚
    /// </summary>
    public bool RemoveConnection(Connection connection)
    {
        if (connection == null) return false;
        connection.Disconnect();
        return Connections.Remove(connection);
    }

    /// <summary>
    /// 添加根分组
    /// </summary>
    public void AddGroup(NodeGroup group)
    {
        if (group == null) return;
        if (!Groups.Contains(group))
        {
            Groups.Add(group);
        }
    }

    /// <summary>
    /// 移除根分组（不递归删除其中节点，只解除关系）
    /// </summary>
    public bool RemoveGroup(NodeGroup group)
    {
        if (group == null) return false;
        return Groups.Remove(group);
    }

    /// <summary>
    /// 将节点移动到指定分组（传入 null 表示移出分组）
    /// </summary>
    public void MoveNodeToGroup(Node node, NodeGroup? group)
    {
        if (node == null) return;
        if (group != null && !Groups.Contains(group) && !IsChildGroup(group))
        {
            // 非图内分组，先加入为根分组
            AddGroup(group);
        }

        node.Group = group; // Node.Group setter 会维护双向关系
    }

    /// <summary>
    /// 反序列化后重建运行时关系（Pin/Connection/Group等）
    /// </summary>
    public void OnDeserialized()
    {
        // 重建 Pin -> Node 关系
        foreach (var n in Nodes)
        {
            foreach (var pin in n.InputPins) pin.ParentNode = n;
            foreach (var pin in n.OutputPins) pin.ParentNode = n;
            n.OnDeserialized();
        }

        // 根据 GroupId 重建 Node 与 Group 关系
        var allGroups = GetAllGroupsRecursive().ToDictionary(g => g.Id);
        foreach (var n in Nodes)
        {
            if (n.GroupId.HasValue && allGroups.TryGetValue(n.GroupId.Value, out var g))
            {
                MoveNodeToGroup(n, g);
            }
            else
            {
                MoveNodeToGroup(n, null);
            }
        }

        // 重建 Connection 的 Source/Target 引用
        var pinIndex = Nodes
            .SelectMany(n => n.InputPins.Concat(n.OutputPins))
            .ToDictionary(p => p.Id);

        foreach (var c in Connections)
        {
            if (pinIndex.TryGetValue(c.SourcePinId, out var sp))
            {
                c.SourcePin = sp;
            }
            if (pinIndex.TryGetValue(c.TargetPinId, out var tp))
            {
                c.TargetPin = tp;
            }
        }
    }

    /// <summary>
    /// 校验图是否处于一致状态（连接有效、引脚方向正确等）
    /// </summary>
    public bool Validate()
    {
        foreach (var c in Connections)
        {
            if (!c.IsValid()) return false;
        }
        return true;
    }

    /// <summary>
    /// 获取执行引擎键（上层可据此选择具体引擎实现）。
    /// Core 层不直接依赖具体执行引擎接口/实现。
    /// </summary>
    public string GetExecutionEngineKey() => Category.ToString();

    /// <summary>
    /// 遍历所有（包含嵌套）分组
    /// </summary>
    public IEnumerable<NodeGroup> GetAllGroupsRecursive()
    {
        foreach (var g in Groups)
        {
            yield return g;
            foreach (var c in EnumerateChildren(g))
                yield return c;
        }
    }

    private static IEnumerable<NodeGroup> EnumerateChildren(NodeGroup parent)
    {
        foreach (var child in parent.Children)
        {
            yield return child;
            foreach (var nested in EnumerateChildren(child))
                yield return nested;
        }
    }

    private bool IsChildGroup(NodeGroup group)
    {
        return GetAllGroupsRecursive().Contains(group);
    }

    private void OnConnectionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // 当连接被移除时，确保资源释放
        if (e.OldItems != null)
        {
            foreach (Connection c in e.OldItems)
            {
                c.Disconnect();
            }
        }
        OnPropertyChanged(nameof(Connections));
    }

    private void OnGroupsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Groups));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
