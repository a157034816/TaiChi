using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace TaiChi.Cache.Abstractions
{
    /// <summary>
    /// 远端数据统计信息（用于判断本地快照与远端是否一致）。
    /// </summary>
    /// <typeparam name="TPk">主键类型。</typeparam>
    public readonly struct RemoteStats<TPk>
        where TPk : struct, IComparable<TPk>
    {
        /// <summary>
        /// 创建远端统计信息。
        /// </summary>
        /// <param name="count">满足条件的记录数。</param>
        /// <param name="minPk">最小主键（为空表示无数据）。</param>
        /// <param name="maxPk">最大主键（为空表示无数据）。</param>
        public RemoteStats(int count, TPk? minPk, TPk? maxPk)
        {
            Count = count;
            MinPk = minPk;
            MaxPk = maxPk;
        }

        /// <summary>
        /// 记录数。
        /// </summary>
        public int Count { get; }

        /// <summary>
        /// 最小主键（为空表示无数据）。
        /// </summary>
        public TPk? MinPk { get; }

        /// <summary>
        /// 最大主键（为空表示无数据）。
        /// </summary>
        public TPk? MaxPk { get; }
    }

    /// <summary>
    /// 远端存储适配器（不绑定 ORM）。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <typeparam name="TPk">主键类型。</typeparam>
    public interface IRemoteStore<TEntity, TPk>
        where TPk : struct, IComparable<TPk>
    {
        /// <summary>
        /// 获取远端统计信息（count/minPk/maxPk）。
        /// predicate 为 null 表示全量统计。
        /// </summary>
        /// <param name="predicate">可选筛选条件；为 null 表示全量统计。</param>
        /// <param name="ct">取消令牌。</param>
        Task<RemoteStats<TPk>> GetStatsAsync(Expression<Func<TEntity, bool>>? predicate, CancellationToken ct);

        /// <summary>
        /// 全量拉取数据（用于首次、全量覆盖、删除校验失败兜底）。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        Task<IReadOnlyList<TEntity>> FetchAllAsync(CancellationToken ct);
    }

    /// <summary>
    /// 可选能力：按更新时间字段进行增量拉取。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    public interface IRemoteUpdatedSinceStore<TEntity>
    {
        /// <summary>
        /// 拉取指定时间点之后更新过的记录（不包含该时间点本身）。
        /// </summary>
        /// <param name="sinceExclusive">时间阈值（不包含）。</param>
        /// <param name="ct">取消令牌。</param>
        Task<IReadOnlyList<TEntity>> FetchUpdatedSinceAsync(DateTimeOffset sinceExclusive, CancellationToken ct);
    }

    /// <summary>
    /// Keyset 分页请求参数。
    /// </summary>
    /// <typeparam name="TPk">主键类型。</typeparam>
    public readonly struct KeysetPageRequest<TPk>
        where TPk : struct, IComparable<TPk>
    {
        /// <summary>
        /// 创建 keyset 分页请求。
        /// </summary>
        /// <param name="afterKey">上一页的最后一个 key（为空表示从头开始）。</param>
        /// <param name="take">请求条数。</param>
        public KeysetPageRequest(TPk? afterKey, int take)
        {
            AfterKey = afterKey;
            Take = take;
        }

        /// <summary>
        /// 上一页最后一个 key（为空表示从头开始）。
        /// </summary>
        public TPk? AfterKey { get; }

        /// <summary>
        /// 请求条数。
        /// </summary>
        public int Take { get; }
    }

    /// <summary>
    /// Keyset 分页结果。
    /// </summary>
    /// <typeparam name="TPk">主键类型。</typeparam>
    public readonly struct KeysetPage<TPk>
        where TPk : struct, IComparable<TPk>
    {
        /// <summary>
        /// 创建 keyset 分页结果。
        /// </summary>
        /// <param name="keys">当前页的 key 列表。</param>
        /// <param name="nextAfterKey">用于请求下一页的 afterKey。</param>
        /// <param name="hasMore">是否还有更多数据。</param>
        public KeysetPage(IReadOnlyList<TPk> keys, TPk? nextAfterKey, bool hasMore)
        {
            Keys = keys ?? throw new ArgumentNullException(nameof(keys));
            NextAfterKey = nextAfterKey;
            HasMore = hasMore;
        }

        /// <summary>
        /// 当前页的 key 列表。
        /// </summary>
        public IReadOnlyList<TPk> Keys { get; }

        /// <summary>
        /// 下一页的 afterKey（通常等于当前页最后一个 key）。
        /// </summary>
        public TPk? NextAfterKey { get; }

        /// <summary>
        /// 是否还有更多数据。
        /// </summary>
        public bool HasMore { get; }
    }

    /// <summary>
    /// 可选能力：keyset 分页查询主键 + 按主键批量取数，用于 count 不一致时的缺口补齐。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <typeparam name="TPk">主键类型。</typeparam>
    public interface IRemoteKeysetStore<TEntity, TPk>
        where TPk : struct, IComparable<TPk>
    {
        /// <summary>
        /// 以 keyset 分页方式查询满足条件的主键列表。
        /// </summary>
        /// <param name="predicate">筛选条件（不允许为 null）。</param>
        /// <param name="request">分页请求参数。</param>
        /// <param name="ct">取消令牌。</param>
        Task<KeysetPage<TPk>> QueryKeysAsync(Expression<Func<TEntity, bool>> predicate, KeysetPageRequest<TPk> request, CancellationToken ct);

        /// <summary>
        /// 按主键集合批量拉取实体。
        /// </summary>
        /// <param name="keys">主键集合。</param>
        /// <param name="ct">取消令牌。</param>
        Task<IReadOnlyList<TEntity>> FetchByKeysAsync(IReadOnlyList<TPk> keys, CancellationToken ct);
    }
}
