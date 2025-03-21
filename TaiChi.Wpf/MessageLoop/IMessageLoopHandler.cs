using System;
using System.Windows.Interop;

namespace TaiChi.Wpf.MessageLoop
{
    /// <summary>
    /// 消息循环处理器接口
    /// </summary>
    public interface IMessageLoopHandler
    {
        /// <summary>
        /// 处理窗口消息
        /// </summary>
        /// <param name="hwnd">窗口句柄</param>
        /// <param name="msg">消息ID</param>
        /// <param name="wParam">消息参数</param>
        /// <param name="lParam">消息参数</param>
        /// <param name="handled">是否已处理</param>
        /// <returns>处理结果</returns>
        IntPtr HandleMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled);

        /// <summary>
        /// 获取处理器优先级，优先级高的先处理
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// 获取处理器是否启用
        /// </summary>
        bool IsEnabled { get; }
    }
} 