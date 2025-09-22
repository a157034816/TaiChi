using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TaiChi.Wpf.NodeEditor.Core.Config;
using TaiChi.Wpf.NodeEditor.Core.Models;
using TaiChi.Wpf.NodeEditor.Controls.Helpers;
using TaiChi.Wpf.NodeEditor.Controls.AttachedProperties;
using VisualTreeHelper = System.Windows.Media.VisualTreeHelper;

namespace TaiChi.Wpf.NodeEditor.Controls;

/// <summary>
/// 节点控件 - 重构为 Control 以实现完全解耦
/// </summary>
public class NodeControl : Control
{
    #region 依赖属性

    /// <summary>
    /// 节点数据模型
    /// </summary>
    public static readonly DependencyProperty NodeDataProperty =
        DependencyProperty.Register(nameof(NodeData), typeof(Node), typeof(NodeControl),
            new PropertyMetadata(null, OnNodeDataChanged));

    /// <summary>
    /// 节点位置
    /// </summary>
    public static readonly DependencyProperty PositionProperty =
        DependencyProperty.Register(nameof(Position), typeof(Point), typeof(NodeControl),
            new PropertyMetadata(new Point(), OnPositionChanged));

    /// <summary>
    /// 是否选中
    /// </summary>
    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(NodeControl),
            new PropertyMetadata(false));

    /// <summary>
    /// 是否高亮
    /// </summary>
    public static readonly DependencyProperty IsHighlightedProperty =
        DependencyProperty.Register(nameof(IsHighlighted), typeof(bool), typeof(NodeControl),
            new PropertyMetadata(false));

    /// <summary>
    /// 是否正在拖拽
    /// </summary>
    public static readonly DependencyProperty IsDraggingProperty =
        DependencyProperty.Register(nameof(IsDragging), typeof(bool), typeof(NodeControl),
            new PropertyMetadata(false));

    /// <summary>
    /// 节点内容
    /// </summary>
    public static readonly DependencyProperty ContentProperty =
        DependencyProperty.Register(nameof(Content), typeof(object), typeof(NodeControl),
            new PropertyMetadata(null));

    /// <summary>
    /// 节点内容模板
    /// </summary>
    public static readonly DependencyProperty NodeContentTemplateProperty =
        DependencyProperty.Register(nameof(NodeContentTemplate), typeof(DataTemplate), typeof(NodeControl),
            new PropertyMetadata(null));

    /// <summary>
    /// 节点背景画刷
    /// </summary>
    public static readonly DependencyProperty NodeBackgroundProperty =
        DependencyProperty.Register(nameof(NodeBackground), typeof(Brush), typeof(NodeControl),
            new PropertyMetadata(Brushes.White));

    /// <summary>
    /// 节点边框画刷
    /// </summary>
    public static readonly DependencyProperty NodeBorderBrushProperty =
        DependencyProperty.Register(nameof(NodeBorderBrush), typeof(Brush), typeof(NodeControl),
            new PropertyMetadata(Brushes.Gray));

    /// <summary>
    /// 节点边框厚度
    /// </summary>
    public static readonly DependencyProperty NodeBorderThicknessProperty =
        DependencyProperty.Register(nameof(NodeBorderThickness), typeof(Thickness), typeof(NodeControl),
            new PropertyMetadata(new Thickness(1)));

    /// <summary>
    /// 节点是否可拖拽
    /// </summary>
    public static readonly DependencyProperty IsDraggableProperty =
        DependencyProperty.Register(nameof(IsDraggable), typeof(bool), typeof(NodeControl),
            new PropertyMetadata(true));

    /// <summary>
    /// 节点是否可选择
    /// </summary>
    public static readonly DependencyProperty IsSelectableProperty =
        DependencyProperty.Register(nameof(IsSelectable), typeof(bool), typeof(NodeControl),
            new PropertyMetadata(true));

    /// <summary>
    /// 是否启用拖拽硬件加速
    /// </summary>
    public static readonly DependencyProperty EnableDragHardwareAccelerationProperty =
        DependencyProperty.Register(nameof(EnableDragHardwareAcceleration), typeof(bool), typeof(NodeControl),
            new PropertyMetadata(true));

    /// <summary>
    /// 是否启用拖拽时简化渲染
    /// </summary>
    public static readonly DependencyProperty EnableDragSimplifiedRenderingProperty =
        DependencyProperty.Register(nameof(EnableDragSimplifiedRendering), typeof(bool), typeof(NodeControl),
            new PropertyMetadata(true));

    #endregion
        // 将控件实测尺寸回写给 ViewModel 的代理属性，便于 VM 计算精确的引脚中心
        public static readonly DependencyProperty NodeWidthProxyProperty =
            DependencyProperty.Register(nameof(NodeWidthProxy), typeof(double), typeof(NodeControl),
                new PropertyMetadata(0.0));

        public static readonly DependencyProperty NodeHeightProxyProperty =
            DependencyProperty.Register(nameof(NodeHeightProxy), typeof(double), typeof(NodeControl),
                new PropertyMetadata(0.0));


    #region 属性包装器

    /// <summary>
    /// 节点数据模型
    /// </summary>
    public Node NodeData
    {
        get => (Node)GetValue(NodeDataProperty);
        set => SetValue(NodeDataProperty, value);
    }

    /// <summary>
    /// 节点位置
    /// </summary>
    public Point Position
    {
        get => (Point)GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
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
        public double NodeWidthProxy
        {
            get => (double)GetValue(NodeWidthProxyProperty);
            set => SetValue(NodeWidthProxyProperty, value);
        }

        public double NodeHeightProxy
        {
            get => (double)GetValue(NodeHeightProxyProperty);
            set => SetValue(NodeHeightProxyProperty, value);
        }

    /// 是否高亮
    /// </summary>
    public bool IsHighlighted
    {
        get => (bool)GetValue(IsHighlightedProperty);
        set => SetValue(IsHighlightedProperty, value);
    }

    /// <summary>
    /// 是否正在拖拽
    /// </summary>
    public bool IsDragging
    {
        get => (bool)GetValue(IsDraggingProperty);
        set => SetValue(IsDraggingProperty, value);
    }

    /// <summary>
    /// 节点内容
    /// </summary>
    public object Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <summary>
    /// 节点内容模板
    /// </summary>
    public DataTemplate NodeContentTemplate
    {
        get => (DataTemplate)GetValue(NodeContentTemplateProperty);
        set => SetValue(NodeContentTemplateProperty, value);
    }

    /// <summary>
    /// 节点背景画刷
    /// </summary>
    public Brush NodeBackground
    {
        get => (Brush)GetValue(NodeBackgroundProperty);
        set => SetValue(NodeBackgroundProperty, value);
    }

    /// <summary>
    /// 节点边框画刷
    /// </summary>
    public Brush NodeBorderBrush
    {
        get => (Brush)GetValue(NodeBorderBrushProperty);
        set => SetValue(NodeBorderBrushProperty, value);
    }

    /// <summary>
    /// 节点边框厚度
    /// </summary>
    public Thickness NodeBorderThickness
    {
        get => (Thickness)GetValue(NodeBorderThicknessProperty);
        set => SetValue(NodeBorderThicknessProperty, value);
    }

    /// <summary>
    /// 节点是否可拖拽
    /// </summary>
    public bool IsDraggable
    {
        get => (bool)GetValue(IsDraggableProperty);
        set => SetValue(IsDraggableProperty, value);
    }

    /// <summary>
    /// 节点是否可选择
    /// </summary>
    public bool IsSelectable
    {
        get => (bool)GetValue(IsSelectableProperty);
        set => SetValue(IsSelectableProperty, value);
    }

    /// <summary>
    /// 是否启用拖拽硬件加速
    /// </summary>
    public bool EnableDragHardwareAcceleration
    {
        get => (bool)GetValue(EnableDragHardwareAccelerationProperty);
        set => SetValue(EnableDragHardwareAccelerationProperty, value);
    }

    /// <summary>
    /// 是否启用拖拽时简化渲染
    /// </summary>
    public bool EnableDragSimplifiedRendering
    {
        get => (bool)GetValue(EnableDragSimplifiedRenderingProperty);
        set => SetValue(EnableDragSimplifiedRenderingProperty, value);
    }

    #endregion

    #region 依赖属性回调

    private static void OnNodeDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NodeControl control && e.NewValue is Node node)
        {
            // 当节点数据变化时，更新位置（防止循环绑定）
            if (!control._isUpdatingPosition)
            {
                control.Position = new Point(node.Position.X, node.Position.Y);
            }
        }
    }

    private static void OnPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NodeControl control)
        {
            var position = (Point)e.NewValue;

            // 使用CanvasExtensions设置位置
            CanvasExtensions.SetPosition(control, position);

            // 通知引脚更新连接器位置
            control.UpdatePinConnectorPositions();
        }
    }

    #endregion

    #region 路由事件

    /// <summary>
    /// 节点点击事件
    /// </summary>
    public static readonly RoutedEvent NodeClickEvent = EventManager.RegisterRoutedEvent(
        "NodeClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(NodeControl));

    /// <summary>
    /// 节点双击事件
    /// </summary>
    public static readonly RoutedEvent NodeDoubleClickEvent = EventManager.RegisterRoutedEvent(
        "NodeDoubleClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(NodeControl));

    /// <summary>
    /// 节点拖拽开始事件
    /// </summary>
    public static readonly RoutedEvent NodeDragStartedEvent = EventManager.RegisterRoutedEvent(
        "NodeDragStarted", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(NodeControl));

    /// <summary>
    /// 节点拖拽完成事件
    /// </summary>
    public static readonly RoutedEvent NodeDragCompletedEvent = EventManager.RegisterRoutedEvent(
        "NodeDragCompleted", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(NodeControl));

    /// <summary>
    /// 节点拖拽中事件
    /// </summary>
    public static readonly RoutedEvent NodeDragDeltaEvent = EventManager.RegisterRoutedEvent(
        "NodeDragDelta", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(NodeControl));

    /// <summary>
    /// 节点选择状态变化事件
    /// </summary>
    public static readonly RoutedEvent NodeSelectionChangedEvent = EventManager.RegisterRoutedEvent(
        "NodeSelectionChanged", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(NodeControl));




    /// <summary>
    /// 节点选择变化事件
    /// </summary>
    public static readonly RoutedEvent SelectionChangedEvent = EventManager.RegisterRoutedEvent(
        "SelectionChanged", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(NodeControl));

    /// <summary>
    /// 引脚连接器点击事件
    /// </summary>
    public static readonly RoutedEvent PinConnectorClickEvent = EventManager.RegisterRoutedEvent(
        "PinConnectorClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(NodeControl));

    #endregion

    #region 事件包装器

    /// <summary>
    /// 节点点击事件
    /// </summary>
    public event RoutedEventHandler NodeClick
    {
        add { AddHandler(NodeClickEvent, value); }
        remove { RemoveHandler(NodeClickEvent, value); }
    }

    /// <summary>
    /// 节点双击事件
    /// </summary>
    public event RoutedEventHandler NodeDoubleClick
    {
        add { AddHandler(NodeDoubleClickEvent, value); }
        remove { RemoveHandler(NodeDoubleClickEvent, value); }
    }

    /// <summary>
    /// 节点拖拽开始事件
    /// </summary>
    public event RoutedEventHandler NodeDragStarted
    {
        add { AddHandler(NodeDragStartedEvent, value); }
        remove { RemoveHandler(NodeDragStartedEvent, value); }
    }

    /// <summary>
    /// 节点拖拽中事件
    /// </summary>
    public event RoutedEventHandler NodeDragDelta
    {
        add { AddHandler(NodeDragDeltaEvent, value); }
        remove { RemoveHandler(NodeDragDeltaEvent, value); }
    }

    /// <summary>
    /// 节点拖拽完成事件
    /// </summary>
    public event RoutedEventHandler NodeDragCompleted
    {
        add { AddHandler(NodeDragCompletedEvent, value); }
        remove { RemoveHandler(NodeDragCompletedEvent, value); }
    }

    /// <summary>
    /// 节点选择变化事件
    /// </summary>
    public event RoutedEventHandler SelectionChanged
    {
        add { AddHandler(SelectionChangedEvent, value); }
        remove { RemoveHandler(SelectionChangedEvent, value); }
    }

    /// <summary>
    /// 引脚连接器点击事件
    /// </summary>
    public event RoutedEventHandler PinConnectorClick
    {
        add { AddHandler(PinConnectorClickEvent, value); }
        remove { RemoveHandler(PinConnectorClickEvent, value); }
    }

    #endregion

    #region 私有字段

    /// <summary>
    /// 拖拽开始位置
    /// </summary>
    private Point _dragStartPoint;

    /// <summary>
    /// 拖拽开始时的节点位置
    /// </summary>
    private Point _dragStartNodePosition;

    /// <summary>
    /// 是否已经开始拖拽
    /// </summary>
    private bool _isDragStarted;

    /// <summary>
    /// 鼠标相对节点左上角的偏移（父Canvas坐标系）
    /// </summary>
    private Point _dragOffsetInCanvas;

    /// <summary>
    /// 是否正在更新位置（防止循环绑定）
    /// </summary>
    private bool _isUpdatingPosition;

    /// <summary>
    /// 父级Canvas缓存引用
    /// </summary>
    private Canvas? _parentCanvasCache;

    /// <summary>
    /// NodeCanvas缓存引用
    /// </summary>
    private NodeCanvas? _nodeCanvasCache;

    /// <summary>
    /// 上次事件处理时间戳
    /// </summary>
    private DateTime _lastMouseMoveTime;

    /// <summary>
    /// 拖拽时的原始背景画刷
    /// </summary>
    private Brush? _originalBackground;

    /// <summary>
    /// 拖拽时的原始边框画刷
    /// </summary>
    private Brush? _originalBorderBrush;

    /// <summary>
    /// 拖拽时的原始边框厚度
    /// </summary>
    private Thickness _originalBorderThickness;

    /// <summary>
    /// 是否已启用硬件加速缓存
    /// </summary>
    private bool _isHardwareAccelerated;

    /// <summary>
    /// 临时位置（用于拖拽过程中的优化）
    /// </summary>
    private Point _tempPosition;

    /// <summary>
    /// 性能监控计时器
    /// </summary>
    private System.Diagnostics.Stopwatch? _performanceTimer;

    /// <summary>
    /// 拖拽性能统计
    /// </summary>
    private class DragPerformanceStats
    {
        public int FrameCount { get; set; }
        public double TotalFrameTime { get; set; }
        public double AverageFrameTime => FrameCount > 0 ? TotalFrameTime / FrameCount : 0;
        public double MaxFrameTime { get; set; }
        public double MinFrameTime { get; set; } = double.MaxValue;
    }

    /// <summary>
    /// 当前拖拽会话的性能统计
    /// </summary>
    private DragPerformanceStats? _currentDragStats;

    #endregion

    #region 构造函数和静态构造函数

    /// <summary>
    /// 静态构造函数
    /// </summary>
    static NodeControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(NodeControl),
            new FrameworkPropertyMetadata(typeof(NodeControl)));
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    public NodeControl()
    {
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
        
        // 初始化性能优化字段
        _lastMouseMoveTime = DateTime.Now;
        _tempPosition = new Point();
        
        // 初始化性能监控
        _performanceTimer = new System.Diagnostics.Stopwatch();
    }

    #endregion

    #region 事件处理

    /// <summary>
    /// 控件加载完成事件处理
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 回写实测尺寸给 VM
        NodeWidthProxy = ActualWidth;
        NodeHeightProxy = ActualHeight;
        
        // 初始化缓存引用
        InitializeCacheReferences();
        
        UpdatePinConnectorPositions();
    }

    /// <summary>
    /// 控件大小变化事件处理
    /// </summary>
    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // 回写实测尺寸给 VM
        NodeWidthProxy = ActualWidth;
        NodeHeightProxy = ActualHeight;
        UpdatePinConnectorPositions();
    }

    /// <summary>
    /// 重写鼠标按下事件处理
    /// </summary>
    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.LeftButton == MouseButtonState.Pressed && NodeData != null)
        {
            if (e.ClickCount == 1)
            {
                // 记录拖拽开始位置
                _dragStartPoint = e.GetPosition(this);
                _dragStartNodePosition = Position;
                _isDragStarted = false;

                // 预计算鼠标相对节点左上角的偏移（父Canvas逻辑坐标系：已去除缩放/平移）
                var canvas = _parentCanvasCache ?? FindParentCanvas();
                if (canvas != null)
                {
                    _parentCanvasCache = canvas; // 缓存引用
                    var mouseOnCanvas = e.GetPosition(canvas);
                    var logicalMouse = ToCanvasLogical(canvas, mouseOnCanvas);
                    var nodeStart = NodeData != null
                        ? new Point(NodeData.Position.X, NodeData.Position.Y)
                        : Position;
                    _dragOffsetInCanvas = new Point(logicalMouse.X - nodeStart.X, logicalMouse.Y - nodeStart.Y);
                }
                else
                {
                    _dragOffsetInCanvas = _dragStartPoint; // 兜底
                }

                // 触发节点点击事件
                var clickArgs = new RoutedEventArgs(NodeClickEvent, this);
                RaiseEvent(clickArgs);

                // 捕获鼠标以便处理拖拽
                Mouse.Capture(this);
                e.Handled = true;
            }
            else if (e.ClickCount == 2)
            {
                // 双击事件
                var doubleClickArgs = new RoutedEventArgs(NodeDoubleClickEvent, this);
                RaiseEvent(doubleClickArgs);
                e.Handled = true;
            }
        }
    }

    /// <summary>
    /// 重写鼠标移动事件处理
    /// </summary>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed && NodeData != null)
        {
            // 事件节流：限制处理频率为约60fps（16ms间隔）
            var currentTime = DateTime.Now;
            var elapsedSinceLastMove = (currentTime - _lastMouseMoveTime).TotalMilliseconds;
            
            if (elapsedSinceLastMove < 16 && _isDragStarted)
            {
                e.Handled = true;
                return;
            }

            var currentPoint = e.GetPosition(this);
            var deltaVector = currentPoint - _dragStartPoint;

            // 检查是否开始拖拽（移动距离超过阈值）
            if (!_isDragStarted && (Math.Abs(deltaVector.X) > 3 || Math.Abs(deltaVector.Y) > 3))
            {
                _isDragStarted = true;
                IsDragging = true;

                // 开始性能监控
                StartPerformanceMonitoring();

                // 应用渲染层优化
                ApplyDragOptimizations();

                // 触发拖拽开始事件
                var dragStartArgs = new RoutedEventArgs(NodeDragStartedEvent, this);
                RaiseEvent(dragStartArgs);
            }

            if (_isDragStarted)
            {
                // 使用缓存的Canvas引用
                var canvas = _parentCanvasCache;
                if (canvas != null)
                {
                    var mouseCanvas = e.GetPosition(canvas);
                    var newPosition = new Point(
                        mouseCanvas.X - _dragOffsetInCanvas.X,
                        mouseCanvas.Y - _dragOffsetInCanvas.Y);

                    // 更新时间戳
                    _lastMouseMoveTime = currentTime;

                    // 性能监控：记录帧时间
                    RecordFramePerformance();

                    // 数据层优化：使用临时位置，延迟数据模型更新
                    _tempPosition = newPosition;
                    
                    // 只在位置变化超过阈值时更新底层数据模型，减少频繁更新
                    if (NodeData != null)
                    {
                        var positionDelta = Math.Abs(newPosition.X - NodeData.Position.X) + 
                                           Math.Abs(newPosition.Y - NodeData.Position.Y);
                        
                        // 只有位置变化超过1像素时才更新数据模型
                        if (positionDelta > 1.0)
                        {
                            NodeData.Position = new NodeEditorPoint(newPosition.X, newPosition.Y);
                        }
                    }
                }

                // 触发拖拽中事件
                var dragDeltaArgs = new RoutedEventArgs(NodeDragDeltaEvent, this);
                RaiseEvent(dragDeltaArgs);
            }

            e.Handled = true;
        }
    }

    /// <summary>
    /// 重写鼠标释放事件处理
    /// </summary>
    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);

        if (IsMouseCaptured)
        {
            if (_isDragStarted)
            {
                IsDragging = false;

                // 数据层优化：拖拽结束时强制同步位置
                if (NodeData != null && 
                    (Math.Abs(_tempPosition.X - NodeData.Position.X) > 0.1 || 
                     Math.Abs(_tempPosition.Y - NodeData.Position.Y) > 0.1))
                {
                    NodeData.Position = new NodeEditorPoint(_tempPosition.X, _tempPosition.Y);
                }

                // 结束性能监控并输出统计信息
                EndPerformanceMonitoring();

                // 移除拖拽优化，恢复原始渲染状态
                RemoveDragOptimizations();

                // 触发拖拽完成事件
                var dragCompletedArgs = new RoutedEventArgs(NodeDragCompletedEvent, this);
                RaiseEvent(dragCompletedArgs);
            }

            // 释放鼠标捕获
            Mouse.Capture(null);
            _isDragStarted = false;
            e.Handled = true;
        }
    }

    /// <summary>
    /// 重写鼠标进入事件处理
    /// </summary>
    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);

        if (NodeData != null)
        {
            IsHighlighted = true;
        }
    }

    /// <summary>
    /// 重写鼠标离开事件处理
    /// </summary>
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);

        if (NodeData != null)
        {
            IsHighlighted = false;
        }
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 向上查找父级的主画布（PART_MainCanvas）
    /// </summary>
    private Canvas? FindParentCanvas()
    {
        return Helpers.VisualTreeHelper.FindParent<Canvas>(this);
    }

    /// <summary>
    /// 向上查找所属的 NodeCanvas 控件
    /// </summary>
    private NodeCanvas? FindNodeCanvas()
    {
        return Helpers.VisualTreeHelper.FindParent<NodeCanvas>(this);
    }

    /// <summary>
    /// 将 Canvas 的鼠标坐标转换为逻辑坐标（去除缩放和平移）
    /// device = Scale * logical + Translate => logical = (device - Translate) / Scale
    /// </summary>
    private Point ToCanvasLogical(Canvas canvas, Point canvasPoint)
    {
        // 使用缓存的NodeCanvas引用
        var nodeCanvas = _nodeCanvasCache ?? FindNodeCanvas();
        if (nodeCanvas == null)
            return canvasPoint;

        // 缓存NodeCanvas引用
        if (_nodeCanvasCache == null)
        {
            _nodeCanvasCache = nodeCanvas;
        }

        var zoom = nodeCanvas.ZoomLevel <= 0 ? 1.0 : nodeCanvas.ZoomLevel;
        var pan = nodeCanvas.PanOffset;
        return new Point((canvasPoint.X - pan.X) / zoom, (canvasPoint.Y - pan.Y) / zoom);
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 获取节点在画布上的位置
    /// </summary>
    /// <returns>节点位置</returns>
    public Point GetCanvasPosition()
    {
        return Position;
    }

    /// <summary>
    /// 设置节点在画布上的位置
    /// </summary>
    /// <param name="position">新位置</param>
    public void SetCanvasPosition(Point position)
    {
        Position = position;
    }

    /// <summary>
    /// 获取所有引脚控件
    /// </summary>
    /// <returns>引脚控件列表</returns>
    public IEnumerable<PinControl> GetPinControls()
    {
        var pinControls = new List<PinControl>();

        // 查找模板中的引脚控件
        if (Template != null)
        {
            // 递归查找所有PinControl
            FindPinControlsRecursive(this, pinControls);
        }

        return pinControls;
    }

    /// <summary>
    /// 递归查找PinControl
    /// </summary>
    private static void FindPinControlsRecursive(DependencyObject parent, List<PinControl> pinControls)
    {
        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is PinControl pinControl)
            {
                pinControls.Add(pinControl);
            }
            else
            {
                FindPinControlsRecursive(child, pinControls);
            }
        }
    }

    /// <summary>
    /// 更新所有引脚的连接器位置
    /// </summary>
    public void UpdatePinConnectorPositions()
    {
        if (NodeData == null) return;

        // 使用优化的批量更新方法
        UpdatePinConnectorPositionsOptimized();
    }

    /// <summary>
    /// 获取指定引脚的连接器位置
    /// </summary>
    /// <param name="pin">引脚数据</param>
    /// <returns>连接器位置</returns>
    public Point GetPinConnectorPosition(Pin pin)
    {
        var pinControls = GetPinControls();
        var pinControl = pinControls.FirstOrDefault(pc => pc.PinData?.Id == pin.Id);

        return pinControl?.GetConnectorPosition() ?? new Point();
    }

    #endregion

    #region 性能优化辅助方法

    /// <summary>
    /// 初始化缓存引用
    /// </summary>
    private void InitializeCacheReferences()
    {
        try
        {
            _parentCanvasCache = FindParentCanvas();
            _nodeCanvasCache = FindNodeCanvas();
        }
        catch (Exception)
        {
            // 忽略初始化异常，运行时动态查找
        }
    }

    /// <summary>
    /// 应用拖拽优化
    /// </summary>
    private void ApplyDragOptimizations()
    {
        if (!EnableDragHardwareAcceleration && !EnableDragSimplifiedRendering)
            return;

        try
        {
            // 硬件加速优化
            if (EnableDragHardwareAcceleration && !_isHardwareAccelerated)
            {
                // 启用BitmapCache
                var bitmapCache = new BitmapCache
                {
                    RenderAtScale = 1.0,
                    EnableClearType = true,
                    SnapsToDevicePixels = true
                };
                
                CacheMode = bitmapCache;
                _isHardwareAccelerated = true;
            }

            // 简化渲染优化
            if (EnableDragSimplifiedRendering)
            {
                // 保存原始值
                _originalBackground = NodeBackground;
                _originalBorderBrush = NodeBorderBrush;
                _originalBorderThickness = NodeBorderThickness;

                // 应用简化样式
                NodeBackground = new SolidColorBrush(Color.FromRgb(240, 240, 240));
                NodeBorderBrush = new SolidColorBrush(Color.FromRgb(180, 180, 180));
                NodeBorderThickness = new Thickness(1);

                // 降低不透明度以提高性能
                Opacity = 0.9;
            }
        }
        catch (Exception)
        {
            // 忽略优化过程中的异常
        }
    }

    /// <summary>
    /// 移除拖拽优化，恢复原始状态
    /// </summary>
    private void RemoveDragOptimizations()
    {
        try
        {
            // 移除硬件加速
            if (_isHardwareAccelerated)
            {
                CacheMode = null;
                _isHardwareAccelerated = false;
            }

            // 恢复原始渲染状态
            if (EnableDragSimplifiedRendering && _originalBackground != null)
            {
                NodeBackground = _originalBackground;
                NodeBorderBrush = _originalBorderBrush;
                NodeBorderThickness = _originalBorderThickness;
                Opacity = 1.0;

                // 清理临时引用
                _originalBackground = null;
                _originalBorderBrush = null;
            }
        }
        catch (Exception)
        {
            // 忽略清理过程中的异常
        }
    }

    /// <summary>
    /// 优化的坐标转换方法
    /// </summary>
    private Point ToCanvasLogicalOptimized(Point canvasPoint)
    {
        if (_nodeCanvasCache == null)
            return canvasPoint;

        var zoom = _nodeCanvasCache.ZoomLevel <= 0 ? 1.0 : _nodeCanvasCache.ZoomLevel;
        var pan = _nodeCanvasCache.PanOffset;
        
        return new Point(
            (canvasPoint.X - pan.X) / zoom,
            (canvasPoint.Y - pan.Y) / zoom);
    }

    /// <summary>
    /// 批量更新引脚位置（优化版）
    /// </summary>
    private void UpdatePinConnectorPositionsOptimized()
    {
        if (NodeData == null) return;

        // 如果正在拖拽，延迟引脚位置更新
        if (_isDragStarted)
        {
            // 使用更低的优先级，避免与拖拽操作竞争
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var pinControls = GetPinControls();
                foreach (var pinControl in pinControls)
                {
                    pinControl.UpdateConnectorPosition();
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
        else
        {
            // 正常情况使用标准优先级
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var pinControls = GetPinControls();
                foreach (var pinControl in pinControls)
                {
                    pinControl.UpdateConnectorPosition();
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    /// <summary>
    /// 清理缓存资源
    /// </summary>
    private void CleanupCacheResources()
    {
        _parentCanvasCache = null;
        _nodeCanvasCache = null;
        _originalBackground = null;
        _originalBorderBrush = null;
    }

    /// <summary>
    /// 批量位置更新器（用于多节点拖拽优化）
    /// </summary>
    private class BatchPositionUpdater
    {
        private readonly List<Action> _pendingUpdates = new();
        private bool _isBatchActive = false;

        /// <summary>
        /// 开始批量更新
        /// </summary>
        public void BeginBatch()
        {
            _isBatchActive = true;
            _pendingUpdates.Clear();
        }

        /// <summary>
        /// 添加位置更新操作
        /// </summary>
        public void AddUpdate(Node nodeData, Point newPosition)
        {
            if (_isBatchActive && nodeData != null)
            {
                _pendingUpdates.Add(() =>
                {
                    nodeData.Position = new NodeEditorPoint(newPosition.X, newPosition.Y);
                });
            }
        }

        /// <summary>
        /// 提交批量更新
        /// </summary>
        public void CommitBatch()
        {
            if (_isBatchActive && _pendingUpdates.Count > 0)
            {
                // 批量执行所有更新操作
                foreach (var update in _pendingUpdates)
                {
                    update();
                }
                
                _pendingUpdates.Clear();
            }
            _isBatchActive = false;
        }
    }

    /// <summary>
    /// 智能位置更新（根据变化幅度决定是否立即更新）
    /// </summary>
    private void UpdatePositionSmart(Point newPosition)
    {
        if (NodeData == null) return;

        var currentPos = new Point(NodeData.Position.X, NodeData.Position.Y);
        var delta = Math.Abs(newPosition.X - currentPos.X) + Math.Abs(newPosition.Y - currentPos.Y);

        // 如果变化幅度大，立即更新；否则延迟更新
        if (delta > 5.0)
        {
            NodeData.Position = new NodeEditorPoint(newPosition.X, newPosition.Y);
        }
        else
        {
            // 延迟小幅度更新，积累到下次大变化或拖拽结束
            _tempPosition = newPosition;
            
            // 设置一个延迟更新任务
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (NodeData != null && 
                    (Math.Abs(_tempPosition.X - NodeData.Position.X) > 0.5 || 
                     Math.Abs(_tempPosition.Y - NodeData.Position.Y) > 0.5))
                {
                    NodeData.Position = new NodeEditorPoint(_tempPosition.X, _tempPosition.Y);
                }
            }), System.Windows.Threading.DispatcherPriority.Input, new CancellationTokenSource().Token, TimeSpan.FromMilliseconds(50));
        }
    }

    /// <summary>
    /// 开始性能监控
    /// </summary>
    private void StartPerformanceMonitoring()
    {
        // 检查是否启用性能监控
        if (!NodeEditorConfig.EnablePerformanceMonitoring) return;
        
        try
        {
            _currentDragStats = new DragPerformanceStats();
            if (_performanceTimer != null)
            {
                _performanceTimer.Reset();
                _performanceTimer.Start();
            }
        }
        catch (Exception)
        {
            // 忽略性能监控异常
        }
    }

    /// <summary>
    /// 记录帧性能
    /// </summary>
    private void RecordFramePerformance()
    {
        // 检查是否启用性能监控
        if (!NodeEditorConfig.EnablePerformanceMonitoring) return;
        
        try
        {
            if (_currentDragStats != null && _performanceTimer != null)
            {
                var frameTime = _performanceTimer.Elapsed.TotalMilliseconds;
                
                _currentDragStats.FrameCount++;
                _currentDragStats.TotalFrameTime += frameTime;
                _currentDragStats.MaxFrameTime = Math.Max(_currentDragStats.MaxFrameTime, frameTime);
                _currentDragStats.MinFrameTime = Math.Min(_currentDragStats.MinFrameTime, frameTime);
                
                _performanceTimer.Restart();
            }
        }
        catch (Exception)
        {
            // 忽略性能记录异常
        }
    }

    /// <summary>
    /// 结束性能监控并输出统计信息
    /// </summary>
    private void EndPerformanceMonitoring()
    {
        // 检查是否启用性能监控
        if (!NodeEditorConfig.EnablePerformanceMonitoring) return;
        
        try
        {
            if (_currentDragStats != null && _performanceTimer != null)
            {
                _performanceTimer.Stop();
                
                // 输出性能统计信息到调试输出
                System.Diagnostics.Debug.WriteLine($"=== Node拖拽性能统计 ===");
                System.Diagnostics.Debug.WriteLine($"总帧数: {_currentDragStats.FrameCount}");
                System.Diagnostics.Debug.WriteLine($"平均帧时间: {_currentDragStats.AverageFrameTime:F2}ms");
                System.Diagnostics.Debug.WriteLine($"最大帧时间: {_currentDragStats.MaxFrameTime:F2}ms");
                System.Diagnostics.Debug.WriteLine($"最小帧时间: {_currentDragStats.MinFrameTime:F2}ms");
                System.Diagnostics.Debug.WriteLine($"总拖拽时间: {_currentDragStats.TotalFrameTime:F2}ms");
                System.Diagnostics.Debug.WriteLine($"理论FPS: {1000.0 / _currentDragStats.AverageFrameTime:F1}");
                System.Diagnostics.Debug.WriteLine($"==========================");
                
                _currentDragStats = null;
            }
        }
        catch (Exception)
        {
            // 忽略性能监控异常
        }
    }

    /// <summary>
    /// 获取当前性能统计信息（用于外部监控）
    /// </summary>
    /// <returns>性能统计信息字符串</returns>
    public string GetPerformanceStats()
    {
        // 检查是否启用性能监控
        if (!NodeEditorConfig.EnablePerformanceMonitoring)
        {
            return "性能监控已禁用";
        }
        
        if (_currentDragStats == null)
        {
            return "当前没有活跃的拖拽操作";
        }
        
        return $"帧数: {_currentDragStats.FrameCount}, " +
               $"平均帧时间: {_currentDragStats.AverageFrameTime:F2}ms, " +
               $"FPS: {1000.0 / _currentDragStats.AverageFrameTime:F1}";
    }

    /// <summary>
    /// 运行性能基准测试
    /// </summary>
    /// <param name="testDurationSeconds">测试持续时间（秒）</param>
    /// <returns>测试结果</returns>
    public string RunPerformanceBenchmark(int testDurationSeconds = 5)
    {
        // 检查是否启用性能监控
        if (!NodeEditorConfig.EnablePerformanceMonitoring)
        {
            return "性能基准测试已禁用";
        }
        
        try
        {
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            var frameCount = 0;
            var lastFrameTime = DateTime.Now;
            
            // 模拟拖拽操作的性能测试
            var startTime = DateTime.Now;
            while ((DateTime.Now - startTime).TotalSeconds < testDurationSeconds)
            {
                // 模拟频繁的坐标转换和位置更新
                var testPoint = new Point(
                    Math.Sin(DateTime.Now.Ticks * 0.001) * 100,
                    Math.Cos(DateTime.Now.Ticks * 0.001) * 100);
                
                // 执行坐标转换
                var logicalPoint = ToCanvasLogicalOptimized(testPoint);
                
                // 执行位置更新
                _tempPosition = logicalPoint;
                
                frameCount++;
                lastFrameTime = DateTime.Now;
                
                // 避免CPU占用过高
                Thread.Sleep(1);
            }
            
            stopwatch.Stop();
            var totalTime = stopwatch.Elapsed.TotalSeconds;
            var averageFPS = frameCount / totalTime;
            
            return $"性能基准测试完成:\n" +
                   $"测试持续时间: {testDurationSeconds}秒\n" +
                   $"总帧数: {frameCount}\n" +
                   $"平均FPS: {averageFPS:F1}\n" +
                   $"每帧平均时间: {1000.0 / averageFPS:F2}ms\n" +
                   $"坐标转换次数: {frameCount}";
        }
        catch (Exception ex)
        {
            return $"性能基准测试失败: {ex.Message}";
        }
    }

    #endregion
}
