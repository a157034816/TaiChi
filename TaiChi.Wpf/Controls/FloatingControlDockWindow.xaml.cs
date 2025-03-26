using System.Windows;
using System.Windows.Input;
using System.Runtime.InteropServices;
using System;

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
    
    // Win32 常量和方法定义，用于禁止窗口移动
    private const int WM_SYSCOMMAND = 0x0112;
    private const int SC_MOVE = 0xF010;
    
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    
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
        
        // 禁止窗口被拖动
        this.MouseLeftButtonDown += (sender, e) => e.Handled = true;
        this.MouseMove += (sender, e) => e.Handled = true;

        this.Loaded += (sender, args) =>
        {
            RecalcPosition(_currentPosition);
        };

        // // 处理本机窗口消息以阻止移动
        // this.SourceInitialized += (sender, e) =>
        // {
        //     System.Windows.Interop.HwndSource source = System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        //     source.AddHook(WndProc);
        // };
    }
    
    /// <summary>
    /// 处理窗口消息，阻止窗口移动
    /// </summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // 检查是否为系统命令消息，如果是移动窗口命令则阻止
        if (msg == WM_SYSCOMMAND)
        {
            int command = wParam.ToInt32() & 0xFFF0;
            if (command == SC_MOVE)
            {
                handled = true;
                return IntPtr.Zero;
            }
        }
        return IntPtr.Zero;
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
                this.Left = workingArea.Left + (workingArea.Width - this.ActualWidth) / 2;
                this.Top = workingArea.Top + (workingArea.Height - this.ActualHeight) / 2;
                break;
                
            case DockPosition.TopLeft:
                this.Left = workingArea.Left;
                this.Top = workingArea.Top;
                break;
                
            case DockPosition.TopRight:
                this.Left = workingArea.Right - this.ActualWidth;
                this.Top = workingArea.Top;
                break;
                
            case DockPosition.BottomLeft:
                this.Left = workingArea.Left;
                this.Top = workingArea.Bottom - this.ActualHeight;
                break;
                
            case DockPosition.BottomRight:
                this.Left = workingArea.Right - this.ActualWidth;
                this.Top = workingArea.Bottom - this.ActualHeight;
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
    
    /// <summary>
    /// 将窗口移动到指定位置（屏幕坐标）
    /// </summary>
    /// <param name="x">窗口左上角X坐标</param>
    /// <param name="y">窗口左上角Y坐标</param>
    public void MoveWindow(double x, double y)
    {
        this.Left = x;
        this.Top = y;
    }
}