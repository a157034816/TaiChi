using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using TaiChi.Wpf.NodeEditor.Core.Registry;
using TaiChi.Wpf.NodeEditor.Core.Models;
using TaiChi.Wpf.NodeEditor.Controls.Helpers;
using TaiChi.Wpf.NodeEditor.Controls.AttachedProperties;
using TaiChi.Wpf.NodeEditor.Controls.Managers;
using VisualTreeHelper = System.Windows.Media.VisualTreeHelper;


namespace TaiChi.Wpf.NodeEditor.Controls;

/// <summary>
/// 节点创建请求事件参数
/// </summary>
public class NodeCreationRequestedEventArgs : RoutedEventArgs
{
    public NodeMetadata NodeMetadata { get; set; }
    public Point Position { get; set; }

    public NodeCreationRequestedEventArgs(RoutedEvent routedEvent, object source) : base(routedEvent, source)
    {
    }
}

/// <summary>
/// 节点删除请求事件参数
/// </summary>
public class NodeDeletionRequestedEventArgs : RoutedEventArgs
{
    public IEnumerable<NodeControl> NodesToDelete { get; set; }

    public NodeDeletionRequestedEventArgs(RoutedEvent routedEvent, object source) : base(routedEvent, source)
    {
    }
}

/// <summary>
/// 连接创建请求事件参数
/// </summary>
public class ConnectionCreationRequestedEventArgs : RoutedEventArgs
{
    public PinControl SourcePin { get; set; }
    public PinControl TargetPin { get; set; }

    public ConnectionCreationRequestedEventArgs(RoutedEvent routedEvent, object source) : base(routedEvent, source)
    {
    }
}

/// <summary>
/// 连接删除请求事件参数
/// </summary>
public class ConnectionDeletionRequestedEventArgs : RoutedEventArgs
{
    public IEnumerable<object> ConnectionsToDelete { get; set; }

    public ConnectionDeletionRequestedEventArgs(RoutedEvent routedEvent, object source) : base(routedEvent, source)
    {
    }
}

/// <summary>
/// 缩放变化事件参数
/// </summary>
public class ZoomChangedEventArgs : RoutedEventArgs
{
    public double ZoomLevel { get; set; }
    public Point ZoomCenter { get; set; }

    public ZoomChangedEventArgs(RoutedEvent routedEvent, object source) : base(routedEvent, source)
    {
    }
}

/// <summary>
/// 平移变化事件参数
/// </summary>
public class PanChangedEventArgs : RoutedEventArgs
{
    public Point PanOffset { get; set; }

    public PanChangedEventArgs(RoutedEvent routedEvent, object source) : base(routedEvent, source)
    {
    }
}

/// <summary>
/// 节点位置变化事件参数
/// </summary>
public class NodePositionChangedEventArgs : RoutedEventArgs
{
    public NodeControl Node { get; set; }
    public Point OldPosition { get; set; }
    public Point NewPosition { get; set; }

    public NodePositionChangedEventArgs(RoutedEvent routedEvent, object source) : base(routedEvent, source)
    {
    }
}

/// <summary>
/// 框选事件参数
/// </summary>
public class SelectionEventArgs : RoutedEventArgs
{
    public Rect SelectionRect { get; set; }
    public IEnumerable<NodeControl> SelectedNodes { get; set; }

    public SelectionEventArgs(RoutedEvent routedEvent, object source) : base(routedEvent, source)
    {
    }
}

/// <summary>
/// 键盘快捷键事件参数
/// </summary>
public class KeyboardShortcutEventArgs : RoutedEventArgs
{
    public Key Key { get; set; }
    public ModifierKeys Modifiers { get; set; }
    public string ShortcutName { get; set; }

    public KeyboardShortcutEventArgs(RoutedEvent routedEvent, object source) : base(routedEvent, source)
    {
    }
}

/// <summary>
/// 节点画布控件 - 重构为 Control 以实现完全解耦
/// </summary>
public class NodeCanvas : Control
{
    #region 依赖属性

    /// <summary>
    /// 节点数据源
    /// </summary>
    public static readonly DependencyProperty NodesSourceProperty =
        DependencyProperty.Register(nameof(NodesSource), typeof(IEnumerable), typeof(NodeCanvas),
            new PropertyMetadata(null, OnNodesSourceChanged));

    /// <summary>
    /// 连接数据源
    /// </summary>
    public static readonly DependencyProperty ConnectionsSourceProperty =
        DependencyProperty.Register(nameof(ConnectionsSource), typeof(IEnumerable), typeof(NodeCanvas),
            new PropertyMetadata(null, OnConnectionsSourceChanged));

    /// <summary>
    /// 缩放级别
    /// </summary>
    public static readonly DependencyProperty ZoomLevelProperty =
        DependencyProperty.Register(nameof(ZoomLevel), typeof(double), typeof(NodeCanvas),
            new PropertyMetadata(1.0, OnZoomLevelChanged));

    /// <summary>
    /// 平移偏移
    /// </summary>
    public static readonly DependencyProperty PanOffsetProperty =
        DependencyProperty.Register(nameof(PanOffset), typeof(Point), typeof(NodeCanvas),
            new PropertyMetadata(new Point(), OnPanOffsetChanged));

    /// <summary>
    /// 是否只读
    /// </summary>
    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(NodeCanvas),
            new PropertyMetadata(false));

    /// <summary>
    /// 是否显示网格
    /// </summary>
    public static readonly DependencyProperty ShowGridProperty =
        DependencyProperty.Register(nameof(ShowGrid), typeof(bool), typeof(NodeCanvas),
            new PropertyMetadata(true));

    /// <summary>
    /// 网格大小
    /// </summary>
    public static readonly DependencyProperty GridSizeProperty =
        DependencyProperty.Register(nameof(GridSize), typeof(double), typeof(NodeCanvas),
            new PropertyMetadata(20.0));

    /// <summary>
    /// 画布大小
    /// </summary>
    public static readonly DependencyProperty CanvasSizeProperty =
        DependencyProperty.Register(nameof(CanvasSize), typeof(Size), typeof(NodeCanvas),
            new PropertyMetadata(new Size(5000, 5000)));

    /// <summary>
    /// 是否启用对齐到网格
    /// </summary>
    public static readonly DependencyProperty SnapToGridProperty =
        DependencyProperty.Register(nameof(SnapToGrid), typeof(bool), typeof(NodeCanvas),
            new PropertyMetadata(false));

    /// <summary>
    /// 是否允许多选
    /// </summary>
    public static readonly DependencyProperty AllowMultiSelectionProperty =
        DependencyProperty.Register(nameof(AllowMultiSelection), typeof(bool), typeof(NodeCanvas),
            new PropertyMetadata(true));

    /// <summary>
    /// 分组数据源
    /// </summary>
    public static readonly DependencyProperty GroupsSourceProperty =
        DependencyProperty.Register(nameof(GroupsSource), typeof(IEnumerable), typeof(NodeCanvas),
            new PropertyMetadata(null));

    /// <summary>
    /// 分组管理器（用于执行分组相关操作，如创建/添加/移除等）
    /// </summary>
    public static readonly DependencyProperty GroupManagerProperty =
        DependencyProperty.Register(nameof(GroupManager), typeof(NodeGroupManager), typeof(NodeCanvas),
            new PropertyMetadata(null));

    /// <summary>
    /// 上下文菜单提供器（由上层应用提供）。
    /// </summary>
    public static readonly DependencyProperty ContextMenuProviderProperty =
        DependencyProperty.Register(nameof(ContextMenuProvider), typeof(INodeCanvasContextMenuProvider), typeof(NodeCanvas),
            new PropertyMetadata(null));

    #endregion

    #region 属性包装器

    /// <summary>
    /// 节点数据源
    /// </summary>
    public IEnumerable NodesSource
    {
        get => (IEnumerable)GetValue(NodesSourceProperty);
        set => SetValue(NodesSourceProperty, value);
    }

    /// <summary>
    /// 连接数据源
    /// </summary>
    public IEnumerable ConnectionsSource
    {
        get => (IEnumerable)GetValue(ConnectionsSourceProperty);
        set => SetValue(ConnectionsSourceProperty, value);
    }

    /// <summary>
    /// 缩放级别
    /// </summary>
    public double ZoomLevel
    {
        get => (double)GetValue(ZoomLevelProperty);
        set => SetValue(ZoomLevelProperty, value);
    }

    /// <summary>
    /// 平移偏移
    /// </summary>
    public Point PanOffset
    {
        get => (Point)GetValue(PanOffsetProperty);
        set => SetValue(PanOffsetProperty, value);
    }

    /// <summary>
    /// 是否只读
    /// </summary>
    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>
    /// 是否显示网格
    /// </summary>
    public bool ShowGrid
    {
        get => (bool)GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    /// <summary>
    /// 网格大小
    /// </summary>
    public double GridSize
    {
        get => (double)GetValue(GridSizeProperty);
        set => SetValue(GridSizeProperty, value);
    }

    /// <summary>
    /// 画布大小
    /// </summary>
    public Size CanvasSize
    {
        get => (Size)GetValue(CanvasSizeProperty);
        set => SetValue(CanvasSizeProperty, value);
    }

    /// <summary>
    /// 是否启用对齐到网格
    /// </summary>
    public bool SnapToGrid
    {
        get => (bool)GetValue(SnapToGridProperty);
        set => SetValue(SnapToGridProperty, value);
    }

    /// <summary>
    /// 是否允许多选
    /// </summary>
    public bool AllowMultiSelection
    {
        get => (bool)GetValue(AllowMultiSelectionProperty);
        set => SetValue(AllowMultiSelectionProperty, value);
    }

    /// <summary>
    /// 分组数据源
    /// </summary>
    public IEnumerable GroupsSource
    {
        get => (IEnumerable)GetValue(GroupsSourceProperty);
        set => SetValue(GroupsSourceProperty, value);
    }

    /// <summary>
    /// 分组管理器
    /// </summary>
    public NodeGroupManager? GroupManager
    {
        get => (NodeGroupManager?)GetValue(GroupManagerProperty);
        set => SetValue(GroupManagerProperty, value);
    }

    /// <summary>
    /// 上下文菜单提供器（由上层应用提供）。
    /// </summary>
    public INodeCanvasContextMenuProvider? ContextMenuProvider
    {
        get => (INodeCanvasContextMenuProvider?)GetValue(ContextMenuProviderProperty);
        set => SetValue(ContextMenuProviderProperty, value);
    }

    #endregion

    #region 依赖属性回调

    private static void OnNodesSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NodeCanvas canvas)
        {
            canvas.OnNodesSourceChanged();
        }
    }

    private static void OnConnectionsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NodeCanvas canvas)
        {
            canvas.OnConnectionsSourceChanged();
        }
    }

    private static void OnZoomLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NodeCanvas canvas)
        {
            canvas.UpdateZoomTransform();
        }
    }

    private static void OnPanOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NodeCanvas canvas)
        {
            canvas.UpdatePanTransform();
        }
    }

    #endregion

    #region 事件包装器

    /// <summary>
    /// 节点创建请求事件
    /// </summary>
    public event RoutedEventHandler NodeCreationRequested
    {
        add { AddHandler(NodeCreationRequestedEvent, value); }
        remove { RemoveHandler(NodeCreationRequestedEvent, value); }
    }

    /// <summary>
    /// 节点删除请求事件
    /// </summary>
    public event RoutedEventHandler NodeDeletionRequested
    {
        add { AddHandler(NodeDeletionRequestedEvent, value); }
        remove { RemoveHandler(NodeDeletionRequestedEvent, value); }
    }

    /// <summary>
    /// 连接创建请求事件
    /// </summary>
    public event RoutedEventHandler ConnectionCreationRequested
    {
        add { AddHandler(ConnectionCreationRequestedEvent, value); }
        remove { RemoveHandler(ConnectionCreationRequestedEvent, value); }
    }

    /// <summary>
    /// 连接删除请求事件
    /// </summary>
    public event RoutedEventHandler ConnectionDeletionRequested
    {
        add { AddHandler(ConnectionDeletionRequestedEvent, value); }
        remove { RemoveHandler(ConnectionDeletionRequestedEvent, value); }
    }

    /// <summary>
    /// 画布缩放变化事件
    /// </summary>
    public event RoutedEventHandler ZoomChanged
    {
        add { AddHandler(ZoomChangedEvent, value); }
        remove { RemoveHandler(ZoomChangedEvent, value); }
    }

    /// <summary>
    /// 画布平移变化事件
    /// </summary>
    public event RoutedEventHandler PanChanged
    {
        add { AddHandler(PanChangedEvent, value); }
        remove { RemoveHandler(PanChangedEvent, value); }
    }

    /// <summary>
    /// 节点位置变化事件
    /// </summary>
    public event RoutedEventHandler NodePositionChanged
    {
        add { AddHandler(NodePositionChangedEvent, value); }
        remove { RemoveHandler(NodePositionChangedEvent, value); }
    }

    /// <summary>
    /// 框选开始事件
    /// </summary>
    public event RoutedEventHandler SelectionStarted
    {
        add { AddHandler(SelectionStartedEvent, value); }
        remove { RemoveHandler(SelectionStartedEvent, value); }
    }

    /// <summary>
    /// 框选完成事件
    /// </summary>
    public event RoutedEventHandler SelectionCompleted
    {
        add { AddHandler(SelectionCompletedEvent, value); }
        remove { RemoveHandler(SelectionCompletedEvent, value); }
    }

    /// <summary>
    /// 键盘快捷键事件
    /// </summary>
    public event RoutedEventHandler KeyboardShortcut
    {
        add { AddHandler(KeyboardShortcutEvent, value); }
        remove { RemoveHandler(KeyboardShortcutEvent, value); }
    }

    #endregion

    #region 私有字段

    // 交互状态
    private bool _isPanning;
    private bool _isDraggingNode = false; // 初始化以避免警告
    private bool _isCreatingConnection;
    private bool _isSelecting;

    // 鼠标位置记录
    private Point _panStartPoint;
    private Point _dragStartPoint;
    private Point _selectionStartPoint;
    private Point _lastMousePosition;

    // 拖拽相关
    private readonly List<NodeControl> _draggingNodes = new();
    private readonly Dictionary<NodeControl, Point> _initialNodePositions = new();

    // 连接创建相关
    private PinControl? _connectionSourcePin;
    private Point _connectionCurrentPoint;
    private Path? _connectionPreviewPath;

    // 模板元素
    private Canvas? _mainCanvas;
    private ScrollViewer? _canvasScrollViewer;
    private ScaleTransform? _zoomTransform;
    private TranslateTransform? _panTransform;
    private SelectionRectangle? _selectionRectangle;
    private GridBackground? _gridBackground;

    // 最近一次弹出上下文菜单的位置（画布逻辑坐标）
    private Point? _lastContextMenuLogicalPosition;

    // 当前拖拽时的分组命中（用于Ctrl+拖拽加入分组的视觉反馈）
    private NodeGroup? _currentDropTargetGroup;

    #region 访问器

    private Canvas? MainCanvas => _mainCanvas;
    private ScrollViewer? CanvasScrollViewer => _canvasScrollViewer;

    #endregion

    #endregion

    #region 路由事件

    /// <summary>
    /// 画布点击事件
    /// </summary>
    public static readonly RoutedEvent CanvasClickEvent = EventManager.RegisterRoutedEvent(
        "CanvasClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(NodeCanvas));

    /// <summary>
    /// 画布双击事件
    /// </summary>
    public static readonly RoutedEvent CanvasDoubleClickEvent = EventManager.RegisterRoutedEvent(
        "CanvasDoubleClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(NodeCanvas));

    /// <summary>
    /// 节点选择变化事件
    /// </summary>
    public static readonly RoutedEvent NodeSelectionChangedEvent = EventManager.RegisterRoutedEvent(
        "NodeSelectionChanged", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(NodeCanvas));

    /// <summary>
    /// 节点创建请求事件
    /// </summary>
    public static readonly RoutedEvent NodeCreationRequestedEvent = EventManager.RegisterRoutedEvent(
        "NodeCreationRequested", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(NodeCanvas));

    /// <summary>
    /// 节点删除请求事件
    /// </summary>
    public static readonly RoutedEvent NodeDeletionRequestedEvent = EventManager.RegisterRoutedEvent(
        "NodeDeletionRequested", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(NodeCanvas));

    /// <summary>
    /// 连接创建请求事件
    /// </summary>
    public static readonly RoutedEvent ConnectionCreationRequestedEvent = EventManager.RegisterRoutedEvent(
        "ConnectionCreationRequested", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(NodeCanvas));

    /// <summary>
    /// 连接删除请求事件
    /// </summary>
    public static readonly RoutedEvent ConnectionDeletionRequestedEvent = EventManager.RegisterRoutedEvent(
        "ConnectionDeletionRequested", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(NodeCanvas));

    /// <summary>
    /// 画布缩放变化事件
    /// </summary>
    public static readonly RoutedEvent ZoomChangedEvent = EventManager.RegisterRoutedEvent(
        "ZoomChanged", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(NodeCanvas));

    /// <summary>
    /// 画布平移变化事件
    /// </summary>
    public static readonly RoutedEvent PanChangedEvent = EventManager.RegisterRoutedEvent(
        "PanChanged", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(NodeCanvas));

    /// <summary>
    /// 节点位置变化事件
    /// </summary>
    public static readonly RoutedEvent NodePositionChangedEvent = EventManager.RegisterRoutedEvent(
        "NodePositionChanged", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(NodeCanvas));

    /// <summary>
    /// 框选开始事件
    /// </summary>
    public static readonly RoutedEvent SelectionStartedEvent = EventManager.RegisterRoutedEvent(
        "SelectionStarted", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(NodeCanvas));

    /// <summary>
    /// 框选完成事件
    /// </summary>
    public static readonly RoutedEvent SelectionCompletedEvent = EventManager.RegisterRoutedEvent(
        "SelectionCompleted", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(NodeCanvas));

    /// <summary>
    /// 键盘快捷键事件
    /// </summary>
    public static readonly RoutedEvent KeyboardShortcutEvent = EventManager.RegisterRoutedEvent(
        "KeyboardShortcut", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(NodeCanvas));

    /// <summary>
    /// 连接请求事件
    /// </summary>
    public static readonly RoutedEvent ConnectionRequestedEvent = EventManager.RegisterRoutedEvent(
        "ConnectionRequested", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(NodeCanvas));

    /// <summary>
    /// 连接完成事件
    /// </summary>
    public static readonly RoutedEvent ConnectionCompletedEvent = EventManager.RegisterRoutedEvent(
        "ConnectionCompleted", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(NodeCanvas));

    /// <summary>
    /// 连接删除事件
    /// </summary>
    public static readonly RoutedEvent ConnectionDeletedEvent = EventManager.RegisterRoutedEvent(
        "ConnectionDeleted", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(NodeCanvas));

    /// <summary>
    /// 视口变化事件
    /// </summary>
    public static readonly RoutedEvent ViewportChangedEvent = EventManager.RegisterRoutedEvent(
        "ViewportChanged", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(NodeCanvas));

    /// <summary>
    /// ViewModel集成请求事件
    /// </summary>
    public static readonly RoutedEvent ViewModelIntegrationRequestedEvent = EventManager.RegisterRoutedEvent(
        "ViewModelIntegrationRequested", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(NodeCanvas));

    #endregion

    #region 事件包装器

    /// <summary>
    /// 画布点击事件
    /// </summary>
    public event RoutedEventHandler CanvasClick
    {
        add { AddHandler(CanvasClickEvent, value); }
        remove { RemoveHandler(CanvasClickEvent, value); }
    }

    /// <summary>
    /// 画布双击事件
    /// </summary>
    public event RoutedEventHandler CanvasDoubleClick
    {
        add { AddHandler(CanvasDoubleClickEvent, value); }
        remove { RemoveHandler(CanvasDoubleClickEvent, value); }
    }

    /// <summary>
    /// 节点选择变化事件
    /// </summary>
    public event RoutedEventHandler NodeSelectionChanged
    {
        add { AddHandler(NodeSelectionChangedEvent, value); }
        remove { RemoveHandler(NodeSelectionChangedEvent, value); }
    }

    /// <summary>
    /// 连接请求事件
    /// </summary>
    public event RoutedEventHandler ConnectionRequested
    {
        add { AddHandler(ConnectionRequestedEvent, value); }
        remove { RemoveHandler(ConnectionRequestedEvent, value); }
    }

    /// <summary>
    /// 连接完成事件
    /// </summary>
    public event RoutedEventHandler ConnectionCompleted
    {
        add { AddHandler(ConnectionCompletedEvent, value); }
        remove { RemoveHandler(ConnectionCompletedEvent, value); }
    }

    /// <summary>
    /// 连接删除事件
    /// </summary>
    public event RoutedEventHandler ConnectionDeleted
    {
        add { AddHandler(ConnectionDeletedEvent, value); }
        remove { RemoveHandler(ConnectionDeletedEvent, value); }
    }

    /// <summary>
    /// 视口变化事件
    /// </summary>
    public event RoutedEventHandler ViewportChanged
    {
        add { AddHandler(ViewportChangedEvent, value); }
        remove { RemoveHandler(ViewportChangedEvent, value); }
    }

    /// <summary>
    /// ViewModel集成请求事件
    /// </summary>
    public event RoutedEventHandler ViewModelIntegrationRequested
    {
        add { AddHandler(ViewModelIntegrationRequestedEvent, value); }
        remove { RemoveHandler(ViewModelIntegrationRequestedEvent, value); }
    }

    #endregion

    #region 构造函数和静态构造函数

    /// <summary>
    /// 静态构造函数
    /// </summary>
    static NodeCanvas()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(NodeCanvas),
            new FrameworkPropertyMetadata(typeof(NodeCanvas)));
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    public NodeCanvas()
    {
        // 设置焦点以接收键盘事件
        Focusable = true;
        ClipToBounds = true;

        Loaded += OnLoaded;
    }

    /// <summary>
    /// 应用模板时的处理
    /// </summary>
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        // 获取模板元素
        _mainCanvas = GetTemplateChild("PART_MainCanvas") as Canvas;
        _canvasScrollViewer = GetTemplateChild("PART_CanvasScrollViewer") as ScrollViewer;
        _zoomTransform = GetTemplateChild("PART_ZoomTransform") as ScaleTransform;
        _panTransform = GetTemplateChild("PART_PanTransform") as TranslateTransform;
        _selectionRectangle = GetTemplateChild("PART_SelectionRectangle") as SelectionRectangle;
        _gridBackground = GetTemplateChild("PART_GridBackground") as GridBackground;

        // 绑定事件
        if (_mainCanvas != null)
        {
            _mainCanvas.MouseDown += OnCanvasMouseDown;
            _mainCanvas.MouseMove += OnCanvasMouseMove;
            _mainCanvas.MouseUp += OnCanvasMouseUp;
            // 预览阶段处理分组点击以控制选中，不拦截拖拽
            _mainCanvas.PreviewMouseLeftButtonDown += OnCanvasPreviewMouseLeftButtonDown;
            _mainCanvas.MouseWheel += OnCanvasMouseWheel;
            _mainCanvas.KeyDown += OnCanvasKeyDown;
            _mainCanvas.Drop += OnCanvasDrop;
            _mainCanvas.DragOver += OnCanvasDragOver;
            _mainCanvas.AllowDrop = true;
            _mainCanvas.Focusable = true;
        }

        // 注册Pin连接拖拽事件处理器
        AddHandler(PinControl.ConnectionDragStartedEvent, new RoutedEventHandler(OnConnectionDragStarted));
        AddHandler(PinControl.ConnectionDragDeltaEvent, new RoutedEventHandler(OnConnectionDragDelta));
        AddHandler(PinControl.ConnectionDragCompletedEvent, new RoutedEventHandler(OnConnectionDragCompleted));

        // 注册 Node 拖拽事件处理器（支持多选一起移动）
        AddHandler(NodeControl.NodeDragStartedEvent, new RoutedEventHandler(OnNodeDragStarted));
        AddHandler(NodeControl.NodeDragDeltaEvent, new RoutedEventHandler(OnNodeDragDelta));
        AddHandler(NodeControl.NodeDragCompletedEvent, new RoutedEventHandler(OnNodeDragCompleted));

        // 初始化变换
        UpdateZoomTransform();
        UpdatePanTransform();
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 控件加载完成处理
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Focus();
    }

    /// <summary>
    /// 节点数据源变化处理
    /// </summary>
    private void OnNodesSourceChanged()
    {
        // 数据源变化时的处理逻辑
        // 使用者可以通过事件监听来处理
    }

    /// <summary>
    /// 连接数据源变化处理
    /// </summary>
    private void OnConnectionsSourceChanged()
    {
        // 数据源变化时的处理逻辑
        // 使用者可以通过事件监听来处理
    }

    /// <summary>
    /// 更新缩放变换
    /// </summary>
    private void UpdateZoomTransform()
    {
        if (_zoomTransform != null)
        {
            _zoomTransform.ScaleX = ZoomLevel;
            _zoomTransform.ScaleY = ZoomLevel;
        }
    }

    /// <summary>
    /// 更新平移变换
    /// </summary>
    private void UpdatePanTransform()
    {
        if (_panTransform != null)
        {
            _panTransform.X = PanOffset.X;
            _panTransform.Y = PanOffset.Y;
        }
    }

    /// <summary>
    /// 检查点是否在节点上
    /// </summary>
    private bool IsOverNode(Point position)
    {
        if (_mainCanvas == null) return false;

        var hitTest = VisualTreeHelper.HitTest(_mainCanvas, position);
        return hitTest?.VisualHit is DependencyObject hit &&
               (hit is NodeControl || FindAncestor<NodeControl>(hit) != null);
    }

    /// <summary>
    /// 获取指定位置的节点控件
    /// </summary>
    private NodeControl? GetNodeAtPosition(Point position)
    {
        if (_mainCanvas == null) return null;

        var hitTest = VisualTreeHelper.HitTest(_mainCanvas, position);
        if (hitTest?.VisualHit is DependencyObject hit)
        {
            return hit as NodeControl ?? FindAncestor<NodeControl>(hit);
        }

        return null;
    }

    /// <summary>
    /// 获取所有节点控件
    /// </summary>
    private IEnumerable<NodeControl> GetAllNodeControls()
    {
        if (_mainCanvas == null) yield break;

        // 递归遍历 MainCanvas 的可视树，查找所有 NodeControl
        var stack = new Stack<DependencyObject>();
        stack.Push(_mainCanvas);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            var childCount = VisualTreeHelper.GetChildrenCount(current);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(current, i);
                if (child is NodeControl nodeControl)
                {
                    yield return nodeControl;
                }

                stack.Push(child);
            }
        }
    }

    /// <summary>
    /// 开始平移操作
    /// </summary>
    private void StartPanning(Point startPoint)
    {
        _isPanning = true;
        _panStartPoint = startPoint;
        Mouse.Capture(_mainCanvas);
    }

    /// <summary>
    /// 停止平移操作
    /// </summary>
    private void StopPanning()
    {
        _isPanning = false;
        Mouse.Capture(null);
    }

    /// <summary>
    /// 开始选择框操作
    /// </summary>
    private void StartSelection(Point startPoint)
    {
        _isSelecting = true;
    // 转换为画布逻辑坐标，避免缩放/平移影响
    var logicalStart = ToCanvasLogical(startPoint);
    _selectionStartPoint = logicalStart;

        if (_selectionRectangle != null)
        {
        _selectionRectangle.StartSelection(logicalStart);
        }

        Mouse.Capture(_mainCanvas);
    }

    /// <summary>
    /// 更新选择框
    /// </summary>
    private void UpdateSelection(Point currentPoint)
    {
        if (!_isSelecting || _selectionRectangle == null) return;

    // 使用逻辑坐标更新选择框
    var logicalPoint = ToCanvasLogical(currentPoint);
    _selectionRectangle.UpdateSelection(logicalPoint);

        // 更新选中的节点
        var rect = _selectionRectangle.GetSelectionRect();
        UpdateSelectedNodes(rect);
    }

    /// <summary>
    /// 完成选择框操作
    /// </summary>
    private void CompleteSelection()
    {
        _isSelecting = false;

        if (_selectionRectangle != null)
        {
            _selectionRectangle.EndSelection();
        }

        Mouse.Capture(null);

        // 触发选择变化事件
        var args = new RoutedEventArgs(NodeSelectionChangedEvent, this);
    }

    /// <summary>
    /// 更新选中的节点
    /// </summary>
    private void UpdateSelectedNodes(Rect selectionRect)
    {
        foreach (var nodeControl in GetAllNodeControls())
        {
            var nodeRect = new Rect(nodeControl.Position, nodeControl.RenderSize);
            nodeControl.IsSelected = selectionRect.IntersectsWith(nodeRect);
        }
    }

    /// <summary>
    /// 查找指定类型的祖先元素
    /// </summary>
    private static T? FindAncestor<T>(DependencyObject current) where T : class
    {
        while (current != null)
        {
            if (current is T ancestor)
                return ancestor;
            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    #endregion

    #region 画布事件处理

    /// <summary>
    /// 画布鼠标按下处理
    /// </summary>
    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (IsReadOnly) return;

        // 确保主画布有焦点以接收键盘事件
        _mainCanvas?.Focus();

        var position = e.GetPosition(_mainCanvas);

        // 右键：空白处弹出上下文菜单（复制/粘贴/删除/添加节点）
        if (e.RightButton == MouseButtonState.Pressed && !IsOverNode(position))
        {
            var menu = ContextMenuProvider?.BuildCanvasContextMenu(this, position);
            if (menu != null)
            {
                menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
                menu.IsOpen = true;
            }

            if (e.ClickCount == 1)
            {
                var clickArgs = new RoutedEventArgs(CanvasClickEvent, this);
                RaiseEvent(clickArgs);
            }

            e.Handled = true;
            return;
        }

        // 右键：在节点上弹出节点上下文菜单（仅 复制/粘贴/删除，不包含“添加节点”）
        if (e.RightButton == MouseButtonState.Pressed && IsOverNode(position))
        {
            var node = GetNodeAtPosition(position);
            if (node != null)
            {
                // 若该节点未选中，则清空选择并选中该节点
                if (!node.IsSelected)
                {
                    ClearNodeSelection();
                    node.IsSelected = true;
                }
                
                var menu = ContextMenuProvider?.BuildNodeContextMenu(this, node, position);
                if (menu != null)
                {
                    menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
                    menu.IsOpen = true;
                }

                e.Handled = true;
                return;
            }
        }

        if (e.MiddleButton == MouseButtonState.Pressed)
        {
            // 开始平移
            StartPanning(e.GetPosition(_canvasScrollViewer));
        }
        else if (e.LeftButton == MouseButtonState.Pressed &&
                 (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) &&
                 !IsOverNode(position))
        {
            // Ctrl+左键开始平移
            StartPanning(e.GetPosition(_canvasScrollViewer));
            e.Handled = true;
        }
        else if (e.RightButton == MouseButtonState.Pressed && !IsOverNode(position))
        {
            // 右键点击空白处，触发上下文菜单事件
            if (e.ClickCount == 1)
            {
                var clickArgs = new RoutedEventArgs(CanvasClickEvent, this);
                RaiseEvent(clickArgs);
            }

            e.Handled = true;
        }
        else if (e.LeftButton == MouseButtonState.Pressed && !IsOverNode(position))
        {
            if (e.ClickCount == 1)
            {
                // 单击空白处，清除选择并开始框选（包含节点与分组）
                ClearNodeSelection();
                ClearGroupSelection();
                StartSelection(position);

                var clickArgs = new RoutedEventArgs(CanvasClickEvent, this);
                RaiseEvent(clickArgs);
            }
            else if (e.ClickCount == 2)
            {
                // 双击空白处，触发节点创建请求
                var doubleClickArgs = new RoutedEventArgs(NodeCreationRequestedEvent, this);
                RaiseEvent(doubleClickArgs);
            }

            e.Handled = true;
        }
    }

    /// <summary>
    /// 画布鼠标移动处理
    /// </summary>
    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(_mainCanvas);
        _lastMousePosition = position;

        if (_isPanning)
        {
            // 使用 ScrollViewer 坐标计算位移，避免因 RenderTransform 改变导致的参考系变化
            UpdatePanning(e.GetPosition(_canvasScrollViewer));
            e.Handled = true;
        }
        else if (_isSelecting)
        {
            // 更新选择框
            UpdateSelection(position);
            e.Handled = true;
        }
        else if (_isCreatingConnection)
        {
            // 更新连接线预览
            _connectionCurrentPoint = position;
            UpdateConnectionPreview();
            e.Handled = true;
        }
        else if (_isDraggingNode)
        {
            UpdateNodeDragging(position);
            e.Handled = true;
        }
    }

    /// <summary>
    /// 画布鼠标释放处理
    /// </summary>
    private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanning)
        {
            StopPanning();
            e.Handled = true;
        }
        else if (_isSelecting)
        {
            CompleteSelection();
            e.Handled = true;
        }
        else if (_isCreatingConnection)
        {
            CompleteConnection(e.GetPosition(_mainCanvas));
            e.Handled = true;
        }
    }

    /// <summary>
    /// 画布鼠标滚轮处理
    /// </summary>
    private void OnCanvasMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        {
            // Ctrl+滚轮缩放
            var delta = e.Delta > 0 ? 1.1 : 0.9;
            var newZoom = Math.Max(0.1, Math.Min(5.0, ZoomLevel * delta));
            ZoomLevel = newZoom;

            // 触发视口变化事件
            var args = new RoutedEventArgs(ViewportChangedEvent, this);
            RaiseEvent(args);

            e.Handled = true;
        }
    }

    /// <summary>
    /// 画布键盘按下处理
    /// </summary>
    private void OnCanvasKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
        {
            // 删除选中的节点和连接
            DeleteSelectedItems();
        }
        else if (e.Key == Key.A && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
        {
            // Ctrl+A 全选
            SelectAllNodes();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            // Esc 取消当前操作
            CancelCurrentOperation();
            e.Handled = true;
        }
    }

    /// <summary>
    /// 画布拖放处理
    /// </summary>
    private void OnCanvasDrop(object sender, DragEventArgs e)
    {
        if (IsReadOnly) return;

        var position = e.GetPosition(_mainCanvas);

        // 尝试从拖拽数据中获取NodeMetadata
        var metadata = Helpers.DragDropHelper.GetNodeMetadata(e.Data);
        if (metadata != null)
        {
            // 如果启用网格对齐，对位置进行对齐
            if (SnapToGrid)
            {
                position = SnapPointToGrid(position);
            }

            // 触发节点创建请求事件，传递NodeMetadata和位置信息
            var args = new NodeCreationRequestedEventArgs(NodeCreationRequestedEvent, this)
            {
                NodeMetadata = metadata,
                Position = position
            };
            RaiseEvent(args);
        }

        e.Handled = true;
    }

    /// <summary>
    /// 画布拖放悬停处理
    /// </summary>
    private void OnCanvasDragOver(object sender, DragEventArgs e)
    {
        e.Effects = IsReadOnly ? DragDropEffects.None : DragDropEffects.Copy;
        e.Handled = true;
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 更新平移
    /// </summary>
    private void UpdatePanning(Point currentPoint)
    {
        if (!_isPanning) return;

        var delta = currentPoint - _panStartPoint;
        var newOffset = new Point(PanOffset.X + delta.X, PanOffset.Y + delta.Y);
        PanOffset = newOffset;

        _panStartPoint = currentPoint;
    }

    /// 更新节点拖拽（占位实现，避免编译错误；实际拖拽由 NodeControl 处理）
    private void UpdateNodeDragging(Point position)
    {
        // TODO: 如需支持框选后批量拖拽，可在此实现根据选中集合进行偏移
    }

    /// <summary>
    /// 清除节点选择
    /// </summary>
    // 多选拖拽事件处理
    private void OnNodeDragStarted(object sender, RoutedEventArgs e)
    {
        if (IsReadOnly) return;
        if (e.OriginalSource is not NodeControl dragged) return;

        _draggingNodes.Clear();
        _initialNodePositions.Clear();

        // 收集所有选中的节点；若无选中，则仅移动当前节点
        var selected = GetAllNodeControls().Where(n => n.IsSelected).ToList();
        if (selected.Count == 0)
        {
            selected.Add(dragged);
        }

        foreach (var node in selected)
        {
            _draggingNodes.Add(node);
            var init = node.NodeData != null
                ? new Point(node.NodeData.Position.X, node.NodeData.Position.Y)
                : node.Position;
            _initialNodePositions[node] = init;
        }

        _isDraggingNode = true;
        e.Handled = false; // 不拦截，让节点自身拖拽继续
    }

    private void OnNodeDragDelta(object sender, RoutedEventArgs e)
    {
        if (IsReadOnly || !_isDraggingNode) return;
        if (e.OriginalSource is not NodeControl dragged) return;

        // 被拖拽节点当前与初始位置的位移（逻辑坐标）
        if (!_initialNodePositions.TryGetValue(dragged, out var draggedStart)) return;

        var draggedNow = dragged.NodeData != null
            ? new Point(dragged.NodeData.Position.X, dragged.NodeData.Position.Y)
            : dragged.Position;

        var dx = draggedNow.X - draggedStart.X;
        var dy = draggedNow.Y - draggedStart.Y;

        if (Math.Abs(dx) < 0.0001 && Math.Abs(dy) < 0.0001) return;

        // 同步其他选中节点的位置（考虑分组边界约束/动态扩展）
        foreach (var node in _draggingNodes)
        {
            if (node == dragged) continue; // 被拖拽节点自己由其内部逻辑负责

            if (_initialNodePositions.TryGetValue(node, out var initPos))
            {
                var newPos = new Point(initPos.X + dx, initPos.Y + dy);

                if (node.NodeData != null)
                {
                    var desired = new TaiChi.Wpf.NodeEditor.Core.Models.NodeEditorPoint(newPos.X, newPos.Y);
                    if (GroupManager != null && node.NodeData.Group != null)
                    {
                        var adjusted = GroupManager.ConstrainOrExpandNodePosition(node.NodeData, node.NodeData.Group, desired, dynamicExpand: true);
                        node.NodeData.Position = adjusted;
                    }
                    else
                    {
                        node.NodeData.Position = desired;
                    }
                }
                else
                {
                    node.SetCanvasPosition(newPos);
                }
            }
        }

        // 对被拖拽的节点本身也做一次约束/扩展校正
        if (dragged.NodeData != null && dragged.NodeData.Group != null && GroupManager != null)
        {
            var desired = dragged.NodeData.Position;
            var adjusted = GroupManager.ConstrainOrExpandNodePosition(dragged.NodeData, dragged.NodeData.Group, desired, dynamicExpand: true);
            if (!desired.Equals(adjusted))
            {
                dragged.NodeData.Position = adjusted;
            }
        }

        // Ctrl 按住时，提供分组命中反馈
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        {
            var pos = Mouse.GetPosition(_mainCanvas);
            UpdateDropTargetHighlight(pos);
        }
        else
        {
            ClearDropTargetHighlight();
        }

        e.Handled = false;
    }

    private void OnNodeDragCompleted(object sender, RoutedEventArgs e)
    {
        if (!_isDraggingNode) return;

        // 如果按住 Ctrl 并且存在命中分组，则将选中节点加入该分组
        if ((Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) && _currentDropTargetGroup != null)
        {
            var target = _currentDropTargetGroup;
            var nodes = _draggingNodes
                .Where(nc => nc.NodeData != null)
                .Select(nc => nc.NodeData!)
                .Distinct()
                .ToList();

            if (nodes.Count > 0)
            {
                if (GroupManager != null)
                {
                    foreach (var n in nodes)
                        GroupManager.AddNodeToGroup(n, target);
                }
                else
                {
                    // 退化处理：直接设置 Group 引用
                    foreach (var n in nodes)
                        n.Group = target;
                }
            }
        }

        ClearDropTargetHighlight();

        _draggingNodes.Clear();
        _initialNodePositions.Clear();
        _isDraggingNode = false;

        e.Handled = false;
    }

    private void ClearNodeSelection()
    {
        foreach (var nodeControl in GetAllNodeControls())
        {
            nodeControl.IsSelected = false;
        }
    }

    /// <summary>
    /// 画布预览鼠标按下处理（用于分组选中逻辑，避免被分组内部控件提前标记 Handled）
    /// </summary>
    private void OnCanvasPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsReadOnly) return;
        if (_mainCanvas == null) return;

        var position = e.GetPosition(_mainCanvas);
        var hitGroup = HitTestGroupAt(position);
        if (hitGroup != null)
        {
            // Ctrl 支持多选/切换；否则为单选
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                hitGroup.IsSelected = !hitGroup.IsSelected;
            }
            else
            {
                foreach (var g in EnumerateAllGroups())
                {
                    g.IsSelected = ReferenceEquals(g, hitGroup);
                }
            }
            // 不标记 Handled，允许分组控件继续处理拖拽
        }
    }

    /// <summary>
    /// 清除所有分组的选中状态
    /// </summary>
    private void ClearGroupSelection()
    {
        foreach (var g in EnumerateAllGroups())
        {
            g.IsSelected = false;
        }
    }

    /// <summary>
    /// 选择所有节点
    /// </summary>
    private void SelectAllNodes()
    {
        foreach (var nodeControl in GetAllNodeControls())
        {
            nodeControl.IsSelected = true;
        }

        var args = new RoutedEventArgs(NodeSelectionChangedEvent, this);
    }

    /// <summary>
    /// 删除选中的项目
    /// </summary>
    private void DeleteSelectedItems()
    {
        DeleteSelectedNodes();
        // 触发删除事件，由使用者处理具体的删除逻辑
    }

    /// <summary>
    /// 取消当前操作
    /// </summary>
    private void CancelCurrentOperation()
    {
        if (_isPanning)
        {
            StopPanning();
        }
        else if (_isSelecting)
        {
            CompleteSelection();
        }
        else if (_isCreatingConnection)
        {
            CancelConnection();
        }
    }

    /// <summary>
    /// 更新连接预览
    /// </summary>
    private void UpdateConnectionPreview()
    {
        if (_mainCanvas == null || _connectionSourcePin == null) return;

        // 如果还没有预览路径，创建一个
        if (_connectionPreviewPath == null)
        {
            // 根据源 Pin 的类型决定预览连线颜色
            var previewBrush = GetPreviewBrushFromSourcePin(_connectionSourcePin);

            _connectionPreviewPath = new Path
            {
                Stroke = previewBrush,
                StrokeThickness = 2,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                IsHitTestVisible = false,
                Opacity = 0.7
            };

            // 添加高光效果，颜色基于笔刷颜色做适度变亮
            var glowColor = (previewBrush as SolidColorBrush)?.Color ?? Color.FromRgb(123, 179, 240);
            glowColor = Lighten(glowColor, 0.35);
            _connectionPreviewPath.Effect = new DropShadowEffect
            {
                Color = glowColor,
                BlurRadius = 6,
                ShadowDepth = 0,
                Opacity = 0.6
            };

            _mainCanvas.Children.Add(_connectionPreviewPath);
        }
        else
        {
            // 每次更新时同步颜色（以防外部动态改变了 Pin 的配色）
            var previewBrush = GetPreviewBrushFromSourcePin(_connectionSourcePin);
            if (!ReferenceEquals(_connectionPreviewPath.Stroke, previewBrush))
            {
                _connectionPreviewPath.Stroke = previewBrush;
                var glowColor = (previewBrush as SolidColorBrush)?.Color ?? Color.FromRgb(123, 179, 240);
                glowColor = Lighten(glowColor, 0.35);
                if (_connectionPreviewPath.Effect is DropShadowEffect dse)
                {
                    dse.Color = glowColor;
                }
            }
        }

        // 获取源引脚位置
        var sourcePoint = _connectionSourcePin.GetConnectorPosition();

        // 如果源点位置为(0,0)，使用备用方法
        if (sourcePoint.X == 0 && sourcePoint.Y == 0)
        {
            try
            {
                var transform = _connectionSourcePin.TransformToAncestor(_mainCanvas);
                var pinBounds = new Rect(0, 0, _connectionSourcePin.ActualWidth, _connectionSourcePin.ActualHeight);
                var transformedBounds = transform.TransformBounds(pinBounds);
                var physicalPoint = new Point(
                    transformedBounds.Left + transformedBounds.Width / 2,
                    transformedBounds.Top + transformedBounds.Height / 2);

                // 将物理坐标转换为逻辑坐标（去除NodeCanvas的缩放和平移变换）
                sourcePoint = ToCanvasLogical(physicalPoint);
            }
            catch
            {
                // 如果所有方法都失败，跳过预览更新
                return;
            }
        }

        // 创建平滑的贝塞尔曲线路径
        var pathGeometry = new PathGeometry();
        var pathFigure = new PathFigure { StartPoint = sourcePoint };

        // 计算控制点以创建平滑的曲线
        var deltaX = Math.Abs(_connectionCurrentPoint.X - sourcePoint.X);
        var controlOffset = Math.Max(deltaX * 0.6, 80); // 增加曲线弯曲度

        Point controlPoint1, controlPoint2;

        // 根据引脚方向调整控制点
        if (_connectionSourcePin.PinData?.Direction == TaiChi.Wpf.NodeEditor.Core.Enums.PinDirection.Output)
        {
            // 输出引脚：向右延伸
            controlPoint1 = new Point(sourcePoint.X + controlOffset, sourcePoint.Y);
            controlPoint2 = new Point(_connectionCurrentPoint.X - controlOffset, _connectionCurrentPoint.Y);
        }
        else
        {
            // 输入引脚：向左延伸
            controlPoint1 = new Point(sourcePoint.X - controlOffset, sourcePoint.Y);
            controlPoint2 = new Point(_connectionCurrentPoint.X + controlOffset, _connectionCurrentPoint.Y);
        }

        var bezierSegment = new BezierSegment(controlPoint1, controlPoint2, _connectionCurrentPoint, true);
        pathFigure.Segments.Add(bezierSegment);
        pathGeometry.Figures.Add(pathFigure);

        _connectionPreviewPath.Data = pathGeometry;
    }

    /// <summary>
    /// 根据源 Pin 计算临时连线的画刷颜色（与正式连线保持一致的类型配色）。
    /// </summary>
    private static Brush GetPreviewBrushFromSourcePin(PinControl? sourcePin)
    {
        // 优先使用 Pin 的数据类型映射（与 ConnectionViewModel 保持一致）
        var dataType = sourcePin?.PinData?.DataType;
        if (dataType != null)
        {
            return GetConnectionColorByType(dataType);
        }

        // 回退：若 Pin 本身带有 StrokeColor，则沿用
        var pinStroke = sourcePin?.StrokeColor as SolidColorBrush;
        if (pinStroke != null)
            return pinStroke;

        // 默认主题主色 #4A90E2
        return new SolidColorBrush(Color.FromRgb(74, 144, 226));
    }

    /// <summary>
    /// 连接线颜色与数据类型的简单映射（与 ConnectionViewModel 中逻辑一致）。
    /// </summary>
    private static Brush GetConnectionColorByType(Type dataType)
    {
        var name = dataType.Name;
        return name switch
        {
            nameof(Int32) or nameof(Int64) or nameof(Int16) => Brushes.Blue,
            nameof(Single) or nameof(Double) or nameof(Decimal) => Brushes.Green,
            nameof(String) => Brushes.Pink,
            nameof(Boolean) => Brushes.Orange,
            _ => Brushes.Gray
        };
    }

    /// <summary>
    /// 将颜色按给定比例向白色提亮。
    /// </summary>
    private static Color Lighten(Color color, double amount)
    {
        amount = Math.Max(0, Math.Min(1, amount));
        byte L(byte c) => (byte)(c + (255 - c) * amount);
        return Color.FromRgb(L(color.R), L(color.G), L(color.B));
    }

    /// <summary>
    /// 完成连接创建
    /// </summary>
    private void CompleteConnection(Point endPoint)
    {
        _isCreatingConnection = false;

        // 清理预览连接线
        ClearConnectionPreview();

        // 查找目标引脚
        var targetNode = GetNodeAtPosition(endPoint);
        if (targetNode != null && _connectionSourcePin != null)
        {
            // 触发连接完成事件
            var args = new RoutedEventArgs(ConnectionCompletedEvent, this);
            RaiseEvent(args);
        }

        _connectionSourcePin = null;
        Mouse.Capture(null);
    }

    /// <summary>
    /// 取消连接创建
    /// </summary>
    private void CancelConnection()
    {
        _isCreatingConnection = false;

        // 清理预览连接线
        ClearConnectionPreview();

        _connectionSourcePin = null;
        Mouse.Capture(null);
    }

    /// <summary>
    /// 清理连接预览
    /// </summary>
    private void ClearConnectionPreview()
    {
        if (_connectionPreviewPath != null && _mainCanvas != null)
        {
            _mainCanvas.Children.Remove(_connectionPreviewPath);
            _connectionPreviewPath = null;
        }
    }

    /// <summary>
    /// Pin连接拖拽开始事件处理
    /// </summary>
    private void OnConnectionDragStarted(object sender, RoutedEventArgs e)
    {
        if (IsReadOnly) return;

        if (e.OriginalSource is PinControl pinControl &&
            pinControl.PinData != null)
        {
            _isCreatingConnection = true;
            _connectionSourcePin = pinControl;

            // 获取当前鼠标位置作为初始目标点
            _connectionCurrentPoint = Mouse.GetPosition(_mainCanvas);

            // 通过事件通知外部开始连接创建，实现完全解耦
            var integrationArgs = new RoutedEventArgs(ViewModelIntegrationRequestedEvent, this);
            RaiseEvent(integrationArgs);

            // 立即更新预览以显示初始连接线
            UpdateConnectionPreview();

            // 触发连接请求事件
            var args = new RoutedEventArgs(ConnectionRequestedEvent, this);
            RaiseEvent(args);

            e.Handled = true;
        }
    }

    /// <summary>
    /// Pin连接拖拽中事件处理
    /// </summary>
    private void OnConnectionDragDelta(object sender, RoutedEventArgs e)
    {
        if (IsReadOnly || !_isCreatingConnection) return;

        if (e.OriginalSource is PinControl pinControl)
        {
            // 获取当前鼠标位置
            var mousePosition = Mouse.GetPosition(_mainCanvas);
            _connectionCurrentPoint = mousePosition;

            // 通过事件通知外部更新连接目标点，实现完全解耦
            var integrationArgs = new RoutedEventArgs(ViewModelIntegrationRequestedEvent, this);
            RaiseEvent(integrationArgs);

            // 更新连接预览
            UpdateConnectionPreview();

            e.Handled = true;
        }
    }

    /// <summary>
    /// Pin连接拖拽完成事件处理
    /// </summary>
    private void OnConnectionDragCompleted(object sender, RoutedEventArgs e)
    {
        if (IsReadOnly || !_isCreatingConnection) return;

        if (e.OriginalSource is PinControl sourcePinControl)
        {
            // 获取鼠标释放位置
            var mousePosition = Mouse.GetPosition(_mainCanvas);

            // 查找目标Pin
            var targetPinControl = GetPinAtPosition(mousePosition);

            if (targetPinControl != null && targetPinControl != sourcePinControl)
            {
                // 尝试创建连接
                TryCreateConnection(sourcePinControl, targetPinControl);
            }

            // 通过事件通知外部完成连接创建，实现完全解耦
            var integrationArgs = new RoutedEventArgs(ViewModelIntegrationRequestedEvent, this);
            RaiseEvent(integrationArgs);

            // 完成连接创建
            CompleteConnection(mousePosition);

            e.Handled = true;
        }
    }

    /// <summary>
    /// 获取指定位置的Pin控件
    /// </summary>
    private PinControl GetPinAtPosition(Point position)
    {
        if (_mainCanvas == null) return null;

        // 首先尝试精确命中测试
        var hitTest = VisualTreeHelper.HitTest(_mainCanvas, position);
        if (hitTest?.VisualHit != null)
        {
            // 向上查找PinControl
            var element = hitTest.VisualHit as DependencyObject;
            while (element != null)
            {
                if (element is PinControl pinControl)
                    return pinControl;
                element = VisualTreeHelper.GetParent(element);
            }
        }

        // 如果精确命中失败，尝试在容差范围内查找
        const double tolerance = 15.0; // 15像素的容差范围
        return GetPinAtPositionWithTolerance(position, tolerance);
    }

    /// <summary>
    /// 在容差范围内获取Pin控件
    /// </summary>
    private PinControl GetPinAtPositionWithTolerance(Point position, double tolerance)
    {
        if (_mainCanvas == null) return null;

        // 查找所有PinControl
        var allPins = FindVisualChildren<PinControl>(_mainCanvas);

        PinControl closestPin = null;
        double closestDistance = double.MaxValue;

        foreach (var pin in allPins)
        {
            try
            {
                // 直接使用 PinControl 的连接器位置方法
                var pinCenter = pin.GetConnectorPosition();

                var distance = Math.Sqrt(
                    Math.Pow(position.X - pinCenter.X, 2) +
                    Math.Pow(position.Y - pinCenter.Y, 2));

                // 如果在容差范围内且是最近的Pin
                if (distance <= tolerance && distance < closestDistance)
                {
                    closestDistance = distance;
                    closestPin = pin;
                }
            }
            catch
            {
                // 忽略转换错误
            }
        }

        return closestPin;
    }

    /// <summary>
    /// 查找所有指定类型的子元素
    /// </summary>
    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
    {
        if (depObj != null)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T t)
                {
                    yield return t;
                }

                foreach (var childOfChild in FindVisualChildren<T>(child))
                {
                    yield return childOfChild;
                }
            }
        }
    }

    /// <summary>
    /// 尝试创建连接
    /// </summary>
    private void TryCreateConnection(PinControl sourcePin, PinControl targetPin)
    {
        if (sourcePin?.PinData == null || targetPin?.PinData == null)
            return;

        // 触发连接创建请求事件，让ViewModel处理连接逻辑
        var args = new ConnectionCreationRequestedEventArgs(ConnectionCreationRequestedEvent, this)
        {
            SourcePin = sourcePin,
            TargetPin = targetPin
        };
        RaiseEvent(args);
    }

    /// <summary>
    /// 创建上下文菜单（包含 添加节点/复制/粘贴/删除）
    /// </summary>
    private ContextMenu CreateContextMenu(Point position)
    {
        var contextMenu = new ContextMenu();

        // 添加“添加节点”菜单项
        var addNodeMenuItem = CreateAddNodeMenuItem(position);
        contextMenu.Items.Add(addNodeMenuItem);

        contextMenu.Items.Add(new Separator());

        // 添加复制菜单项
        var copyMenuItem = new MenuItem
        {
            Header = "复制",
            IsEnabled = HasSelectedNodes()
        };
        copyMenuItem.Click += (s, e) => CopySelectedNodes();
        contextMenu.Items.Add(copyMenuItem);

        // 添加粘贴菜单项
        var pasteMenuItem = new MenuItem
        {
            Header = "粘贴",
            IsEnabled = CanPasteNodes()
        };
        pasteMenuItem.Click += (s, e) => PasteNodes();
        contextMenu.Items.Add(pasteMenuItem);

        // 添加删除菜单项
        var deleteMenuItem = new MenuItem
        {
            Header = "删除",
            IsEnabled = HasSelectedNodes()
        };
        deleteMenuItem.Click += (s, e) => DeleteSelectedItems();
        contextMenu.Items.Add(deleteMenuItem);

        return contextMenu;
    }

    /// <summary>
    /// 创建“添加节点”菜单项
    /// </summary>
    private MenuItem CreateAddNodeMenuItem(Point position)
    {
        var addNodeMenuItem = new MenuItem { Header = "添加节点" };

        // 先添加 Path 为空的节点，直接出现在最外层
        var rootNodes = NodeRegistry.AllNodes
            .Where(n => string.IsNullOrWhiteSpace(n.Path))
            .OrderBy(n => n.Name)
            .ToList();

        foreach (var node in rootNodes)
        {
            var nodeMenuItem = new MenuItem
            {
                Header = node.Name,
                Tag = node
            };

            nodeMenuItem.Click += (sender, e) =>
            {
                if (sender is MenuItem menuItem && menuItem.Tag is NodeMetadata metadata)
                {
                    CreateNodeFromMetadata(metadata, position);
                }
            };

            addNodeMenuItem.Items.Add(nodeMenuItem);
        }

        // 再添加按 Path 分组的节点（仅 Path 非空）
        var nodeGroups = GroupNodesByPath();

        // 递归创建菜单项
        CreateMenuItemsFromGroups(addNodeMenuItem, nodeGroups, position);

        return addNodeMenuItem;
    }

    /// <summary>
    /// 按Path对节点进行分组
    /// </summary>
    private Dictionary<string, object> GroupNodesByPath()
    {
        // 仅对 Path 非空的节点进行分组；Path 为空的节点在外层直接列出
        var allNodes = NodeRegistry.AllNodes.Where(n => !string.IsNullOrWhiteSpace(n.Path)).ToList();
        var groups = new Dictionary<string, object>();

        foreach (var node in allNodes)
        {
            var pathParts = node.Path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (pathParts.Length == 0) continue;

            var currentGroup = groups;

            // 处理路径的每一部分
            for (int i = 0; i < pathParts.Length; i++)
            {
                var part = pathParts[i];

                if (i == pathParts.Length - 1)
                {
                    // 最后一级，存储节点列表
                    if (!currentGroup.ContainsKey(part))
                    {
                        currentGroup[part] = new List<NodeMetadata>();
                    }

                    var nodeList = currentGroup[part] as List<NodeMetadata>;
                    if (nodeList != null)
                    {
                        nodeList.Add(node);
                    }
                }
                else
                {
                    // 中间级，创建子分组
                    if (!currentGroup.ContainsKey(part))
                    {
                        currentGroup[part] = new Dictionary<string, object>();
                    }

                    var subGroup = currentGroup[part] as Dictionary<string, object>;
                    if (subGroup != null)
                    {
                        currentGroup = subGroup;
                    }
                }
            }
        }

        return groups;
    }

    /// <summary>
    /// 从分组创建菜单项
    /// </summary>
    private void CreateMenuItemsFromGroups(MenuItem parentMenuItem, Dictionary<string, object> groups, Point position)
    {
        foreach (var group in groups.OrderBy(g => g.Key))
        {
            if (group.Value is List<NodeMetadata> nodeList)
            {
                // 如果是单个分组且只有一个节点，直接添加到父菜单
                if (nodeList.Count == 1 && groups.Count == 1)
                {
                    var node = nodeList[0];
                    var nodeMenuItem = new MenuItem
                    {
                        Header = node.Name,
                        Tag = node
                    };

                    nodeMenuItem.Click += (sender, e) =>
                    {
                        if (sender is MenuItem menuItem && menuItem.Tag is NodeMetadata metadata)
                        {
                            CreateNodeFromMetadata(metadata, position);
                        }
                    };

                    parentMenuItem.Items.Add(nodeMenuItem);
                }
                else
                {
                    // 创建分组菜单项
                    var groupMenuItem = new MenuItem { Header = group.Key };

                    // 为分组中的每个节点创建菜单项
                    foreach (var node in nodeList.OrderBy(n => n.Name))
                    {
                        var nodeMenuItem = new MenuItem
                        {
                            Header = node.Name,
                            Tag = node
                        };

                        nodeMenuItem.Click += (sender, e) =>
                        {
                            if (sender is MenuItem menuItem && menuItem.Tag is NodeMetadata metadata)
                            {
                                CreateNodeFromMetadata(metadata, position);
                            }
                        };

                        groupMenuItem.Items.Add(nodeMenuItem);
                    }

                    parentMenuItem.Items.Add(groupMenuItem);
                }
            }
            else if (group.Value is Dictionary<string, object> subGroups)
            {
                // 分组节点：创建子菜单
                var groupMenuItem = new MenuItem { Header = group.Key };
                CreateMenuItemsFromGroups(groupMenuItem, subGroups, position);
                parentMenuItem.Items.Add(groupMenuItem);
            }
        }
    }


    /// <summary>
    /// 获取当前视口中心在主画布(MainCanvas)逻辑坐标中的点
    /// </summary>
    /// <returns>以 MainCanvas 逻辑坐标表示的中心点</returns>
    public NodeEditorPoint GetViewportCenterLogicalPoint()
    {
        try
        {
            if (CanvasScrollViewer != null && MainCanvas != null)
            {
                var vw = CanvasScrollViewer.ViewportWidth;
                var vh = CanvasScrollViewer.ViewportHeight;

                Point centerInScrollViewer;
                if (double.IsNaN(vw) || vw <= 0 || double.IsNaN(vh) || vh <= 0)
                {
                    // 回退到控件的实际大小
                    centerInScrollViewer = new Point(ActualWidth / 2.0, ActualHeight / 2.0);
                }
                else
                {
                    centerInScrollViewer = new Point(vw / 2.0, vh / 2.0);
                }

                // 将视口中心从 ScrollViewer 坐标系转换到 MainCanvas 的本地坐标系（逻辑坐标）
                var toMain = CanvasScrollViewer.TransformToVisual(MainCanvas);
                var centerInMain = toMain.Transform(centerInScrollViewer);
                return new NodeEditorPoint(centerInMain.X, centerInMain.Y);
            }
        }
        catch
        {
            // 忽略转换异常，走回退逻辑
        }

        return new NodeEditorPoint(0, 0);
    }

    /// <summary>
    /// 将Canvas的物理坐标转换为逻辑坐标（去除NodeCanvas的缩放和平移变换）
    /// </summary>
    /// <param name="physicalPoint">物理坐标点</param>
    /// <returns>逻辑坐标点</returns>
    private Point ToCanvasLogical(Point physicalPoint)
    {
        // 获取缩放和平移参数
        var zoom = ZoomLevel <= 0 ? 1.0 : ZoomLevel;
        var pan = PanOffset;

        // 逆向变换：physical = logical * zoom + pan => logical = (physical - pan) / zoom
        return new Point(
            (physicalPoint.X - pan.X) / zoom,
            (physicalPoint.Y - pan.Y) / zoom);
    }

    #endregion

    #region 控件内置逻辑方法

    /// <summary>
    /// 检查是否有选中的节点
    /// </summary>
    private bool HasSelectedNodes()
    {
        return GetAllNodeControls().Any(n => n.IsSelected);
    }

    /// <summary>
    /// 检查是否可以粘贴节点
    /// </summary>
    private bool CanPasteNodes()
    {
        try
        {
            var vm = DataContext;
            if (vm != null)
            {
                var method = vm.GetType().GetMethod("ClipboardHasNodes");
                if (method != null)
                {
                    var result = method.Invoke(vm, null);
                    if (result is bool b) return b;
                }
            }
        }
        catch { }
        return false;
    }

    /// <summary>
    /// 复制选中的节点
    /// </summary>
    private void CopySelectedNodes()
    {
        try
        {
            var vm = DataContext;
            if (vm != null)
            {
                var method = vm.GetType().GetMethod("CopySelectionToClipboard");
                method?.Invoke(vm, null);
            }
        }
        catch { }
    }

    /// <summary>
    /// 剪切选中的节点
    /// </summary>
    private void CutSelectedNodes()
    {
        CopySelectedNodes();
        DeleteSelectedNodes();
    }

    /// <summary>
    /// 粘贴节点
    /// </summary>
    private void PasteNodes()
    {
        try
        {
            var vm = DataContext;
            if (vm != null)
            {
                var method = vm.GetType().GetMethod("PasteFromClipboardAt");
                if (method != null)
                {
                    // 目标逻辑坐标：优先使用右键菜单位置，其次使用视口中心
                    var target = _lastContextMenuLogicalPosition.HasValue
                        ? new TaiChi.Wpf.NodeEditor.Core.Models.NodeEditorPoint(_lastContextMenuLogicalPosition.Value.X, _lastContextMenuLogicalPosition.Value.Y)
                        : GetViewportCenterLogicalPoint();
                    method.Invoke(vm, new object[] { target });
                }
            }
        }
        catch { }
    }

    /// <summary>
    /// 删除选中的节点
    /// </summary>
    private void DeleteSelectedNodes()
    {
        var selectedNodes = GetAllNodeControls().Where(n => n.IsSelected).ToList();
        if (selectedNodes.Any())
        {
            // 触发节点删除请求事件，让外部处理具体删除逻辑
            var args = new NodeDeletionRequestedEventArgs(NodeDeletionRequestedEvent, this)
            {
                NodesToDelete = selectedNodes
            };
            RaiseEvent(args);
        }
    }

    /// <summary>
    /// 从元数据创建节点
    /// </summary>
    private void CreateNodeFromMetadata(NodeMetadata metadata, Point position)
    {
        // 先将物理坐标转换为画布逻辑坐标（去除缩放和平移）
        var logical = ToCanvasLogical(position);

        // 如启用网格对齐，按逻辑网格对齐
        if (SnapToGrid && GridSize > 0)
        {
            var x = Math.Round(logical.X / GridSize) * GridSize;
            var y = Math.Round(logical.Y / GridSize) * GridSize;
            logical = new Point(x, y);
        }

        // 触发节点创建请求事件，让外部处理具体创建逻辑
        var args = new NodeCreationRequestedEventArgs(NodeCreationRequestedEvent, this)
        {
            NodeMetadata = metadata,
            Position = logical
        };
        RaiseEvent(args);
    }

    /// <summary>
    /// 将点对齐到网格
    /// </summary>
    /// <param name="point">原始点</param>
    /// <returns>对齐后的点</returns>
    public Point SnapPointToGrid(Point point)
    {
        if (_gridBackground != null && SnapToGrid)
        {
            return _gridBackground.SnapToGrid(point);
        }

        // 如果没有网格背景控件，使用默认网格大小进行对齐
        var gridSize = GridSize;
        if (gridSize > 0)
        {
            var x = Math.Round(point.X / gridSize) * gridSize;
            var y = Math.Round(point.Y / gridSize) * gridSize;
            return new Point(x, y);
        }

        return point;
    }

    /// <summary>
    /// 为兼容旧调用处：构建画布右键菜单（内部转调 CreateContextMenu）
    /// </summary>
    private ContextMenu BuildCanvasContextMenu(Point position)
    {
        return CreateContextMenu(position);
    }

    /// <summary>
    /// 构建节点上的上下文菜单（不包含“添加节点”）
    /// </summary>
    private ContextMenu BuildNodeContextMenu(NodeControl node, Point position)
    {
        var menu = new ContextMenu();

        // 复制
        var copyItem = new MenuItem
        {
            Header = "复制",
            IsEnabled = HasSelectedNodes()
        };
        copyItem.Click += (s, e) => CopySelectedNodes();
        menu.Items.Add(copyItem);

        // 粘贴
        var pasteItem = new MenuItem
        {
            Header = "粘贴",
            IsEnabled = CanPasteNodes()
        };
        pasteItem.Click += (s, e) => PasteNodes();
        menu.Items.Add(pasteItem);

        // 分隔线
        menu.Items.Add(new Separator());

        // 删除
        var deleteItem = new MenuItem
        {
            Header = "删除",
            IsEnabled = HasSelectedNodes()
        };
        deleteItem.Click += (s, e) => DeleteSelectedItems();
        menu.Items.Add(deleteItem);

        // 允许上层附加分组相关菜单项
        ContextMenuProvider?.AppendGroupItemsForNode(menu, this, node, position);

        return menu;
    }

    // 分组命中与高亮辅助逻辑
    private void UpdateDropTargetHighlight(Point physicalPoint)
    {
        var hit = HitTestGroupAt(physicalPoint);
        if (!ReferenceEquals(hit, _currentDropTargetGroup))
        {
            // 清理旧高亮
            if (_currentDropTargetGroup != null)
                _currentDropTargetGroup.IsSelected = false;

            _currentDropTargetGroup = hit;

            if (_currentDropTargetGroup != null)
                _currentDropTargetGroup.IsSelected = true;
        }
    }

    private void ClearDropTargetHighlight()
    {
        if (_currentDropTargetGroup != null)
        {
            _currentDropTargetGroup.IsSelected = false;
            _currentDropTargetGroup = null;
        }
    }

    private NodeGroup? HitTestGroupAt(Point physicalPoint)
    {
        if (_mainCanvas == null) return null;
        var logical = ToCanvasLogical(physicalPoint);
        foreach (var g in EnumerateAllGroups())
        {
            var b = g.Bounds;
            if (logical.X >= b.X && logical.X <= b.X + b.Width &&
                logical.Y >= b.Y && logical.Y <= b.Y + b.Height)
            {
                return g;
            }
        }
        return null;
    }

    private IEnumerable<NodeGroup> EnumerateAllGroups()
    {
        if (GroupsSource == null) yield break;

        foreach (var item in GroupsSource)
        {
            if (item is NodeGroup g)
            {
                foreach (var gg in FlattenGroup(g))
                    yield return gg;
            }
        }
    }

    private IEnumerable<NodeGroup> FlattenGroup(NodeGroup root)
    {
        yield return root;
        foreach (var c in root.Children)
        {
            foreach (var gg in FlattenGroup(c))
                yield return gg;
        }
    }

    #endregion
}
