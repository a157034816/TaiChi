using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using TaiChi.Cache.Internal;

namespace TaiChi.Cache
{
    /// <summary>
    /// 实体缓存查询构建器（链式 API，最终以异步方式执行）。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <typeparam name="TPk">主键类型。</typeparam>
    /// <remarks>
    /// 该类型仅负责收集查询参数（谓词/排序/分页），实际执行交由 <see cref="EntityCache{TEntity, TPk}"/>。
    /// 实例不是线程安全的，建议按“每次查询创建一个实例”的方式使用。
    /// </remarks>
    public sealed class CacheQuery<TEntity, TPk>
        where TPk : struct, IComparable<TPk>
    {
        /// <summary>
        /// 底层实体缓存。
        /// </summary>
        private readonly EntityCache<TEntity, TPk> _cache;

        /// <summary>
        /// 过滤条件（多次 Where 会使用 AndAlso 合并）。
        /// </summary>
        private Expression<Func<TEntity, bool>>? _predicate;

        /// <summary>
        /// 排序表达式。
        /// </summary>
        private LambdaExpression? _orderBy;

        /// <summary>
        /// 是否降序排序。
        /// </summary>
        private bool _orderByDescending;

        /// <summary>
        /// 跳过条数。
        /// </summary>
        private int? _skip;

        /// <summary>
        /// 取用条数。
        /// </summary>
        private int? _take;

        /// <summary>
        /// 创建查询对象（内部使用）。
        /// </summary>
        internal CacheQuery(EntityCache<TEntity, TPk> cache)
        {
            _cache = cache;
        }

        /// <summary>
        /// 追加过滤条件。
        /// </summary>
        /// <param name="predicate">过滤谓词，不允许为空。</param>
        /// <returns>当前查询对象，用于链式调用。</returns>
        public CacheQuery<TEntity, TPk> Where(Expression<Func<TEntity, bool>> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            _predicate = ExpressionHelper.TryAndAlso(_predicate, predicate);
            return this;
        }

        /// <summary>
        /// 指定升序排序键。
        /// </summary>
        /// <typeparam name="TKey">排序键类型。</typeparam>
        /// <param name="keySelector">排序键选择表达式，不允许为空。</param>
        /// <returns>当前查询对象，用于链式调用。</returns>
        public CacheQuery<TEntity, TPk> OrderBy<TKey>(Expression<Func<TEntity, TKey>> keySelector)
        {
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
            _orderBy = keySelector;
            _orderByDescending = false;
            return this;
        }

        /// <summary>
        /// 指定降序排序键。
        /// </summary>
        /// <typeparam name="TKey">排序键类型。</typeparam>
        /// <param name="keySelector">排序键选择表达式，不允许为空。</param>
        /// <returns>当前查询对象，用于链式调用。</returns>
        public CacheQuery<TEntity, TPk> OrderByDescending<TKey>(Expression<Func<TEntity, TKey>> keySelector)
        {
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
            _orderBy = keySelector;
            _orderByDescending = true;
            return this;
        }

        /// <summary>
        /// 设置跳过条数。
        /// </summary>
        /// <param name="count">跳过条数。</param>
        /// <returns>当前查询对象，用于链式调用。</returns>
        public CacheQuery<TEntity, TPk> Skip(int count)
        {
            _skip = count;
            return this;
        }

        /// <summary>
        /// 设置取用条数。
        /// </summary>
        /// <param name="count">取用条数。</param>
        /// <returns>当前查询对象，用于链式调用。</returns>
        public CacheQuery<TEntity, TPk> Take(int count)
        {
            _take = count;
            return this;
        }

        /// <summary>
        /// 异步执行查询并返回列表。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>结果列表（可能为空）。</returns>
        public Task<List<TEntity>> ToListAsync(CancellationToken ct = default)
        {
            return _cache.ToListAsync(_predicate, _orderBy, _orderByDescending, _skip, _take, ct);
        }

        /// <summary>
        /// 异步执行计数查询。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>结果数量。</returns>
        public Task<int> CountAsync(CancellationToken ct = default)
        {
            return _cache.CountAsync(_predicate, ct);
        }

        /// <summary>
        /// 异步返回第一条结果（若不存在则返回 <see langword="null"/>）。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>第一条实体或 <see langword="null"/>。</returns>
        public Task<TEntity?> FirstOrDefaultAsync(CancellationToken ct = default)
        {
            return _cache.FirstOrDefaultAsync(_predicate, _orderBy, _orderByDescending, ct);
        }
    }
}

