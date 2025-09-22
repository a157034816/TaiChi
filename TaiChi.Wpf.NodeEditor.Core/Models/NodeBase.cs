using System.Reflection;
using TaiChi.Wpf.NodeEditor.Core.Attributes;
using TaiChi.Wpf.NodeEditor.Core.Enums;
using TaiChi.Wpf.NodeEditor.Core.Registry;

namespace TaiChi.Wpf.NodeEditor.Core.Models;

/// <summary>
/// 节点基类，为基于类的节点提供基础实现。
/// 继承此类的节点可以通过特性标记来定义引脚和流程控制。
/// </summary>
public abstract class NodeBase : Node
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected NodeBase()
    {
        // 不在构造函数中初始化引脚，让NodeMetadata.CreateInstance统一处理
    }

    /// <summary>
    /// 节点的核心定义方法，派生类必须实现此方法来定义节点的具体逻辑
    /// </summary>
    public abstract void Definition();

    /// <summary>
    /// 从NodeMetadata初始化引脚（由NodeMetadata.CreateInstance调用）
    /// </summary>
    /// <param name="metadata">节点元数据</param>
    internal void InitializePinsFromMetadata(NodeMetadata metadata)
    {
        // 根据元数据创建引脚
        foreach (var pinMetadata in metadata.InputPins)
        {
            var pin = new Pin
            {
                Name = pinMetadata.Name,
                Direction = pinMetadata.Direction,
                DataType = pinMetadata.DataType,
                Value = pinMetadata.DefaultValue,
                ParentNode = this,
                IsFlowPin = pinMetadata.IsFlowPin
            };

            // 如果是数据引脚，设置Tag为属性名用于映射
            if (!pinMetadata.IsFlowPin)
            {
                pin.Tag = FindPropertyNameForPin(pinMetadata.Name);
            }
            else
            {
                pin.Tag = pinMetadata.IsFlowPin && pinMetadata.Direction == PinDirection.Input ? "FlowEntry" : "FlowExit";
            }

            InputPins.Add(pin);
        }

        foreach (var pinMetadata in metadata.OutputPins)
        {
            var pin = new Pin
            {
                Name = pinMetadata.Name,
                Direction = pinMetadata.Direction,
                DataType = pinMetadata.DataType,
                ParentNode = this,
                IsFlowPin = pinMetadata.IsFlowPin
            };

            // 如果是数据引脚，设置Tag为属性名用于映射
            if (!pinMetadata.IsFlowPin)
            {
                pin.Tag = FindPropertyNameForPin(pinMetadata.Name);
            }
            else
            {
                pin.Tag = pinMetadata.IsFlowPin && pinMetadata.Direction == PinDirection.Output ? "FlowExit" : "FlowEntry";
            }

            OutputPins.Add(pin);
        }
    }

    /// <summary>
    /// 根据引脚名称查找对应的属性名
    /// </summary>
    /// <param name="pinName">引脚名称</param>
    /// <returns>属性名</returns>
    private string? FindPropertyNameForPin(string pinName)
    {
        var type = GetType();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            // 检查Pin特性
            var pinAttribute = property.GetCustomAttribute<PinAttribute>();
            if (pinAttribute != null && pinAttribute.Name == pinName)
            {
                return property.Name;
            }

            // 检查PinReturn特性
            var pinReturnAttribute = property.GetCustomAttribute<PinReturnAttribute>();
            if (pinReturnAttribute != null && pinReturnAttribute.Name == pinName)
            {
                return property.Name;
            }
        }

        return null;
    }

    /// <summary>
    /// 重写执行方法，调用Definition方法
    /// </summary>
    protected override void OnExecute()
    {
        // 从输入引脚获取值并设置到对应属性
        UpdatePropertiesFromInputPins();
        
        // 执行节点定义的逻辑
        Definition();
        
        // 从属性获取值并设置到输出引脚
        UpdateOutputPinsFromProperties();
    }

    /// <summary>
    /// 根据特性初始化引脚
    /// </summary>
    private void InitializePinsFromAttributes()
    {
        var type = GetType();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            // 处理输入引脚（Pin特性）
            var pinAttribute = property.GetCustomAttribute<PinAttribute>();
            if (pinAttribute != null)
            {
                var inputPin = AddInputPin(pinAttribute.Name, property.PropertyType);
                inputPin.Tag = property.Name; // 存储属性名用于后续映射
            }

            // 处理输出引脚（PinReturn特性）
            var pinReturnAttribute = property.GetCustomAttribute<PinReturnAttribute>();
            if (pinReturnAttribute != null)
            {
                var outputPin = AddOutputPin(pinReturnAttribute.Name, property.PropertyType);
                outputPin.Tag = property.Name; // 存储属性名用于后续映射
            }
        }

        // 处理流程入口和出口
        InitializeFlowPins();
    }

    /// <summary>
    /// 初始化流程控制引脚
    /// </summary>
    private void InitializeFlowPins()
    {
        var type = GetType();

        // 处理入口
        var entryAttributes = type.GetCustomAttributes<NodeEntryAttribute>();
        foreach (var entry in entryAttributes)
        {
            var entryPin = AddInputPin(entry.Name, typeof(object)); // 流程引脚使用object类型
            entryPin.Tag = "FlowEntry"; // 标记为流程入口
            entryPin.IsFlowPin = true; // 标记为流程引脚
        }

        // 处理出口
        var exitAttributes = type.GetCustomAttributes<NodeExitAttribute>();
        foreach (var exit in exitAttributes)
        {
            var exitPin = AddOutputPin(exit.Name, typeof(object)); // 流程引脚使用object类型
            exitPin.Tag = "FlowExit"; // 标记为流程出口
            exitPin.IsFlowPin = true; // 标记为流程引脚
        }
    }

    /// <summary>
    /// 从输入引脚更新属性值
    /// </summary>
    private void UpdatePropertiesFromInputPins()
    {
        var type = GetType();
        
        foreach (var inputPin in InputPins)
        {
            if (inputPin.Tag is string propertyName && inputPin.Tag.ToString() != "FlowEntry")
            {
                var property = type.GetProperty(propertyName);
                if (property != null && property.CanWrite && inputPin.Value != null)
                {
                    try
                    {
                        // 类型转换
                        var convertedValue = Convert.ChangeType(inputPin.Value, property.PropertyType);
                        property.SetValue(this, convertedValue);
                    }
                    catch (Exception ex)
                    {
                        // 记录转换错误但不中断执行
                        Console.WriteLine($"Failed to convert value for property {propertyName}: {ex.Message}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// 从属性更新输出引脚值
    /// </summary>
    private void UpdateOutputPinsFromProperties()
    {
        var type = GetType();
        
        foreach (var outputPin in OutputPins)
        {
            if (outputPin.Tag is string propertyName && outputPin.Tag.ToString() != "FlowExit")
            {
                var property = type.GetProperty(propertyName);
                if (property != null && property.CanRead)
                {
                    try
                    {
                        var value = property.GetValue(this);
                        outputPin.Value = value;
                    }
                    catch (Exception ex)
                    {
                        // 记录获取值错误但不中断执行
                        Console.WriteLine($"Failed to get value for property {propertyName}: {ex.Message}");
                    }
                }
            }
        }
    }
}
