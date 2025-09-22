using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using TaiChi.Wpf.NodeEditor.Core.Enums;

namespace TaiChi.Wpf.NodeEditor.Core.Models;

/// <summary>
/// 引脚类，代表节点上的连接点，用于数据的输入和输出
/// </summary>
public class Pin : INotifyPropertyChanged
{
    private object? _value;
    private Connection? _connection;

    /// <summary>
    /// 引脚的唯一标识符
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 显示在引脚旁边的名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 该引脚所属的父节点
    /// </summary>
    [JsonIgnore]
    public Node? ParentNode { get; set; }

    /// <summary>
    /// 引脚的方向（输入或输出）
    /// </summary>
    public PinDirection Direction { get; set; }

    /// <summary>
    /// 该引脚接受或产生的数据类型
    /// </summary>
    [JsonIgnore]
    public Type DataType { get; set; } = typeof(object);

    /// <summary>
    /// 该引脚接受或产生的数据类型的完全限定名（用于序列化）
    /// </summary>
    public string DataTypeName
    {
        get => DataType.AssemblyQualifiedName ?? typeof(object).AssemblyQualifiedName!;
        set
        {
            var type = Type.GetType(value);
            if (type != null)
            {
                DataType = type;
            }
        }
    }

    /// <summary>
    /// 引脚当前持有的数据值
    /// </summary>
    public object? Value
    {
        get => _value;
        set
        {
            if (!Equals(_value, value))
            {
                _value = value;
                OnPropertyChanged();
                OnValueChanged();
            }
        }
    }

    /// <summary>
    /// 与此引脚相连的连接线。一个输入引脚通常只允许一个连接，输出引脚可以有多个
    /// </summary>
    [JsonIgnore]
    public Connection? Connection
    {
        get => _connection;
        set
        {
            if (_connection != value)
            {
                _connection = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 用于存储额外信息的标签，通常用于关联属性名或其他元数据
    /// </summary>
    [JsonIgnore]
    public object? Tag { get; set; }

    /// <summary>
    /// 指示此引脚是否为流程引脚（执行流程控制，而非数据传递）
    /// </summary>
    [JsonIgnore]
    public bool IsFlowPin { get; set; }

    /// <summary>
    /// 当引脚值发生变化时触发的事件
    /// </summary>
    public event EventHandler? ValueChanged;

    /// <summary>
    /// 属性变化通知事件
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 触发值变化事件
    /// </summary>
    protected virtual void OnValueChanged()
    {
        ValueChanged?.Invoke(this, EventArgs.Empty);
        
        // 如果是输出引脚，通知父节点输出发生变化
        if (Direction == PinDirection.Output && ParentNode != null)
        {
            ParentNode.OnOutputChanged(this);
        }
        
        // 如果是输入引脚，通知父节点输入发生变化
        if (Direction == PinDirection.Input && ParentNode != null)
        {
            ParentNode.OnInputChanged(this);
        }
    }

    /// <summary>
    /// 触发属性变化通知
    /// </summary>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// 检查此引脚是否可以与另一个引脚连接
    /// </summary>
    /// <param name="otherPin">要检查的另一个引脚</param>
    /// <returns>如果可以连接返回true，否则返回false</returns>
    public bool CanConnectTo(Pin otherPin)
    {
        if (otherPin == null || otherPin == this)
            return false;

        // 不能连接到同一个节点的引脚
        if (ParentNode == otherPin.ParentNode)
            return false;

        // 方向必须不同
        if (Direction == otherPin.Direction)
            return false;

        // 输入引脚只能有一个连接
        if (Direction == PinDirection.Input && Connection != null)
            return false;

        if (otherPin.Direction == PinDirection.Input && otherPin.Connection != null)
            return false;

        // 流程引脚只能连接到流程引脚
        if (IsFlowPin && !otherPin.IsFlowPin)
            return false;

        if (!IsFlowPin && otherPin.IsFlowPin)
            return false;

        // 数据类型必须兼容
        return IsDataTypeCompatible(otherPin.DataType);
    }

    /// <summary>
    /// 检查数据类型是否兼容
    /// </summary>
    /// <param name="otherType">要检查的数据类型</param>
    /// <returns>如果兼容返回true，否则返回false</returns>
    public bool IsDataTypeCompatible(Type otherType)
    {
        // 如果任一类型是object，则认为兼容
        if (DataType == typeof(object) || otherType == typeof(object))
            return true;

        // 检查类型是否相同或可以赋值
        return DataType.IsAssignableFrom(otherType) || otherType.IsAssignableFrom(DataType);
    }
}
