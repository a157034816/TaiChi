using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using TaiChi.Cache.Abstractions;
using TaiChi.Cache.Internal;
using ZiggyCreatures.Caching.Fusion;

namespace TaiChi.Cache
{
    /// <summary>
    /// 实体缓存：在本地维护按主键有序的快照，并按需从远端拉取以保证一致性。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <typeparam name="TPk">主键类型。</typeparam>
    /// <remarks>
    /// 设计要点：
    /// - 本地快照用于承载读取/过滤/排序，尽量减少远端访问；
    /// - 通过远端统计（count/minPk/maxPk）判断一致性，不一致时执行增量/补齐/全量刷新；
    /// - 通过 FusionCache 的 tag 失效来清理缓存条目，并保留 legacy tag 兼容旧的清理方式。
    /// </remarks>
    public sealed class EntityCache<TEntity, TPk> : ICacheInvalidationTarget
        where TPk : struct, IComparable<TPk>
    {
        /// <summary>
        /// 底层 FusionCache 实例。
        /// </summary>
        private readonly IFusionCache _fusionCache;

        /// <summary>
        /// 远端数据源。
        /// </summary>
        private readonly IRemoteStore<TEntity, TPk> _remote;

        /// <summary>
        /// 不可变配置。
        /// </summary>
        private readonly EntityCacheConfig<TEntity, TPk> _config;

        /// <summary>
        /// 实体类型名（用于 tag 兼容与日志字段）。
        /// </summary>
        private readonly string _typeName;

        /// <summary>
        /// 当前缓存实例的有效缓存键。
        /// </summary>
        private readonly string _cacheKey;

        /// <summary>
        /// 兼容 tag：沿用旧行为，以便上层通过 Type.Name 清理缓存时仍可生效。
        /// </summary>
        private readonly string _legacyTag;

        /// <summary>
        /// 唯一 tag：包含程序集、类型与 cacheKey，用于精确失效特定缓存实例。
        /// </summary>
        private readonly string _uniqueTag;

        /// <summary>
        /// FusionCache 条目选项。
        /// </summary>
        private readonly FusionCacheEntryOptions _entryOptions;

        /// <summary>
        /// 本地表读写锁：保护本地表结构（插入/删除/读取）。
        /// </summary>
        private readonly ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim();

        /// <summary>
        /// 状态锁：保护拉取任务与标记位等共享状态。
        /// </summary>
        private readonly object _stateGate = new object();

        /// <summary>
        /// 远端统计缓存锁：保护 _remoteStatsCache。
        /// </summary>
        private readonly object _remoteStatsGate = new object();

        /// <summary>
        /// 远端统计缓存（按 predicate 序列化结果作为键）。
        /// </summary>
        private readonly Dictionary<string, RemoteStatsCacheEntry> _remoteStatsCache = new Dictionary<string, RemoteStatsCacheEntry>();

        /// <summary>
        /// 当前缓存状态标记位。
        /// </summary>
        private CacheTypeMark _mark = CacheTypeMark.None;

        /// <summary>
        /// 拉取代际编号：用于避免“过期拉取”覆盖新状态。
        /// </summary>
        private int _fetchGeneration;

        /// <summary>
        /// 当前共享拉取任务（用于并发复用）。
        /// </summary>
        private Task? _fetchTask;

        /// <summary>
        /// 当前拉取任务的取消令牌源。
        /// </summary>
        private CancellationTokenSource? _fetchCts;

        /// <summary>
        /// 创建实体缓存实例（由 <see cref="TaiChiCache"/> 负责注册与复用）。
        /// </summary>
        /// <param name="fusionCache">FusionCache 实例。</param>
        /// <param name="remote">远端数据源。</param>
        /// <param name="config">不可变配置。</param>
        internal EntityCache(IFusionCache fusionCache, IRemoteStore<TEntity, TPk> remote, EntityCacheConfig<TEntity, TPk> config)
        {
            _fusionCache = fusionCache ?? throw new ArgumentNullException(nameof(fusionCache));
            _remote = remote ?? throw new ArgumentNullException(nameof(remote));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _typeName = typeof(TEntity).Name;
            _cacheKey = _config.EffectiveCacheKey;
            _legacyTag = _typeName;
            _uniqueTag = $"TaiChi.Cache::{typeof(TEntity).Assembly.GetName().Name}::{typeof(TEntity).FullName ?? _typeName}::{_cacheKey}";
            _entryOptions = new FusionCacheEntryOptions
            {
                Duration = _config.CacheDuration,
            };
        }

        /// <summary>
        /// 写入诊断日志（仅在配置了 sink 且满足级别/开关条件时生效）。
        /// </summary>
        /// <remarks>
        /// 该方法必须“尽力而为”：即使日志 sink 抛出异常也会被吞掉，避免影响缓存主流程。
        /// </remarks>
        /// <param name="level">日志级别。</param>
        /// <param name="eventName">事件名（用于筛选/聚合）。</param>
        /// <param name="message">日志正文。</param>
        /// <param name="exception">可选异常。</param>
        /// <param name="fields">可选结构化字段。</param>
        private void Log(
            CacheLogLevel level,
            string eventName,
            string message,
            Exception? exception = null,
            IReadOnlyDictionary<string, object?>? fields = null)
        {
            var sink = _config.DiagnosticsLogSink;
            if (sink == null)
            {
                return;
            }

            if (level < CacheLogLevel.Warn && !_config.EnableDiagnosticsLogging)
            {
                return;
            }

            try
            {
                sink(new CacheLogEntry(level, eventName, typeof(TEntity), _cacheKey, message, exception, fields));
            }
            catch
            {
                // 诊断日志不应影响缓存主流程。
            }
        }

        /// <summary>
        /// 便捷创建结构化日志字段字典。
        /// </summary>
        /// <param name="items">字段键值对。</param>
        /// <returns>只读字段字典。</returns>
        private IReadOnlyDictionary<string, object?> CreateFields(params (string Key, object? Value)[] items)
        {
            var fields = new Dictionary<string, object?>(items.Length);
            foreach (var item in items)
            {
                fields[item.Key] = item.Value;
            }

            return fields;
        }

        /// <summary>
        /// 当缓存发生失效时触发的事件。
        /// </summary>
        public event EventHandler<CacheInvalidationEventArgs>? Invalidated;

        /// <summary>
        /// 创建一个查询构建器，用于链式构造缓存查询。
        /// </summary>
        public CacheQuery<TEntity, TPk> Query()
        {
            return new CacheQuery<TEntity, TPk>(this);
        }

        /// <summary>
        /// 主动触发缓存刷新。
        /// </summary>
        /// <param name="mode">刷新模式。</param>
        /// <param name="ct">取消令牌。</param>
        public Task RefreshAsync(CacheRefreshMode mode = CacheRefreshMode.Auto, CancellationToken ct = default)
        {
            return StartRefreshAsync(mode, ct);
        }

        /// <summary>
        /// 为访问做准备：等待可能存在的共享拉取任务，并在需要时触发自动刷新与删除校验。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        public Task PrepareForAccessAsync(CancellationToken ct = default)
        {
            return PrepareForQueryAsync(ct);
        }

        /// <summary>
        /// 触发缓存失效。
        /// </summary>
        /// <param name="scope">失效范围。</param>
        /// <param name="ct">取消令牌。</param>
        public Task InvalidateAsync(CacheInvalidateScope scope, CancellationToken ct = default)
        {
            return InvalidateAsync(scope, Array.Empty<TPk>(), null, ct);
        }

        /// <summary>
        /// 按主键集合触发缓存失效。
        /// </summary>
        /// <param name="keys">主键集合。</param>
        /// <param name="ct">取消令牌。</param>
        public Task InvalidateByKeysAsync(IEnumerable<TPk> keys, CancellationToken ct = default)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            return InvalidateAsync(CacheInvalidateScope.ByKeys, keys.ToArray(), null, ct);
        }

        /// <summary>
        /// 读取当前本地快照，不触发远端访问。
        /// </summary>
        public IReadOnlyList<TEntity> GetLocalSnapshot()
        {
            var table = GetOrCreateTable();

            _rwLock.EnterReadLock();
            try
            {
                return table.GetOrCreateSnapshot();
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        /// <summary>
        /// 按主键尝试本地命中，不触发远端访问。
        /// </summary>
        public bool TryGetLocal(TPk key, out TEntity entity)
        {
            var table = GetOrCreateTable();

            _rwLock.EnterReadLock();
            try
            {
                return table.TryGetValue(key, out entity!);
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        /// <summary>
        /// 读取当前本地缓存状态，不触发远端访问。
        /// </summary>
        public EntityCacheLocalState<TPk> GetLocalState()
        {
            var table = GetOrCreateTable();
            var mark = GetMark();

            _rwLock.EnterReadLock();
            try
            {
                var (minPk, maxPk) = table.GetMinMaxKeys();
                return new EntityCacheLocalState<TPk>(
                    table.Count,
                    minPk,
                    maxPk,
                    table.LastMaxUpdatedAt,
                    table.LastDeleteCheckAt,
                    table.InsertsSinceReorder,
                    mark
                );
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        /// <summary>
        /// 当前本地缓存是否已具备可直接用于整表读取/计数的完整快照。
        /// </summary>
        public bool IsFullSnapshotReady()
        {
            var table = GetOrCreateTable();

            _rwLock.EnterReadLock();
            try
            {
                return table.FullSnapshotReady;
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        /// <summary>
        /// 执行查询并返回列表（内部由 <see cref="CacheQuery{TEntity, TPk}"/> 调用）。
        /// </summary>
        /// <param name="predicate">可选过滤谓词。</param>
        /// <param name="orderBy">可选排序表达式。</param>
        /// <param name="orderByDescending">是否降序。</param>
        /// <param name="skip">可选跳过条数。</param>
        /// <param name="take">可选取用条数。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>结果列表（可能为空）。</returns>
        /// <remarks>
        /// 该方法会先执行本地查询并对比远端统计：
        /// - 一致则直接返回本地结果；
        /// - 不一致则按策略刷新/补齐后再返回。
        /// </remarks>
        internal async Task<List<TEntity>> ToListAsync(
            Expression<Func<TEntity, bool>>? predicate,
            LambdaExpression? orderBy,
            bool orderByDescending,
            int? skip,
            int? take,
            CancellationToken ct)
        {
            await PrepareForQueryAsync(ct).ConfigureAwait(false);

            var finalPredicate = ExpressionHelper.TryAndAlso(_config.NotDeletedPredicate, predicate);

            var local = ExecuteLocalQuery(finalPredicate, orderBy, orderByDescending, skip, take);
            var remoteStats = await GetRemoteStatsAsync(finalPredicate, ct).ConfigureAwait(false);

            if (local.TotalCount == remoteStats.Count)
            {
                Log(
                    CacheLogLevel.Debug,
                    "cache.query.local-hit",
                    "本地查询结果与远端统计一致，直接返回本地结果。",
                    fields: CreateFields(
                        ("predicate", finalPredicate?.ToString()),
                        ("orderBy", orderBy?.ToString()),
                        ("orderByDescending", orderByDescending),
                        ("skip", skip),
                        ("take", take),
                        ("localCount", local.TotalCount),
                        ("remoteCount", remoteStats.Count),
                        ("source", "initial")));

                return local.Paged;
            }

            Log(
                CacheLogLevel.Info,
                "cache.query.stats-mismatch",
                "本地查询结果与远端统计不一致，准备刷新或补齐。",
                fields: CreateFields(
                    ("predicate", finalPredicate?.ToString()),
                    ("orderBy", orderBy?.ToString()),
                    ("orderByDescending", orderByDescending),
                    ("skip", skip),
                    ("take", take),
                    ("localCount", local.TotalCount),
                    ("remoteCount", remoteStats.Count)));

            await EnsureUpdatedForQueryAsync(finalPredicate, local.TotalCount, remoteStats.Count, local.AllKeys, ct).ConfigureAwait(false);

            var refreshed = ExecuteLocalQuery(finalPredicate, orderBy, orderByDescending, skip, take);

            Log(
                CacheLogLevel.Debug,
                "cache.query.local-hit",
                "刷新后返回最新本地查询结果。",
                fields: CreateFields(
                    ("predicate", finalPredicate?.ToString()),
                    ("orderBy", orderBy?.ToString()),
                    ("orderByDescending", orderByDescending),
                    ("skip", skip),
                    ("take", take),
                    ("localCount", refreshed.TotalCount),
                    ("remoteCount", remoteStats.Count),
                    ("source", "refreshed")));

            return refreshed.Paged;
        }

        /// <summary>
        /// 执行计数查询（内部由 <see cref="CacheQuery{TEntity, TPk}"/> 调用）。
        /// </summary>
        /// <param name="predicate">可选过滤谓词。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>结果数量。</returns>
        internal async Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate, CancellationToken ct)
        {
            await PrepareForQueryAsync(ct).ConfigureAwait(false);

            var finalPredicate = ExpressionHelper.TryAndAlso(_config.NotDeletedPredicate, predicate);
            var local = ExecuteLocalQuery(finalPredicate, null, false, null, null);
            var remoteStats = await GetRemoteStatsAsync(finalPredicate, ct).ConfigureAwait(false);

            if (local.TotalCount == remoteStats.Count)
            {
                Log(
                    CacheLogLevel.Debug,
                    "cache.query.local-hit",
                    "本地计数结果与远端统计一致，直接返回本地计数。",
                    fields: CreateFields(
                        ("predicate", finalPredicate?.ToString()),
                        ("localCount", local.TotalCount),
                        ("remoteCount", remoteStats.Count),
                        ("source", "initial")));

                return local.TotalCount;
            }

            Log(
                CacheLogLevel.Info,
                "cache.query.stats-mismatch",
                "本地计数结果与远端统计不一致，准备刷新或补齐。",
                fields: CreateFields(
                    ("predicate", finalPredicate?.ToString()),
                    ("localCount", local.TotalCount),
                    ("remoteCount", remoteStats.Count)));

            await EnsureUpdatedForQueryAsync(finalPredicate, local.TotalCount, remoteStats.Count, local.AllKeys, ct).ConfigureAwait(false);
            local = ExecuteLocalQuery(finalPredicate, null, false, null, null);

            Log(
                CacheLogLevel.Debug,
                "cache.query.local-hit",
                "刷新后返回最新本地计数结果。",
                fields: CreateFields(
                    ("predicate", finalPredicate?.ToString()),
                    ("localCount", local.TotalCount),
                    ("remoteCount", remoteStats.Count),
                    ("source", "refreshed")));

            return local.TotalCount;
        }

        /// <summary>
        /// 执行查询并返回第一条结果（内部由 <see cref="CacheQuery{TEntity, TPk}"/> 调用）。
        /// </summary>
        /// <param name="predicate">可选过滤谓词。</param>
        /// <param name="orderBy">可选排序表达式。</param>
        /// <param name="orderByDescending">是否降序。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>第一条实体或 <see langword="null"/>。</returns>
        internal async Task<TEntity?> FirstOrDefaultAsync(
            Expression<Func<TEntity, bool>>? predicate,
            LambdaExpression? orderBy,
            bool orderByDescending,
            CancellationToken ct)
        {
            var list = await ToListAsync(predicate, orderBy, orderByDescending, 0, 1, ct).ConfigureAwait(false);
            return list.FirstOrDefault();
        }

        /// <summary>
        /// 查询前的准备步骤：等待共享拉取、按需触发自动刷新，并执行删除一致性校验。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        private async Task PrepareForQueryAsync(CancellationToken ct)
        {
            await WaitIfFetchingAsync(ct).ConfigureAwait(false);

            if (HasMark(CacheTypeMark.NeedUpdate) && !HasMark(CacheTypeMark.Fetching))
            {
                await StartRefreshAsync(CacheRefreshMode.Auto, ct).ConfigureAwait(false);
            }

            await MaybeRunDeleteCheckAsync(ct).ConfigureAwait(false);
        }

        /// <summary>
        /// 若存在共享拉取任务，则在超时窗口内等待其完成（超时/失败时不阻断本次查询）。
        /// </summary>
        /// <remarks>
        /// 超时不会取消拉取任务；拉取失败也不会向上抛出，避免影响查询链路。
        /// </remarks>
        /// <param name="ct">取消令牌。</param>
        private async Task WaitIfFetchingAsync(CancellationToken ct)
        {
            Task? fetchTask;

            lock (_stateGate)
            {
                fetchTask = _fetchTask;
            }

            if (fetchTask == null)
            {
                return;
            }

            Log(
                CacheLogLevel.Debug,
                "cache.prepare.waiting",
                "查询前等待共享拉取任务完成。",
                fields: CreateFields(("timeoutMs", _config.WaitFetchTimeout.TotalMilliseconds)));

            var timeoutTask = Task.Delay(_config.WaitFetchTimeout, ct);
            var completed = await Task.WhenAny(fetchTask, timeoutTask).ConfigureAwait(false);
            if (completed == timeoutTask)
            {
                Log(
                    CacheLogLevel.Warn,
                    "cache.prepare.timeout",
                    "等待共享拉取超时，当前查询将继续执行。",
                    fields: CreateFields(("timeoutMs", _config.WaitFetchTimeout.TotalMilliseconds)));

                return;
            }

            try
            {
                await fetchTask.ConfigureAwait(false);
            }
            catch
            {
                // 拉取失败时不阻断查询，交由 NeedUpdate 标记触发下次重试
            }
        }

        /// <summary>
        /// 取消指定代次的拉取任务，并将缓存标记为需要刷新。
        /// </summary>
        /// <param name="generation">期望取消的代次编号。</param>
        /// <remarks>
        /// 仅当当前仍处于同一代次时才会生效；取消会推进代次，防止过期拉取结果回写。
        /// </remarks>
        private void CancelFetch(int generation)
        {
            CancellationTokenSource? cts;
            lock (_stateGate)
            {
                if (_fetchTask == null || _fetchGeneration != generation)
                {
                    return;
                }

                cts = _fetchCts;
                _fetchCts = null;
                _fetchTask = null;

                _fetchGeneration++;
                _mark &= ~CacheTypeMark.Fetching;
                _mark |= CacheTypeMark.NeedUpdate;
            }

            cts?.Cancel();
        }

        /// <summary>
        /// 若当前存在拉取任务，则发起取消（线程安全）。
        /// </summary>
        private void CancelFetchIfRunning()
        {
            int generation;
            lock (_stateGate)
            {
                if (_fetchTask == null)
                {
                    return;
                }

                generation = _fetchGeneration;
            }

            CancelFetch(generation);
        }

        /// <summary>
        /// 判断是否包含指定缓存标记位（线程安全）。
        /// </summary>
        private bool HasMark(CacheTypeMark mark)
        {
            lock (_stateGate)
            {
                return (_mark & mark) != 0;
            }
        }

        /// <summary>
        /// 读取当前缓存标记位集合（线程安全）。
        /// </summary>
        private CacheTypeMark GetMark()
        {
            lock (_stateGate)
            {
                return _mark;
            }
        }

        /// <summary>
        /// 启用/禁用指定缓存标记位（线程安全）。
        /// </summary>
        /// <param name="mark">要修改的标记位。</param>
        /// <param name="enabled">是否启用。</param>
        private void SetMark(CacheTypeMark mark, bool enabled)
        {
            lock (_stateGate)
            {
                if (enabled)
                {
                    _mark |= mark;
                }
                else
                {
                    _mark &= ~mark;
                }
            }
        }

        /// <summary>
        /// 在配置的时间间隔内执行一次“删除一致性”校验（通过远端统计判断是否需要全量刷新）。
        /// </summary>
        /// <remarks>
        /// 该校验只用于发现“远端删除导致本地多出数据”的情况；一旦发现不一致，会触发全量刷新兜底。
        /// </remarks>
        /// <param name="ct">取消令牌。</param>
        private async Task MaybeRunDeleteCheckAsync(CancellationToken ct)
        {
            if (_config.DeleteCheckInterval < TimeSpan.Zero)
            {
                return;
            }

            var table = GetOrCreateTable();

            int localCount;
            DateTimeOffset? last;

            _rwLock.EnterReadLock();
            try
            {
                localCount = table.Count;
                last = table.LastDeleteCheckAt;
            }
            finally
            {
                _rwLock.ExitReadLock();
            }

            // 初次/空缓存时不做删除校验：由“count不一致→补齐/刷新”流程接管。
            if (localCount == 0)
            {
                return;
            }

            var now = DateTimeOffset.UtcNow;

            if (last != null && _config.DeleteCheckInterval > TimeSpan.Zero && now - last < _config.DeleteCheckInterval)
            {
                return;
            }

            table.LastDeleteCheckAt = now;

            TPk? localMin;
            TPk? localMax;

            _rwLock.EnterReadLock();
            try
            {
                localCount = table.Count;
                (localMin, localMax) = table.GetMinMaxKeys();
            }
            finally
            {
                _rwLock.ExitReadLock();
            }

            // 删除校验开始后，本地可能被刷新/清空；此时无需继续校验。
            if (localCount == 0)
            {
                return;
            }

            var remoteStats = await GetRemoteStatsAsync(_config.NotDeletedPredicate, ct).ConfigureAwait(false);

            bool mismatch = localCount != remoteStats.Count;
            if (!mismatch && localCount > 0)
            {
                mismatch = !Nullable.Equals(localMin, remoteStats.MinPk) || !Nullable.Equals(localMax, remoteStats.MaxPk);
            }

            if (mismatch)
            {
                await StartRefreshAsync(CacheRefreshMode.Full, ct).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 获取或创建本地缓存表（使用 FusionCache 存储，并绑定 tag 便于失效清理）。
        /// </summary>
        private CacheTable<TEntity, TPk> GetOrCreateTable()
        {
            return _fusionCache.GetOrSet(
                _cacheKey,
                _ => new CacheTable<TEntity, TPk>(),
                _entryOptions,
                tags: new[] { _legacyTag, _uniqueTag }
            );
        }

        /// <summary>
        /// 获取远端统计信息，并在短窗口内复用结果以降低远端压力。
        /// </summary>
        /// <param name="predicate">可选过滤谓词（会参与缓存 key 计算）。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>远端统计结果。</returns>
        private async Task<RemoteStats<TPk>> GetRemoteStatsAsync(Expression<Func<TEntity, bool>>? predicate, CancellationToken ct)
        {
            if (_config.RemoteStatsReuseWindow <= TimeSpan.Zero)
            {
                return await _remote.GetStatsAsync(predicate, ct).ConfigureAwait(false);
            }

            var cacheKey = GetRemoteStatsCacheKey(predicate);
            var now = DateTimeOffset.UtcNow;

            lock (_remoteStatsGate)
            {
                if (_remoteStatsCache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAt > now)
                {
                    return cached.Stats;
                }
            }

            var stats = await _remote.GetStatsAsync(predicate, ct).ConfigureAwait(false);

            lock (_remoteStatsGate)
            {
                _remoteStatsCache[cacheKey] = new RemoteStatsCacheEntry(stats, DateTimeOffset.UtcNow.Add(_config.RemoteStatsReuseWindow));
            }

            return stats;
        }

        /// <summary>
        /// 为远端统计缓存构造 key（包含表达式文本与闭包捕获值，避免不同闭包值误命中同一缓存项）。
        /// </summary>
        private string GetRemoteStatsCacheKey(Expression<Func<TEntity, bool>>? predicate)
        {
            if (predicate == null)
            {
                return "*";
            }

            var key = predicate.ToString();
            var capturedValues = CapturedValueKeyBuilder.Build(predicate.Body);
            if (string.IsNullOrEmpty(capturedValues))
            {
                return key;
            }

            return $"{key}|{capturedValues}";
        }

        /// <summary>
        /// 清空远端统计缓存（例如发生失效/刷新后需要重新获取统计）。
        /// </summary>
        private void ClearRemoteStatsCache()
        {
            lock (_remoteStatsGate)
            {
                _remoteStatsCache.Clear();
            }
        }

        /// <summary>
        /// 远端统计缓存条目（用于在短窗口内复用 stats 查询结果）。
        /// </summary>
        private sealed class RemoteStatsCacheEntry
        {
            /// <summary>
            /// 创建远端统计缓存条目。
            /// </summary>
            /// <param name="stats">远端统计信息。</param>
            /// <param name="expiresAt">过期时间。</param>
            public RemoteStatsCacheEntry(RemoteStats<TPk> stats, DateTimeOffset expiresAt)
            {
                Stats = stats;
                ExpiresAt = expiresAt;
            }

            /// <summary>
            /// 远端统计信息。
            /// </summary>
            public RemoteStats<TPk> Stats { get; }

            /// <summary>
            /// 过期时间（UTC）。
            /// </summary>
            public DateTimeOffset ExpiresAt { get; }
        }

        /// <summary>
        /// 捕获谓词表达式中的闭包值，构建远端统计缓存 key，避免仅靠 ToString 导致不同闭包值命中同一 key。
        /// </summary>
        private sealed class CapturedValueKeyBuilder : ExpressionVisitor
        {
            /// <summary>
            /// 已捕获并序列化的闭包值片段（用于拼接远端统计缓存 key）。
            /// </summary>
            private readonly List<string> _values = new List<string>();

            /// <summary>
            /// 从表达式中提取可求值的常量/闭包成员/方法调用结果，并序列化为 key 片段。
            /// </summary>
            public static string Build(Expression expression)
            {
                var builder = new CapturedValueKeyBuilder();
                builder.Visit(expression);
                return string.Join("|", builder._values);
            }

            protected override Expression VisitMember(MemberExpression node)
            {
                AppendEvaluatedValue(node);
                return base.VisitMember(node);
            }

            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                AppendEvaluatedValue(node);
                return base.VisitMethodCall(node);
            }

            /// <summary>
            /// 尝试对表达式片段求值并追加到 key 片段集合。
            /// </summary>
            /// <remarks>
            /// 若表达式包含参数（依赖实体实例），则不可安全求值，会直接跳过。
            /// </remarks>
            private void AppendEvaluatedValue(Expression node)
            {
                if (ContainsParameter(node))
                {
                    return;
                }

                if (TryEvaluate(node, out var value))
                {
                    _values.Add(SerializeValue(value));
                }
            }

            /// <summary>
            /// 判断表达式片段是否包含参数引用（包含则表示依赖实体实例，不能安全求值）。
            /// </summary>
            private static bool ContainsParameter(Expression node)
            {
                var detector = new ParameterDetector();
                detector.Visit(node);
                return detector.Found;
            }

            /// <summary>
            /// 尝试将表达式片段编译并执行，获取其运行时值。
            /// </summary>
            /// <param name="node">待求值的表达式片段。</param>
            /// <param name="value">求值结果。</param>
            /// <returns>成功返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
            private static bool TryEvaluate(Expression node, out object? value)
            {
                try
                {
                    value = Expression.Lambda<Func<object?>>(Expression.Convert(node, typeof(object))).Compile()();
                    return true;
                }
                catch
                {
                    value = null;
                    return false;
                }
            }

            /// <summary>
            /// 将捕获值序列化为稳定字符串（用于 cache key 组成部分）。
            /// </summary>
            /// <remarks>
            /// 针对常见类型做特殊处理，确保跨文化设置下的稳定性（例如时间使用 ISO 8601 格式）。
            /// </remarks>
            private static string SerializeValue(object? value)
            {
                if (value == null)
                {
                    return "<null>";
                }

                if (value is string text)
                {
                    return text;
                }

                if (value is DateTimeOffset dto)
                {
                    return dto.ToString("O", CultureInfo.InvariantCulture);
                }

                if (value is DateTime dt)
                {
                    return dt.ToString("O", CultureInfo.InvariantCulture);
                }

                if (value is System.Collections.IEnumerable enumerable)
                {
                    var list = new List<string>();
                    foreach (var item in enumerable)
                    {
                        list.Add(SerializeValue(item));
                    }

                    return $"[{string.Join(",", list)}]";
                }

                return Convert.ToString(value, CultureInfo.InvariantCulture) ?? value.ToString() ?? string.Empty;
            }

            /// <summary>
            /// 用于判断某个表达式片段是否包含参数（包含则不可安全求值）。
            /// </summary>
            private sealed class ParameterDetector : ExpressionVisitor
            {
                /// <summary>
                /// 是否发现参数节点。
                /// </summary>
                public bool Found { get; private set; }

                protected override Expression VisitParameter(ParameterExpression node)
                {
                    Found = true;
                    return node;
                }
            }
        }

        /// <summary>
        /// 本地查询执行结果（包含分页结果、总数与命中的主键集合）。
        /// </summary>
        private sealed class LocalQueryResult
        {
            /// <summary>
            /// 创建本地查询结果。
            /// </summary>
            /// <param name="paged">分页结果。</param>
            /// <param name="totalCount">总数。</param>
            /// <param name="allKeys">命中的主键集合。</param>
            public LocalQueryResult(List<TEntity> paged, int totalCount, HashSet<TPk> allKeys)
            {
                Paged = paged;
                TotalCount = totalCount;
                AllKeys = allKeys;
            }

            /// <summary>
            /// 分页结果。
            /// </summary>
            public List<TEntity> Paged { get; }

            /// <summary>
            /// 总数。
            /// </summary>
            public int TotalCount { get; }

            /// <summary>
            /// 命中的主键集合。
            /// </summary>
            public HashSet<TPk> AllKeys { get; }
        }

        /// <summary>
        /// 在本地快照上执行查询（过滤/排序/分页），并返回分页结果与统计信息。
        /// </summary>
        /// <remarks>
        /// 该方法不会修改缓存表，只在读锁保护下读取 <see cref="CacheTable{TEntity, TPk}"/> 的快照。
        /// </remarks>
        private LocalQueryResult ExecuteLocalQuery(
            Expression<Func<TEntity, bool>>? predicate,
            LambdaExpression? orderBy,
            bool orderByDescending,
            int? skip,
            int? take)
        {
            var table = GetOrCreateTable();
            Func<TEntity, bool>? compiledPredicate = predicate?.Compile();

            _rwLock.EnterReadLock();
            try
            {
                IEnumerable<TEntity> filtered = table.Items;
                if (compiledPredicate != null)
                {
                    filtered = filtered.Where(compiledPredicate);
                }

                var filteredList = filtered.ToList();
                var keys = new HashSet<TPk>(filteredList.Count);
                foreach (var item in filteredList)
                {
                    keys.Add(_config.PrimaryKeyGetter(item));
                }

                IEnumerable<TEntity> page = filteredList;
                if (orderBy != null)
                {
                    page = ApplyOrderBy(page, orderBy, orderByDescending);
                }

                if (skip != null)
                {
                    page = page.Skip(skip.Value);
                }

                if (take != null)
                {
                    page = page.Take(take.Value);
                }

                return new LocalQueryResult(page.ToList(), filteredList.Count, keys);
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        /// <summary>
        /// 在本地对结果集应用排序（使用表达式编译并通过 <see cref="Delegate.DynamicInvoke"/> 取键）。
        /// </summary>
        /// <remarks>
        /// 该实现偏向通用性而非极致性能，仅用于本地快照查询路径。
        /// </remarks>
        private static IEnumerable<TEntity> ApplyOrderBy(IEnumerable<TEntity> source, LambdaExpression orderBy, bool descending)
        {
            var compiled = orderBy.Compile();
            if (descending)
            {
                return source.OrderByDescending(x => compiled.DynamicInvoke(x));
            }

            return source.OrderBy(x => compiled.DynamicInvoke(x));
        }

        /// <summary>
        /// 根据本地与远端统计差异，触发刷新或“缺口补齐”，以尽量保证查询结果的完整性。
        /// </summary>
        /// <param name="predicate">当前查询的过滤谓词（可为 null）。</param>
        /// <param name="localCount">本地命中数量。</param>
        /// <param name="remoteCount">远端统计数量。</param>
        /// <param name="localKeys">本地命中的主键集合。</param>
        /// <param name="ct">取消令牌。</param>
        private async Task EnsureUpdatedForQueryAsync(
            Expression<Func<TEntity, bool>>? predicate,
            int localCount,
            int remoteCount,
            HashSet<TPk> localKeys,
            CancellationToken ct)
        {
            SetMark(CacheTypeMark.NeedUpdate, true);

            if (remoteCount <= localCount)
            {
                await StartRefreshAsync(CacheRefreshMode.Auto, ct).ConfigureAwait(false);
                return;
            }

            if (predicate != null && _remote is IRemoteKeysetStore<TEntity, TPk> keysetStore)
            {
                await StartFillMissingAsync(keysetStore, predicate, remoteCount - localCount, localKeys, ct).ConfigureAwait(false);
                return;
            }

            // 无缺口补齐能力时，直接全量兜底（增量无法保证补齐“历史缺失数据”）
            await StartRefreshAsync(CacheRefreshMode.Full, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// 启动缓存刷新任务（增量可用则增量，否则全量替换），并复用共享拉取任务以避免并发重复拉取。
        /// </summary>
        private Task StartRefreshAsync(CacheRefreshMode mode, CancellationToken ct)
        {
            return StartFetchAsync(
                "refresh",
                "cache.refresh.start",
                "开始刷新缓存。",
                CreateFields(("mode", mode.ToString())),
                "cache.refresh.failed",
                "刷新缓存失败。",
                async (generation, token) =>
                {
                    IReadOnlyList<TEntity> fetched;
                    var strategy = "full";
                    DateTimeOffset? since = null;

                    if (mode == CacheRefreshMode.Auto
                        && _config.UpdatedAtGetter != null
                        && _remote is IRemoteUpdatedSinceStore<TEntity> updatedSinceStore)
                    {
                        var table = GetOrCreateTable();
                        since = table.LastMaxUpdatedAt;
                        if (since != null)
                        {
                            strategy = "updated-since";
                            fetched = await updatedSinceStore.FetchUpdatedSinceAsync(since.Value, token).ConfigureAwait(false);
                            ApplyUpserts(generation, fetched, markSnapshotIncomplete: false);

                            if (IsCurrentGeneration(generation))
                            {
                                Log(
                                    CacheLogLevel.Info,
                                    "cache.refresh.completed",
                                    "增量刷新完成。",
                                    fields: CreateFields(
                                        ("mode", mode.ToString()),
                                        ("strategy", strategy),
                                        ("since", since),
                                        ("appliedCount", fetched.Count)));
                            }

                            return;
                        }
                    }

                    fetched = await _remote.FetchAllAsync(token).ConfigureAwait(false);
                    ApplyFullReplace(generation, fetched);

                    if (IsCurrentGeneration(generation))
                    {
                        Log(
                            CacheLogLevel.Info,
                            "cache.refresh.completed",
                            "全量刷新完成。",
                            fields: CreateFields(
                                ("mode", mode.ToString()),
                                ("strategy", strategy),
                                ("appliedCount", fetched.Count)));
                    }
                },
                ct
            );
        }

        /// <summary>
        /// 启动“缺口补齐”任务：按 keyset 分页找出缺失主键，再按主键批量拉取并写入本地缓存。
        /// </summary>
        /// <remarks>
        /// 该路径用于应对“本地缺数据但远端统计更大”的情况；补齐后会将快照标记为不完整，
        /// 后续仍可能通过刷新流程收敛到全量一致。
        /// </remarks>
        private Task StartFillMissingAsync(
            IRemoteKeysetStore<TEntity, TPk> keysetStore,
            Expression<Func<TEntity, bool>> predicate,
            int diff,
            HashSet<TPk> localKeys,
            CancellationToken ct)
        {
            if (diff <= 0)
            {
                return Task.CompletedTask;
            }

            return StartFetchAsync(
                "fill-missing",
                "cache.fill-missing.start",
                "开始按缺口主键补齐缓存。",
                CreateFields(
                    ("predicate", predicate.ToString()),
                    ("expectedMissingCount", diff)),
                "cache.fill-missing.failed",
                "按缺口补齐缓存失败。",
                async (generation, token) =>
                {
                    var missingKeys = new List<TPk>(diff);
                    TPk? afterKey = null;
                    bool hasMore = true;
                    int fetchedEntityCount = 0;

                    while (hasMore && missingKeys.Count < diff)
                    {
                        token.ThrowIfCancellationRequested();

                        var page = await keysetStore.QueryKeysAsync(
                            predicate,
                            new KeysetPageRequest<TPk>(afterKey, _config.KeysetPageSize),
                            token
                        ).ConfigureAwait(false);

                        foreach (var key in page.Keys)
                        {
                            if (!localKeys.Contains(key))
                            {
                                missingKeys.Add(key);
                                if (missingKeys.Count >= diff)
                                {
                                    break;
                                }
                            }
                        }

                        hasMore = page.HasMore;
                        afterKey = page.NextAfterKey;
                        if (page.Keys.Count == 0)
                        {
                            break;
                        }
                    }

                    for (int i = 0; i < missingKeys.Count; i += _config.FetchByKeysBatchSize)
                    {
                        var batch = missingKeys.Skip(i).Take(_config.FetchByKeysBatchSize).ToArray();
                        var entities = await keysetStore.FetchByKeysAsync(batch, token).ConfigureAwait(false);
                        fetchedEntityCount += entities.Count;
                        ApplyUpserts(generation, entities, markSnapshotIncomplete: true);
                    }

                    if (IsCurrentGeneration(generation))
                    {
                        Log(
                            CacheLogLevel.Info,
                            "cache.fill-missing.completed",
                            "缺口补齐完成。",
                            fields: CreateFields(
                                ("predicate", predicate.ToString()),
                                ("expectedMissingCount", diff),
                                ("missingKeyCount", missingKeys.Count),
                                ("fetchedEntityCount", fetchedEntityCount)));
                    }
                },
                ct
            );
        }

        /// <summary>
        /// 统一启动/复用共享拉取任务：同一时间只允许一个拉取在跑，其他调用方复用同一个任务。
        /// </summary>
        /// <param name="operationName">操作名（用于日志字段）。</param>
        /// <param name="startEventName">开始事件名。</param>
        /// <param name="startMessage">开始日志内容。</param>
        /// <param name="startFields">开始日志字段。</param>
        /// <param name="failedEventName">失败事件名。</param>
        /// <param name="failedMessage">失败日志内容。</param>
        /// <param name="action">实际执行动作（接收 generation 与取消令牌）。</param>
        /// <param name="ct">调用方取消令牌。</param>
        private Task StartFetchAsync(
            string operationName,
            string startEventName,
            string startMessage,
            IReadOnlyDictionary<string, object?>? startFields,
            string failedEventName,
            string failedMessage,
            Func<int, CancellationToken, Task> action,
            CancellationToken ct)
        {
            Task task;
            int generation;
            CancellationTokenSource cts;

            lock (_stateGate)
            {
                if ((_mark & CacheTypeMark.Fetching) != 0 && _fetchTask != null)
                {
                    return _fetchTask;
                }

                _fetchGeneration++;
                generation = _fetchGeneration;

                cts = new CancellationTokenSource();
                _fetchCts = cts;
                _mark |= CacheTypeMark.Fetching;

                task = RunFetchAsync(generation, cts, operationName, failedEventName, failedMessage, action, ct);
                _fetchTask = task;
            }

            Log(CacheLogLevel.Info, startEventName, startMessage, fields: startFields);
            return task;
        }

        /// <summary>
        /// 执行拉取任务，并在代次一致时更新缓存标记；代次不一致时丢弃“过期结果”。
        /// </summary>
        /// <remarks>
        /// 该方法负责把调用方取消与内部取消合并，并在失败/取消时将缓存标记为需要更新。
        /// </remarks>
        private async Task RunFetchAsync(
            int generation,
            CancellationTokenSource cts,
            string operationName,
            string failedEventName,
            string failedMessage,
            Func<int, CancellationToken, Task> action,
            CancellationToken callerToken)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(callerToken, cts.Token);

            try
            {
                await action(generation, linked.Token).ConfigureAwait(false);
                if (IsCurrentGeneration(generation))
                {
                    SetMark(CacheTypeMark.NeedUpdate, false);
                }
                else
                {
                    Log(
                        CacheLogLevel.Warn,
                        "cache.fetch.stale-discarded",
                        "抓取任务完成时已被更新代次替代，结果不会标记为最新。",
                        fields: CreateFields(
                            ("operation", operationName),
                            ("generation", generation)));
                }
            }
            catch (OperationCanceledException) when (!callerToken.IsCancellationRequested)
            {
                if (IsCurrentGeneration(generation))
                {
                    SetMark(CacheTypeMark.NeedUpdate, true);
                }
            }
            catch (OperationCanceledException) when (callerToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (IsCurrentGeneration(generation))
                {
                    SetMark(CacheTypeMark.NeedUpdate, true);
                }

                Log(
                    CacheLogLevel.Error,
                    failedEventName,
                    failedMessage,
                    ex,
                    CreateFields(
                        ("operation", operationName),
                        ("generation", generation)));

                throw;
            }
            finally
            {
                lock (_stateGate)
                {
                    if (_fetchGeneration == generation)
                    {
                        _mark &= ~CacheTypeMark.Fetching;
                        _fetchTask = null;
                        _fetchCts = null;
                    }
                }

                cts.Dispose();
            }
        }

        /// <summary>
        /// 判断给定代次是否仍为当前代次（用于防止过期拉取覆盖新状态）。
        /// </summary>
        private bool IsCurrentGeneration(int generation)
        {
            lock (_stateGate)
            {
                return _fetchGeneration == generation;
            }
        }

        /// <summary>
        /// 全量替换本地快照（清空后批量 Upsert），并在代次一致时标记快照已就绪。
        /// </summary>
        /// <param name="generation">拉取代次。</param>
        /// <param name="entities">拉取到的实体集合。</param>
        private void ApplyFullReplace(int generation, IReadOnlyList<TEntity> entities)
        {
            bool staleDiscarded = false;

            _rwLock.EnterWriteLock();
            try
            {
                if (!IsCurrentGeneration(generation))
                {
                    staleDiscarded = true;
                    return;
                }

                var table = GetOrCreateTable();
                table.Clear();
                table.UpsertMany(entities, _config.PrimaryKeyGetter, _config.UpdatedAtGetter, _config.IsDeleted, _config.ReorderThreshold);
                table.FullSnapshotReady = true;
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }

            if (staleDiscarded)
            {
                Log(
                    CacheLogLevel.Warn,
                    "cache.fetch.stale-discarded",
                    "检测到过期抓取结果，已放弃覆盖本地缓存。",
                    fields: CreateFields(
                        ("generation", generation),
                        ("entityCount", entities.Count)));
            }
        }

        /// <summary>
        /// 增量写入本地快照（批量 Upsert），并可选择将快照标记为“不完整”。
        /// </summary>
        /// <param name="generation">拉取代次。</param>
        /// <param name="entities">需要写入的实体集合。</param>
        /// <param name="markSnapshotIncomplete">是否将快照标记为不完整（例如缺口补齐路径）。</param>
        private void ApplyUpserts(int generation, IReadOnlyList<TEntity> entities, bool markSnapshotIncomplete)
        {
            bool staleDiscarded = false;

            _rwLock.EnterWriteLock();
            try
            {
                if (!IsCurrentGeneration(generation))
                {
                    staleDiscarded = true;
                    return;
                }

                var table = GetOrCreateTable();
                if (markSnapshotIncomplete)
                {
                    table.FullSnapshotReady = false;
                }

                if (entities.Count > 0)
                {
                    table.UpsertMany(entities, _config.PrimaryKeyGetter, _config.UpdatedAtGetter, _config.IsDeleted, _config.ReorderThreshold);
                }
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }

            if (staleDiscarded)
            {
                Log(
                    CacheLogLevel.Warn,
                    "cache.fetch.stale-discarded",
                    "检测到过期抓取结果，已放弃写入本地缓存。",
                    fields: CreateFields(
                        ("generation", generation),
                        ("entityCount", entities.Count),
                        ("markSnapshotIncomplete", markSnapshotIncomplete)));
            }
        }

        Task ICacheInvalidationTarget.InvalidateAsync(CacheInvalidateScope scope, CancellationToken ct)
        {
            return InvalidateAsync(scope, Array.Empty<TPk>(), null, ct);
        }

        /// <summary>
        /// 执行缓存失效：全量失效会按 tag 清理 FusionCache；按主键失效会从本地表移除指定 key 并标记需要刷新。
        /// </summary>
        /// <param name="scope">失效范围。</param>
        /// <param name="keys">按主键失效时的 key 集合。</param>
        /// <param name="reason">可选失效原因（用于日志/事件）。</param>
        /// <param name="ct">取消令牌。</param>
        private Task InvalidateAsync(CacheInvalidateScope scope, IReadOnlyCollection<TPk> keys, string? reason, CancellationToken ct)
        {
            if (scope == CacheInvalidateScope.Full)
            {
                _rwLock.EnterWriteLock();
                try
                {
                    CancelFetchIfRunning();
                    _fusionCache.RemoveByTag(_uniqueTag);
                }
                finally
                {
                    _rwLock.ExitWriteLock();
                }

                ClearRemoteStatsCache();
                SetMark(CacheTypeMark.NeedUpdate, true);

                Log(
                    CacheLogLevel.Info,
                    "cache.invalidate.full",
                    "已执行缓存全量失效。",
                    fields: CreateFields(("reason", reason)));

                Invalidated?.Invoke(this, new CacheInvalidationEventArgs(scope, typeof(TEntity), null, reason));
                return Task.CompletedTask;
            }

            _rwLock.EnterWriteLock();
            try
            {
                CancelFetchIfRunning();

                var table = GetOrCreateTable();
                table.FullSnapshotReady = false;

                foreach (var key in keys)
                {
                    table.RemoveByKey(key);
                }
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }

            ClearRemoteStatsCache();
            SetMark(CacheTypeMark.NeedUpdate, true);

            Log(
                CacheLogLevel.Info,
                "cache.invalidate.by-keys",
                "已按主键执行缓存失效。",
                fields: CreateFields(
                    ("reason", reason),
                    ("keyCount", keys.Count)));

            Invalidated?.Invoke(this, new CacheInvalidationEventArgs(scope, typeof(TEntity), keys.Cast<object>().ToArray(), reason));
            return Task.CompletedTask;
        }
    }
}
