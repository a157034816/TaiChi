using System;

namespace TaiChi.Wpf.Tour
{
    /// <summary>
    /// 漫游式引导气泡相对目标控件的首选摆放位置。
    /// Auto 表示自动根据可用空间计算一个合适的位置。
    /// </summary>
    public enum TourPlacement
    {
        Auto = 0,
        Top,
        Bottom,
        Left,
        Right
    }
}
