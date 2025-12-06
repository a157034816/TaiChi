using TaiChi.Wpf.NodeEditor.Core.Enums;

namespace TaiChi.Wpf.NodeEditor.Core.Registry;

/// <summary>
/// 引脚元数据，描述引脚的定义信息
/// </summary>
public class PinMetadata
{
    /// <summary>
    /// 引脚的显示名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 引脚的方向（输入或输出）
    /// </summary>
    public PinDirection Direction { get; set; }

    /// <summary>
    /// 引脚的数据类型
    /// </summary>
    public Type DataType { get; set; } = typeof(object);

    /// <summary>
    /// 引脚的描述信息
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 引脚的默认值
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// 指示此引脚是否为流程引脚（执行流程控制，而非数据传递）
    /// </summary>
    public bool IsFlowPin { get; set; }

    /// <summary>
    /// 构造函数
    /// </summary>
    public PinMetadata()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name">引脚名称</param>
    /// <param name="direction">引脚方向</param>
    /// <param name="dataType">数据类型</param>
    public PinMetadata(string name, PinDirection direction, Type dataType)
    {
        Name = name;
        Direction = direction;
        DataType = dataType;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name">引脚名称</param>
    /// <param name="direction">引脚方向</param>
    /// <param name="dataType">数据类型</param>
    /// <param name="description">描述信息</param>
    public PinMetadata(string name, PinDirection direction, Type dataType, string description)
        : this(name, direction, dataType)
    {
        Description = description;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name">引脚名称</param>
    /// <param name="direction">引脚方向</param>
    /// <param name="dataType">数据类型</param>
    /// <param name="description">描述信息</param>
    /// <param name="defaultValue">默认值</param>
    public PinMetadata(string name, PinDirection direction, Type dataType, string description, object? defaultValue)
        : this(name, direction, dataType, description)
    {
        DefaultValue = defaultValue;
    }

    /// <summary>
    /// 创建一个输入引脚元数据
    /// </summary>
    /// <param name="name">引脚名称</param>
    /// <param name="dataType">数据类型</param>
    /// <param name="description">描述信息</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>输入引脚元数据</returns>
    public static PinMetadata CreateInput(string name, Type dataType, string description = "", object? defaultValue = null)
    {
        return new PinMetadata(name, PinDirection.Input, dataType, description, defaultValue);
    }

    /// <summary>
    /// 创建一个输出引脚元数据
    /// </summary>
    /// <param name="name">引脚名称</param>
    /// <param name="dataType">数据类型</param>
    /// <param name="description">描述信息</param>
    /// <returns>输出引脚元数据</returns>
    public static PinMetadata CreateOutput(string name, Type dataType, string description = "")
    {
        return new PinMetadata(name, PinDirection.Output, dataType, description);
    }

    public override string ToString()
    {
        return $"{Direction} Pin: {Name} ({DataType.Name})";
    }
}
