using System;

namespace TaiChi.Cache.Abstractions
{
    /// <summary>
    /// 实体缓存的本地状态快照（用于诊断与测试断言）。
    /// </summary>
    /// <typeparam name="TPk">主键类型。</typeparam>
    public readonly struct EntityCacheLocalState<TPk>
        where TPk : struct, IComparable<TPk>
    {
        /// <summary>
        /// 创建本地状态快照。
        /// </summary>
        /// <param name="count">本地实体数量。</param>
        /// <param name="minPk">本地最小主键。</param>
        /// <param name="maxPk">本地最大主键。</param>
        /// <param name="lastMaxUpdatedAt">本地记录的最大更新时间（用于增量拉取）。</param>
        /// <param name="lastDeleteCheckAt">上次删除校验时间。</param>
        /// <param name="insertsSinceReorder">自上次重排以来的插入次数。</param>
        /// <param name="mark">本地标记位。</param>
        public EntityCacheLocalState(
            int count,
            TPk? minPk,
            TPk? maxPk,
            DateTimeOffset? lastMaxUpdatedAt,
            DateTimeOffset? lastDeleteCheckAt,
            int insertsSinceReorder,
            CacheTypeMark mark)
        {
            Count = count;
            MinPk = minPk;
            MaxPk = maxPk;
            LastMaxUpdatedAt = lastMaxUpdatedAt;
            LastDeleteCheckAt = lastDeleteCheckAt;
            InsertsSinceReorder = insertsSinceReorder;
            Mark = mark;
        }

        /// <summary>
        /// 本地实体数量。
        /// </summary>
        public int Count { get; }

        /// <summary>
        /// 本地最小主键。
        /// </summary>
        public TPk? MinPk { get; }

        /// <summary>
        /// 本地最大主键。
        /// </summary>
        public TPk? MaxPk { get; }

        /// <summary>
        /// 本地记录的最大更新时间（用于增量拉取阈值）。
        /// </summary>
        public DateTimeOffset? LastMaxUpdatedAt { get; }

        /// <summary>
        /// 上次删除校验时间。
        /// </summary>
        public DateTimeOffset? LastDeleteCheckAt { get; }

        /// <summary>
        /// 自上次重排以来的插入次数。
        /// </summary>
        public int InsertsSinceReorder { get; }

        /// <summary>
        /// 本地标记位。
        /// </summary>
        public CacheTypeMark Mark { get; }

        /// <summary>
        /// 是否需要更新。
        /// </summary>
        public bool NeedUpdate => (Mark & CacheTypeMark.NeedUpdate) != 0;

        /// <summary>
        /// 是否正在拉取/刷新。
        /// </summary>
        public bool IsFetching => (Mark & CacheTypeMark.Fetching) != 0;
    }
}
