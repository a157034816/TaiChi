using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Effects;
using TaiChi.Wpf.NodeEditor.Core.Models;

namespace TaiChi.Wpf.NodeEditor.Controls;

/// <summary>
/// 分组可视化控件：用于在画布上呈现节点分组。
/// 说明：本控件仅负责视觉与基本交互占位，布局定位/尺寸应由外部（如 NodeCanvas）基于 GroupData.Bounds 管理。
/// </summary>
public class NodeGroupControl : UserControl
{
    // ZoomLevel（来自外部画布，用于将物理拖拽位移换算到逻辑位移）
    public static readonly DependencyProperty ZoomLevelProperty =
        DependencyProperty.Register(
            nameof(ZoomLevel), typeof(double), typeof(NodeGroupControl),
            new PropertyMetadata(1.0));

    public double ZoomLevel
    {
        get => (double)GetValue(ZoomLevelProperty);
        set => SetValue(ZoomLevelProperty, value);
    }

    // GroupData（数据绑定）
    public static readonly DependencyProperty GroupDataProperty =
        DependencyProperty.Register(
            nameof(GroupData), typeof(NodeGroup), typeof(NodeGroupControl),
            new PropertyMetadata(null, OnGroupDataChanged));

    public NodeGroup? GroupData
    {
        get => (NodeGroup?)GetValue(GroupDataProperty);
        set => SetValue(GroupDataProperty, value);
    }

    // IsSelected（选中状态）
    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(
            nameof(IsSelected), typeof(bool), typeof(NodeGroupControl),
            new PropertyMetadata(false));

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    // 视觉样式相关属性
    public static readonly DependencyProperty GroupBackgroundProperty =
        DependencyProperty.Register(
            nameof(GroupBackground), typeof(Brush), typeof(NodeGroupControl),
            new PropertyMetadata(new SolidColorBrush(Color.FromArgb(40, 100, 149, 237)))); // 透明浅蓝

    public Brush GroupBackground
    {
        get => (Brush)GetValue(GroupBackgroundProperty);
        set => SetValue(GroupBackgroundProperty, value);
    }

    public static readonly DependencyProperty GroupBorderBrushProperty =
        DependencyProperty.Register(
            nameof(GroupBorderBrush), typeof(Brush), typeof(NodeGroupControl),
            new PropertyMetadata(Brushes.SteelBlue));

    public Brush GroupBorderBrush
    {
        get => (Brush)GetValue(GroupBorderBrushProperty);
        set => SetValue(GroupBorderBrushProperty, value);
    }

    public static readonly DependencyProperty GroupBorderThicknessProperty =
        DependencyProperty.Register(
            nameof(GroupBorderThickness), typeof(Thickness), typeof(NodeGroupControl),
            new PropertyMetadata(new Thickness(1)));

    public Thickness GroupBorderThickness
    {
        get => (Thickness)GetValue(GroupBorderThicknessProperty);
        set => SetValue(GroupBorderThicknessProperty, value);
    }

    public static readonly DependencyProperty GroupCornerRadiusProperty =
        DependencyProperty.Register(
            nameof(GroupCornerRadius), typeof(CornerRadius), typeof(NodeGroupControl),
            new PropertyMetadata(new CornerRadius(8)));

    public CornerRadius GroupCornerRadius
    {
        get => (CornerRadius)GetValue(GroupCornerRadiusProperty);
        set => SetValue(GroupCornerRadiusProperty, value);
    }

    public static readonly DependencyProperty HeaderBackgroundProperty =
        DependencyProperty.Register(
            nameof(HeaderBackground), typeof(Brush), typeof(NodeGroupControl),
            new PropertyMetadata(Brushes.SteelBlue));

    public Brush HeaderBackground
    {
        get => (Brush)GetValue(HeaderBackgroundProperty);
        set => SetValue(HeaderBackgroundProperty, value);
    }

    public static readonly DependencyProperty HeaderForegroundProperty =
        DependencyProperty.Register(
            nameof(HeaderForeground), typeof(Brush), typeof(NodeGroupControl),
            new PropertyMetadata(Brushes.White));

    public Brush HeaderForeground
    {
        get => (Brush)GetValue(HeaderForegroundProperty);
        set => SetValue(HeaderForegroundProperty, value);
    }

    public static readonly DependencyProperty HeaderHeightProperty =
        DependencyProperty.Register(
            nameof(HeaderHeight), typeof(double), typeof(NodeGroupControl),
            new PropertyMetadata(24.0));

    public double HeaderHeight
    {
        get => (double)GetValue(HeaderHeightProperty);
        set => SetValue(HeaderHeightProperty, value);
    }

    // 注入的分组管理器（可选）：若提供则用于执行级联节点/子分组移动
    public static readonly DependencyProperty GroupManagerProperty =
        DependencyProperty.Register(
            nameof(GroupManager), typeof(Managers.NodeGroupManager), typeof(NodeGroupControl),
            new PropertyMetadata(null));

    public Managers.NodeGroupManager? GroupManager
    {
        get => (Managers.NodeGroupManager?)GetValue(GroupManagerProperty);
        set => SetValue(GroupManagerProperty, value);
    }

    private Border? _rootBorder;
    private Border? _headerBorder;
    private TextBlock? _headerText;
    private bool _isDragging;
    private Point _dragStart;
    private NodeEditorRect _startBounds;

    public NodeGroupControl()
    {
        BuildVisualTree();
        SizeChanged += (_, __) => UpdateClip();
        Loaded += (_, __) => UpdateClip();
        PreviewMouseLeftButtonUp += OnMouseLeftButtonUp;
    }

    private static void OnGroupDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (NodeGroupControl)d;

        if (e.OldValue is NodeGroup oldGroup)
        {
            oldGroup.PropertyChanged -= ctrl.OnGroupDataPropertyChanged;
        }
        if (e.NewValue is NodeGroup newGroup)
        {
            ctrl.DataContext = newGroup;
            newGroup.PropertyChanged += ctrl.OnGroupDataPropertyChanged;
            ctrl.ApplyBounds(newGroup.Bounds);
            ctrl.IsSelected = newGroup.IsSelected;
        }
        else
        {
            ctrl.DataContext = null;
        }
    }

    private void OnGroupDataPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (GroupData == null) return;
        switch (e.PropertyName)
        {
            case nameof(NodeGroup.Bounds):
                ApplyBounds(GroupData.Bounds);
                break;
            case nameof(NodeGroup.IsSelected):
                IsSelected = GroupData.IsSelected;
                break;
        }
    }

    private void ApplyBounds(NodeEditorRect rect)
    {
        Width = Math.Max(0, rect.Width);
        Height = Math.Max(0, rect.Height);
        // Canvas.Left / Canvas.Top 由外层设置
        UpdateClip();
    }

    private void BuildVisualTree()
    {
        // 栅格布局：Header + 内容层 + 背景
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // 背景边框（既承担视觉，也可用于拖拽命中）
        _rootBorder = new Border
        {
            CornerRadius = GroupCornerRadius,
            BorderThickness = GroupBorderThickness,
            Background = GroupBackground,
            BorderBrush = GroupBorderBrush,
            SnapsToDevicePixels = true,
            IsHitTestVisible = true
        };

        // 将背景边框属性绑定到控件依赖属性
        _rootBorder.SetBinding(Border.BackgroundProperty, new Binding(nameof(GroupBackground)) { Source = this });
        _rootBorder.SetBinding(Border.BorderBrushProperty, new Binding(nameof(GroupBorderBrush)) { Source = this });
        _rootBorder.SetBinding(Border.BorderThicknessProperty, new Binding(nameof(GroupBorderThickness)) { Source = this });
        _rootBorder.SetBinding(Border.CornerRadiusProperty, new Binding(nameof(GroupCornerRadius)) { Source = this });
        Grid.SetRowSpan(_rootBorder, 2);

        _headerBorder = new Border
        {
            Height = HeaderHeight,
            Background = HeaderBackground,
            CornerRadius = new CornerRadius(8, 8, 0, 0),
            Padding = new Thickness(8, 2, 8, 2)
        };
        _headerBorder.SetBinding(HeightProperty, new Binding(nameof(HeaderHeight)) { Source = this });
        _headerBorder.SetBinding(Border.BackgroundProperty, new Binding(nameof(HeaderBackground)) { Source = this });
        _headerBorder.PreviewMouseLeftButtonDown += OnHeaderMouseLeftButtonDown;
        _headerBorder.PreviewMouseMove += OnHeaderMouseMove;
        _headerBorder.PreviewMouseLeftButtonUp += OnMouseLeftButtonUp;

        // 标题与背景均可拖拽移动
        _rootBorder.PreviewMouseLeftButtonDown += OnHeaderMouseLeftButtonDown;
        _rootBorder.PreviewMouseMove += OnHeaderMouseMove;
        _rootBorder.PreviewMouseLeftButtonUp += OnMouseLeftButtonUp;

        _headerText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            FontWeight = FontWeights.Bold,
            Foreground = HeaderForeground,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        _headerText.SetBinding(TextBlock.TextProperty, new Binding("Name") { Mode = BindingMode.OneWay });
        _headerText.SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(HeaderForeground)) { Source = this });

        _headerBorder.Child = _headerText;
        Grid.SetRow(_headerBorder, 0);

        // 内容占位（分组内部的节点由外层画布控制，不在此控件中渲染）
        var contentPresenter = new ContentPresenter
        {
            Margin = new Thickness(4),
            IsHitTestVisible = false // 分组主体区域不拦截命中，事件可透传给下层 Canvas
        };
        Grid.SetRow(contentPresenter, 1);

        // 组装：背景 -> 标题 -> 内容
        grid.Children.Add(_rootBorder);
        grid.Children.Add(_headerBorder);
        grid.Children.Add(contentPresenter);

        // 选中样式（基于 IsSelected 的 DataTrigger）
        var borderStyle = new Style(typeof(Border));
        var selectedTrigger = new DataTrigger
        {
            Binding = new Binding(nameof(IsSelected)) { Source = this },
            Value = true
        };
        selectedTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, Brushes.Orange));
        selectedTrigger.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(2)));
        selectedTrigger.Setters.Add(new Setter(Border.EffectProperty, new DropShadowEffect
        {
            Color = Colors.Orange,
            BlurRadius = 8,
            ShadowDepth = 0,
            Opacity = 0.8
        }));
        borderStyle.Triggers.Add(selectedTrigger);
        _rootBorder.Style = borderStyle;

        Content = grid;
    }

    private void UpdateClip()
    {
        if (_rootBorder == null) return;
        _rootBorder.Clip = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight), GroupCornerRadius.TopLeft, GroupCornerRadius.TopLeft);
    }

    private void OnHeaderMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (GroupData == null) return;
        _isDragging = true;
        _dragStart = e.GetPosition(this);
        _startBounds = GroupData.Bounds;
        CaptureMouse();
        e.Handled = true;
    }

    private void OnHeaderMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDragging || GroupData == null) return;
        var current = e.GetPosition(this);
        var delta = current - _dragStart;

        // 将物理位移转换为画布逻辑位移（去除缩放影响）
        var zoom = ZoomLevel <= 0 ? 1.0 : ZoomLevel;
        var dx = delta.X / zoom;
        var dy = delta.Y / zoom;

        if (GroupManager != null)
        {
            GroupManager.MoveGroup(GroupData, dx, dy, cascadeNodes: true, cascadeChildren: true);
        }
        else
        {
            // 退化处理：仅移动分组边界和组内节点
            GroupData.Bounds = new NodeEditorRect(_startBounds.X + dx, _startBounds.Y + dy, _startBounds.Width, _startBounds.Height);
            foreach (var n in GroupData.GetAllNodesRecursive())
            {
                n.Position = new NodeEditorPoint(n.Position.X + dx, n.Position.Y + dy);
            }
        }

        e.Handled = true;
    }

    private void OnMouseLeftButtonUp(object? sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        ReleaseMouseCapture();
        e.Handled = true;
    }

    // 由外部通过 ZoomLevel 依赖属性提供，不再向上查找 NodeCanvas
}
