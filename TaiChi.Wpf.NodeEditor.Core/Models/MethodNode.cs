using System.Collections.ObjectModel;
using TaiChi.Wpf.NodeEditor.Core.Enums;

namespace TaiChi.Wpf.NodeEditor.Core.Models;

/// <summary>
/// 通用的方法节点类，用于包装基于 MethodInfo 的节点执行逻辑。
/// 这个类允许将普通的C#方法转换为可执行的节点，支持动态引脚配置。
/// </summary>
public class MethodNode : Node
{
    private Func<object?[], object?[]>? _executionLogic;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name">节点名称</param>
    public MethodNode(string name)
    {
        Name = name;
    }

    /// <summary>
    /// 动态添加输入引脚
    /// </summary>
    /// <param name="name">引脚名称</param>
    /// <param name="dataType">数据类型</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>创建的引脚实例</returns>
    public Pin AddInputPin(string name, Type dataType, object? defaultValue = null)
    {
        var pin = new Pin
        {
            Name = name,
            Direction = PinDirection.Input,
            DataType = dataType,
            Value = defaultValue,
            ParentNode = this
        };
        InputPins.Add(pin);
        return pin;
    }

    /// <summary>
    /// 动态添加输出引脚
    /// </summary>
    /// <param name="name">引脚名称</param>
    /// <param name="dataType">数据类型</param>
    /// <returns>创建的引脚实例</returns>
    public Pin AddOutputPin(string name, Type dataType)
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
    /// 设置节点的执行逻辑函数
    /// </summary>
    /// <param name="logic">执行逻辑函数，接收输入值数组，返回输出值数组</param>
    public void SetExecutionLogic(Func<object?[], object?[]> logic)
    {
        _executionLogic = logic ?? throw new ArgumentNullException(nameof(logic));
    }

    /// <summary>
    /// 执行节点逻辑
    /// </summary>
    protected override void OnExecute()
    {
        if (_executionLogic == null)
        {
            throw new InvalidOperationException($"Node '{Name}' has no execution logic set");
        }

        try
        {
            // 从输入引脚收集数据
            var inputs = GetInputValues();

            // 调用核心执行逻辑
            var outputs = _executionLogic(inputs);

            // 将结果设置到输出引脚
            SetOutputValues(outputs);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error executing node '{Name}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 从所有输入引脚收集值
    /// </summary>
    /// <returns>输入值数组，按引脚添加顺序排列</returns>
    private object?[] GetInputValues()
    {
        var values = new object?[InputPins.Count];
        for (int i = 0; i < InputPins.Count; i++)
        {
            values[i] = InputPins[i].Value;
        }
        return values;
    }

    /// <summary>
    /// 将输出值设置到输出引脚
    /// </summary>
    /// <param name="outputs">输出值数组</param>
    private void SetOutputValues(object?[] outputs)
    {
        if (outputs == null)
            return;

        // 确保输出值数量不超过输出引脚数量
        var outputCount = Math.Min(outputs.Length, OutputPins.Count);
        
        for (int i = 0; i < outputCount; i++)
        {
            if (i < OutputPins.Count)
            {
                OutputPins[i].Value = outputs[i];
            }
        }
    }

    /// <summary>
    /// 当输入引脚的值发生变化时调用
    /// </summary>
    /// <param name="inputPin">发生变化的输入引脚</param>
    public override void OnInputChanged(Pin inputPin)
    {
        // 检查是否所有必需的输入引脚都有值
        var requiredInputsReady = InputPins.All(p => p.Value != null || IsOptionalInput(p));
        
        if (requiredInputsReady && _executionLogic != null)
        {
            Execute();
        }
    }

    /// <summary>
    /// 检查输入引脚是否是可选的（有默认值）
    /// </summary>
    /// <param name="pin">要检查的引脚</param>
    /// <returns>如果引脚是可选的返回true</returns>
    private static bool IsOptionalInput(Pin pin)
    {
        // 如果引脚有默认值或者其数据类型是可空的，则认为是可选的
        return pin.Value != null || 
               pin.DataType.IsGenericType && pin.DataType.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

    /// <summary>
    /// 获取节点的调试信息
    /// </summary>
    /// <returns>调试信息字符串</returns>
    public string GetDebugInfo()
    {
        var inputInfo = string.Join(", ", InputPins.Select(p => $"{p.Name}:{p.Value ?? "null"}"));
        var outputInfo = string.Join(", ", OutputPins.Select(p => $"{p.Name}:{p.Value ?? "null"}"));
        
        return $"MethodNode '{Name}' - Inputs: [{inputInfo}], Outputs: [{outputInfo}], HasLogic: {_executionLogic != null}";
    }

    /// <summary>
    /// 重置节点状态，清空所有引脚的值
    /// </summary>
    public void Reset()
    {
        foreach (var pin in InputPins)
        {
            pin.Value = null;
        }
        
        foreach (var pin in OutputPins)
        {
            pin.Value = null;
        }
        
        State = NodeState.Normal;
    }
}