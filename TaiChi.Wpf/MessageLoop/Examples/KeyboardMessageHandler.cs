using System;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Input;

namespace TaiChi.Wpf.MessageLoop.Examples
{
    /// <summary>
    /// 键盘消息处理器示例
    /// </summary>
    public class KeyboardMessageHandler : MessageLoopHandlerBase
    {
        // 定义Windows消息常量
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        
        /// <summary>
        /// 初始化键盘消息处理器
        /// </summary>
        /// <param name="priority">处理器优先级</param>
        public KeyboardMessageHandler(int priority = 100) : base(priority)
        {
        }
        
        /// <summary>
        /// 处理键盘消息
        /// </summary>
        public override IntPtr HandleMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // 只处理键盘相关消息
            switch (msg)
            {
                case WM_KEYDOWN:
                case WM_SYSKEYDOWN:
                    OnKeyDown((int)wParam, ref handled);
                    break;
                    
                case WM_KEYUP:
                case WM_SYSKEYUP:
                    OnKeyUp((int)wParam, ref handled);
                    break;
            }
            
            return IntPtr.Zero;
        }
        
        /// <summary>
        /// 当键按下时触发
        /// </summary>
        /// <param name="keyCode">键代码</param>
        /// <param name="handled">是否已处理</param>
        protected virtual void OnKeyDown(int keyCode, ref bool handled)
        {
            if (keyCode == (int)Key.F4)
            {
                Debug.WriteLine("捕获到F4键");
                
                // 如果要阻止窗口关闭，可以设置handled = true
                // handled = true;
            }
        }
        
        /// <summary>
        /// 当键抬起时触发
        /// </summary>
        /// <param name="keyCode">键代码</param>
        /// <param name="handled">是否已处理</param>
        protected virtual void OnKeyUp(int keyCode, ref bool handled)
        {
            // 在此处理键抬起事件
        }
    }
} 