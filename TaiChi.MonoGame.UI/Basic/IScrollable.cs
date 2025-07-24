using System;

namespace TaiChi.MonoGame.UI.Basic;

/// <summary>
/// 可滚动组件接口，定义了可滚动控件应具备的基本属性和事件
/// </summary>
public interface IScrollable
{
    /// <summary>
    /// 当前滚动偏移量
    /// </summary>
    float ScrollOffset { get; set; }

    /// <summary>
    /// 最大滚动偏移量
    /// </summary>
    float MaxScrollOffset { get; }

    /// <summary>
    /// 内容总高度
    /// </summary>
    float ContentHeight { get; }

    /// <summary>
    /// 可视区域高度
    /// </summary>
    float ViewportHeight { get; }

    /// <summary>
    /// 滚动偏移变化事件
    /// </summary>
    event EventHandler<ScrollEventArgs> OnScrollOffsetChanged;

    /// <summary>
    /// 内容高度变化事件
    /// </summary>
    event EventHandler<ContentHeightEventArgs> OnContentHeightChanged;

    /// <summary>
    /// 滚动到指定偏移位置
    /// </summary>
    /// <param name="offset">目标偏移位置</param>
    void ScrollTo(float offset);

    /// <summary>
    /// 滚动指定的距离
    /// </summary>
    /// <param name="delta">滚动距离，正值向下滚动，负值向上滚动</param>
    void ScrollBy(float delta);

    /// <summary>
    /// 滚动到顶部
    /// </summary>
    void ScrollToTop();

    /// <summary>
    /// 滚动到底部
    /// </summary>
    void ScrollToBottom();

    /// <summary>
    /// 获取滚动百分比（0.0 - 1.0）
    /// </summary>
    /// <returns>滚动百分比</returns>
    float GetScrollPercentage();

    /// <summary>
    /// 设置滚动百分比（0.0 - 1.0）
    /// </summary>
    /// <param name="percentage">滚动百分比</param>
    void SetScrollPercentage(float percentage);
}

/// <summary>
/// 滚动事件参数
/// </summary>
public class ScrollEventArgs : EventArgs
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="scrollOffset">当前滚动偏移</param>
    /// <param name="maxScrollOffset">最大滚动偏移</param>
    public ScrollEventArgs(float scrollOffset, float maxScrollOffset)
    {
        ScrollOffset = scrollOffset;
        MaxScrollOffset = maxScrollOffset;
    }

    /// <summary>
    /// 当前滚动偏移
    /// </summary>
    public float ScrollOffset { get; }

    /// <summary>
    /// 最大滚动偏移
    /// </summary>
    public float MaxScrollOffset { get; }
}

/// <summary>
/// 内容高度变化事件参数
/// </summary>
public class ContentHeightEventArgs : EventArgs
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="contentHeight">内容总高度</param>
    public ContentHeightEventArgs(float contentHeight)
    {
        ContentHeight = contentHeight;
    }

    /// <summary>
    /// 内容总高度
    /// </summary>
    public float ContentHeight { get; }
}