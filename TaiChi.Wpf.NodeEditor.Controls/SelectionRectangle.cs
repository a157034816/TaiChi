using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TaiChi.Wpf.NodeEditor.Controls;

/// <summary>
/// 选择框控件
/// </summary>
public class SelectionRectangle : Control
{
    #region 依赖属性

    /// <summary>
    /// 起始点
    /// </summary>
    public static readonly DependencyProperty StartPointProperty =
        DependencyProperty.Register(nameof(StartPoint), typeof(Point), typeof(SelectionRectangle),
            new PropertyMetadata(new Point(), OnRectangleChanged));

    /// <summary>
    /// 结束点
    /// </summary>
    public static readonly DependencyProperty EndPointProperty =
        DependencyProperty.Register(nameof(EndPoint), typeof(Point), typeof(SelectionRectangle),
            new PropertyMetadata(new Point(), OnRectangleChanged));

    /// <summary>
    /// 选择框颜色
    /// </summary>
    public static readonly DependencyProperty SelectionBrushProperty =
        DependencyProperty.Register(nameof(SelectionBrush), typeof(Brush), typeof(SelectionRectangle),
            new PropertyMetadata(Brushes.Blue));

    /// <summary>
    /// 选择框边框颜色
    /// </summary>
    public static readonly DependencyProperty SelectionBorderBrushProperty =
        DependencyProperty.Register(nameof(SelectionBorderBrush), typeof(Brush), typeof(SelectionRectangle),
            new PropertyMetadata(Brushes.DarkBlue));

    /// <summary>
    /// 选择框边框粗细
    /// </summary>
    public static readonly DependencyProperty SelectionBorderThicknessProperty =
        DependencyProperty.Register(nameof(SelectionBorderThickness), typeof(Thickness), typeof(SelectionRectangle),
            new PropertyMetadata(new Thickness(1)));

    /// <summary>
    /// 选择框透明度
    /// </summary>
    public static readonly DependencyProperty SelectionOpacityProperty =
        DependencyProperty.Register(nameof(SelectionOpacity), typeof(double), typeof(SelectionRectangle),
            new PropertyMetadata(0.3));

    /// <summary>
    /// 是否显示选择框
    /// </summary>
    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(SelectionRectangle),
            new PropertyMetadata(false, OnIsActiveChanged));

    #endregion

    #region 属性包装器

    /// <summary>
    /// 起始点
    /// </summary>
    public Point StartPoint
    {
        get => (Point)GetValue(StartPointProperty);
        set => SetValue(StartPointProperty, value);
    }

    /// <summary>
    /// 结束点
    /// </summary>
    public Point EndPoint
    {
        get => (Point)GetValue(EndPointProperty);
        set => SetValue(EndPointProperty, value);
    }

    /// <summary>
    /// 选择框颜色
    /// </summary>
    public Brush SelectionBrush
    {
        get => (Brush)GetValue(SelectionBrushProperty);
        set => SetValue(SelectionBrushProperty, value);
    }

    /// <summary>
    /// 选择框边框颜色
    /// </summary>
    public Brush SelectionBorderBrush
    {
        get => (Brush)GetValue(SelectionBorderBrushProperty);
        set => SetValue(SelectionBorderBrushProperty, value);
    }

    /// <summary>
    /// 选择框边框粗细
    /// </summary>
    public Thickness SelectionBorderThickness
    {
        get => (Thickness)GetValue(SelectionBorderThicknessProperty);
        set => SetValue(SelectionBorderThicknessProperty, value);
    }

    /// <summary>
    /// 选择框透明度
    /// </summary>
    public double SelectionOpacity
    {
        get => (double)GetValue(SelectionOpacityProperty);
        set => SetValue(SelectionOpacityProperty, value);
    }

    /// <summary>
    /// 是否显示选择框
    /// </summary>
    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    #endregion

    #region 私有字段

    private Rectangle? _rectangle;

    #endregion

    #region 构造函数

    /// <summary>
    /// 静态构造函数
    /// </summary>
    static SelectionRectangle()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SelectionRectangle),
            new FrameworkPropertyMetadata(typeof(SelectionRectangle)));
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    public SelectionRectangle()
    {
        Visibility = Visibility.Collapsed;
    }

    #endregion

    #region 重写方法

    /// <summary>
    /// 应用模板
    /// </summary>
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _rectangle = GetTemplateChild("PART_SelectionRectangle") as Rectangle;
        UpdateRectangle();
    }

    #endregion

    #region 依赖属性回调

    private static void OnRectangleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SelectionRectangle control)
        {
            control.UpdateRectangle();
        }
    }

    private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SelectionRectangle control)
        {
            control.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
            control.UpdateRectangle();
        }
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 更新矩形
    /// </summary>
    private void UpdateRectangle()
    {
        if (!IsActive)
            return;

        var rect = GetSelectionRect();
        
        Canvas.SetLeft(this, rect.X);
        Canvas.SetTop(this, rect.Y);
        Width = rect.Width;
        Height = rect.Height;

        // 样式由模板绑定提供（TemplateBinding），无需在此设置。
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 获取选择矩形
    /// </summary>
    /// <returns>选择矩形</returns>
    public Rect GetSelectionRect()
    {
        var left = Math.Min(StartPoint.X, EndPoint.X);
        var top = Math.Min(StartPoint.Y, EndPoint.Y);
        var width = Math.Abs(EndPoint.X - StartPoint.X);
        var height = Math.Abs(EndPoint.Y - StartPoint.Y);

        return new Rect(left, top, width, height);
    }

    /// <summary>
    /// 开始选择
    /// </summary>
    /// <param name="startPoint">起始点</param>
    public void StartSelection(Point startPoint)
    {
        StartPoint = startPoint;
        EndPoint = startPoint;
        IsActive = true;
    }

    /// <summary>
    /// 更新选择
    /// </summary>
    /// <param name="currentPoint">当前点</param>
    public void UpdateSelection(Point currentPoint)
    {
        if (IsActive)
        {
            EndPoint = currentPoint;
        }
    }

    /// <summary>
    /// 结束选择
    /// </summary>
    public void EndSelection()
    {
        IsActive = false;
    }

    /// <summary>
    /// 检查点是否在选择框内
    /// </summary>
    /// <param name="point">要检查的点</param>
    /// <returns>如果在选择框内返回true</returns>
    public bool ContainsPoint(Point point)
    {
        if (!IsActive) return false;

        var rect = GetSelectionRect();
        return rect.Contains(point);
    }

    /// <summary>
    /// 检查矩形是否与选择框相交
    /// </summary>
    /// <param name="rect">要检查的矩形</param>
    /// <returns>如果相交返回true</returns>
    public bool IntersectsWith(Rect rect)
    {
        if (!IsActive) return false;

        var selectionRect = GetSelectionRect();
        return selectionRect.IntersectsWith(rect);
    }

    #endregion
}
