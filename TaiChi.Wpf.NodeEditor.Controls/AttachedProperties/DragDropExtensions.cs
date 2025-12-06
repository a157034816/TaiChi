using System.Windows;
using System.Windows.Input;
using TaiChi.Wpf.NodeEditor.Controls.Helpers;
using TaiChi.Wpf.NodeEditor.Core.Registry;

namespace TaiChi.Wpf.NodeEditor.Controls.AttachedProperties;

/// <summary>
/// 拖拽扩展附加属性
/// </summary>
public static class DragDropExtensions
{
    #region EnableDrag 附加属性

    /// <summary>
    /// 是否启用拖拽
    /// </summary>
    public static readonly DependencyProperty EnableDragProperty =
        DependencyProperty.RegisterAttached(
            "EnableDrag",
            typeof(bool),
            typeof(DragDropExtensions),
            new PropertyMetadata(false, OnEnableDragChanged));

    /// <summary>
    /// 获取是否启用拖拽
    /// </summary>
    public static bool GetEnableDrag(DependencyObject obj)
    {
        return (bool)obj.GetValue(EnableDragProperty);
    }

    /// <summary>
    /// 设置是否启用拖拽
    /// </summary>
    public static void SetEnableDrag(DependencyObject obj, bool value)
    {
        obj.SetValue(EnableDragProperty, value);
    }

    private static void OnEnableDragChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement element)
        {
            if ((bool)e.NewValue)
            {
                element.MouseDown += OnMouseDown;
                element.MouseMove += OnMouseMove;
                element.MouseUp += OnMouseUp;
            }
            else
            {
                element.MouseDown -= OnMouseDown;
                element.MouseMove -= OnMouseMove;
                element.MouseUp -= OnMouseUp;
            }
        }
    }

    #endregion

    #region DragData 附加属性

    /// <summary>
    /// 拖拽数据
    /// </summary>
    public static readonly DependencyProperty DragDataProperty =
        DependencyProperty.RegisterAttached(
            "DragData",
            typeof(object),
            typeof(DragDropExtensions),
            new PropertyMetadata(null));

    /// <summary>
    /// 获取拖拽数据
    /// </summary>
    public static object GetDragData(DependencyObject obj)
    {
        return obj.GetValue(DragDataProperty);
    }

    /// <summary>
    /// 设置拖拽数据
    /// </summary>
    public static void SetDragData(DependencyObject obj, object value)
    {
        obj.SetValue(DragDataProperty, value);
    }

    #endregion

    #region DragThreshold 附加属性

    /// <summary>
    /// 拖拽阈值
    /// </summary>
    public static readonly DependencyProperty DragThresholdProperty =
        DependencyProperty.RegisterAttached(
            "DragThreshold",
            typeof(double),
            typeof(DragDropExtensions),
            new PropertyMetadata(5.0));

    /// <summary>
    /// 获取拖拽阈值
    /// </summary>
    public static double GetDragThreshold(DependencyObject obj)
    {
        return (double)obj.GetValue(DragThresholdProperty);
    }

    /// <summary>
    /// 设置拖拽阈值
    /// </summary>
    public static void SetDragThreshold(DependencyObject obj, double value)
    {
        obj.SetValue(DragThresholdProperty, value);
    }

    #endregion

    #region 私有字段

    private static readonly DependencyProperty IsDraggingProperty =
        DependencyProperty.RegisterAttached("IsDragging", typeof(bool), typeof(DragDropExtensions));

    private static readonly DependencyProperty DragStartPointProperty =
        DependencyProperty.RegisterAttached("DragStartPoint", typeof(Point), typeof(DragDropExtensions));

    #endregion

    #region 事件处理

    private static void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is UIElement element && e.LeftButton == MouseButtonState.Pressed)
        {
            element.SetValue(DragStartPointProperty, e.GetPosition(element));
            element.SetValue(IsDraggingProperty, false);
            element.CaptureMouse();
        }
    }

    private static void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is UIElement element && 
            e.LeftButton == MouseButtonState.Pressed && 
            element.IsMouseCaptured)
        {
            var startPoint = (Point)element.GetValue(DragStartPointProperty);
            var currentPoint = e.GetPosition(element);
            var threshold = GetDragThreshold(element);

            if (!((bool)element.GetValue(IsDraggingProperty)) && 
                DragDropHelper.HasMouseMovedEnoughForDrag(startPoint, currentPoint))
            {
                element.SetValue(IsDraggingProperty, true);
                StartDrag(element);
            }
        }
    }

    private static void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is UIElement element)
        {
            element.ReleaseMouseCapture();
            element.SetValue(IsDraggingProperty, false);
        }
    }

    private static void StartDrag(UIElement element)
    {
        var dragData = GetDragData(element);
        
        if (dragData is NodeMetadata metadata)
        {
            DragDropHelper.StartDragNodeMetadata(element, metadata);
        }
        else if (dragData != null)
        {
            var dataObject = new DataObject();
            dataObject.SetData(dragData.GetType(), dragData);
            
            try
            {
                DragDrop.DoDragDrop(element, dataObject, DragDropEffects.Copy);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Drag operation failed: {ex.Message}");
            }
        }
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 手动开始拖拽操作
    /// </summary>
    /// <param name="element">拖拽源元素</param>
    /// <param name="data">拖拽数据</param>
    public static void StartDragOperation(UIElement element, object data)
    {
        if (element == null || data == null) return;

        SetDragData(element, data);
        StartDrag(element);
    }

    /// <summary>
    /// 检查元素是否正在拖拽
    /// </summary>
    /// <param name="element">要检查的元素</param>
    /// <returns>如果正在拖拽返回true</returns>
    public static bool IsElementDragging(UIElement element)
    {
        return element != null && (bool)element.GetValue(IsDraggingProperty);
    }

    #endregion
}
