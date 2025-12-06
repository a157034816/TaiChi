using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace TaiChi.Wpf.NodeEditor.Controls.Events;

/// <summary>
/// 弱引用事件管理器，用于管理事件订阅并避免内存泄漏
/// </summary>
/// <typeparam name="T">事件参数类型，必须继承自 EventArgs</typeparam>
public class WeakEventManager<T> where T : System.EventArgs
{
    private readonly object _lock = new object();
    private readonly List<WeakReference<EventHandler<T>>> _handlers = new();

    /// <summary>
    /// 添加事件处理器
    /// </summary>
    /// <param name="handler">要添加的事件处理器</param>
    public void AddHandler(EventHandler<T> handler)
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        lock (_lock)
        {
            // 检查是否已经存在相同的处理器
            if (!_handlers.Any(h => h.TryGetTarget(out var existingHandler) && existingHandler == handler))
            {
                _handlers.Add(new WeakReference<EventHandler<T>>(handler));
            }
        }
    }

    /// <summary>
    /// 移除事件处理器
    /// </summary>
    /// <param name="handler">要移除的事件处理器</param>
    public void RemoveHandler(EventHandler<T> handler)
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        lock (_lock)
        {
            for (int i = _handlers.Count - 1; i >= 0; i--)
            {
                if (_handlers[i].TryGetTarget(out var existingHandler) && existingHandler == handler)
                {
                    _handlers.RemoveAt(i);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 触发事件
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    public void RaiseEvent(object sender, T e)
    {
        if (e == null)
            throw new ArgumentNullException(nameof(e));

        // 复制当前的活动处理器列表，避免在锁定状态下调用处理器
        var activeHandlers = CopyActiveHandlers();

        // 在锁外调用处理器，避免死锁
        foreach (var handler in activeHandlers)
        {
            try
            {
                handler(sender, e);
            }
            catch (Exception ex)
            {
                // 记录异常但不中断其他处理器的执行
                System.Diagnostics.Debug.WriteLine($"WeakEventManager: 事件处理器执行异常: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 复制当前的活动处理器列表并清理死亡引用
    /// </summary>
    /// <returns>活动的事件处理器列表</returns>
    private List<EventHandler<T>> CopyActiveHandlers()
    {
        lock (_lock)
        {
            var activeHandlers = new List<EventHandler<T>>();
            
            // 从后向前遍历，便于安全地移除死亡引用
            for (int i = _handlers.Count - 1; i >= 0; i--)
            {
                if (_handlers[i].TryGetTarget(out var handler))
                {
                    activeHandlers.Add(handler);
                }
                else
                {
                    // 移除死亡引用
                    _handlers.RemoveAt(i);
                }
            }

            return activeHandlers;
        }
    }

    /// <summary>
    /// 清理所有死亡引用
    /// </summary>
    public void CleanupDeadReferences()
    {
        lock (_lock)
        {
            for (int i = _handlers.Count - 1; i >= 0; i--)
            {
                if (!_handlers[i].TryGetTarget(out _))
                {
                    _handlers.RemoveAt(i);
                }
            }
        }
    }

    /// <summary>
    /// 获取当前活动处理器数量
    /// </summary>
    public int ActiveHandlerCount
    {
        get
        {
            lock (_lock)
            {
                return _handlers.Count(h => h.TryGetTarget(out _));
            }
        }
    }

    /// <summary>
    /// 获取总处理器数量（包括死亡引用）
    /// </summary>
    public int TotalHandlerCount
    {
        get
        {
            lock (_lock)
            {
                return _handlers.Count;
            }
        }
    }

    /// <summary>
    /// 清理所有事件处理器
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _handlers.Clear();
        }
    }
}