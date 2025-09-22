using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TaiChi.Wpf.NodeEditor.Controls.AttachedProperties;

/// <summary>
/// Canvas 扩展附加属性
/// </summary>
public static class CanvasExtensions
{
    #region SnapToGrid 附加属性

    /// <summary>
    /// 是否启用网格对齐
    /// </summary>
    public static readonly DependencyProperty SnapToGridProperty =
        DependencyProperty.RegisterAttached(
            "SnapToGrid",
            typeof(bool),
            typeof(CanvasExtensions),
            new PropertyMetadata(false));

    /// <summary>
    /// 获取是否启用网格对齐
    /// </summary>
    public static bool GetSnapToGrid(DependencyObject obj)
    {
        return (bool)obj.GetValue(SnapToGridProperty);
    }

    /// <summary>
    /// 设置是否启用网格对齐
    /// </summary>
    public static void SetSnapToGrid(DependencyObject obj, bool value)
    {
        obj.SetValue(SnapToGridProperty, value);
    }

    #endregion

    #region GridSize 附加属性

    /// <summary>
    /// 网格大小
    /// </summary>
    public static readonly DependencyProperty GridSizeProperty =
        DependencyProperty.RegisterAttached(
            "GridSize",
            typeof(double),
            typeof(CanvasExtensions),
            new PropertyMetadata(20.0));

    /// <summary>
    /// 获取网格大小
    /// </summary>
    public static double GetGridSize(DependencyObject obj)
    {
        return (double)obj.GetValue(GridSizeProperty);
    }

    /// <summary>
    /// 设置网格大小
    /// </summary>
    public static void SetGridSize(DependencyObject obj, double value)
    {
        obj.SetValue(GridSizeProperty, value);
    }

    #endregion

    #region IsSelectable 附加属性

    /// <summary>
    /// 元素是否可选择
    /// </summary>
    public static readonly DependencyProperty IsSelectableProperty =
        DependencyProperty.RegisterAttached(
            "IsSelectable",
            typeof(bool),
            typeof(CanvasExtensions),
            new PropertyMetadata(true));

    /// <summary>
    /// 获取元素是否可选择
    /// </summary>
    public static bool GetIsSelectable(DependencyObject obj)
    {
        return (bool)obj.GetValue(IsSelectableProperty);
    }

    /// <summary>
    /// 设置元素是否可选择
    /// </summary>
    public static void SetIsSelectable(DependencyObject obj, bool value)
    {
        obj.SetValue(IsSelectableProperty, value);
    }

    #endregion

    #region IsDraggable 附加属性

    /// <summary>
    /// 元素是否可拖拽
    /// </summary>
    public static readonly DependencyProperty IsDraggableProperty =
        DependencyProperty.RegisterAttached(
            "IsDraggable",
            typeof(bool),
            typeof(CanvasExtensions),
            new PropertyMetadata(true));

    /// <summary>
    /// 获取元素是否可拖拽
    /// </summary>
    public static bool GetIsDraggable(DependencyObject obj)
    {
        return (bool)obj.GetValue(IsDraggableProperty);
    }

    /// <summary>
    /// 设置元素是否可拖拽
    /// </summary>
    public static void SetIsDraggable(DependencyObject obj, bool value)
    {
        obj.SetValue(IsDraggableProperty, value);
    }

    #endregion

    #region ZIndex 扩展

    /// <summary>
    /// 将元素置于最前
    /// </summary>
    public static void BringToFront(UIElement element)
    {
        if (VisualTreeHelper.GetParent(element) is Canvas canvas)
        {
            var maxZIndex = 0;
            foreach (UIElement child in canvas.Children)
            {
                var zIndex = Panel.GetZIndex(child);
                if (zIndex > maxZIndex)
                    maxZIndex = zIndex;
            }
            Panel.SetZIndex(element, maxZIndex + 1);
        }
    }

    /// <summary>
    /// 将元素置于最后
    /// </summary>
    public static void SendToBack(UIElement element)
    {
        if (VisualTreeHelper.GetParent(element) is Canvas canvas)
        {
            var minZIndex = 0;
            foreach (UIElement child in canvas.Children)
            {
                var zIndex = Panel.GetZIndex(child);
                if (zIndex < minZIndex)
                    minZIndex = zIndex;
            }
            Panel.SetZIndex(element, minZIndex - 1);
        }
    }

    #endregion

    #region 位置辅助方法

    /// <summary>
    /// 获取元素在Canvas中的位置
    /// </summary>
    public static Point GetPosition(UIElement element)
    {
        var left = Canvas.GetLeft(element);
        var top = Canvas.GetTop(element);
        
        if (double.IsNaN(left)) left = 0;
        if (double.IsNaN(top)) top = 0;
        
        return new Point(left, top);
    }

    /// <summary>
    /// 设置元素在Canvas中的位置
    /// </summary>
    public static void SetPosition(UIElement element, Point position)
    {
        Canvas.SetLeft(element, position.X);
        Canvas.SetTop(element, position.Y);
    }

    /// <summary>
    /// 设置元素在Canvas中的位置（带网格对齐）
    /// </summary>
    public static void SetPosition(UIElement element, Point position, bool snapToGrid, double gridSize = 20.0)
    {
        if (snapToGrid && gridSize > 0)
        {
            var x = Math.Round(position.X / gridSize) * gridSize;
            var y = Math.Round(position.Y / gridSize) * gridSize;
            position = new Point(x, y);
        }
        
        SetPosition(element, position);
    }

    /// <summary>
    /// 获取元素的边界矩形
    /// </summary>
    public static Rect GetBounds(FrameworkElement element)
    {
        var position = GetPosition(element);
        return new Rect(position.X, position.Y, element.ActualWidth, element.ActualHeight);
    }

    #endregion
}
