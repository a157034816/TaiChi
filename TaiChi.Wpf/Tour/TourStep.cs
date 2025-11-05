using System;
using System.Windows;
using System.Windows.Controls;

namespace TaiChi.Wpf.Tour
{
    /// <summary>
    /// 单个引导步骤的信息载体。
    /// </summary>
    public sealed class TourStep
    {
        /// <summary>
        /// 步骤标题，显示在气泡头部。
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 步骤说明文本，显示在气泡主体。
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 目标元素（用于高亮与定位）。
        /// </summary>
        public FrameworkElement? Target { get; set; }

        /// <summary>
        /// 首选摆放位置；Auto 将自动计算合适位置。
        /// </summary>
        public TourPlacement Placement { get; set; } = TourPlacement.Auto;

        /// <summary>
        /// 高亮矩形四周的额外留白像素。
        /// </summary>
        public Thickness HighlightPadding { get; set; } = new Thickness(6);

        /// <summary>
        /// 高亮圆角半径。
        /// </summary>
        public double CornerRadius { get; set; } = 6;

        /// <summary>
        /// 若 <see cref="Target"/> 为空，使用此矩形（以窗口坐标系）作为高亮区域；
        /// 可用于强调某个窗口区域而非具体控件。
        /// </summary>
        public Rect? FallbackHighlightRect { get; set; }

        /// <summary>
        /// 自定义步骤 ID（可用于事件回调识别）。
        /// </summary>
        public string? Id { get; set; }
    }
}
