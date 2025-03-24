using System.Windows;

namespace TaiChi.Wpf.Controls;

/// <summary>
/// 窗口停靠位置枚举
/// </summary>
public enum DockPosition
{
    /// <summary>
    /// 中间
    /// </summary>
    Center,
    
    /// <summary>
    /// 左上角
    /// </summary>
    TopLeft,
    
    /// <summary>
    /// 右上角
    /// </summary>
    TopRight,
    
    /// <summary>
    /// 左下角
    /// </summary>
    BottomLeft,
    
    /// <summary>
    /// 右下角
    /// </summary>
    BottomRight
}

/// <summary>
/// 一个浮动的控件底座, 它可以紧贴在屏幕的任意位置或者居中。
/// </summary>
public partial class FloatingControlDockWindow : Window
{
    private double _windowWidth;
    private double _windowHeight;
    private DockPosition _currentPosition;
    
    public FloatingControlDockWindow()
    {
        InitializeComponent();
        
        // 添加屏幕大小变化事件处理
        SystemParameters.StaticPropertyChanged += SystemParameters_StaticPropertyChanged;
        
        // 窗口关闭时取消事件注册
        this.Closed += (s, e) => 
        {
            SystemParameters.StaticPropertyChanged -= SystemParameters_StaticPropertyChanged;
        };
        
        this.SizeChanged += (s, e) =>
        {
            this.RecalcPosition(_currentPosition);
        };
    }
    
    /// <summary>
    /// 处理屏幕大小或工作区变化事件
    /// </summary>
    private void SystemParameters_StaticPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // 当工作区属性发生变化时重新定位窗口
        if (e.PropertyName == "WorkArea")
        {
            // 使用记录的位置信息重新设置窗口位置
            SetSizeAndPosition(_windowWidth, _windowHeight, _currentPosition);
        }
    }
    
    /// <summary>
    /// 设置窗口内容
    /// </summary>
    /// <param name="content">要显示的内容</param>
    public void SetContent(UIElement content)
    {
        if (content != null)
        {
            RootGrid.Children.Add(content);
        }
    }
    
    /// <summary>
    /// 设置窗口大小和位置
    /// </summary>
    /// <param name="width">窗口宽度，当为0时使用工作区宽度</param>
    /// <param name="height">窗口高度，当为0时使用工作区高度</param>
    /// <param name="position">停靠位置</param>
    public void SetSizeAndPosition(double width, double height, DockPosition position)
    {
        // 记录当前的窗口尺寸和位置信息
        _windowWidth = width;
        _windowHeight = height;
        _currentPosition = position;
        
        // 设置窗口大小
        this.Width = width;
        this.Height = height;
        
        RecalcPosition(position);
    }

    private void RecalcPosition(DockPosition position)
    {
        // 获取工作区大小（排除任务栏）
        var workingArea = SystemParameters.WorkArea;
        // 根据指定位置设置窗口位置
        switch (position)
        {
            case DockPosition.Center:
                this.Left = workingArea.Left + (workingArea.Width - this.Width) / 2;
                this.Top = workingArea.Top + (workingArea.Height - this.Height) / 2;
                break;
                
            case DockPosition.TopLeft:
                this.Left = workingArea.Left;
                this.Top = workingArea.Top;
                break;
                
            case DockPosition.TopRight:
                this.Left = workingArea.Right - this.Width;
                this.Top = workingArea.Top;
                break;
                
            case DockPosition.BottomLeft:
                this.Left = workingArea.Left;
                this.Top = workingArea.Bottom - this.Height;
                break;
                
            case DockPosition.BottomRight:
                this.Left = workingArea.Right - this.Width;
                this.Top = workingArea.Bottom - this.Height;
                break;
        }
    }
    
    /// <summary>
    /// 设置窗口大小自适应内容
    /// </summary>
    /// <param name="sizeToContent"></param>
    public void SetSizeToContentMode(SizeToContent sizeToContent)
    {
        this.SizeToContent = sizeToContent;
    }

    /// <summary>
    /// 窗口显示在指定位置
    /// </summary>
    /// <param name="width">窗口宽度，当为0时使用工作区宽度</param>
    /// <param name="height">窗口高度，当为0时使用工作区高度</param>
    /// <param name="position">停靠位置</param>
    public void ShowAtPosition(double width, double height, DockPosition position)
    {
        SetSizeAndPosition(width, height, position);
        this.Show();
    }
    
    /// <summary>
    /// 窗口显示在指定位置并设置内容
    /// </summary>
    /// <param name="content">要显示的内容</param>
    /// <param name="width">窗口宽度，当为0时使用工作区宽度</param>
    /// <param name="height">窗口高度，当为0时使用工作区高度</param>
    /// <param name="position">停靠位置</param>
    public void ShowWithContent(UIElement content, double width, double height, DockPosition position)
    {
        SetContent(content);
        SetSizeAndPosition(width, height, position);
        this.Show();
    }
}