using System.Windows;

namespace TaiChi.Wpf.MessageLoop.Examples
{
    /// <summary>
    /// 消息循环管理器使用示例
    /// </summary>
    public static class UsageExample
    {
        /// <summary>
        /// 为应用程序注册消息循环处理
        /// </summary>
        /// <param name="application">WPF应用程序实例</param>
        public static void RegisterForApplication(Application application)
        {
            // 创建并注册键盘消息处理器
            var keyboardHandler = new KeyboardMessageHandler(100);
            keyboardHandler.Register();
            
            // 创建并注册鼠标消息处理器（示例）
            // var mouseHandler = new MouseMessageHandler(50);
            // mouseHandler.Register();
            
            // 监听应用程序启动事件，为主窗口注册消息循环
            application.Startup += (sender, e) =>
            {
                application.MainWindow.Loaded += (s, args) =>
                {
                    // 为主窗口注册消息循环
                    MessageLoopManager.Instance.RegisterForWindow(application.MainWindow);
                };
            };
            
            // 监听应用程序退出事件，清理资源
            application.Exit += (sender, e) =>
            {
                // 注销消息处理器
                keyboardHandler.Unregister();
                // mouseHandler.Unregister();
            };
        }
        
        /// <summary>
        /// 为单个窗口注册消息循环处理
        /// </summary>
        /// <param name="window">WPF窗口</param>
        /// <returns>创建的消息处理器</returns>
        public static KeyboardMessageHandler RegisterForWindow(Window window)
        {
            // 创建并注册键盘消息处理器
            var keyboardHandler = new KeyboardMessageHandler(100);
            keyboardHandler.Register();
            
            // 为窗口注册消息循环
            MessageLoopManager.Instance.RegisterForWindow(window);
            
            // 窗口关闭时注销处理器
            window.Closed += (sender, e) => keyboardHandler.Unregister();
            
            return keyboardHandler;
        }
    }
} 