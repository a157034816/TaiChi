using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Interop;

namespace TaiChi.Wpf.MessageLoop
{
    /// <summary>
    /// 消息循环管理器
    /// </summary>
    public class MessageLoopManager
    {
        private static readonly Lazy<MessageLoopManager> _instance = new Lazy<MessageLoopManager>(() => new MessageLoopManager());
        
        /// <summary>
        /// 获取消息循环管理器实例
        /// </summary>
        public static MessageLoopManager Instance => _instance.Value;
        
        private readonly List<IMessageLoopHandler> _handlers = new List<IMessageLoopHandler>();
        private readonly object _syncLock = new object();
        
        private MessageLoopManager()
        {
        }
        
        /// <summary>
        /// 注册消息循环处理器
        /// </summary>
        /// <param name="handler">处理器实例</param>
        public void RegisterHandler(IMessageLoopHandler handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
                
            lock (_syncLock)
            {
                if (!_handlers.Contains(handler))
                {
                    _handlers.Add(handler);
                    // 按优先级排序，优先级高的排在前面
                    _handlers.Sort((x, y) => y.Priority.CompareTo(x.Priority));
                }
            }
        }
        
        /// <summary>
        /// 注销消息循环处理器
        /// </summary>
        /// <param name="handler">处理器实例</param>
        public void UnregisterHandler(IMessageLoopHandler handler)
        {
            if (handler == null)
                return;
                
            lock (_syncLock)
            {
                _handlers.Remove(handler);
            }
        }
        
        /// <summary>
        /// 为窗口注册消息循环
        /// </summary>
        /// <param name="window">要注册的窗口</param>
        public void RegisterForWindow(Window window)
        {
            if (window == null)
                throw new ArgumentNullException(nameof(window));
                
            // 如果窗口已经加载，直接注册
            if (window.IsLoaded)
            {
                RegisterWindowHook(window);
            }
            else
            {
                // 等待窗口加载完成后注册
                window.Loaded += (s, e) => RegisterWindowHook(window);
            }
            
            // 窗口关闭时移除钩子
            window.Closed += (s, e) => UnregisterWindowHook(window);
        }
        
        private void RegisterWindowHook(Window window)
        {
            var hwndSource = PresentationSource.FromVisual(window) as HwndSource;
            if (hwndSource != null)
            {
                hwndSource.AddHook(WndProc);
            }
        }
        
        private void UnregisterWindowHook(Window window)
        {
            var hwndSource = PresentationSource.FromVisual(window) as HwndSource;
            if (hwndSource != null)
            {
                hwndSource.RemoveHook(WndProc);
            }
        }
        
        /// <summary>
        /// 窗口消息处理函数
        /// </summary>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            IMessageLoopHandler[] handlersSnapshot;
            
            lock (_syncLock)
            {
                // 创建处理器列表的快照
                handlersSnapshot = _handlers.Where(h => h.IsEnabled).ToArray();
            }
            
            // 按顺序调用所有处理器
            foreach (var handler in handlersSnapshot)
            {
                try
                {
                    var result = handler.HandleMessage(hwnd, msg, wParam, lParam, ref handled);
                    
                    // 如果消息被处理，直接返回结果
                    if (handled)
                        return result;
                }
                catch (Exception)
                {
                    // 忽略单个处理器的异常，确保其他处理器仍能被调用
                }
            }
            
            return IntPtr.Zero;
        }
    }
} 