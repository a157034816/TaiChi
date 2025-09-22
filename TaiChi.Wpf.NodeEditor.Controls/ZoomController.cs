using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TaiChi.Wpf.NodeEditor.Controls.Helpers;

namespace TaiChi.Wpf.NodeEditor.Controls;

/// <summary>
/// 缩放控制器控件
/// </summary>
public class ZoomController : Control
{
    #region 依赖属性

    /// <summary>
    /// 当前缩放级别
    /// </summary>
    public static readonly DependencyProperty ZoomLevelProperty =
        DependencyProperty.Register(nameof(ZoomLevel), typeof(double), typeof(ZoomController),
            new PropertyMetadata(1.0, OnZoomLevelChanged));

    /// <summary>
    /// 最小缩放级别
    /// </summary>
    public static readonly DependencyProperty MinZoomProperty =
        DependencyProperty.Register(nameof(MinZoom), typeof(double), typeof(ZoomController),
            new PropertyMetadata(0.1));

    /// <summary>
    /// 最大缩放级别
    /// </summary>
    public static readonly DependencyProperty MaxZoomProperty =
        DependencyProperty.Register(nameof(MaxZoom), typeof(double), typeof(ZoomController),
            new PropertyMetadata(5.0));

    /// <summary>
    /// 缩放步长
    /// </summary>
    public static readonly DependencyProperty ZoomStepProperty =
        DependencyProperty.Register(nameof(ZoomStep), typeof(double), typeof(ZoomController),
            new PropertyMetadata(0.1));

    /// <summary>
    /// 是否显示百分比文本
    /// </summary>
    public static readonly DependencyProperty ShowPercentageProperty =
        DependencyProperty.Register(nameof(ShowPercentage), typeof(bool), typeof(ZoomController),
            new PropertyMetadata(true));

    /// <summary>
    /// 是否显示预设缩放按钮
    /// </summary>
    public static readonly DependencyProperty ShowPresetButtonsProperty =
        DependencyProperty.Register(nameof(ShowPresetButtons), typeof(bool), typeof(ZoomController),
            new PropertyMetadata(true));

    /// <summary>
    /// 放大命令依赖属性
    /// </summary>
    public static readonly DependencyProperty ZoomInCommandProperty =
        DependencyProperty.Register(nameof(ZoomInCommand), typeof(ICommand), typeof(ZoomController),
            new PropertyMetadata(null));

    /// <summary>
    /// 缩小命令依赖属性
    /// </summary>
    public static readonly DependencyProperty ZoomOutCommandProperty =
        DependencyProperty.Register(nameof(ZoomOutCommand), typeof(ICommand), typeof(ZoomController),
            new PropertyMetadata(null));

    /// <summary>
    /// 适应画布命令依赖属性
    /// </summary>
    public static readonly DependencyProperty FitToCanvasCommandProperty =
        DependencyProperty.Register(nameof(FitToCanvasCommand), typeof(ICommand), typeof(ZoomController),
            new PropertyMetadata(null));

    /// <summary>
    /// 重置缩放命令依赖属性
    /// </summary>
    public static readonly DependencyProperty ResetZoomCommandProperty =
        DependencyProperty.Register(nameof(ResetZoomCommand), typeof(ICommand), typeof(ZoomController),
            new PropertyMetadata(null));

    /// <summary>
    /// 设置缩放级别命令依赖属性
    /// </summary>
    public static readonly DependencyProperty SetZoomCommandProperty =
        DependencyProperty.Register(nameof(SetZoomCommand), typeof(ICommand), typeof(ZoomController),
            new PropertyMetadata(null));

    #endregion

    #region 路由事件

    /// <summary>
    /// 缩放变化事件
    /// </summary>
    public static readonly RoutedEvent ZoomChangedEvent = EventManager.RegisterRoutedEvent(
        "ZoomChanged", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ZoomController));

    /// <summary>
    /// 适应画布事件
    /// </summary>
    public static readonly RoutedEvent FitToCanvasEvent = EventManager.RegisterRoutedEvent(
        "FitToCanvas", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ZoomController));

    /// <summary>
    /// 重置缩放事件
    /// </summary>
    public static readonly RoutedEvent ResetZoomEvent = EventManager.RegisterRoutedEvent(
        "ResetZoom", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ZoomController));

    #endregion

    #region 属性包装器

    /// <summary>
    /// 当前缩放级别
    /// </summary>
    public double ZoomLevel
    {
        get => (double)GetValue(ZoomLevelProperty);
        set => SetValue(ZoomLevelProperty, value);
    }

    /// <summary>
    /// 最小缩放级别
    /// </summary>
    public double MinZoom
    {
        get => (double)GetValue(MinZoomProperty);
        set => SetValue(MinZoomProperty, value);
    }

    /// <summary>
    /// 最大缩放级别
    /// </summary>
    public double MaxZoom
    {
        get => (double)GetValue(MaxZoomProperty);
        set => SetValue(MaxZoomProperty, value);
    }

    /// <summary>
    /// 缩放步长
    /// </summary>
    public double ZoomStep
    {
        get => (double)GetValue(ZoomStepProperty);
        set => SetValue(ZoomStepProperty, value);
    }

    /// <summary>
    /// 是否显示百分比文本
    /// </summary>
    public bool ShowPercentage
    {
        get => (bool)GetValue(ShowPercentageProperty);
        set => SetValue(ShowPercentageProperty, value);
    }

    /// <summary>
    /// 是否显示预设缩放按钮
    /// </summary>
    public bool ShowPresetButtons
    {
        get => (bool)GetValue(ShowPresetButtonsProperty);
        set => SetValue(ShowPresetButtonsProperty, value);
    }

    #endregion

    #region 事件包装器

    /// <summary>
    /// 缩放变化事件
    /// </summary>
    public event RoutedEventHandler ZoomChanged
    {
        add { AddHandler(ZoomChangedEvent, value); }
        remove { RemoveHandler(ZoomChangedEvent, value); }
    }

    /// <summary>
    /// 适应画布事件
    /// </summary>
    public event RoutedEventHandler FitToCanvas
    {
        add { AddHandler(FitToCanvasEvent, value); }
        remove { RemoveHandler(FitToCanvasEvent, value); }
    }

    /// <summary>
    /// 重置缩放事件
    /// </summary>
    public event RoutedEventHandler ResetZoom
    {
        add { AddHandler(ResetZoomEvent, value); }
        remove { RemoveHandler(ResetZoomEvent, value); }
    }

    #endregion

    #region 命令属性包装器

    /// <summary>
    /// 放大命令
    /// </summary>
    public ICommand ZoomInCommand
    {
        get => (ICommand)GetValue(ZoomInCommandProperty);
        set => SetValue(ZoomInCommandProperty, value);
    }

    /// <summary>
    /// 缩小命令
    /// </summary>
    public ICommand ZoomOutCommand
    {
        get => (ICommand)GetValue(ZoomOutCommandProperty);
        set => SetValue(ZoomOutCommandProperty, value);
    }

    /// <summary>
    /// 适应画布命令
    /// </summary>
    public ICommand FitToCanvasCommand
    {
        get => (ICommand)GetValue(FitToCanvasCommandProperty);
        set => SetValue(FitToCanvasCommandProperty, value);
    }

    /// <summary>
    /// 重置缩放命令
    /// </summary>
    public ICommand ResetZoomCommand
    {
        get => (ICommand)GetValue(ResetZoomCommandProperty);
        set => SetValue(ResetZoomCommandProperty, value);
    }

    /// <summary>
    /// 设置缩放级别命令
    /// </summary>
    public ICommand SetZoomCommand
    {
        get => (ICommand)GetValue(SetZoomCommandProperty);
        set => SetValue(SetZoomCommandProperty, value);
    }

    #endregion

    #region 构造函数

    /// <summary>
    /// 静态构造函数
    /// </summary>
    static ZoomController()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ZoomController),
            new FrameworkPropertyMetadata(typeof(ZoomController)));
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    public ZoomController()
    {
        // 初始化命令
        ZoomInCommand = CommandHelper.CreateCommand(ZoomIn, CanZoomIn);
        ZoomOutCommand = CommandHelper.CreateCommand(ZoomOut, CanZoomOut);
        FitToCanvasCommand = CommandHelper.CreateCommand(OnFitToCanvas);
        ResetZoomCommand = CommandHelper.CreateCommand(OnResetZoom);
        SetZoomCommand = CommandHelper.CreateCommand<double>(SetZoom);
    }

    #endregion

    #region 依赖属性回调

    private static void OnZoomLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ZoomController controller)
        {
            var args = new RoutedEventArgs(ZoomChangedEvent, controller);
            controller.RaiseEvent(args);
        }
    }

    #endregion

    #region 命令实现

    /// <summary>
    /// 放大
    /// </summary>
    private void ZoomIn()
    {
        var newZoom = Math.Min(MaxZoom, ZoomLevel + ZoomStep);
        ZoomLevel = newZoom;
    }

    /// <summary>
    /// 是否可以放大
    /// </summary>
    private bool CanZoomIn()
    {
        return ZoomLevel < MaxZoom;
    }

    /// <summary>
    /// 缩小
    /// </summary>
    private void ZoomOut()
    {
        var newZoom = Math.Max(MinZoom, ZoomLevel - ZoomStep);
        ZoomLevel = newZoom;
    }

    /// <summary>
    /// 是否可以缩小
    /// </summary>
    private bool CanZoomOut()
    {
        return ZoomLevel > MinZoom;
    }

    /// <summary>
    /// 适应画布
    /// </summary>
    private void OnFitToCanvas()
    {
        var args = new RoutedEventArgs(FitToCanvasEvent, this);
        RaiseEvent(args);
    }

    /// <summary>
    /// 重置缩放
    /// </summary>
    private void OnResetZoom()
    {
        ZoomLevel = 1.0;
        var args = new RoutedEventArgs(ResetZoomEvent, this);
        RaiseEvent(args);
    }

    /// <summary>
    /// 设置缩放级别
    /// </summary>
    private void SetZoom(double zoom)
    {
        ZoomLevel = Math.Max(MinZoom, Math.Min(MaxZoom, zoom));
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 获取缩放百分比文本
    /// </summary>
    public string GetZoomPercentageText()
    {
        return $"{Math.Round(ZoomLevel * 100)}%";
    }

    /// <summary>
    /// 设置预设缩放级别
    /// </summary>
    public void SetPresetZoom(double zoom)
    {
        SetZoom(zoom);
    }

    #endregion
}
