using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TaiChi.Wpf.NodeEditor.Core.Models;
using TaiChi.Wpf.NodeEditor.Core.Enums;
using TaiChi.Wpf.NodeEditor.Controls.Events;
using TaiChi.Wpf.NodeEditor.Controls.Helpers;
using TaiChi.Wpf.NodeEditor.Controls.Selectors;
using VisualTreeHelper = TaiChi.Wpf.NodeEditor.Controls.Helpers.VisualTreeHelper;
using WpfVisualTreeHelper = System.Windows.Media.VisualTreeHelper;

namespace TaiChi.Wpf.NodeEditor.Controls;

/// <summary>
/// 引脚控件 - 重构为 Control 以实现完全解耦
/// </summary>
public class PinControl : Control
{
    #region 样式键常量

    /// <summary>
    /// 基础样式键 - 外部项目可以通过此键覆盖基础样式
    /// </summary>
    public static readonly ResourceKey PinStyleKey = new ComponentResourceKey(typeof(PinControl), "PinStyle");

    #endregion

    #region 依赖属性

    /// <summary>
    /// 引脚数据模型
    /// </summary>
    public static readonly DependencyProperty PinDataProperty =
        DependencyProperty.Register(nameof(PinData), typeof(Pin), typeof(PinControl),
            new PropertyMetadata(null, OnPinDataChanged));

    /// <summary>
    /// 是否高亮显示
    /// </summary>
    public static readonly DependencyProperty IsHighlightedProperty =
        DependencyProperty.Register(nameof(IsHighlighted), typeof(bool), typeof(PinControl),
            new PropertyMetadata(false));

    /// <summary>
    /// 是否正在连接
    /// </summary>
    public static readonly DependencyProperty IsConnectingProperty =
        DependencyProperty.Register(nameof(IsConnecting), typeof(bool), typeof(PinControl),
            new PropertyMetadata(false));

    /// <summary>
    /// 连接器位置（相对于画布）
    /// </summary>
    public static readonly DependencyProperty ConnectorPositionProperty =
        DependencyProperty.Register(nameof(ConnectorPosition), typeof(Point), typeof(PinControl),
            new PropertyMetadata(new Point()));

    /// <summary>
    /// 相对于节点左上角的偏移量
    /// </summary>
    public static readonly DependencyProperty RelativeOffsetProperty =
        DependencyProperty.Register(nameof(RelativeOffset), typeof(Point), typeof(PinControl),
            new PropertyMetadata(new Point(), OnRelativeOffsetChanged));

    /// <summary>
    /// 输入连接器数据模板
    /// </summary>
    public static readonly DependencyProperty PinInputConnectorTemplateProperty =
        DependencyProperty.Register(nameof(PinInputConnectorTemplate), typeof(DataTemplate), typeof(PinControl),
            new PropertyMetadata(null));

    /// <summary>
    /// 输出连接器数据模板
    /// </summary>
    public static readonly DependencyProperty PinOutputConnectorTemplateProperty =
        DependencyProperty.Register(nameof(PinOutputConnectorTemplate), typeof(DataTemplate), typeof(PinControl),
            new PropertyMetadata(null));

    /// <summary>
    /// 输入引脚未连接时用于显示编辑控件的模板
    /// </summary>
    public static readonly DependencyProperty PinInputValueTemplateProperty =
        DependencyProperty.Register(nameof(PinInputValueTemplate), typeof(DataTemplate), typeof(PinControl),
            new PropertyMetadata(null));

    /// <summary>
    /// 输入连接器数据模板选择器
    /// </summary>
    public static readonly DependencyProperty PinInputConnectorTemplateSelectorProperty =
        DependencyProperty.Register(nameof(PinInputConnectorTemplateSelector), typeof(IPinConnectorDataTemplateSelector), typeof(PinControl),
            new PropertyMetadata(null));

    /// <summary>
    /// 输出连接器数据模板选择器
    /// </summary>
    public static readonly DependencyProperty PinOutputConnectorTemplateSelectorProperty =
        DependencyProperty.Register(nameof(PinOutputConnectorTemplateSelector), typeof(IPinConnectorDataTemplateSelector), typeof(PinControl),
            new PropertyMetadata(null));

    /// <summary>
    /// 输入引脚未连接时用于显示编辑控件的模板选择器
    /// </summary>
    public static readonly DependencyProperty PinInputValueTemplateSelectorProperty =
        DependencyProperty.Register(nameof(PinInputValueTemplateSelector), typeof(IPinInputValueTemplateSelector), typeof(PinControl),
            new PropertyMetadata(null));

    /// <summary>
    /// Pin描边颜色
    /// </summary>
    public static readonly DependencyProperty StrokeColorProperty =
        DependencyProperty.Register(nameof(StrokeColor), typeof(Brush), typeof(PinControl),
            new PropertyMetadata(Brushes.Gray));

    /// <summary>
    /// Pin描边粗细
    /// </summary>
    public static readonly DependencyProperty StrokeThicknessProperty =
        DependencyProperty.Register(nameof(StrokeThickness), typeof(double), typeof(PinControl),
            new PropertyMetadata(1.0));

    /// <summary>
    /// Pin主颜色
    /// </summary>
    public static readonly DependencyProperty MainColorProperty =
        DependencyProperty.Register(nameof(MainColor), typeof(Brush), typeof(PinControl),
            new PropertyMetadata(Brushes.Gray));

    #endregion

    #region 属性包装器

    /// <summary>
    /// 引脚数据模型
    /// </summary>
    public Pin PinData
    {
        get => (Pin)GetValue(PinDataProperty);
        set => SetValue(PinDataProperty, value);
    }

    /// <summary>
    /// 是否高亮显示
    /// </summary>
    public bool IsHighlighted
    {
        get => (bool)GetValue(IsHighlightedProperty);
        set => SetValue(IsHighlightedProperty, value);
    }

    /// <summary>
    /// 是否正在连接
    /// </summary>
    public bool IsConnecting
    {
        get => (bool)GetValue(IsConnectingProperty);
        set => SetValue(IsConnectingProperty, value);
    }

    /// <summary>
    /// 连接器位置（相对于画布）
    /// </summary>
    public Point ConnectorPosition
    {
        get => (Point)GetValue(ConnectorPositionProperty);
        set => SetValue(ConnectorPositionProperty, value);
    }

    /// <summary>
    /// 相对于节点左上角的偏移量
    /// </summary>
    public Point RelativeOffset
    {
        get => (Point)GetValue(RelativeOffsetProperty);
        set => SetValue(RelativeOffsetProperty, value);
    }

    /// <summary>
    /// 输入连接器数据模板
    /// </summary>
    public DataTemplate? PinInputConnectorTemplate
    {
        get => (DataTemplate?)GetValue(PinInputConnectorTemplateProperty);
        set => SetValue(PinInputConnectorTemplateProperty, value);
    }

    /// <summary>
    /// 输出连接器数据模板
    /// </summary>
    public DataTemplate? PinOutputConnectorTemplate
    {
        get => (DataTemplate?)GetValue(PinOutputConnectorTemplateProperty);
        set => SetValue(PinOutputConnectorTemplateProperty, value);
    }

    /// <summary>
    /// 输入引脚未连接时用于显示编辑控件的模板
    /// </summary>
    public DataTemplate? PinInputValueTemplate
    {
        get => (DataTemplate?)GetValue(PinInputValueTemplateProperty);
        set => SetValue(PinInputValueTemplateProperty, value);
    }

    /// <summary>
    /// 输入连接器数据模板选择器
    /// </summary>
    public IPinConnectorDataTemplateSelector? PinInputConnectorTemplateSelector
    {
        get => (IPinConnectorDataTemplateSelector?)GetValue(PinInputConnectorTemplateSelectorProperty);
        set => SetValue(PinInputConnectorTemplateSelectorProperty, value);
    }

    /// <summary>
    /// 输出连接器数据模板选择器
    /// </summary>
    public IPinConnectorDataTemplateSelector? PinOutputConnectorTemplateSelector
    {
        get => (IPinConnectorDataTemplateSelector?)GetValue(PinOutputConnectorTemplateSelectorProperty);
        set => SetValue(PinOutputConnectorTemplateSelectorProperty, value);
    }

    /// <summary>
    /// 输入引脚未连接时用于显示编辑控件的模板选择器
    /// </summary>
    public IPinInputValueTemplateSelector? PinInputValueTemplateSelector
    {
        get => (IPinInputValueTemplateSelector?)GetValue(PinInputValueTemplateSelectorProperty);
        set => SetValue(PinInputValueTemplateSelectorProperty, value);
    }

    /// <summary>
    /// Pin描边颜色
    /// </summary>
    public Brush StrokeColor
    {
        get => (Brush)GetValue(StrokeColorProperty);
        set => SetValue(StrokeColorProperty, value);
    }

    /// <summary>
    /// Pin描边粗细
    /// </summary>
    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    /// <summary>
    /// Pin主颜色
    /// </summary>
    public Brush MainColor
    {
        get => (Brush)GetValue(MainColorProperty);
        set => SetValue(MainColorProperty, value);
    }

    #endregion

    #region 依赖属性回调

    private static void OnPinDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PinControl control && e.NewValue is Pin pin)
        {
            // 根据数据类型自动设置颜色
            // control.PinColor = GetColorForDataType(pin.DataType);
        }
    }

    /// <summary>
    /// 根据数据类型获取颜色
    /// </summary>
    private static Brush GetColorForDataType(Type dataType)
    {
        return dataType?.Name switch
        {
            nameof(Int32) => Brushes.Blue,
            nameof(Double) => Brushes.Green,
            nameof(String) => Brushes.Red,
            nameof(Boolean) => Brushes.Orange,
            _ => Brushes.Gray
        };
    }

    /// <summary>
    /// 相对偏移量属性变化回调
    /// </summary>
    private static void OnRelativeOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PinControl control)
        {
            // 触发相对偏移量变化事件
            var newOffset = (Point)e.NewValue;
            var pinId = control.PinData?.Id ?? Guid.Empty;
            
            if (pinId != Guid.Empty)
            {
                OnRelativeOffsetChanged(pinId, newOffset);
            }
        }
    }


    #endregion

    #region 路由事件

    /// <summary>
    /// 连接器点击事件
    /// </summary>
    public static readonly RoutedEvent ConnectorClickEvent = EventManager.RegisterRoutedEvent(
        "ConnectorClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PinControl));

    /// <summary>
    /// 连接器鼠标进入事件
    /// </summary>
    public static readonly RoutedEvent ConnectorMouseEnterEvent = EventManager.RegisterRoutedEvent(
        "ConnectorMouseEnter", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PinControl));

    /// <summary>
    /// 连接器鼠标离开事件
    /// </summary>
    public static readonly RoutedEvent ConnectorMouseLeaveEvent = EventManager.RegisterRoutedEvent(
        "ConnectorMouseLeave", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PinControl));

    /// <summary>
    /// 连接拖拽开始事件
    /// </summary>
    public static readonly RoutedEvent ConnectionDragStartedEvent = EventManager.RegisterRoutedEvent(
        "ConnectionDragStarted", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PinControl));

    /// <summary>
    /// 连接拖拽中事件
    /// </summary>
    public static readonly RoutedEvent ConnectionDragDeltaEvent = EventManager.RegisterRoutedEvent(
        "ConnectionDragDelta", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PinControl));

    /// <summary>
    /// 连接拖拽完成事件
    /// </summary>
    public static readonly RoutedEvent ConnectionDragCompletedEvent = EventManager.RegisterRoutedEvent(
        "ConnectionDragCompleted", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PinControl));

    /// <summary>
    /// 引脚悬停进入事件
    /// </summary>
    public static readonly RoutedEvent PinMouseEnterEvent = EventManager.RegisterRoutedEvent(
        "PinMouseEnter", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PinControl));

    /// <summary>
    /// 引脚悬停离开事件
    /// </summary>
    public static readonly RoutedEvent PinMouseLeaveEvent = EventManager.RegisterRoutedEvent(
        "PinMouseLeave", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PinControl));

    /// <summary>
    /// 引脚连接状态变化事件
    /// </summary>
    public static readonly RoutedEvent PinConnectionChangedEvent = EventManager.RegisterRoutedEvent(
        "PinConnectionChanged", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PinControl));

    /// <summary>
    /// 引脚位置变化事件
    /// </summary>
    public static readonly RoutedEvent PinPositionChangedEvent = EventManager.RegisterRoutedEvent(
        "PinPositionChanged", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PinControl));

    #endregion

    #region 事件包装器

    /// <summary>
    /// 连接器点击事件
    /// </summary>
    public event RoutedEventHandler ConnectorClick
    {
        add { AddHandler(ConnectorClickEvent, value); }
        remove { RemoveHandler(ConnectorClickEvent, value); }
    }

    /// <summary>
    /// 连接器鼠标进入事件
    /// </summary>
    public event RoutedEventHandler ConnectorMouseEnter
    {
        add { AddHandler(ConnectorMouseEnterEvent, value); }
        remove { RemoveHandler(ConnectorMouseEnterEvent, value); }
    }

    /// <summary>
    /// 连接器鼠标离开事件
    /// </summary>
    public event RoutedEventHandler ConnectorMouseLeave
    {
        add { AddHandler(ConnectorMouseLeaveEvent, value); }
        remove { RemoveHandler(ConnectorMouseLeaveEvent, value); }
    }

    /// <summary>
    /// 连接拖拽开始事件
    /// </summary>
    public event RoutedEventHandler ConnectionDragStarted
    {
        add { AddHandler(ConnectionDragStartedEvent, value); }
        remove { RemoveHandler(ConnectionDragStartedEvent, value); }
    }

    /// <summary>
    /// 引脚位置变化事件
    /// </summary>
    public event RoutedEventHandler PinPositionChanged
    {
        add { AddHandler(PinPositionChangedEvent, value); }
        remove { RemoveHandler(PinPositionChangedEvent, value); }
    }

    #endregion

    #region 相对偏移量变化事件

    /// <summary>
    /// 引脚相对偏移量变化事件，使用弱引用管理器避免内存泄漏
    /// </summary>
    public static event EventHandler<PinRelativeOffsetChangedEventArgs> RelativeOffsetChanged
    {
        add => _relativeOffsetEventManager.AddHandler(value);
        remove => _relativeOffsetEventManager.RemoveHandler(value);
    }

    /// <summary>
    /// 触发相对偏移量变化事件
    /// </summary>
    /// <param name="pinId">引脚唯一标识符</param>
    /// <param name="relativeOffset">新的相对偏移量</param>
    private static void OnRelativeOffsetChanged(Guid pinId, Point relativeOffset)
    {
        var eventArgs = new PinRelativeOffsetChangedEventArgs(pinId, relativeOffset);
        _relativeOffsetEventManager.RaiseEvent(null, eventArgs);
    }

    #endregion

    #region 连接器大小变化事件



    /// <summary>
    /// 清理所有死亡的事件处理器引用
    /// </summary>
    public static void CleanupEventHandlers()
    {
        _relativeOffsetEventManager.CleanupDeadReferences();
    }

    /// <summary>
    /// 获取当前活动的事件处理器数量
    /// </summary>
    public static int ActiveEventHandlerCount => _relativeOffsetEventManager.ActiveHandlerCount;

    #endregion

    #region 辅助方法

    /// <summary>
    /// 从资源获取默认连接器大小
    /// </summary>
    /// <returns>连接器大小</returns>
    private static double GetDefaultConnectorSize()
    {
        try
        {
            // 尝试从当前应用程序资源中获取 DefaultConnectorSize
            if (Application.Current.TryFindResource("DefaultConnectorSize") is double size)
            {
                return size;
            }
        }
        catch (Exception)
        {
            // 资源查找失败时忽略异常
        }
        
        // 如果找不到资源，返回默认值
        return 12.0;
    }

    /// <summary>
    /// 查找父级NodeControl
    /// </summary>
    /// <returns>找到的NodeControl，如果未找到返回null</returns>
    private NodeControl? FindParentNodeControl()
    {
        try
        {
            return Helpers.VisualTreeHelper.FindParent<NodeControl>(this);
        }
        catch (Exception ex)
        {
            // 捕获视觉树遍历过程中的异常
            System.Diagnostics.Debug.WriteLine($"查找父级NodeControl时发生异常: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 获取节点相对于NodeCanvas的逻辑坐标（使用缓存优化）
    /// </summary>
    /// <returns>节点相对于NodeCanvas的逻辑坐标</returns>
    private Point GetNodeCanvasPosition()
    {
        if (_parentNodeControl == null) return new Point(0, 0);
        
        // 如果缓存有效，直接返回缓存结果
        if (_isCacheValid && _nodeCanvasPositionCache.HasValue)
        {
            return _nodeCanvasPositionCache.Value;
        }
        
        // 查找主画布，优先使用缓存
        var mainCanvas = _mainCanvasCache ?? FindMainCanvas();
        if (mainCanvas == null) return new Point(0, 0);
        
        try
        {
            // 获取节点相对于画布的变换
            var transform = _parentNodeControl.TransformToAncestor(mainCanvas);
            var nodeBounds = new Rect(0, 0, _parentNodeControl.ActualWidth, _parentNodeControl.ActualHeight);
            var transformedBounds = transform.TransformBounds(nodeBounds);
            
            // 转换为逻辑坐标（去除缩放和平移）
            var logicalPosition = ToCanvasLogical(mainCanvas, new Point(transformedBounds.X, transformedBounds.Y));
            
            // 缓存结果
            _nodeCanvasPositionCache = logicalPosition;
            _isCacheValid = true;
            
            return logicalPosition;
        }
        catch (Exception)
        {
            return new Point(0, 0);
        }
    }

    /// <summary>
    /// 计算相对于节点左上角的偏移量
    /// </summary>
    private void CalculateRelativeOffset()
    {
        // 验证前置条件
        if (_parentNodeControl == null)
        {
            // 记录调试信息：找不到父级节点
            return;
        }
        
        try
        {
            // 获取引脚连接器相对于NodeCanvas的逻辑坐标
            var pinCanvasPosition = GetConnectorPosition();
            
            // 获取节点相对于NodeCanvas的逻辑坐标
            var nodeCanvasPosition = GetNodeCanvasPosition();
            
            // 验证坐标有效性
            if (double.IsNaN(pinCanvasPosition.X) || double.IsNaN(pinCanvasPosition.Y) ||
                double.IsInfinity(pinCanvasPosition.X) || double.IsInfinity(pinCanvasPosition.Y) ||
                double.IsNaN(nodeCanvasPosition.X) || double.IsNaN(nodeCanvasPosition.Y) ||
                double.IsInfinity(nodeCanvasPosition.X) || double.IsInfinity(nodeCanvasPosition.Y))
            {
                // 坐标值无效，跳过计算
                return;
            }
            
            // 计算相对偏移（引脚位置 - 节点位置）
            var relativeOffset = new Point(
                pinCanvasPosition.X - nodeCanvasPosition.X,
                pinCanvasPosition.Y - nodeCanvasPosition.Y);
            
            // 再次验证计算结果
            if (double.IsNaN(relativeOffset.X) || double.IsNaN(relativeOffset.Y) ||
                double.IsInfinity(relativeOffset.X) || double.IsInfinity(relativeOffset.Y))
            {
                // 计算结果无效，跳过更新
                return;
            }
            
            // 更新相对偏移属性
            RelativeOffset = relativeOffset;
        }
        catch (Exception ex)
        {
            // 捕获并处理计算过程中的异常
            // 可以在这里添加日志记录或错误处理逻辑
            System.Diagnostics.Debug.WriteLine($"计算相对偏移量时发生异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 节点大小变化事件处理
    /// </summary>
    private void OnNodeSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // 节点大小变化时重新计算偏移量
        // 同时使缓存失效，因为节点位置可能发生变化
        _nodeCanvasPositionCache = null;
        _isCacheValid = false;
        
        CalculateRelativeOffset();
    }

    /// <summary>
    /// 控件卸载事件处理
    /// </summary>
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // 清理事件监听，避免内存泄漏
        if (_parentNodeControl != null)
        {
            _parentNodeControl.SizeChanged -= OnNodeSizeChanged;
            _parentNodeControl = null;
        }
        
        // 清理缓存
        ClearCache();
    }

    /// <summary>
    /// 初始化主画布缓存
    /// </summary>
    private void InitializeMainCanvasCache()
    {
        try
        {
            _mainCanvasCache = FindMainCanvas();
            _isCacheValid = _mainCanvasCache != null;
        }
        catch (Exception)
        {
            _isCacheValid = false;
        }
    }

    /// <summary>
    /// 清理缓存
    /// </summary>
    private void ClearCache()
    {
        _mainCanvasCache = null;
        _nodeCanvasPositionCache = null;
        _isCacheValid = false;
    }

    /// <summary>
    /// 查找主画布
    /// </summary>
    /// <returns>找到的主画布，如果未找到返回null</returns>
    private Canvas? FindMainCanvas()
    {
        try
        {
            // 首先尝试从自身向上查找
            var mainCanvas = Helpers.VisualTreeHelper.FindParent<Canvas>(this);
            while (mainCanvas != null && (mainCanvas as FrameworkElement)?.Name != MainCanvasName)
            {
                mainCanvas = Helpers.VisualTreeHelper.FindParent<Canvas>(mainCanvas);
                
                // 防止无限循环：如果已经到达根元素，停止查找
                if (mainCanvas != null && WpfVisualTreeHelper.GetParent(mainCanvas) == null)
                {
                    break;
                }
            }
            
            // 如果找不到，尝试从父级节点查找
            if (mainCanvas == null && _parentNodeControl != null)
            {
                mainCanvas = Helpers.VisualTreeHelper.FindParent<Canvas>(_parentNodeControl);
                while (mainCanvas != null && (mainCanvas as FrameworkElement)?.Name != MainCanvasName)
                {
                    mainCanvas = Helpers.VisualTreeHelper.FindParent<Canvas>(mainCanvas);
                    
                    // 防止无限循环：如果已经到达根元素，停止查找
                    if (mainCanvas != null && WpfVisualTreeHelper.GetParent(mainCanvas) == null)
                    {
                        break;
                    }
                }
            }
            
            return mainCanvas;
        }
        catch (Exception ex)
        {
            // 捕获视觉树遍历过程中的异常
            System.Diagnostics.Debug.WriteLine($"查找主画布时发生异常: {ex.Message}");
            return null;
        }
    }

    #endregion

    #region 私有字段

    /// <summary>
    /// 父级NodeControl引用
    /// </summary>
    private NodeControl? _parentNodeControl;

    /// <summary>
    /// 主画布缓存引用，避免重复查找
    /// </summary>
    private Canvas? _mainCanvasCache;

    /// <summary>
    /// 节点画布位置缓存，避免重复计算
    /// </summary>
    private Point? _nodeCanvasPositionCache;

    /// <summary>
    /// 缓存是否有效的标志
    /// </summary>
    private bool _isCacheValid = false;

    /// <summary>
    /// 主画布名称常量
    /// </summary>
    private const string MainCanvasName = "PART_MainCanvas";

    /// <summary>
    /// 静态弱引用事件管理器，用于管理所有 PinControl 实例的相对偏移量变化事件
    /// </summary>
    private static readonly WeakEventManager<PinRelativeOffsetChangedEventArgs> _relativeOffsetEventManager = new();


    #endregion

    #region 构造函数和静态构造函数

    /// <summary>
    /// 静态构造函数
    /// </summary>
    static PinControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(PinControl),
            new FrameworkPropertyMetadata(typeof(PinControl)));
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    public PinControl()
    {
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    #endregion

    #region 事件处理

    /// <summary>
    /// 控件加载完成事件处理
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 查找父级NodeControl
        _parentNodeControl = FindParentNodeControl();
        
        if (_parentNodeControl != null)
        {
            // 监听节点大小变化事件
            _parentNodeControl.SizeChanged += OnNodeSizeChanged;
        }
        
        // 初始化主画布缓存
        InitializeMainCanvasCache();
        
        // 计算初始相对偏移量
        CalculateRelativeOffset();
        
        // 更新连接器位置
        UpdateConnectorPosition();
    }

    /// <summary>
    /// 控件大小变化事件处理
    /// </summary>
    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateConnectorPosition();
    }

    /// <summary>
    /// 重写鼠标按下事件处理
    /// </summary>
    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.LeftButton == MouseButtonState.Pressed && PinData != null)
        {
            // 设置连接状态
            IsConnecting = true;

            // 触发连接器点击事件
            var args = new RoutedEventArgs(ConnectorClickEvent, this);
            RaiseEvent(args);

            // 如果是拖拽操作，触发拖拽开始事件
            var dragArgs = new RoutedEventArgs(ConnectionDragStartedEvent, this);
            RaiseEvent(dragArgs);

            // 捕获鼠标
            Mouse.Capture(this);
            e.Handled = true;
        }
    }

    /// <summary>
    /// 重写鼠标移动事件处理
    /// </summary>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed && PinData != null)
        {
            // 触发连接拖拽中事件
            var dragArgs = new RoutedEventArgs(ConnectionDragDeltaEvent, this);
            RaiseEvent(dragArgs);
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
            // 释放鼠标捕获
            Mouse.Capture(null);

            // 重置连接状态
            IsConnecting = false;

            // 触发连接拖拽完成事件
            var dragCompletedArgs = new RoutedEventArgs(ConnectionDragCompletedEvent, this);
            RaiseEvent(dragCompletedArgs);

            e.Handled = true;
        }
    }

    /// <summary>
    /// 重写鼠标进入事件处理
    /// </summary>
    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);

        if (PinData != null)
        {
            IsHighlighted = true;

            var args = new RoutedEventArgs(ConnectorMouseEnterEvent, this);
            RaiseEvent(args);
        }
    }

    /// <summary>
    /// 重写鼠标离开事件处理
    /// </summary>
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);

        if (PinData != null)
        {
            IsHighlighted = false;

            var args = new RoutedEventArgs(ConnectorMouseLeaveEvent, this);
            RaiseEvent(args);
        }
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 获取连接器的中心点位置（相对于画布的逻辑坐标）
    /// </summary>
    /// <returns>连接器中心点位置</returns>
    public Point GetConnectorPosition()
    {
        // 首先尝试查找模板中的连接器元素
        FrameworkElement? connector = null;
        if (PinData?.Direction == PinDirection.Output)
        {
            connector = GetTemplateChild("PART_OutputConnector") as FrameworkElement;
        }
        else if (PinData?.Direction == PinDirection.Input)
        {
            connector = GetTemplateChild("PART_InputConnector") as FrameworkElement;
        }
        connector ??= GetTemplateChild("PART_Connector") as FrameworkElement; // 回退兼容旧模板

        // 如果找不到连接器元素，使用PinControl自身作为连接器
        if (connector == null)
        {
            connector = this;
        }

        if (PinData == null)
            return new Point(0, 0);

        // 获取连接器相对于画布的位置
        // 寻找主画布 PART_MainCanvas，以保证坐标系与连线路径一致
        var mainCanvas = VisualTreeHelper.FindParent<Canvas>(this);

        // 如果找到的不是PART_MainCanvas，继续向上查找
        while (mainCanvas != null && (mainCanvas as FrameworkElement)?.Name != "PART_MainCanvas")
        {
            mainCanvas = VisualTreeHelper.FindParent<Canvas>(mainCanvas);
        }

        if (mainCanvas == null)
        {
            // 如果找不到主画布，尝试使用任何Canvas作为参考
            mainCanvas = VisualTreeHelper.FindParent<Canvas>(this);
            if (mainCanvas == null)
            {
                return new Point(0, 0);
            }
        }

        try
        {
            // 检查控件是否已经完成布局
            if (connector.ActualWidth == 0 || connector.ActualHeight == 0)
            {
                // 如果控件还没有布局完成，尝试强制更新布局
                connector.UpdateLayout();

                // 如果仍然是0，使用默认尺寸
                if (connector.ActualWidth == 0 || connector.ActualHeight == 0)
                {
                    // 使用默认的引脚尺寸，从资源中获取
                    var defaultSize = GetDefaultConnectorSize();
                    var connectorBounds = new Rect(0, 0, defaultSize, defaultSize);

                    // 尝试获取相对位置
                    var transform = connector.TransformToAncestor(mainCanvas);
                    var transformedBounds = transform.TransformBounds(connectorBounds);

                    Point connectionPoint;
                    // 使用连接器中心作为连接点，避免在未测量完成时左右边缘导致的偏移
                    connectionPoint = new Point(
                        transformedBounds.Left + transformedBounds.Width / 2,
                        transformedBounds.Top + transformedBounds.Height / 2);

                    // 将物理坐标转换为逻辑坐标（去除NodeCanvas的缩放和平移变换）
                    return ToCanvasLogical(mainCanvas, connectionPoint);
                }
            }

            var transform2 = connector.TransformToAncestor(mainCanvas);
            var connectorBounds2 = new Rect(0, 0, connector.ActualWidth, connector.ActualHeight);
            var transformedBounds2 = transform2.TransformBounds(connectorBounds2);

            // 根据引脚方向调整连接点位置
            Point connectionPoint2;
            if (PinData.Direction == PinDirection.Output)
            {
                // 输出引脚：连接点在连接器中心
                connectionPoint2 = new Point(
                    transformedBounds2.Left + transformedBounds2.Width / 2,
                    transformedBounds2.Top + transformedBounds2.Height / 2);
            }
            else
            {
                // 输入引脚：连接点在连接器中心
                connectionPoint2 = new Point(
                    transformedBounds2.Left + transformedBounds2.Width / 2,
                    transformedBounds2.Top + transformedBounds2.Height / 2);
            }

            // 将物理坐标转换为逻辑坐标（去除NodeCanvas的缩放和平移变换）
            return ToCanvasLogical(mainCanvas, connectionPoint2);
        }
        catch (Exception)
        {
            return new Point(0, 0);
        }
    }

    /// <summary>
    /// 更新连接器位置
    /// </summary>
    public void UpdateConnectorPosition()
    {
        if (PinData != null)
        {
            // 如果控件还没有加载完成，延迟更新
            if (!IsLoaded)
            {
                Loaded += (s, e) => UpdateConnectorPosition();
                return;
            }

            var position = GetConnectorPosition();
            ConnectorPosition = position;

            // 同步到ViewModel的ConnectorPosition
            SyncToViewModel(position);
        }
    }

    /// <summary>
    /// 将位置同步到对应的PinViewModel
    /// </summary>
    /// <param name="position">连接器位置</param>
    private void SyncToViewModel(Point position)
    {
        // 通过路由事件通知位置变化，实现完全解耦
        var args = new RoutedEventArgs(PinPositionChangedEvent, this);
        RaiseEvent(args);
    }
    /// <summary>
    /// 将Canvas的物理坐标转换为逻辑坐标（去除NodeCanvas的缩放和平移变换）
    /// </summary>
    /// <param name="canvas">主画布</param>
    /// <param name="physicalPoint">物理坐标点</param>
    /// <returns>逻辑坐标点</returns>
    private Point ToCanvasLogical(Canvas canvas, Point physicalPoint)
    {
        // 查找NodeCanvas控件以获取缩放和平移信息
        var nodeCanvas = VisualTreeHelper.FindParent<NodeCanvas>(canvas);
        if (nodeCanvas == null)
            return physicalPoint;

        // 获取缩放和平移参数
        var zoom = nodeCanvas.ZoomLevel <= 0 ? 1.0 : nodeCanvas.ZoomLevel;
        var pan = nodeCanvas.PanOffset;

        // 逆向变换：physical = logical * zoom + pan => logical = (physical - pan) / zoom
        return new Point(
            (physicalPoint.X - pan.X) / zoom,
            (physicalPoint.Y - pan.Y) / zoom);
    }

    #endregion
}
