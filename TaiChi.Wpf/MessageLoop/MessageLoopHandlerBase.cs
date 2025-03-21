using System;

namespace TaiChi.Wpf.MessageLoop
{
    /// <summary>
    /// 消息循环处理器基类
    /// </summary>
    public abstract class MessageLoopHandlerBase : IMessageLoopHandler
    {
        /// <summary>
        /// 初始化消息循环处理器基类
        /// </summary>
        /// <param name="priority">处理器优先级</param>
        protected MessageLoopHandlerBase(int priority = 0)
        {
            Priority = priority;
            IsEnabled = true;
        }

        /// <summary>
        /// 获取或设置处理器优先级，优先级高的先处理
        /// </summary>
        public int Priority { get; }

        /// <summary>
        /// 获取或设置处理器是否启用
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// 处理窗口消息
        /// </summary>
        /// <param name="hwnd">窗口句柄</param>
        /// <param name="msg">消息ID</param>
        /// <param name="wParam">消息参数</param>
        /// <param name="lParam">消息参数</param>
        /// <param name="handled">是否已处理</param>
        /// <returns>处理结果</returns>
        public abstract IntPtr HandleMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled);

        /// <summary>
        /// 注册处理器到消息循环管理器
        /// </summary>
        public void Register()
        {
            MessageLoopManager.Instance.RegisterHandler(this);
        }

        /// <summary>
        /// 从消息循环管理器注销处理器
        /// </summary>
        public void Unregister()
        {
            MessageLoopManager.Instance.UnregisterHandler(this);
        }
    }
} 