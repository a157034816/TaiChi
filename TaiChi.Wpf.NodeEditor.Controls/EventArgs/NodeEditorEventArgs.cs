using System;
using System.Collections.Generic;
using System.Windows;
using TaiChi.Wpf.NodeEditor.Core.Models;
using TaiChi.Wpf.NodeEditor.Core.Registry;

namespace TaiChi.Wpf.NodeEditor.Controls.EventArgs;

/// <summary>
/// 节点编辑器事件参数基类
/// </summary>
public abstract class NodeEditorEventArgs : RoutedEventArgs
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="routedEvent">路由事件</param>
    /// <param name="source">事件源</param>
    protected NodeEditorEventArgs(RoutedEvent routedEvent, object source) : base(routedEvent, source)
    {
    }
}

/// <summary>
/// 节点事件参数
/// </summary>
public class NodeEventArgs : NodeEditorEventArgs
{
    /// <summary>
    /// 节点
    /// </summary>
    public Node Node { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="routedEvent">路由事件</param>
    /// <param name="source">事件源</param>
    /// <param name="node">节点</param>
    public NodeEventArgs(RoutedEvent routedEvent, object source, Node node) : base(routedEvent, source)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
    }
}

/// <summary>
/// 引脚事件参数
/// </summary>
public class PinEventArgs : NodeEditorEventArgs
{
    /// <summary>
    /// 引脚
    /// </summary>
    public Pin Pin { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="routedEvent">路由事件</param>
    /// <param name="source">事件源</param>
    /// <param name="pin">引脚</param>
    public PinEventArgs(RoutedEvent routedEvent, object source, Pin pin) : base(routedEvent, source)
    {
        Pin = pin ?? throw new ArgumentNullException(nameof(pin));
    }
}

/// <summary>
/// 连接事件参数
/// </summary>
public class ConnectionEventArgs : NodeEditorEventArgs
{
    /// <summary>
    /// 连接
    /// </summary>
    public Connection Connection { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="routedEvent">路由事件</param>
    /// <param name="source">事件源</param>
    /// <param name="connection">连接</param>
    public ConnectionEventArgs(RoutedEvent routedEvent, object source, Connection connection) : base(routedEvent, source)
    {
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }
}

/// <summary>
/// 节点创建事件参数
/// </summary>
public class NodeCreationEventArgs : NodeEditorEventArgs
{
    /// <summary>
    /// 节点元数据
    /// </summary>
    public NodeMetadata NodeMetadata { get; }

    /// <summary>
    /// 创建位置
    /// </summary>
    public Point Position { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="routedEvent">路由事件</param>
    /// <param name="source">事件源</param>
    /// <param name="nodeMetadata">节点元数据</param>
    /// <param name="position">创建位置</param>
    public NodeCreationEventArgs(RoutedEvent routedEvent, object source, NodeMetadata nodeMetadata, Point position) 
        : base(routedEvent, source)
    {
        NodeMetadata = nodeMetadata ?? throw new ArgumentNullException(nameof(nodeMetadata));
        Position = position;
    }
}

/// <summary>
/// 连接创建事件参数
/// </summary>
public class ConnectionCreationEventArgs : NodeEditorEventArgs
{
    /// <summary>
    /// 源引脚
    /// </summary>
    public Pin SourcePin { get; }

    /// <summary>
    /// 目标引脚
    /// </summary>
    public Pin TargetPin { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="routedEvent">路由事件</param>
    /// <param name="source">事件源</param>
    /// <param name="sourcePin">源引脚</param>
    /// <param name="targetPin">目标引脚</param>
    public ConnectionCreationEventArgs(RoutedEvent routedEvent, object source, Pin sourcePin, Pin targetPin) 
        : base(routedEvent, source)
    {
        SourcePin = sourcePin ?? throw new ArgumentNullException(nameof(sourcePin));
        TargetPin = targetPin ?? throw new ArgumentNullException(nameof(targetPin));
    }
}

/// <summary>
/// 选择变化事件参数
/// </summary>
public class SelectionChangedEventArgs : NodeEditorEventArgs
{
    /// <summary>
    /// 选中的节点
    /// </summary>
    public IReadOnlyList<Node> SelectedNodes { get; }

    /// <summary>
    /// 选中的连接
    /// </summary>
    public IReadOnlyList<Connection> SelectedConnections { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="routedEvent">路由事件</param>
    /// <param name="source">事件源</param>
    /// <param name="selectedNodes">选中的节点</param>
    /// <param name="selectedConnections">选中的连接</param>
    public SelectionChangedEventArgs(RoutedEvent routedEvent, object source, 
        IReadOnlyList<Node> selectedNodes, IReadOnlyList<Connection> selectedConnections) 
        : base(routedEvent, source)
    {
        SelectedNodes = selectedNodes ?? throw new ArgumentNullException(nameof(selectedNodes));
        SelectedConnections = selectedConnections ?? throw new ArgumentNullException(nameof(selectedConnections));
    }
}

/// <summary>
/// 画布变化事件参数
/// </summary>
public class CanvasChangedEventArgs : NodeEditorEventArgs
{
    /// <summary>
    /// 缩放级别
    /// </summary>
    public double ZoomLevel { get; }

    /// <summary>
    /// 平移偏移
    /// </summary>
    public Point PanOffset { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="routedEvent">路由事件</param>
    /// <param name="source">事件源</param>
    /// <param name="zoomLevel">缩放级别</param>
    /// <param name="panOffset">平移偏移</param>
    public CanvasChangedEventArgs(RoutedEvent routedEvent, object source, double zoomLevel, Point panOffset) 
        : base(routedEvent, source)
    {
        ZoomLevel = zoomLevel;
        PanOffset = panOffset;
    }
}

/// <summary>
/// 拖拽事件参数
/// </summary>
public class DragEventArgs : NodeEditorEventArgs
{
    /// <summary>
    /// 拖拽的数据
    /// </summary>
    public object DragData { get; }

    /// <summary>
    /// 拖拽位置
    /// </summary>
    public Point Position { get; }

    /// <summary>
    /// 拖拽效果
    /// </summary>
    public System.Windows.DragDropEffects Effects { get; set; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="routedEvent">路由事件</param>
    /// <param name="source">事件源</param>
    /// <param name="dragData">拖拽数据</param>
    /// <param name="position">拖拽位置</param>
    public DragEventArgs(RoutedEvent routedEvent, object source, object dragData, Point position) 
        : base(routedEvent, source)
    {
        DragData = dragData ?? throw new ArgumentNullException(nameof(dragData));
        Position = position;
        Effects = System.Windows.DragDropEffects.None;
    }
}

/// <summary>
/// 可取消的事件参数基类
/// </summary>
public abstract class CancelableEventArgs : NodeEditorEventArgs
{
    /// <summary>
    /// 是否取消操作
    /// </summary>
    public bool Cancel { get; set; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="routedEvent">路由事件</param>
    /// <param name="source">事件源</param>
    protected CancelableEventArgs(RoutedEvent routedEvent, object source) : base(routedEvent, source)
    {
        Cancel = false;
    }
}

/// <summary>
/// 可取消的节点删除事件参数
/// </summary>
public class NodeDeletingEventArgs : CancelableEventArgs
{
    /// <summary>
    /// 要删除的节点
    /// </summary>
    public IReadOnlyList<Node> NodesToDelete { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="routedEvent">路由事件</param>
    /// <param name="source">事件源</param>
    /// <param name="nodesToDelete">要删除的节点</param>
    public NodeDeletingEventArgs(RoutedEvent routedEvent, object source, IReadOnlyList<Node> nodesToDelete) 
        : base(routedEvent, source)
    {
        NodesToDelete = nodesToDelete ?? throw new ArgumentNullException(nameof(nodesToDelete));
    }
}

/// <summary>
/// 可取消的连接删除事件参数
/// </summary>
public class ConnectionDeletingEventArgs : CancelableEventArgs
{
    /// <summary>
    /// 要删除的连接
    /// </summary>
    public IReadOnlyList<Connection> ConnectionsToDelete { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="routedEvent">路由事件</param>
    /// <param name="source">事件源</param>
    /// <param name="connectionsToDelete">要删除的连接</param>
    public ConnectionDeletingEventArgs(RoutedEvent routedEvent, object source, IReadOnlyList<Connection> connectionsToDelete) 
        : base(routedEvent, source)
    {
        ConnectionsToDelete = connectionsToDelete ?? throw new ArgumentNullException(nameof(connectionsToDelete));
    }
}
