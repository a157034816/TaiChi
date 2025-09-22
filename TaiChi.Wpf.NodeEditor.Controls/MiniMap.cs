using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using TaiChi.Wpf.NodeEditor.Controls.Helpers;

namespace TaiChi.Wpf.NodeEditor.Controls;

/// <summary>
/// 小地图控件
/// </summary>
public class MiniMap : Control
{
    #region 依赖属性

    /// <summary>
    /// 目标画布
    /// </summary>
    public static readonly DependencyProperty TargetCanvasProperty =
        DependencyProperty.Register(nameof(TargetCanvas), typeof(Canvas), typeof(MiniMap),
            new PropertyMetadata(null, OnTargetCanvasChanged));

    /// <summary>
    /// 缩放比例
    /// </summary>
    public static readonly DependencyProperty ScaleFactorProperty =
        DependencyProperty.Register(nameof(ScaleFactor), typeof(double), typeof(MiniMap),
            new PropertyMetadata(0.1, OnScaleFactorChanged));

    /// <summary>
    /// 视口矩形颜色
    /// </summary>
    public static readonly DependencyProperty ViewportBrushProperty =
        DependencyProperty.Register(nameof(ViewportBrush), typeof(Brush), typeof(MiniMap),
            new PropertyMetadata(Brushes.Blue));

    /// <summary>
    /// 视口边框颜色
    /// </summary>
    public static readonly DependencyProperty ViewportBorderBrushProperty =
        DependencyProperty.Register(nameof(ViewportBorderBrush), typeof(Brush), typeof(MiniMap),
            new PropertyMetadata(Brushes.DarkBlue));

    /// <summary>
    /// 背景颜色
    /// </summary>
    public static readonly DependencyProperty MapBackgroundProperty =
        DependencyProperty.Register(nameof(MapBackground), typeof(Brush), typeof(MiniMap),
            new PropertyMetadata(Brushes.LightGray));

    /// <summary>
    /// 节点颜色
    /// </summary>
    public static readonly DependencyProperty NodeBrushProperty =
        DependencyProperty.Register(nameof(NodeBrush), typeof(Brush), typeof(MiniMap),
            new PropertyMetadata(Brushes.Gray));

    /// <summary>
    /// 当前视口位置
    /// </summary>
    public static readonly DependencyProperty ViewportPositionProperty =
        DependencyProperty.Register(nameof(ViewportPosition), typeof(Point), typeof(MiniMap),
            new PropertyMetadata(new Point(), OnViewportChanged));

    /// <summary>
    /// 当前视口大小
    /// </summary>
    public static readonly DependencyProperty ViewportSizeProperty =
        DependencyProperty.Register(nameof(ViewportSize), typeof(Size), typeof(MiniMap),
            new PropertyMetadata(new Size(), OnViewportChanged));

    /// <summary>
    /// 画布大小
    /// </summary>
    public static readonly DependencyProperty CanvasSizeProperty =
        DependencyProperty.Register(nameof(CanvasSize), typeof(Size), typeof(MiniMap),
            new PropertyMetadata(new Size(), OnCanvasSizeChanged));

    #endregion

    #region 路由事件

    /// <summary>
    /// 视口位置变化事件
    /// </summary>
    public static readonly RoutedEvent ViewportChangedEvent = EventManager.RegisterRoutedEvent(
        "ViewportChanged", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MiniMap));

    #endregion

    #region 属性包装器

    /// <summary>
    /// 目标画布
    /// </summary>
    public Canvas TargetCanvas
    {
        get => (Canvas)GetValue(TargetCanvasProperty);
        set => SetValue(TargetCanvasProperty, value);
    }

    /// <summary>
    /// 缩放比例
    /// </summary>
    public double ScaleFactor
    {
        get => (double)GetValue(ScaleFactorProperty);
        set => SetValue(ScaleFactorProperty, value);
    }

    /// <summary>
    /// 视口矩形颜色
    /// </summary>
    public Brush ViewportBrush
    {
        get => (Brush)GetValue(ViewportBrushProperty);
        set => SetValue(ViewportBrushProperty, value);
    }

    /// <summary>
    /// 视口边框颜色
    /// </summary>
    public Brush ViewportBorderBrush
    {
        get => (Brush)GetValue(ViewportBorderBrushProperty);
        set => SetValue(ViewportBorderBrushProperty, value);
    }

    /// <summary>
    /// 背景颜色
    /// </summary>
    public Brush MapBackground
    {
        get => (Brush)GetValue(MapBackgroundProperty);
        set => SetValue(MapBackgroundProperty, value);
    }

    /// <summary>
    /// 节点颜色
    /// </summary>
    public Brush NodeBrush
    {
        get => (Brush)GetValue(NodeBrushProperty);
        set => SetValue(NodeBrushProperty, value);
    }

    /// <summary>
    /// 当前视口位置
    /// </summary>
    public Point ViewportPosition
    {
        get => (Point)GetValue(ViewportPositionProperty);
        set => SetValue(ViewportPositionProperty, value);
    }

    /// <summary>
    /// 当前视口大小
    /// </summary>
    public Size ViewportSize
    {
        get => (Size)GetValue(ViewportSizeProperty);
        set => SetValue(ViewportSizeProperty, value);
    }

    /// <summary>
    /// 画布大小
    /// </summary>
    public Size CanvasSize
    {
        get => (Size)GetValue(CanvasSizeProperty);
        set => SetValue(CanvasSizeProperty, value);
    }

    #endregion

    #region 事件包装器

    /// <summary>
    /// 视口位置变化事件
    /// </summary>
    public event RoutedEventHandler ViewportChanged
    {
        add { AddHandler(ViewportChangedEvent, value); }
        remove { RemoveHandler(ViewportChangedEvent, value); }
    }

    #endregion

    #region 私有字段

    private Canvas? _miniMapCanvas;
    private Rectangle? _viewportRectangle;
    private bool _isDragging;
    private Point _dragStartPoint;

    #endregion

    #region 构造函数

    /// <summary>
    /// 静态构造函数
    /// </summary>
    static MiniMap()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(MiniMap),
            new FrameworkPropertyMetadata(typeof(MiniMap)));
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    public MiniMap()
    {
        Loaded += OnLoaded;
    }

    #endregion

    #region 重写方法

    /// <summary>
    /// 应用模板
    /// </summary>
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _miniMapCanvas = GetTemplateChild("PART_MiniMapCanvas") as Canvas;
        _viewportRectangle = GetTemplateChild("PART_ViewportRectangle") as Rectangle;

        if (_miniMapCanvas != null)
        {
            _miniMapCanvas.MouseDown += OnMiniMapMouseDown;
            _miniMapCanvas.MouseMove += OnMiniMapMouseMove;
            _miniMapCanvas.MouseUp += OnMiniMapMouseUp;
        }

        UpdateMiniMap();
    }

    #endregion

    #region 依赖属性回调

    private static void OnTargetCanvasChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MiniMap miniMap)
        {
            miniMap.UpdateMiniMap();
        }
    }

    private static void OnScaleFactorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MiniMap miniMap)
        {
            miniMap.UpdateMiniMap();
        }
    }

    private static void OnViewportChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MiniMap miniMap)
        {
            miniMap.UpdateViewportRectangle();
        }
    }

    private static void OnCanvasSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MiniMap miniMap)
        {
            miniMap.UpdateMiniMap();
        }
    }

    #endregion

    #region 事件处理

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateMiniMap();
    }

    private void OnMiniMapMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && _miniMapCanvas != null)
        {
            _isDragging = true;
            _dragStartPoint = e.GetPosition(_miniMapCanvas);
            _miniMapCanvas.CaptureMouse();

            // 立即更新视口位置
            UpdateViewportFromMiniMap(_dragStartPoint);
            e.Handled = true;
        }
    }

    private void OnMiniMapMouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging && _miniMapCanvas != null)
        {
            var currentPoint = e.GetPosition(_miniMapCanvas);
            UpdateViewportFromMiniMap(currentPoint);
            e.Handled = true;
        }
    }

    private void OnMiniMapMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging && _miniMapCanvas != null)
        {
            _isDragging = false;
            _miniMapCanvas.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 更新小地图
    /// </summary>
    private void UpdateMiniMap()
    {
        if (_miniMapCanvas == null || TargetCanvas == null)
            return;

        // 清除现有内容
        _miniMapCanvas.Children.Clear();

        // 计算小地图尺寸
        var mapWidth = CanvasSize.Width * ScaleFactor;
        var mapHeight = CanvasSize.Height * ScaleFactor;

        _miniMapCanvas.Width = mapWidth;
        _miniMapCanvas.Height = mapHeight;

        // 绘制节点
        DrawNodes();

        // 更新视口矩形
        UpdateViewportRectangle();
    }

    /// <summary>
    /// 绘制节点
    /// </summary>
    private void DrawNodes()
    {
        if (_miniMapCanvas == null || TargetCanvas == null)
            return;

        foreach (UIElement child in TargetCanvas.Children)
        {
            if (child is FrameworkElement element)
            {
                var left = Canvas.GetLeft(element);
                var top = Canvas.GetTop(element);

                if (double.IsNaN(left)) left = 0;
                if (double.IsNaN(top)) top = 0;

                // 创建小地图中的节点表示
                var miniNode = new Rectangle
                {
                    Width = Math.Max(2, element.ActualWidth * ScaleFactor),
                    Height = Math.Max(2, element.ActualHeight * ScaleFactor),
                    Fill = NodeBrush,
                    Stroke = Brushes.DarkGray,
                    StrokeThickness = 0.5
                };

                Canvas.SetLeft(miniNode, left * ScaleFactor);
                Canvas.SetTop(miniNode, top * ScaleFactor);

                _miniMapCanvas.Children.Add(miniNode);
            }
        }
    }

    /// <summary>
    /// 更新视口矩形
    /// </summary>
    private void UpdateViewportRectangle()
    {
        if (_viewportRectangle == null)
            return;

        var viewportRect = new Rect(
            ViewportPosition.X * ScaleFactor,
            ViewportPosition.Y * ScaleFactor,
            ViewportSize.Width * ScaleFactor,
            ViewportSize.Height * ScaleFactor);

        Canvas.SetLeft(_viewportRectangle, viewportRect.X);
        Canvas.SetTop(_viewportRectangle, viewportRect.Y);
        _viewportRectangle.Width = viewportRect.Width;
        _viewportRectangle.Height = viewportRect.Height;
    }

    /// <summary>
    /// 从小地图位置更新视口
    /// </summary>
    private void UpdateViewportFromMiniMap(Point miniMapPoint)
    {
        // 将小地图坐标转换为画布坐标
        var canvasX = miniMapPoint.X / ScaleFactor;
        var canvasY = miniMapPoint.Y / ScaleFactor;

        // 调整为视口中心
        var newViewportPosition = new Point(
            canvasX - ViewportSize.Width / 2,
            canvasY - ViewportSize.Height / 2);

        ViewportPosition = newViewportPosition;

        // 触发视口变化事件
        var args = new RoutedEventArgs(ViewportChangedEvent, this);
        RaiseEvent(args);
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 刷新小地图
    /// </summary>
    public void Refresh()
    {
        UpdateMiniMap();
    }

    /// <summary>
    /// 设置视口信息
    /// </summary>
    public void SetViewport(Point position, Size size)
    {
        ViewportPosition = position;
        ViewportSize = size;
    }

    #endregion
}
