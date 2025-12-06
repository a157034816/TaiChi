using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TaiChi.Wpf.NodeEditor.Controls.AttachedProperties;
using TaiChi.Wpf.NodeEditor.Core.Registry;

namespace TaiChi.Wpf.NodeEditor.Controls;

/// <summary>
/// 工具箱项控件
/// </summary>
public class ToolboxItem : Control
{
    #region 依赖属性

    /// <summary>
    /// 节点元数据
    /// </summary>
    public static readonly DependencyProperty NodeMetadataProperty =
        DependencyProperty.Register(nameof(NodeMetadata), typeof(NodeMetadata), typeof(ToolboxItem),
            new PropertyMetadata(null, OnNodeMetadataChanged));

    /// <summary>
    /// 是否选中
    /// </summary>
    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(ToolboxItem),
            new PropertyMetadata(false));

    /// <summary>
    /// 是否高亮
    /// </summary>
    public static readonly DependencyProperty IsHighlightedProperty =
        DependencyProperty.Register(nameof(IsHighlighted), typeof(bool), typeof(ToolboxItem),
            new PropertyMetadata(false));

    /// <summary>
    /// 项背景色
    /// </summary>
    public static readonly DependencyProperty ItemBackgroundProperty =
        DependencyProperty.Register(nameof(ItemBackground), typeof(Brush), typeof(ToolboxItem),
            new PropertyMetadata(Brushes.Transparent));

    /// <summary>
    /// 项边框色
    /// </summary>
    public static readonly DependencyProperty ItemBorderBrushProperty =
        DependencyProperty.Register(nameof(ItemBorderBrush), typeof(Brush), typeof(ToolboxItem),
            new PropertyMetadata(Brushes.Transparent));

    /// <summary>
    /// 项边框粗细
    /// </summary>
    public static readonly DependencyProperty ItemBorderThicknessProperty =
        DependencyProperty.Register(nameof(ItemBorderThickness), typeof(Thickness), typeof(ToolboxItem),
            new PropertyMetadata(new Thickness(1)));

    /// <summary>
    /// 是否允许拖拽
    /// </summary>
    public static readonly DependencyProperty AllowDragProperty =
        DependencyProperty.Register(nameof(AllowDrag), typeof(bool), typeof(ToolboxItem),
            new PropertyMetadata(true));

    /// <summary>
    /// 是否允许双击创建
    /// </summary>
    public static readonly DependencyProperty AllowDoubleClickCreateProperty =
        DependencyProperty.Register(nameof(AllowDoubleClickCreate), typeof(bool), typeof(ToolboxItem),
            new PropertyMetadata(true));

    #endregion

    #region 路由事件

    /// <summary>
    /// 项点击事件
    /// </summary>
    public static readonly RoutedEvent ItemClickEvent = EventManager.RegisterRoutedEvent(
        "ItemClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ToolboxItem));

    /// <summary>
    /// 项选择变化事件
    /// </summary>
    public static readonly RoutedEvent ItemSelectionChangedEvent = EventManager.RegisterRoutedEvent(
        "ItemSelectionChanged", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ToolboxItem));

    /// <summary>
    /// 项双击事件
    /// </summary>
    public static readonly RoutedEvent ItemDoubleClickEvent = EventManager.RegisterRoutedEvent(
        "ItemDoubleClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ToolboxItem));

    /// <summary>
    /// 节点创建请求事件
    /// </summary>
    public static readonly RoutedEvent NodeCreationRequestedEvent = EventManager.RegisterRoutedEvent(
        "NodeCreationRequested", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ToolboxItem));

    #endregion

    #region 属性包装器

    /// <summary>
    /// 节点元数据
    /// </summary>
    public NodeMetadata NodeMetadata
    {
        get => (NodeMetadata)GetValue(NodeMetadataProperty);
        set => SetValue(NodeMetadataProperty, value);
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
    /// 项背景色
    /// </summary>
    public Brush ItemBackground
    {
        get => (Brush)GetValue(ItemBackgroundProperty);
        set => SetValue(ItemBackgroundProperty, value);
    }

    /// <summary>
    /// 项边框色
    /// </summary>
    public Brush ItemBorderBrush
    {
        get => (Brush)GetValue(ItemBorderBrushProperty);
        set => SetValue(ItemBorderBrushProperty, value);
    }

    /// <summary>
    /// 项边框粗细
    /// </summary>
    public Thickness ItemBorderThickness
    {
        get => (Thickness)GetValue(ItemBorderThicknessProperty);
        set => SetValue(ItemBorderThicknessProperty, value);
    }

    /// <summary>
    /// 是否允许拖拽
    /// </summary>
    public bool AllowDrag
    {
        get => (bool)GetValue(AllowDragProperty);
        set => SetValue(AllowDragProperty, value);
    }

    /// <summary>
    /// 是否允许双击创建
    /// </summary>
    public bool AllowDoubleClickCreate
    {
        get => (bool)GetValue(AllowDoubleClickCreateProperty);
        set => SetValue(AllowDoubleClickCreateProperty, value);
    }

    #endregion

    #region 事件包装器

    /// <summary>
    /// 项点击事件
    /// </summary>
    public event RoutedEventHandler ItemClick
    {
        add { AddHandler(ItemClickEvent, value); }
        remove { RemoveHandler(ItemClickEvent, value); }
    }

    /// <summary>
    /// 项选择变化事件
    /// </summary>
    public event RoutedEventHandler ItemSelectionChanged
    {
        add { AddHandler(ItemSelectionChangedEvent, value); }
        remove { RemoveHandler(ItemSelectionChangedEvent, value); }
    }

    /// <summary>
    /// 项双击事件
    /// </summary>
    public event RoutedEventHandler ItemDoubleClick
    {
        add { AddHandler(ItemDoubleClickEvent, value); }
        remove { RemoveHandler(ItemDoubleClickEvent, value); }
    }

    /// <summary>
    /// 节点创建请求事件
    /// </summary>
    public event RoutedEventHandler NodeCreationRequested
    {
        add { AddHandler(NodeCreationRequestedEvent, value); }
        remove { RemoveHandler(NodeCreationRequestedEvent, value); }
    }

    #endregion

    #region 构造函数

    /// <summary>
    /// 静态构造函数
    /// </summary>
    static ToolboxItem()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ToolboxItem),
            new FrameworkPropertyMetadata(typeof(ToolboxItem)));
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    public ToolboxItem()
    {
        // 设置拖拽支持
        DragDropExtensions.SetEnableDrag(this, true);
    }

    #endregion

    #region 依赖属性回调

    private static void OnNodeMetadataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ToolboxItem item && e.NewValue is NodeMetadata metadata)
        {
            // 设置拖拽数据
            DragDropExtensions.SetDragData(item, metadata);
            
            // 设置工具提示
            item.ToolTip = string.IsNullOrEmpty(metadata.Description) 
                ? metadata.Name 
                : $"{metadata.Name}\n{metadata.Description}";
        }
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
                // 单击选择
                IsSelected = true;

                var clickArgs = new RoutedEventArgs(ItemClickEvent, this);
                RaiseEvent(clickArgs);

                var selectionArgs = new RoutedEventArgs(ItemSelectionChangedEvent, this);
                RaiseEvent(selectionArgs);
            }
            else if (e.ClickCount == 2 && AllowDoubleClickCreate)
            {
                // 双击创建节点
                var doubleClickArgs = new RoutedEventArgs(ItemDoubleClickEvent, this);
                RaiseEvent(doubleClickArgs);

                var creationArgs = new RoutedEventArgs(NodeCreationRequestedEvent, this);
                RaiseEvent(creationArgs);
            }

            // 不设置e.Handled = true，让DragDropExtensions也能接收到鼠标事件
        }
    }

    /// <summary>
    /// 重写鼠标进入事件处理
    /// </summary>
    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        IsHighlighted = true;
    }

    /// <summary>
    /// 重写鼠标离开事件处理
    /// </summary>
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        IsHighlighted = false;
    }

    #endregion
}
