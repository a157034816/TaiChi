using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TaiChi.Wpf.NodeEditor.Controls;

/// <summary>
/// 网格背景控件
/// </summary>
public class GridBackground : Control
{
    #region 依赖属性

    /// <summary>
    /// 网格大小
    /// </summary>
    public static readonly DependencyProperty GridSizeProperty =
        DependencyProperty.Register(nameof(GridSize), typeof(double), typeof(GridBackground),
            new PropertyMetadata(20.0, OnGridPropertyChanged));

    /// <summary>
    /// 网格线颜色
    /// </summary>
    public static readonly DependencyProperty GridBrushProperty =
        DependencyProperty.Register(nameof(GridBrush), typeof(Brush), typeof(GridBackground),
            new PropertyMetadata(Brushes.LightGray, OnGridPropertyChanged));

    /// <summary>
    /// 网格线粗细
    /// </summary>
    public static readonly DependencyProperty GridThicknessProperty =
        DependencyProperty.Register(nameof(GridThickness), typeof(double), typeof(GridBackground),
            new PropertyMetadata(0.5, OnGridPropertyChanged));

    /// <summary>
    /// 主网格线间隔（每隔多少个小网格显示一条主网格线）
    /// </summary>
    public static readonly DependencyProperty MajorGridIntervalProperty =
        DependencyProperty.Register(nameof(MajorGridInterval), typeof(int), typeof(GridBackground),
            new PropertyMetadata(5, OnGridPropertyChanged));

    /// <summary>
    /// 主网格线颜色
    /// </summary>
    public static readonly DependencyProperty MajorGridBrushProperty =
        DependencyProperty.Register(nameof(MajorGridBrush), typeof(Brush), typeof(GridBackground),
            new PropertyMetadata(Brushes.Gray, OnGridPropertyChanged));

    /// <summary>
    /// 主网格线粗细
    /// </summary>
    public static readonly DependencyProperty MajorGridThicknessProperty =
        DependencyProperty.Register(nameof(MajorGridThickness), typeof(double), typeof(GridBackground),
            new PropertyMetadata(1.0, OnGridPropertyChanged));

    /// <summary>
    /// 是否显示主网格线
    /// </summary>
    public static readonly DependencyProperty ShowMajorGridProperty =
        DependencyProperty.Register(nameof(ShowMajorGrid), typeof(bool), typeof(GridBackground),
            new PropertyMetadata(true, OnGridPropertyChanged));

    /// <summary>
    /// 缩放级别
    /// </summary>
    public static readonly DependencyProperty ZoomLevelProperty =
        DependencyProperty.Register(nameof(ZoomLevel), typeof(double), typeof(GridBackground),
            new PropertyMetadata(1.0, OnGridPropertyChanged));

    #endregion

    #region 属性包装器

    /// <summary>
    /// 网格大小
    /// </summary>
    public double GridSize
    {
        get => (double)GetValue(GridSizeProperty);
        set => SetValue(GridSizeProperty, value);
    }

    /// <summary>
    /// 网格线颜色
    /// </summary>
    public Brush GridBrush
    {
        get => (Brush)GetValue(GridBrushProperty);
        set => SetValue(GridBrushProperty, value);
    }

    /// <summary>
    /// 网格线粗细
    /// </summary>
    public double GridThickness
    {
        get => (double)GetValue(GridThicknessProperty);
        set => SetValue(GridThicknessProperty, value);
    }

    /// <summary>
    /// 主网格线间隔
    /// </summary>
    public int MajorGridInterval
    {
        get => (int)GetValue(MajorGridIntervalProperty);
        set => SetValue(MajorGridIntervalProperty, value);
    }

    /// <summary>
    /// 主网格线颜色
    /// </summary>
    public Brush MajorGridBrush
    {
        get => (Brush)GetValue(MajorGridBrushProperty);
        set => SetValue(MajorGridBrushProperty, value);
    }

    /// <summary>
    /// 主网格线粗细
    /// </summary>
    public double MajorGridThickness
    {
        get => (double)GetValue(MajorGridThicknessProperty);
        set => SetValue(MajorGridThicknessProperty, value);
    }

    /// <summary>
    /// 是否显示主网格线
    /// </summary>
    public bool ShowMajorGrid
    {
        get => (bool)GetValue(ShowMajorGridProperty);
        set => SetValue(ShowMajorGridProperty, value);
    }

    /// <summary>
    /// 缩放级别
    /// </summary>
    public double ZoomLevel
    {
        get => (double)GetValue(ZoomLevelProperty);
        set => SetValue(ZoomLevelProperty, value);
    }

    #endregion

    #region 构造函数

    /// <summary>
    /// 静态构造函数
    /// </summary>
    static GridBackground()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(GridBackground),
            new FrameworkPropertyMetadata(typeof(GridBackground)));
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    public GridBackground()
    {
        IsHitTestVisible = false;
        UpdateBackground();
    }

    #endregion

    #region 依赖属性回调

    private static void OnGridPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GridBackground control)
        {
            control.UpdateBackground();
        }
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 更新背景
    /// </summary>
    private void UpdateBackground()
    {
        var scaledGridSize = GridSize * ZoomLevel;
        
        // 如果网格太小，不显示
        if (scaledGridSize < 2)
        {
            Background = null;
            return;
        }

        var drawingGroup = new DrawingGroup();

        // 绘制小网格线
        var minorGridDrawing = CreateGridDrawing(scaledGridSize, GridBrush, GridThickness);
        drawingGroup.Children.Add(minorGridDrawing);

        // 绘制主网格线
        if (ShowMajorGrid && MajorGridInterval > 1)
        {
            var majorGridSize = scaledGridSize * MajorGridInterval;
            var majorGridDrawing = CreateGridDrawing(majorGridSize, MajorGridBrush, MajorGridThickness);
            drawingGroup.Children.Add(majorGridDrawing);
        }

        var drawingBrush = new DrawingBrush(drawingGroup)
        {
            TileMode = TileMode.Tile,
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, scaledGridSize, scaledGridSize)
        };

        Background = drawingBrush;
    }

    /// <summary>
    /// 创建网格绘图
    /// </summary>
    /// <param name="gridSize">网格大小</param>
    /// <param name="brush">画刷</param>
    /// <param name="thickness">线条粗细</param>
    /// <returns>网格绘图</returns>
    private static GeometryDrawing CreateGridDrawing(double gridSize, Brush brush, double thickness)
    {
        var geometry = new GeometryGroup();
        
        // 垂直线
        var verticalLine = new LineGeometry(new Point(0, 0), new Point(0, gridSize));
        geometry.Children.Add(verticalLine);
        
        // 水平线
        var horizontalLine = new LineGeometry(new Point(0, 0), new Point(gridSize, 0));
        geometry.Children.Add(horizontalLine);

        return new GeometryDrawing
        {
            Geometry = geometry,
            Pen = new Pen(brush, thickness)
        };
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 将屏幕坐标对齐到网格
    /// </summary>
    /// <param name="point">屏幕坐标</param>
    /// <returns>对齐后的坐标</returns>
    public Point SnapToGrid(Point point)
    {
        var scaledGridSize = GridSize * ZoomLevel;
        
        if (scaledGridSize <= 0)
            return point;

        var x = Math.Round(point.X / scaledGridSize) * scaledGridSize;
        var y = Math.Round(point.Y / scaledGridSize) * scaledGridSize;
        
        return new Point(x, y);
    }

    /// <summary>
    /// 检查是否应该显示网格
    /// </summary>
    /// <returns>如果应该显示网格返回true</returns>
    public bool ShouldShowGrid()
    {
        return GridSize * ZoomLevel >= 2;
    }

    #endregion
}
