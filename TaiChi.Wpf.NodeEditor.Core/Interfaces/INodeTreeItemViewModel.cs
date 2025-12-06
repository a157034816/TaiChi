using TaiChi.Wpf.NodeEditor.Core.Registry;

namespace TaiChi.Wpf.NodeEditor.Core.Interfaces;

/// <summary>
/// 树形视图中节点项（叶子节点）的接口定义
/// </summary>
public interface INodeTreeItemViewModel : ITreeItemViewModel
{
    /// <summary>
    /// 节点元数据
    /// </summary>
    NodeMetadata? NodeMetadata { get; }

    /// <summary>
    /// 节点描述
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 节点类型名称
    /// </summary>
    string NodeType { get; }

    /// <summary>
    /// 输入引脚数量
    /// </summary>
    int InputPinCount { get; }

    /// <summary>
    /// 输出引脚数量
    /// </summary>
    int OutputPinCount { get; }

    /// <summary>
    /// 是否为基于方法的节点
    /// </summary>
    bool IsMethodBased { get; }
}