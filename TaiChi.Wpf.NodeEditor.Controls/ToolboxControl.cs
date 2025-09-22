using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TaiChi.Wpf.NodeEditor.Core.Registry;

namespace TaiChi.Wpf.NodeEditor.Controls;

/// <summary>
/// 工具箱控件 - 重构为 Control 以实现完全解耦
/// </summary>
public class ToolboxControl : Control
{
    #region 依赖属性

    /// <summary>
    /// 树形结构的根节点集合
    /// </summary>
    public static readonly DependencyProperty RootItemsProperty =
        DependencyProperty.Register(nameof(RootItems), typeof(IEnumerable), typeof(ToolboxControl),
            new PropertyMetadata(null, OnRootItemsChanged));

    /// <summary>
    /// 搜索文本
    /// </summary>
    public static readonly DependencyProperty SearchTextProperty =
        DependencyProperty.Register(nameof(SearchText), typeof(string), typeof(ToolboxControl),
            new PropertyMetadata(string.Empty, OnSearchTextChanged));

    /// <summary>
    /// 是否显示搜索框
    /// </summary>
    public static readonly DependencyProperty ShowSearchProperty =
        DependencyProperty.Register(nameof(ShowSearch), typeof(bool), typeof(ToolboxControl),
            new PropertyMetadata(true));

    /// <summary>
    /// 是否允许拖拽
    /// </summary>
    public static readonly DependencyProperty AllowDragProperty =
        DependencyProperty.Register(nameof(AllowDrag), typeof(bool), typeof(ToolboxControl),
            new PropertyMetadata(true));

    /// <summary>
    /// 是否允许双击创建
    /// </summary>
    public static readonly DependencyProperty AllowDoubleClickCreateProperty =
        DependencyProperty.Register(nameof(AllowDoubleClickCreate), typeof(bool), typeof(ToolboxControl),
            new PropertyMetadata(true));

    /// <summary>
    /// 工具箱布局方向
    /// </summary>
    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(ToolboxControl),
            new PropertyMetadata(Orientation.Vertical));

    /// <summary>
    /// 节点项模板
    /// </summary>
    public static readonly DependencyProperty NodeItemTemplateProperty =
        DependencyProperty.Register(nameof(NodeItemTemplate), typeof(DataTemplate), typeof(ToolboxControl),
            new PropertyMetadata(null));

    /// <summary>
    /// 分类头部模板
    /// </summary>
    public static readonly DependencyProperty CategoryHeaderTemplateProperty =
        DependencyProperty.Register(nameof(CategoryHeaderTemplate), typeof(DataTemplate), typeof(ToolboxControl),
            new PropertyMetadata(null));

    /// <summary>
    /// 是否显示分类计数
    /// </summary>
    public static readonly DependencyProperty ShowCategoryCountProperty =
        DependencyProperty.Register(nameof(ShowCategoryCount), typeof(bool), typeof(ToolboxControl),
            new PropertyMetadata(true));

    /// <summary>
    /// 目标 NodeCanvas（用于在控件层转发创建请求）
    /// </summary>
    public static readonly DependencyProperty TargetCanvasProperty =
        DependencyProperty.Register(nameof(TargetCanvas), typeof(NodeCanvas), typeof(ToolboxControl),
            new PropertyMetadata(null));

    #endregion

    #region 属性包装器

    /// <summary>
    /// 树形结构的根节点集合
    /// </summary>
    public IEnumerable RootItems
    {
        get => (IEnumerable)GetValue(RootItemsProperty);
        set => SetValue(RootItemsProperty, value);
    }

    /// <summary>
    /// 搜索文本
    /// </summary>
    public string SearchText
    {
        get => (string)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    /// <summary>
    /// 是否显示搜索框
    /// </summary>
    public bool ShowSearch
    {
        get => (bool)GetValue(ShowSearchProperty);
        set => SetValue(ShowSearchProperty, value);
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

    /// <summary>
    /// 工具箱布局方向
    /// </summary>
    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>
    /// 节点项模板
    /// </summary>
    public DataTemplate NodeItemTemplate
    {
        get => (DataTemplate)GetValue(NodeItemTemplateProperty);
        set => SetValue(NodeItemTemplateProperty, value);
    }

    /// <summary>
    /// 分类头部模板
    /// </summary>
    public DataTemplate CategoryHeaderTemplate
    {
        get => (DataTemplate)GetValue(CategoryHeaderTemplateProperty);
        set => SetValue(CategoryHeaderTemplateProperty, value);
    }

    /// <summary>
    /// 是否显示分类计数
    /// </summary>
    public bool ShowCategoryCount
    {
        get => (bool)GetValue(ShowCategoryCountProperty);
        set => SetValue(ShowCategoryCountProperty, value);
    }

    /// <summary>
    /// 绑定的目标画布
    /// </summary>
    public NodeCanvas? TargetCanvas
    {
        get => (NodeCanvas?)GetValue(TargetCanvasProperty);
        set => SetValue(TargetCanvasProperty, value);
    }

    #endregion

    #region 依赖属性回调

    private static void OnRootItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ToolboxControl control)
        {
            control.OnRootItemsChanged();
        }
    }

    private static void OnSearchTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ToolboxControl control)
        {
            control.OnSearchTextChanged();
        }
    }

    #endregion

    #region 路由事件

    /// <summary>
    /// 节点拖拽开始事件
    /// </summary>
    public static readonly RoutedEvent NodeDragStartedEvent = EventManager.RegisterRoutedEvent(
        "NodeDragStarted", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ToolboxControl));

    /// <summary>
    /// 节点选择事件
    /// </summary>
    public static readonly RoutedEvent NodeSelectedEvent = EventManager.RegisterRoutedEvent(
        "NodeSelected", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ToolboxControl));

    /// <summary>
    /// 节点双击事件
    /// </summary>
    public static readonly RoutedEvent NodeDoubleClickEvent = EventManager.RegisterRoutedEvent(
        "NodeDoubleClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ToolboxControl));

    /// <summary>
    /// 搜索文本变化事件
    /// </summary>
    public static readonly RoutedEvent SearchTextChangedEvent = EventManager.RegisterRoutedEvent(
        "SearchTextChanged", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ToolboxControl));

    /// <summary>
    /// 节点类别展开/折叠事件
    /// </summary>
    public static readonly RoutedEvent CategoryExpandedChangedEvent = EventManager.RegisterRoutedEvent(
        "CategoryExpandedChanged", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ToolboxControl));

    /// <summary>
    /// 节点过滤变化事件
    /// </summary>
    public static readonly RoutedEvent NodeFilterChangedEvent = EventManager.RegisterRoutedEvent(
        "NodeFilterChanged", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ToolboxControl));

    /// <summary>
    /// 工具箱布局变化事件
    /// </summary>
    public static readonly RoutedEvent LayoutChangedEvent = EventManager.RegisterRoutedEvent(
        "LayoutChanged", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ToolboxControl));



    #endregion

    #region 事件包装器

    /// <summary>
    /// 节点拖拽开始事件
    /// </summary>
    public event RoutedEventHandler NodeDragStarted
    {
        add { AddHandler(NodeDragStartedEvent, value); }
        remove { RemoveHandler(NodeDragStartedEvent, value); }
    }

    /// <summary>
    /// 节点选择事件
    /// </summary>
    public event RoutedEventHandler NodeSelected
    {
        add { AddHandler(NodeSelectedEvent, value); }
        remove { RemoveHandler(NodeSelectedEvent, value); }
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
    /// 搜索文本变化事件
    /// </summary>
    public event RoutedEventHandler SearchTextChanged
    {
        add { AddHandler(SearchTextChangedEvent, value); }
        remove { RemoveHandler(SearchTextChangedEvent, value); }
    }

    #endregion

    /// <summary>
    /// 最近一次命中的节点元数据（供外部读取）
    /// </summary>
    public NodeMetadata? LastHitNodeMetadata { get; private set; }

    #region 私有字段

    /// <summary>
    /// 拖拽相关字段
    /// </summary>
    private bool _isDragging;
    private Point _dragStartPoint;
    private NodeMetadata? _draggedNode;

    // 模板元素
    private TextBox? _searchTextBox;
    private TreeView? _treeView;

    #endregion

    #region 构造函数和静态构造函数

    /// <summary>
    /// 静态构造函数
    /// </summary>
    static ToolboxControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ToolboxControl),
            new FrameworkPropertyMetadata(typeof(ToolboxControl)));
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    public ToolboxControl()
    {
        // 初始化默认数据
        // Note: 实际的数据绑定将在ViewModel层面通过RootItems属性完成
        // 这里保留向后兼容，但主要使用TreeViewModel
    }

    /// <summary>
    /// 应用模板时的处理
    /// </summary>
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        // 获取模板元素
        _searchTextBox = GetTemplateChild("PART_SearchTextBox") as TextBox;
_treeView = GetTemplateChild("PART_TreeView") as TreeView;

        // 绑定搜索框事件
        if (_searchTextBox != null)
        {
            _searchTextBox.TextChanged += OnSearchTextBoxChanged;
        }

        // 监听子项的双击与创建请求事件，并在控件层转发为 NodeDoubleClick
        AddHandler(ToolboxItem.ItemDoubleClickEvent, new RoutedEventHandler(OnToolboxItemDoubleClick));
        AddHandler(ToolboxItem.NodeCreationRequestedEvent, new RoutedEventHandler(OnToolboxItemDoubleClick));
    }

    #endregion

    #region 选择附加属性

    /// <summary>
    /// 工具箱项是否被选中（附加属性）
    /// </summary>
    public static readonly DependencyProperty IsItemSelectedProperty = DependencyProperty.RegisterAttached(
        "IsItemSelected", typeof(bool), typeof(ToolboxControl),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

    public static bool GetIsItemSelected(DependencyObject obj) => (bool)obj.GetValue(IsItemSelectedProperty);
    public static void SetIsItemSelected(DependencyObject obj, bool value) => obj.SetValue(IsItemSelectedProperty, value);

    #endregion



    #region 事件处理

    /// <summary>
    /// 根节点数据变化处理
    /// </summary>
    private void OnRootItemsChanged()
    {
        // 处理根节点数据变化
    }

    /// <summary>
    /// 搜索文本变化处理
    /// </summary>
    private void OnSearchTextChanged()
    {
        ApplySearchFilter();

        var args = new RoutedEventArgs(SearchTextChangedEvent, this);
        RaiseEvent(args);
    }

    /// <summary>
    /// 搜索文本框文本变化处理
    /// </summary>
    private void OnSearchTextBoxChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            SearchText = textBox.Text;
        }
    }

    /// <summary>
    /// 子项双击或创建请求 => 转发为 ToolboxControl 的 NodeDoubleClick，并记录 LastHitNodeMetadata
    /// </summary>
    private void OnToolboxItemDoubleClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is ToolboxItem item && item.NodeMetadata != null)
        {
            LastHitNodeMetadata = item.NodeMetadata;

            // 如果绑定了目标画布，则在控件层向画布转发创建请求（位置为视口中心，逻辑坐标）
            if (TargetCanvas != null)
            {
                try
                {
                    var center = TargetCanvas.GetViewportCenterLogicalPoint();
                    var centerPoint = new System.Windows.Point(center.X, center.Y);
                    var createArgs = new NodeCreationRequestedEventArgs(NodeCanvas.NodeCreationRequestedEvent, TargetCanvas)
                    {
                        NodeMetadata = item.NodeMetadata,
                        Position = centerPoint
                    };
                    TargetCanvas.RaiseEvent(createArgs);
                }
                catch { }
            }

            // 同时抛出双击事件，作为应用层扩展的接口
            var doubleClickArgs = new RoutedEventArgs(NodeDoubleClickEvent, this);
            RaiseEvent(doubleClickArgs);
        }
    }

    /// <summary>
    /// 重写鼠标按下事件处理
    /// </summary>
    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);

        if (!AllowDrag && !AllowDoubleClickCreate) return;

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            var nodeMetadata = GetNodeMetadataFromPoint(e.GetPosition(this));
            LastHitNodeMetadata = nodeMetadata;
            if (nodeMetadata != null)
            {
                System.Diagnostics.Debug.WriteLine($"ToolboxControl OnMouseDown: {nodeMetadata.Name}, ClickCount: {e.ClickCount}");

                if (e.ClickCount == 2 && AllowDoubleClickCreate)
                {
                    // 双击创建节点
                    System.Diagnostics.Debug.WriteLine("Raising NodeDoubleClickEvent");
                    var doubleClickArgs = new RoutedEventArgs(NodeDoubleClickEvent, this);
                    RaiseEvent(doubleClickArgs);
                    e.Handled = true;
                    return;
                }

                // 不再在ToolboxControl层面处理拖拽，让ToolboxItem的DragDropExtensions处理
                // 只处理选择逻辑
                LastHitNodeMetadata = nodeMetadata;

                // 不设置e.Handled = true，让事件继续传递给ToolboxItem
            }
        }
    }

    /// <summary>
    /// 重写鼠标移动事件处理
    /// </summary>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        // 不再在ToolboxControl层面处理拖拽移动
        // 让ToolboxItem的DragDropExtensions处理
    }

    /// <summary>
    /// 重写鼠标释放事件处理
    /// </summary>
    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);

        // 不再在ToolboxControl层面处理拖拽释放
        // 让ToolboxItem的DragDropExtensions处理
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 开始拖放操作
    /// </summary>
    /// <param name="nodeMetadata">节点元数据</param>
    private void StartDragDrop(NodeMetadata nodeMetadata)
    {
        _isDragging = true;

        // 触发节点拖拽开始事件
        var args = new RoutedEventArgs(NodeDragStartedEvent, this);
        RaiseEvent(args);

        // 创建拖拽数据
        var dataObject = new DataObject();
        dataObject.SetData(typeof(NodeMetadata), nodeMetadata);
        dataObject.SetData(DataFormats.Text, nodeMetadata.Name);

        try
        {
            // 开始拖放操作
            var result = DragDrop.DoDragDrop(this, dataObject, DragDropEffects.Copy);
        }
        catch (Exception ex)
        {
            // 处理拖放异常，记录日志但不中断程序
            System.Diagnostics.Debug.WriteLine($"Drag drop failed: {ex.Message}");
        }
        finally
        {
            // 清理状态
            _isDragging = false;
            _draggedNode = null;
            Mouse.Capture(null);
        }
    }

    /// <summary>
    /// 创建拖拽时的视觉效果
    /// </summary>
    /// <param name="nodeMetadata">节点元数据</param>
    /// <returns>拖拽视觉控件</returns>
    private FrameworkElement CreateDragVisual(NodeMetadata nodeMetadata)
    {
        var border = new Border
        {
            Background = System.Windows.Media.Brushes.LightBlue,
            BorderBrush = System.Windows.Media.Brushes.Blue,
            BorderThickness = new Thickness(2),
            Padding = new Thickness(8, 4, 8, 4),
            CornerRadius = new CornerRadius(3),
            Opacity = 0.8
        };

        var stackPanel = new StackPanel();

        var nameText = new TextBlock
        {
            Text = nodeMetadata.Name,
            FontWeight = FontWeights.Bold,
            FontSize = 11,
            Foreground = System.Windows.Media.Brushes.Black
        };
        stackPanel.Children.Add(nameText);

        if (!string.IsNullOrEmpty(nodeMetadata.Description))
        {
            var descText = new TextBlock
            {
                Text = nodeMetadata.Description,
                FontSize = 10,
                Foreground = System.Windows.Media.Brushes.Gray,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 150
            };
            stackPanel.Children.Add(descText);
        }

        border.Child = stackPanel;
        return border;
    }

    /// <summary>
    /// 根据点击位置获取节点元数据
    /// </summary>
    /// <param name="position">点击位置</param>
    /// <returns>节点元数据</returns>
    public NodeMetadata? GetNodeMetadataFromPoint(Point position)
    {
        var hitTest = VisualTreeHelper.HitTest(this, position);
        if (hitTest?.VisualHit is DependencyObject hit)
        {
            // 向上查找包含NodeMetadata的元素
            var current = hit;
            while (current != null)
            {
                if (current is FrameworkElement element && element.DataContext is NodeMetadata metadata)
                {
                    return metadata;
                }
                current = VisualTreeHelper.GetParent(current);
            }
        }
        return null;
    }

    /// <summary>
    /// 应用搜索过滤
    /// </summary>
    private void ApplySearchFilter()
    {
        // 搜索过滤逻辑可以在模板的绑定中实现
        // 或者通过CollectionViewSource来实现
    }

    /// <summary>
    /// 获取所有节点元数据
    /// </summary>
    /// <returns>节点元数据列表</returns>
    public IEnumerable<NodeMetadata> GetAllNodeMetadata()
    {
        // 使用NodeRegistry.AllNodes获取所有节点元数据
        return NodeRegistry.AllNodes;
    }

    /// <summary>
    /// 根据搜索文本过滤节点
    /// </summary>
    /// <param name="searchText">搜索文本</param>
    /// <returns>过滤后的节点元数据</returns>
    public IEnumerable<NodeMetadata> FilterNodes(string searchText)
    {
        var allNodes = GetAllNodeMetadata();

        if (string.IsNullOrWhiteSpace(searchText))
        {
            return allNodes;
        }

        return allNodes.Where(node =>
            node.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            node.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            node.Path.Contains(searchText, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 设置搜索焦点
    /// </summary>
    public void FocusSearch()
    {
        _searchTextBox?.Focus();
        _searchTextBox?.SelectAll();
    }

    /// <summary>
    /// 清空搜索
    /// </summary>
    public void ClearSearch()
    {
        SearchText = string.Empty;
    }

    #endregion

}
