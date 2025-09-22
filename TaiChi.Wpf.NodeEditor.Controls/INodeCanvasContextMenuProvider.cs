using System.Windows;
using System.Windows.Controls;

namespace TaiChi.Wpf.NodeEditor.Controls;

/// <summary>
/// 为 NodeCanvas 提供上下文菜单的提供器接口，由上层应用实现。
/// </summary>
public interface INodeCanvasContextMenuProvider
{
    /// <summary>
    /// 构建画布空白处的上下文菜单。
    /// position 为相对 NodeCanvas 内部主画布的物理坐标。
    /// </summary>
    ContextMenu? BuildCanvasContextMenu(NodeCanvas canvas, Point position);

    /// <summary>
    /// 构建节点上的上下文菜单。
    /// position 为相对 NodeCanvas 内部主画布的物理坐标。
    /// </summary>
    ContextMenu? BuildNodeContextMenu(NodeCanvas canvas, NodeControl node, Point position);
}

