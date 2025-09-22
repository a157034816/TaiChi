using System;
using System.Windows;

namespace TaiChi.Wpf.NodeEditor.Controls.Events;

/// <summary>
/// 引脚相对偏移量变化事件的参数类
/// </summary>
public class PinRelativeOffsetChangedEventArgs : System.EventArgs
{
    /// <summary>
    /// 引脚的唯一标识符
    /// </summary>
    public Guid PinId { get; }

    /// <summary>
    /// 引脚相对于节点左上角的偏移量
    /// </summary>
    public Point RelativeOffset { get; }

    /// <summary>
    /// 事件发生的时间戳
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="pinId">引脚唯一标识符</param>
    /// <param name="relativeOffset">相对偏移量</param>
    public PinRelativeOffsetChangedEventArgs(Guid pinId, Point relativeOffset)
    {
        PinId = pinId;
        RelativeOffset = relativeOffset;
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// 构造函数（用于测试或特殊场景）
    /// </summary>
    /// <param name="pinId">引脚唯一标识符</param>
    /// <param name="relativeOffset">相对偏移量</param>
    /// <param name="timestamp">时间戳</param>
    internal PinRelativeOffsetChangedEventArgs(Guid pinId, Point relativeOffset, DateTime timestamp)
    {
        PinId = pinId;
        RelativeOffset = relativeOffset;
        Timestamp = timestamp;
    }
}