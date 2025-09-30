using System.Windows;
using System.Windows.Controls;
using TaiChi.Wpf.NodeEditor.Core.Models;

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

    /// <summary>
    /// 构建分组上的上下文菜单。
    /// position 为相对 NodeCanvas 内部主画布的物理坐标。
    /// </summary>
    ContextMenu? BuildGroupContextMenu(NodeCanvas canvas, NodeGroup group, Point position);

    /// <summary>
    /// 在画布空白处的菜单中附加分组相关项（如：从当前选择创建分组）。
    /// 实现方可根据业务需要将分组菜单项添加到传入的 menu 中。
    /// </summary>
    void AppendGroupItemsForCanvas(ContextMenu menu, NodeCanvas canvas, Point position);

    /// <summary>
    /// 在节点菜单中附加分组相关项（如：移出分组、移动到其他分组）。
    /// 实现方可根据业务需要将分组菜单项添加到传入的 menu 中。
    /// </summary>
    void AppendGroupItemsForNode(ContextMenu menu, NodeCanvas canvas, NodeControl node, Point position);
}
