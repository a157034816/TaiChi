using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using TaiChi.Cache.Abstractions;

namespace TaiChi.Cache.Test.Fakes
{
    /// <summary>
    /// 测试用远端存储实现（内存版）：用于模拟远端数据与调用次数统计。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <typeparam name="TPk">主键类型。</typeparam>
    /// <remarks>
    /// 额外提供 Hook 能力，用于构造“远端拉取阻塞/返回特定结果”等场景，便于验证并发与失效语义。
    /// </remarks>
    public sealed class FakeRemoteStore<TEntity, TPk> :
        IRemoteStore<TEntity, TPk>,
        IRemoteKeysetStore<TEntity, TPk>,
        IRemoteUpdatedSinceStore<TEntity>
        where TPk : struct, IComparable<TPk>
    {
        /// <summary>
        /// 主键获取器。
        /// </summary>
        private readonly Func<TEntity, TPk> _keyGetter;

        /// <summary>
        /// 更新时间获取器（可选）。
        /// </summary>
        private readonly Func<TEntity, DateTimeOffset?>? _updatedAtGetter;

        /// <summary>
        /// 远端数据快照（受 <see cref="_gate"/> 保护）。
        /// </summary>
        private readonly List<TEntity> _data = new List<TEntity>();

        /// <summary>
        /// 线程同步锁，保证 Seed/读取互斥。
        /// </summary>
        private readonly object _gate = new object();

        /// <summary>
        /// 仅用于注入副作用（阻塞/延迟），不改变返回数据。
        /// </summary>
        private Func<int, CancellationToken, Task>? _fetchAllHook;

        /// <summary>
        /// 用于注入 FetchAll 返回值（可模拟分页/空返回等）。
        /// </summary>
        private Func<int, CancellationToken, Task<IReadOnlyList<TEntity>>>? _fetchAllResultHook;

        /// <summary>
        /// 创建测试用远端存储。
        /// </summary>
        /// <param name="keyGetter">主键获取器。</param>
        /// <param name="updatedAtGetter">更新时间获取器（可选）。</param>
        public FakeRemoteStore(Func<TEntity, TPk> keyGetter, Func<TEntity, DateTimeOffset?>? updatedAtGetter = null)
        {
            _keyGetter = keyGetter ?? throw new ArgumentNullException(nameof(keyGetter));
            _updatedAtGetter = updatedAtGetter;
        }

        /// <summary>
        /// <see cref="FetchAllAsync"/> 调用次数。
        /// </summary>
        public int FetchAllCallCount { get; private set; }

        /// <summary>
        /// <see cref="GetStatsAsync"/> 调用次数。
        /// </summary>
        public int GetStatsCallCount { get; private set; }

        /// <summary>
        /// <see cref="FetchUpdatedSinceAsync"/> 调用次数。
        /// </summary>
        public int FetchUpdatedSinceCallCount { get; private set; }

        /// <summary>
        /// <see cref="QueryKeysAsync"/> 调用次数。
        /// </summary>
        public int QueryKeysCallCount { get; private set; }

        /// <summary>
        /// <see cref="FetchByKeysAsync"/> 调用次数。
        /// </summary>
        public int FetchByKeysCallCount { get; private set; }

        /// <summary>
        /// 清空所有 Hook。
        /// </summary>
        public void ResetHooks()
        {
            _fetchAllHook = null;
            _fetchAllResultHook = null;
        }

        /// <summary>
        /// 设置 FetchAll 的副作用 Hook（不改变返回值）。
        /// </summary>
        public void SetFetchAllHook(Func<int, CancellationToken, Task> hook)
        {
            _fetchAllHook = hook ?? throw new ArgumentNullException(nameof(hook));
        }

        /// <summary>
        /// 设置 FetchAll 的结果 Hook（用于替换返回值）。
        /// </summary>
        public void SetFetchAllResultHook(Func<int, CancellationToken, Task<IReadOnlyList<TEntity>>> hook)
        {
            _fetchAllResultHook = hook ?? throw new ArgumentNullException(nameof(hook));
        }

        /// <summary>
        /// 重新注入远端数据（会清空旧数据）。
        /// </summary>
        public void Seed(params TEntity[] entities)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            lock (_gate)
            {
                _data.Clear();
                _data.AddRange(entities);
            }
        }

        /// <summary>
        /// 追加一条远端数据。
        /// </summary>
        public void Add(TEntity entity)
        {
            lock (_gate)
            {
                _data.Add(entity);
            }
        }

        /// <inheritdoc />
        public Task<RemoteStats<TPk>> GetStatsAsync(Expression<Func<TEntity, bool>>? predicate, CancellationToken ct)
        {
            GetStatsCallCount++;
            ct.ThrowIfCancellationRequested();

            List<TEntity> snapshot;
            lock (_gate)
            {
                snapshot = _data.ToList();
            }

            IEnumerable<TEntity> query = snapshot;
            if (predicate != null)
            {
                var compiled = predicate.Compile();
                query = query.Where(compiled);
            }

            var keys = query.Select(_keyGetter).OrderBy(x => x).ToList();
            TPk? min = keys.Count > 0 ? keys[0] : null;
            TPk? max = keys.Count > 0 ? keys[keys.Count - 1] : null;
            return Task.FromResult(new RemoteStats<TPk>(keys.Count, min, max));
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<TEntity>> FetchAllAsync(CancellationToken ct)
        {
            FetchAllCallCount++;

            if (_fetchAllResultHook != null)
            {
                return await _fetchAllResultHook(FetchAllCallCount, ct).ConfigureAwait(false);
            }

            if (_fetchAllHook != null)
            {
                await _fetchAllHook(FetchAllCallCount, ct).ConfigureAwait(false);
            }

            ct.ThrowIfCancellationRequested();

            lock (_gate)
            {
                return _data.ToList();
            }
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<TEntity>> FetchUpdatedSinceAsync(DateTimeOffset sinceExclusive, CancellationToken ct)
        {
            FetchUpdatedSinceCallCount++;
            ct.ThrowIfCancellationRequested();

            if (_updatedAtGetter == null)
            {
                return Task.FromResult((IReadOnlyList<TEntity>)Array.Empty<TEntity>());
            }

            lock (_gate)
            {
                var result = _data
                    .Where(x => _updatedAtGetter(x) != null && _updatedAtGetter(x) > sinceExclusive)
                    .ToList();
                return Task.FromResult((IReadOnlyList<TEntity>)result);
            }
        }

        /// <inheritdoc />
        public Task<KeysetPage<TPk>> QueryKeysAsync(Expression<Func<TEntity, bool>> predicate, KeysetPageRequest<TPk> request, CancellationToken ct)
        {
            QueryKeysCallCount++;
            ct.ThrowIfCancellationRequested();

            List<TEntity> snapshot;
            lock (_gate)
            {
                snapshot = _data.ToList();
            }

            var compiled = predicate.Compile();
            var keys = snapshot
                .Where(compiled)
                .Select(_keyGetter)
                .OrderBy(x => x)
                .ToList();

            if (request.AfterKey != null)
            {
                keys = keys.Where(x => x.CompareTo(request.AfterKey.Value) > 0).ToList();
            }

            var pageKeys = keys.Take(request.Take).ToList();
            var nextAfterKey = pageKeys.Count > 0 ? pageKeys[pageKeys.Count - 1] : request.AfterKey;
            var hasMore = keys.Count > pageKeys.Count;

            return Task.FromResult(new KeysetPage<TPk>(pageKeys, nextAfterKey, hasMore));
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<TEntity>> FetchByKeysAsync(IReadOnlyList<TPk> keys, CancellationToken ct)
        {
            FetchByKeysCallCount++;
            ct.ThrowIfCancellationRequested();

            lock (_gate)
            {
                var set = keys.ToHashSet();
                var result = _data.Where(x => set.Contains(_keyGetter(x))).ToList();
                return Task.FromResult((IReadOnlyList<TEntity>)result);
            }
        }
    }
}
