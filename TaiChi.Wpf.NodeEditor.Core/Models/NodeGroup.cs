using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TaiChi.Wpf.NodeEditor.Core.Models;

/// <summary>
/// 节点分组的数据模型（Core层）。
/// 仅包含与分组相关的基础数据与关系，不依赖WPF控件。
/// </summary>
public class NodeGroup : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private NodeEditorRect _bounds;
    private bool _isSelected;
    private NodeGroup? _parent;

    /// <summary>
    /// 分组唯一标识
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 分组名称
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
    /// 分组边界（画布坐标系）
    /// </summary>
    public NodeEditorRect Bounds
    {
        get => _bounds;
        set
        {
            if (_bounds.X != value.X || _bounds.Y != value.Y || _bounds.Width != value.Width || _bounds.Height != value.Height)
            {
                _bounds = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 是否被选中
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 父分组（支持嵌套）
    /// </summary>
    public NodeGroup? Parent
    {
        get => _parent;
        private set
        {
            if (!ReferenceEquals(_parent, value))
            {
                _parent = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 组内节点集合
    /// </summary>
    public ObservableCollection<Node> Nodes { get; } = new();

    /// <summary>
    /// 子分组集合（嵌套）
    /// </summary>
    public ObservableCollection<NodeGroup> Children { get; } = new();

    /// <summary>
    /// 构造函数
    /// </summary>
    public NodeGroup()
    {
        Nodes.CollectionChanged += OnNodesCollectionChanged;
        Children.CollectionChanged += OnChildrenCollectionChanged;
    }

    /// <summary>
    /// 将节点添加到当前分组
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
    /// 从当前分组移除节点
    /// </summary>
    public bool RemoveNode(Node node)
    {
        if (node == null) return false;
        return Nodes.Remove(node);
    }

    /// <summary>
    /// 添加子分组（会自动设置其Parent）
    /// </summary>
    public void AddChild(NodeGroup group)
    {
        if (group == null) return;
        if (ReferenceEquals(group, this)) return; // 禁止将自身作为子分组
        if (!Children.Contains(group))
        {
            Children.Add(group);
        }
    }

    /// <summary>
    /// 移除子分组（会清空其Parent）
    /// </summary>
    public bool RemoveChild(NodeGroup group)
    {
        if (group == null) return false;
        return Children.Remove(group);
    }

    /// <summary>
    /// 递归获取所有子节点（包含子分组中的节点）
    /// </summary>
    public IEnumerable<Node> GetAllNodesRecursive()
    {
        foreach (var n in Nodes)
            yield return n;

        foreach (var child in Children)
        {
            foreach (var n in child.GetAllNodesRecursive())
                yield return n;
        }
    }

    /// <summary>
    /// 递归判断是否包含指定节点
    /// </summary>
    public bool ContainsNodeRecursive(Node node)
    {
        if (Nodes.Contains(node)) return true;
        foreach (var child in Children)
        {
            if (child.ContainsNodeRecursive(node))
                return true;
        }
        return false;
    }

    private void OnNodesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // 仅通知集合变化；节点与分组的双向引用将在后续任务中于 Node/Manager 中处理
        OnPropertyChanged(nameof(Nodes));
    }

    private void OnChildrenCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (NodeGroup g in e.NewItems)
            {
                g.Parent = this;
            }
        }
        if (e.OldItems != null)
        {
            foreach (NodeGroup g in e.OldItems)
            {
                if (ReferenceEquals(g.Parent, this))
                    g.Parent = null;
            }
        }

        OnPropertyChanged(nameof(Children));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

