using System;

namespace TaiChi.Wpf.Tour
{
    /// <summary>
    /// 背景点击（遮罩高亮区域之外）时的行为配置。
    /// 默认 <see cref="None"/> 表示不执行任何动作。
    /// </summary>
    public enum TourBackgroundClickBehavior
    {
        /// <summary>
        /// 不执行任何动作（默认）。
        /// </summary>
        None = 0,

        /// <summary>
        /// 切换到下一步。
        /// </summary>
        Next,

        /// <summary>
        /// 切换到上一步。
        /// </summary>
        Prev,

        /// <summary>
        /// 终止并关闭引导。
        /// </summary>
        Stop
    }
}

