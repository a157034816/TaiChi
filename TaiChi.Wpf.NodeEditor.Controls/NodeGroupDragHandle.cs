using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Data;
using TaiChi.Wpf.NodeEditor.Core.Models;
using TaiChi.Wpf.NodeEditor.Controls.Managers;

namespace TaiChi.Wpf.NodeEditor.Controls;

/// <summary>
/// 仅用于拖拽分组的轻量交互控件：自身不渲染背景，仅提供命中区域（默认为标题高度）。
/// 放置于节点层之上，但由于无背景视觉，不会遮挡节点显示；拖拽时通过 GroupManager 级联移动。
/// </summary>
public class NodeGroupDragHandle : UserControl
{
    public static readonly DependencyProperty GroupDataProperty =
        DependencyProperty.Register(nameof(GroupData), typeof(NodeGroup), typeof(NodeGroupDragHandle), new PropertyMetadata(null));

    public NodeGroup? GroupData
    {
        get => (NodeGroup?)GetValue(GroupDataProperty);
        set => SetValue(GroupDataProperty, value);
    }

    public static readonly DependencyProperty GroupManagerProperty =
        DependencyProperty.Register(nameof(GroupManager), typeof(NodeGroupManager), typeof(NodeGroupDragHandle), new PropertyMetadata(null));

    public NodeGroupManager? GroupManager
    {
        get => (NodeGroupManager?)GetValue(GroupManagerProperty);
        set => SetValue(GroupManagerProperty, value);
    }

    public static readonly DependencyProperty ZoomLevelProperty =
        DependencyProperty.Register(nameof(ZoomLevel), typeof(double), typeof(NodeGroupDragHandle), new PropertyMetadata(1.0));

    public double ZoomLevel
    {
        get => (double)GetValue(ZoomLevelProperty);
        set => SetValue(ZoomLevelProperty, value);
    }

    public static readonly DependencyProperty HeaderHeightProperty =
        DependencyProperty.Register(nameof(HeaderHeight), typeof(double), typeof(NodeGroupDragHandle), new PropertyMetadata(24.0));

    public double HeaderHeight
    {
        get => (double)GetValue(HeaderHeightProperty);
        set => SetValue(HeaderHeightProperty, value);
    }

    private bool _isDragging;
    private Point _start;
    private NodeEditorRect _startBounds;
    private UIElement? _dragRef; // 稳定参考元素，用于计算拖拽位移，避免控件随拖动移动造成位移为零
    private double _appliedDx; // 已应用到分组上的累计位移（用于将绝对位移转为增量位移）
    private double _appliedDy;

    public NodeGroupDragHandle()
    {
        // 构建一个透明的命中区域（高度为 HeaderHeight，顶部对齐）
        var hit = new Border
        {
            Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Top
        };
        hit.SetBinding(FrameworkElement.HeightProperty, new Binding(nameof(HeaderHeight)) { Source = this });

        hit.PreviewMouseLeftButtonDown += OnMouseDown;
        // 移动事件绑定到控件本身，保证捕获后离开命中区也能持续移动
        // hit.PreviewMouseMove += OnMouseMove;
        hit.PreviewMouseLeftButtonUp += OnMouseUp;
        // 兜底：捕获丢失或在控件层级收到 MouseUp 时结束拖拽
        LostMouseCapture += (_, __) => { EndDrag(); };
        PreviewMouseMove += OnMouseMove;
        PreviewMouseLeftButtonUp += OnMouseUp;
        MouseEnter += (_, __) => { if (!_isDragging) Cursor = Cursors.SizeAll; };
        MouseLeave += (_, __) => { if (!_isDragging) Cursor = null; };

        Content = hit;
        IsHitTestVisible = true;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (GroupData == null) return;
        _isDragging = true;
        _dragRef = FindStableRef();
        _start = e.GetPosition(_dragRef ?? this);
        _startBounds = GroupData.Bounds;
        _appliedDx = 0;
        _appliedDy = 0;
        CaptureMouse();
        Cursor = Cursors.SizeAll;
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        // 数据不存在：任何移动都无效，确保退出
        if (GroupData == null)
        {
            EndDrag();
            return;
        }

        // 若正在拖拽但左键已经松开（例如在控件外释放），立即结束拖拽
        if (_isDragging && e.LeftButton != MouseButtonState.Pressed)
        {
            EndDrag();
            return;
        }

        // 未处于拖拽态，直接忽略移动
        if (!_isDragging)
        {
            return;
        }
        var current = e.GetPosition(_dragRef ?? this);
        var delta = current - _start;

        // 使用稳定参考坐标系计算位移；由于参考未应用缩放，需按 ZoomLevel 还原为逻辑位移
        var zoom = ZoomLevel <= 0 ? 1.0 : ZoomLevel;
        var dx = delta.X / zoom;
        var dy = delta.Y / zoom;

        if (GroupManager != null)
        {
            // GroupManager.MoveGroup 是增量移动，这里将“从起点起的绝对位移”dx/dy 转换为“相对上一次调用的增量”
            var incDx = dx - _appliedDx;
            var incDy = dy - _appliedDy;

            if (Math.Abs(incDx) > double.Epsilon || Math.Abs(incDy) > double.Epsilon)
            {
                GroupManager.MoveGroup(GroupData, incDx, incDy, cascadeNodes: true, cascadeChildren: true);
                _appliedDx = dx;
                _appliedDy = dy;
            }
        }
        else
        {
            GroupData.Bounds = new NodeEditorRect(_startBounds.X + dx, _startBounds.Y + dy, _startBounds.Width, _startBounds.Height);
            foreach (var n in GroupData.GetAllNodesRecursive())
            {
                n.Position = new NodeEditorPoint(n.Position.X + dx, n.Position.Y + dy);
            }
        }

        e.Handled = true;
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        EndDrag();
        e.Handled = true;
    }

    private void EndDrag()
    {
        if (_isDragging)
        {
            _isDragging = false;
        }
        _appliedDx = 0;
        _appliedDy = 0;
        // 强制释放全局捕获，避免光标卡住与窗口不可交互
        try { if (Mouse.Captured != null) Mouse.Capture(null); } catch { }
        try { ReleaseMouseCapture(); } catch { }
        // 恢复光标（避免残留为拖拽形态）
        try { Cursor = null; } catch { }
    }

    private UIElement? FindStableRef()
    {
        // 向上查找到包含各层的父 Grid（未应用缩放/平移），作为稳定参考坐标系
        DependencyObject? p = this;
        while (p != null)
        {
            p = VisualTreeHelper.GetParent(p);
            if (p is Grid g)
                return g;
        }
        return null;
    }
}
