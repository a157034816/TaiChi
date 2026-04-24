using System;

namespace TaiChi.Cache.Abstractions
{
    /// <summary>
    /// 缓存类型标记（用于表达本地缓存状态的位掩码）。
    /// </summary>
    [Flags]
    public enum CacheTypeMark
    {
        /// <summary>
        /// 无标记。
        /// </summary>
        None = 0,

        /// <summary>
        /// 本地缓存需要更新（例如发生失效或远端统计不一致）。
        /// </summary>
        NeedUpdate = 1 << 0,

        /// <summary>
        /// 缓存正在拉取/刷新中（用于避免并发重复拉取）。
        /// </summary>
        Fetching = 1 << 1,
    }

    /// <summary>
    /// 缓存刷新模式。
    /// </summary>
    public enum CacheRefreshMode
    {
        /// <summary>
        /// 自动选择（按更新时间增量优先、全量兜底）。
        /// </summary>
        Auto,

        /// <summary>
        /// 强制全量覆盖。
        /// </summary>
        Full,
    }

    /// <summary>
    /// 缓存失效范围。
    /// </summary>
    public enum CacheInvalidateScope
    {
        /// <summary>
        /// 全量失效（整表/整缓存实例）。
        /// </summary>
        Full,

        /// <summary>
        /// 按主键集合失效（局部失效）。
        /// </summary>
        ByKeys,
    }
}

