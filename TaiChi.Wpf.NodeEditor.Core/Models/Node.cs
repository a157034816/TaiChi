using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using TaiChi.Wpf.NodeEditor.Core.Enums;

namespace TaiChi.Wpf.NodeEditor.Core.Models;

/// <summary>
/// 节点类，是节点图中的基本计算单元或逻辑块。每个节点代表一个操作、函数或数据
/// </summary>
public abstract class Node : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private NodeEditorPoint _position;
    private bool _isEnabled = true;
    private NodeState _state = NodeState.Normal;
    private bool _isSelected = false;
    private NodeGroup? _group;

    /// <summary>
    /// 节点的唯一标识符
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 显示在节点头部的名称
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
    /// 节点在画布上的左上角坐标 (X, Y)
    /// </summary>
    public NodeEditorPoint Position
    {
        get => _position;
        set
        {
            if (_position != value)
            {
                _position = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 节点的输入引脚列表
    /// </summary>
    public ObservableCollection<Pin> InputPins { get; } = new();

    /// <summary>
    /// 节点的输出引脚列表
    /// </summary>
    public ObservableCollection<Pin> OutputPins { get; } = new();

    /// <summary>
    /// 指示节点当前是否可用
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled != value)
            {
                _isEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 节点的当前状态（如：正常、执行中、错误）
    /// </summary>
    public NodeState State
    {
        get => _state;
        set
        {
            if (_state != value)
            {
                _state = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 节点是否被选中
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
    /// 所属分组（运行时引用，不参与直接序列化）
    /// </summary>
    [JsonIgnore]
    public NodeGroup? Group
    {
        get => _group;
        set
        {
            if (!ReferenceEquals(_group, value))
            {
                var old = _group;
                _group = value;
                // 维护可序列化的 GroupId
                GroupId = value?.Id;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GroupId));

                // 维护双向关系（避免重复/空引用）
                if (old != null && old.Nodes.Contains(this))
                    old.Nodes.Remove(this);

                if (value != null && !value.Nodes.Contains(this))
                    value.Nodes.Add(this);
            }
        }
    }

    /// <summary>
    /// 所属分组的标识（用于序列化/反序列化后的重建）
    /// </summary>
    public Guid? GroupId { get; set; }

    /// <summary>
    /// 属性变化通知事件
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 构造函数
    /// </summary>
    protected Node()
    {
        // 监听引脚集合变化
        InputPins.CollectionChanged += (s, e) => OnPinsCollectionChanged(e, PinDirection.Input);
        OutputPins.CollectionChanged += (s, e) => OnPinsCollectionChanged(e, PinDirection.Output);
    }

    /// <summary>
    /// 执行节点的核心逻辑。该方法应是可重写的，以便派生类实现具体功能
    /// </summary>
    public virtual void Execute()
    {
        if (!IsEnabled)
            return;

        State = NodeState.Executing;

        try
        {
            OnExecute();
            State = NodeState.Success;
        }
        catch (Exception)
        {
            State = NodeState.Error;
            throw;
        }
        finally
        {
            // 短暂显示成功/错误状态后恢复正常
            Task.Delay(1000).ContinueWith(_ => 
            {
                if (State != NodeState.Executing)
                    State = NodeState.Normal;
            });
        }
    }

    /// <summary>
    /// 派生类重写此方法来实现具体的执行逻辑
    /// </summary>
    protected virtual void OnExecute()
    {
        // 默认实现为空，派生类可以重写
    }

    /// <summary>
    /// 当输入引脚的值发生变化时调用
    /// </summary>
    /// <param name="inputPin">发生变化的输入引脚</param>
    public virtual void OnInputChanged(Pin inputPin)
    {
        // 默认实现：当所有输入引脚都有值时自动执行
        if (InputPins.All(p => p.Value != null))
        {
            Execute();
        }
    }

    /// <summary>
    /// 当输出引脚的值发生变化时调用
    /// </summary>
    /// <param name="outputPin">发生变化的输出引脚</param>
    public virtual void OnOutputChanged(Pin outputPin)
    {
        // 默认实现为空，派生类可以重写
    }

    /// <summary>
    /// 在从数据反序列化后调用，用于初始化瞬态数据或执行设置
    /// </summary>
    public virtual void OnDeserialized()
    {
        // 重新建立引脚与节点的关系
        foreach (var pin in InputPins)
        {
            pin.ParentNode = this;
        }

        foreach (var pin in OutputPins)
        {
            pin.ParentNode = this;
        }

        // 如果反序列化时已有 Group 引用，则同步 GroupId；
        // 常规流程由上层管理器根据 GroupId 重新绑定 Group。
        if (Group != null)
        {
            GroupId = Group.Id;
        }
    }

    /// <summary>
    /// 添加输入引脚
    /// </summary>
    /// <param name="name">引脚名称</param>
    /// <param name="dataType">数据类型</param>
    /// <returns>创建的引脚</returns>
    protected Pin AddInputPin(string name, Type dataType)
    {
        var pin = new Pin
        {
            Name = name,
            Direction = PinDirection.Input,
            DataType = dataType,
            ParentNode = this
        };
        InputPins.Add(pin);
        return pin;
    }

    /// <summary>
    /// 添加输出引脚
    /// </summary>
    /// <param name="name">引脚名称</param>
    /// <param name="dataType">数据类型</param>
    /// <returns>创建的引脚</returns>
    protected Pin AddOutputPin(string name, Type dataType)
    {
        var pin = new Pin
        {
            Name = name,
            Direction = PinDirection.Output,
            DataType = dataType,
            ParentNode = this
        };
        OutputPins.Add(pin);
        return pin;
    }

    /// <summary>
    /// 根据名称查找输入引脚
    /// </summary>
    /// <param name="name">引脚名称</param>
    /// <returns>找到的引脚，如果不存在返回null</returns>
    public Pin? FindInputPin(string name)
    {
        return InputPins.FirstOrDefault(p => p.Name == name);
    }

    /// <summary>
    /// 根据名称查找输出引脚
    /// </summary>
    /// <param name="name">引脚名称</param>
    /// <returns>找到的引脚，如果不存在返回null</returns>
    public Pin? FindOutputPin(string name)
    {
        return OutputPins.FirstOrDefault(p => p.Name == name);
    }

    /// <summary>
    /// 当引脚集合发生变化时的处理
    /// </summary>
    private void OnPinsCollectionChanged(System.Collections.Specialized.NotifyCollectionChangedEventArgs e, PinDirection direction)
    {
        // 为新添加的引脚设置父节点
        if (e.NewItems != null)
        {
            foreach (Pin pin in e.NewItems)
            {
                pin.ParentNode = this;
                pin.Direction = direction;
            }
        }

        // 清理被移除的引脚
        if (e.OldItems != null)
        {
            foreach (Pin pin in e.OldItems)
            {
                pin.ParentNode = null;
                pin.Connection?.Disconnect();
            }
        }
    }

    /// <summary>
    /// 触发属性变化通知
    /// </summary>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// 表示一个点的结构体（用于坐标定位）
/// </summary>
public struct NodeEditorPoint
{
    public double X { get; set; }
    public double Y { get; set; }

    public NodeEditorPoint(double x, double y)
    {
        X = x;
        Y = y;
    }

    public static bool operator ==(NodeEditorPoint left, NodeEditorPoint right)
    {
        return left.X == right.X && left.Y == right.Y;
    }

    public static bool operator !=(NodeEditorPoint left, NodeEditorPoint right)
    {
        return !(left == right);
    }

    public override bool Equals(object? obj)
    {
        return obj is NodeEditorPoint point && this == point;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y);
    }

    public override string ToString()
    {
        return $"({X}, {Y})";
    }
}

/// <summary>
/// 表示一个矩形的结构体
/// </summary>
public struct NodeEditorRect
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    public NodeEditorRect(double x, double y, double width, double height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public bool IntersectsWith(NodeEditorRect other)
    {
        return X < other.X + other.Width &&
               X + Width > other.X &&
               Y < other.Y + other.Height &&
               Y + Height > other.Y;
    }

    public override string ToString()
    {
        return $"({X}, {Y}, {Width}, {Height})";
    }
}
