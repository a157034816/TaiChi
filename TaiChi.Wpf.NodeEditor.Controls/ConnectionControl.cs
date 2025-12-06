using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TaiChi.Wpf.NodeEditor.Core.Models;
using TaiChi.Wpf.NodeEditor.Controls.Helpers;

namespace TaiChi.Wpf.NodeEditor.Controls;

/// <summary>
/// 连接线类型枚举
/// </summary>
public enum ConnectionType
{
    /// <summary>
    /// 直线连接
    /// </summary>
    Straight,

    /// <summary>
    /// 贝塞尔曲线连接
    /// </summary>
    Bezier,

    /// <summary>
    /// 直角连接
    /// </summary>
    Orthogonal,

    /// <summary>
    /// 圆弧连接
    /// </summary>
    Arc
}

/// <summary>
/// 连接线控件 - 重构为 Control 以实现完全解耦
/// </summary>
public class ConnectionControl : Control
{
    #region 依赖属性

    /// <summary>
    /// 起始点
    /// </summary>
    public static readonly DependencyProperty SourcePointProperty =
        DependencyProperty.Register(nameof(SourcePoint), typeof(Point), typeof(ConnectionControl),
            new PropertyMetadata(new Point(), OnPathPointsChanged));

    /// <summary>
    /// 目标点
    /// </summary>
    public static readonly DependencyProperty TargetPointProperty =
        DependencyProperty.Register(nameof(TargetPoint), typeof(Point), typeof(ConnectionControl),
            new PropertyMetadata(new Point(), OnPathPointsChanged));

    /// <summary>
    /// 是否选中
    /// </summary>
    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(ConnectionControl),
            new PropertyMetadata(false));

    /// <summary>
    /// 是否高亮
    /// </summary>
    public static readonly DependencyProperty IsHighlightedProperty =
        DependencyProperty.Register(nameof(IsHighlighted), typeof(bool), typeof(ConnectionControl),
            new PropertyMetadata(false));

    /// <summary>
    /// 是否激活（数据流动动画）
    /// </summary>
    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(ConnectionControl),
            new PropertyMetadata(false));

    /// <summary>
    /// 连接线粗细
    /// </summary>
    public static readonly DependencyProperty StrokeThicknessProperty =
        DependencyProperty.Register(nameof(StrokeThickness), typeof(double), typeof(ConnectionControl),
            new PropertyMetadata(2.0));

    /// <summary>
    /// 连接线颜色
    /// </summary>
    public static readonly DependencyProperty StrokeProperty =
        DependencyProperty.Register(nameof(Stroke), typeof(Brush), typeof(ConnectionControl),
            new PropertyMetadata(Brushes.Black));

    /// <summary>
    /// 连接线样式（实线、虚线等）
    /// </summary>
    public static readonly DependencyProperty StrokeDashArrayProperty =
        DependencyProperty.Register(nameof(StrokeDashArray), typeof(DoubleCollection), typeof(ConnectionControl),
            new PropertyMetadata(null));

    /// <summary>
    /// 连接线类型（直线、贝塞尔曲线等）
    /// </summary>
    public static readonly DependencyProperty ConnectionTypeProperty =
        DependencyProperty.Register(nameof(ConnectionType), typeof(ConnectionType), typeof(ConnectionControl),
            new PropertyMetadata(ConnectionType.Bezier, OnPathPointsChanged));





    /// <summary>
    /// 路径数据（自动计算）
    /// </summary>
    public static readonly DependencyProperty PathDataProperty =
        DependencyProperty.Register(nameof(PathData), typeof(Geometry), typeof(ConnectionControl),
            new PropertyMetadata(null));

    #endregion

    #region 路由事件

    /// <summary>
    /// 连接线点击事件
    /// </summary>
    public static readonly RoutedEvent ConnectionClickEvent = EventManager.RegisterRoutedEvent(
        "ConnectionClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ConnectionControl));

    /// <summary>
    /// 连接线双击事件
    /// </summary>
    public static readonly RoutedEvent ConnectionDoubleClickEvent = EventManager.RegisterRoutedEvent(
        "ConnectionDoubleClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ConnectionControl));

    /// <summary>
    /// 连接线悬停进入事件
    /// </summary>
    public static readonly RoutedEvent ConnectionMouseEnterEvent = EventManager.RegisterRoutedEvent(
        "ConnectionMouseEnter", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ConnectionControl));

    /// <summary>
    /// 连接线悬停离开事件
    /// </summary>
    public static readonly RoutedEvent ConnectionMouseLeaveEvent = EventManager.RegisterRoutedEvent(
        "ConnectionMouseLeave", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ConnectionControl));

    /// <summary>
    /// 连接线选择状态变化事件
    /// </summary>
    public static readonly RoutedEvent ConnectionSelectionChangedEvent = EventManager.RegisterRoutedEvent(
        "ConnectionSelectionChanged", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ConnectionControl));

    /// <summary>
    /// 连接删除请求事件
    /// </summary>
    public static readonly RoutedEvent ConnectionDeleteRequestedEvent = EventManager.RegisterRoutedEvent(
        "ConnectionDeleteRequested", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ConnectionControl));

    #endregion

    #region 属性包装器

    /// <summary>
    /// 起始点
    /// </summary>
    public Point SourcePoint
    {
        get => (Point)GetValue(SourcePointProperty);
        set => SetValue(SourcePointProperty, value);
    }

    /// <summary>
    /// 目标点
    /// </summary>
    public Point TargetPoint
    {
        get => (Point)GetValue(TargetPointProperty);
        set => SetValue(TargetPointProperty, value);
    }

    /// <summary>
    /// 是否选中
    /// </summary>
    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    /// <summary>
    /// 是否高亮
    /// </summary>
    public bool IsHighlighted
    {
        get => (bool)GetValue(IsHighlightedProperty);
        set => SetValue(IsHighlightedProperty, value);
    }

    /// <summary>
    /// 是否激活（数据流动动画）
    /// </summary>
    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    /// <summary>
    /// 线条粗细
    /// </summary>
    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    /// <summary>
    /// 线条颜色
    /// </summary>
    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    /// <summary>
    /// 线条样式（虚线数组）
    /// </summary>
    public DoubleCollection StrokeDashArray
    {
        get => (DoubleCollection)GetValue(StrokeDashArrayProperty);
        set => SetValue(StrokeDashArrayProperty, value);
    }

    /// <summary>
    /// 连接线类型
    /// </summary>
    public ConnectionType ConnectionType
    {
        get => (ConnectionType)GetValue(ConnectionTypeProperty);
        set => SetValue(ConnectionTypeProperty, value);
    }

    /// <summary>
    /// 路径数据（自动计算）
    /// </summary>
    public Geometry PathData
    {
        get => (Geometry)GetValue(PathDataProperty);
        private set => SetValue(PathDataProperty, value);
    }

    #endregion

    #region 事件包装器

    /// <summary>
    /// 连接点击事件
    /// </summary>
    public event RoutedEventHandler ConnectionClick
    {
        add { AddHandler(ConnectionClickEvent, value); }
        remove { RemoveHandler(ConnectionClickEvent, value); }
    }

    /// <summary>
    /// 连接双击事件
    /// </summary>
    public event RoutedEventHandler ConnectionDoubleClick
    {
        add { AddHandler(ConnectionDoubleClickEvent, value); }
        remove { RemoveHandler(ConnectionDoubleClickEvent, value); }
    }

    /// <summary>
    /// 连接鼠标进入事件
    /// </summary>
    public event RoutedEventHandler ConnectionMouseEnter
    {
        add { AddHandler(ConnectionMouseEnterEvent, value); }
        remove { RemoveHandler(ConnectionMouseEnterEvent, value); }
    }

    /// <summary>
    /// 连接鼠标离开事件
    /// </summary>
    public event RoutedEventHandler ConnectionMouseLeave
    {
        add { AddHandler(ConnectionMouseLeaveEvent, value); }
        remove { RemoveHandler(ConnectionMouseLeaveEvent, value); }
    }

    /// <summary>
    /// 连接删除请求事件
    /// </summary>
    public event RoutedEventHandler ConnectionDeleteRequested
    {
        add { AddHandler(ConnectionDeleteRequestedEvent, value); }
        remove { RemoveHandler(ConnectionDeleteRequestedEvent, value); }
    }
    
    /// <summary>
    /// 连接线选择状态变化事件
    /// </summary>
    public event RoutedEventHandler ConnectionSelectionChanged
    {
        add { AddHandler(ConnectionSelectionChangedEvent, value); }
        remove { RemoveHandler(ConnectionSelectionChangedEvent, value); }
    }

    #endregion

    #region 依赖属性回调

    private static void OnPathPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ConnectionControl control)
        {
            control.UpdatePathData();
        }
    }

    /// <summary>
    /// 更新路径数据
    /// </summary>
    private void UpdatePathData()
    {
        PathData = CreateConnectionPath(SourcePoint, TargetPoint, ConnectionType);

        // 调试输出
        System.Diagnostics.Debug.WriteLine($"ConnectionControl.UpdatePathData: Source={SourcePoint}, Target={TargetPoint}");
        System.Diagnostics.Debug.WriteLine($"ConnectionControl.PathData: {PathData}");
        System.Diagnostics.Debug.WriteLine($"ConnectionControl.Stroke: {Stroke}");
        System.Diagnostics.Debug.WriteLine($"ConnectionControl.StrokeThickness: {StrokeThickness}");
        System.Diagnostics.Debug.WriteLine($"ConnectionControl.Visibility: {Visibility}");
        System.Diagnostics.Debug.WriteLine($"ConnectionControl.IsVisible: {IsVisible}");
    }

    /// <summary>
    /// 根据连接类型创建路径
    /// </summary>
    private static PathGeometry CreateConnectionPath(Point source, Point target, ConnectionType connectionType)
    {
        return connectionType switch
        {
            ConnectionType.Straight => GeometryHelper.CreateStraightPath(source, target),
            ConnectionType.Bezier => GeometryHelper.CreateBezierPath(source, target),
            ConnectionType.Orthogonal => GeometryHelper.CreateOrthogonalPath(source, target),
            ConnectionType.Arc => GeometryHelper.CreateArcPath(source, target),
            _ => GeometryHelper.CreateBezierPath(source, target)
        };
    }

    #endregion

    #region 构造函数和静态构造函数

    /// <summary>
    /// 静态构造函数
    /// </summary>
    static ConnectionControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ConnectionControl),
            new FrameworkPropertyMetadata(typeof(ConnectionControl)));
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    public ConnectionControl()
    {
        // 设置为可获得焦点，以便接收键盘事件
        Focusable = true;

        // 初始化路径数据
        UpdatePathData();
    }

    #endregion

    #region 事件处理

    /// <summary>
    /// 重写鼠标按下事件处理
    /// </summary>
    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            if (e.ClickCount == 1)
            {
                // 单击事件
                var args = new RoutedEventArgs(ConnectionClickEvent, this);
                RaiseEvent(args);
            }
            else if (e.ClickCount == 2)
            {
                // 双击事件
                var args = new RoutedEventArgs(ConnectionDoubleClickEvent, this);
                RaiseEvent(args);
            }

            e.Handled = true;
        }
        else if (e.RightButton == MouseButtonState.Pressed)
        {
            // 右键点击，可能是删除请求
            var args = new RoutedEventArgs(ConnectionDeleteRequestedEvent, this);
            RaiseEvent(args);
            e.Handled = true;
        }
    }

    /// <summary>
    /// 重写鼠标进入事件处理
    /// </summary>
    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);

        IsHighlighted = true;

        var args = new RoutedEventArgs(ConnectionMouseEnterEvent, this);
        RaiseEvent(args);
    }

    /// <summary>
    /// 重写鼠标离开事件处理
    /// </summary>
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);

        IsHighlighted = false;

        var args = new RoutedEventArgs(ConnectionMouseLeaveEvent, this);
        RaiseEvent(args);
    }

    /// <summary>
    /// 重写键盘按下事件处理
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Delete && IsSelected)
        {
            // Delete键删除连接
            var args = new RoutedEventArgs(ConnectionDeleteRequestedEvent, this);
            RaiseEvent(args);
            e.Handled = true;
        }
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 检查点是否在连接线附近
    /// </summary>
    /// <param name="point">要检查的点</param>
    /// <param name="tolerance">容差范围</param>
    /// <returns>如果点在连接线附近返回true</returns>
    public bool IsPointNearConnection(Point point, double tolerance = 5.0)
    {
        if (PathData == null)
            return false;

        // 使用WPF的几何图形命中测试
        var pen = new Pen(Brushes.Transparent, tolerance * 2);
        var geometry = PathData.GetWidenedPathGeometry(pen);
        return geometry.FillContains(point);
    }

    /// <summary>
    /// 获取连接线的边界矩形
    /// </summary>
    /// <returns>边界矩形</returns>
    public Rect GetBounds()
    {
        if (PathData == null)
            return Rect.Empty;

        return PathData.Bounds;
    }

    /// <summary>
    /// 获取连接线的渲染边界矩形（包含线条粗细）
    /// </summary>
    /// <returns>渲染边界矩形</returns>
    public Rect GetRenderBounds()
    {
        if (PathData == null)
            return Rect.Empty;

        var pen = new Pen(Brushes.Black, StrokeThickness);
        return PathData.GetRenderBounds(pen);
    }

    #endregion
}
