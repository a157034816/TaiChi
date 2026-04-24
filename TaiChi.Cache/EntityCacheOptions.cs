using System;
using System.Linq.Expressions;

namespace TaiChi.Cache
{
    /// <summary>
    /// 实体缓存配置构建器（按实体/缓存实例级别配置）。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <typeparam name="TPk">主键类型。</typeparam>
    /// <remarks>
    /// 该类型在 <see cref="TaiChiCache.For{TEntity, TPk}"/> 的 configure 回调中使用：
    /// - 必须通过 <see cref="WithPrimaryKey"/> 指定主键；
    /// - 可选指定更新时间/删除时间，以启用增量拉取与删除过滤；
    /// - 其他参数未配置时将使用 <see cref="TaiChiCacheOptions"/> 的全局默认值。
    /// </remarks>
    public sealed class EntityCacheOptions<TEntity, TPk>
        where TPk : struct, IComparable<TPk>
    {
        /// <summary>
        /// 全局默认配置（当某项未显式设置时将回落到该值）。
        /// </summary>
        private readonly TaiChiCacheOptions _global;

        /// <summary>
        /// 主键选择表达式（必选）。
        /// </summary>
        private Expression<Func<TEntity, TPk>>? _primaryKeySelector;

        /// <summary>
        /// 更新时间字段选择表达式（可选；支持 <see cref="DateTime"/> / <see cref="DateTimeOffset"/>，统一以 <see cref="LambdaExpression"/> 暂存）。
        /// </summary>
        private LambdaExpression? _updatedAtSelector;

        /// <summary>
        /// 删除时间字段选择表达式（可选；支持 <see cref="DateTime"/> / <see cref="DateTimeOffset"/>，统一以 <see cref="LambdaExpression"/> 暂存）。
        /// </summary>
        private LambdaExpression? _deletedAtSelector;

        /// <summary>
        /// 缓存实例区分键（用于同一实体配置多套缓存；未设置时使用全局/默认策略）。
        /// </summary>
        private string? _cacheKey;

        /// <summary>
        /// 缓存条目有效期（未设置时回落到全局默认值）。
        /// </summary>
        private TimeSpan? _cacheDuration;

        /// <summary>
        /// 查询前等待共享拉取任务的超时时间（未设置时回落到全局默认值）。
        /// </summary>
        private TimeSpan? _waitFetchTimeout;

        /// <summary>
        /// 删除校验执行间隔（未设置时回落到全局默认值）。
        /// </summary>
        private TimeSpan? _deleteCheckInterval;

        /// <summary>
        /// 重排阈值（插入次数达到阈值时会重建有序结构；未设置时回落到全局默认值）。
        /// </summary>
        private int? _reorderThreshold;

        /// <summary>
        /// keyset 分页查询主键的页大小（未设置时回落到全局默认值）。
        /// </summary>
        private int? _keysetPageSize;

        /// <summary>
        /// 按主键批量拉取的批大小（未设置时回落到全局默认值）。
        /// </summary>
        private int? _fetchByKeysBatchSize;

        /// <summary>
        /// 远端统计信息复用窗口（用于节流/复用远端 stats 查询；未设置时回落到全局默认值）。
        /// </summary>
        private TimeSpan? _remoteStatsReuseWindow;

        /// <summary>
        /// 创建实体缓存配置构建器。
        /// </summary>
        /// <param name="global">全局默认配置。</param>
        public EntityCacheOptions(TaiChiCacheOptions global)
        {
            _global = global ?? throw new ArgumentNullException(nameof(global));
        }

        /// <summary>
        /// 配置主键字段（必选）。
        /// </summary>
        /// <param name="selector">主键选择表达式。</param>
        /// <returns>当前构建器，用于链式调用。</returns>
        public EntityCacheOptions<TEntity, TPk> WithPrimaryKey(Expression<Func<TEntity, TPk>> selector)
        {
            _primaryKeySelector = selector ?? throw new ArgumentNullException(nameof(selector));
            return this;
        }

        /// <summary>
        /// 配置更新时间字段（可选，类型为 <see cref="DateTimeOffset"/>）。
        /// </summary>
        /// <param name="selector">更新时间选择表达式。</param>
        /// <returns>当前构建器，用于链式调用。</returns>
        public EntityCacheOptions<TEntity, TPk> WithUpdatedAt(Expression<Func<TEntity, DateTimeOffset?>> selector)
        {
            _updatedAtSelector = selector ?? throw new ArgumentNullException(nameof(selector));
            return this;
        }

        /// <summary>
        /// 配置更新时间字段（可选，类型为 <see cref="DateTime"/>）。
        /// </summary>
        /// <param name="selector">更新时间选择表达式。</param>
        /// <returns>当前构建器，用于链式调用。</returns>
        public EntityCacheOptions<TEntity, TPk> WithUpdatedAt(Expression<Func<TEntity, DateTime?>> selector)
        {
            _updatedAtSelector = selector ?? throw new ArgumentNullException(nameof(selector));
            return this;
        }

        /// <summary>
        /// 配置删除时间字段（可选，类型为 <see cref="DateTimeOffset"/>）。
        /// </summary>
        /// <param name="selector">删除时间选择表达式。</param>
        /// <returns>当前构建器，用于链式调用。</returns>
        public EntityCacheOptions<TEntity, TPk> WithDeletedAt(Expression<Func<TEntity, DateTimeOffset?>> selector)
        {
            _deletedAtSelector = selector ?? throw new ArgumentNullException(nameof(selector));
            return this;
        }

        /// <summary>
        /// 配置删除时间字段（可选，类型为 <see cref="DateTime"/>）。
        /// </summary>
        /// <param name="selector">删除时间选择表达式。</param>
        /// <returns>当前构建器，用于链式调用。</returns>
        public EntityCacheOptions<TEntity, TPk> WithDeletedAt(Expression<Func<TEntity, DateTime?>> selector)
        {
            _deletedAtSelector = selector ?? throw new ArgumentNullException(nameof(selector));
            return this;
        }

        /// <summary>
        /// 配置缓存键（用于区分同一实体的不同缓存实例）。
        /// </summary>
        /// <param name="cacheKey">缓存键。</param>
        /// <returns>当前构建器，用于链式调用。</returns>
        public EntityCacheOptions<TEntity, TPk> WithCacheKey(string cacheKey)
        {
            _cacheKey = cacheKey ?? throw new ArgumentNullException(nameof(cacheKey));
            return this;
        }

        /// <summary>
        /// 配置缓存条目有效期。
        /// </summary>
        public EntityCacheOptions<TEntity, TPk> WithCacheDuration(TimeSpan duration)
        {
            _cacheDuration = duration;
            return this;
        }

        /// <summary>
        /// 配置查询前等待共享拉取任务的超时时间。
        /// </summary>
        public EntityCacheOptions<TEntity, TPk> WithWaitFetchTimeout(TimeSpan timeout)
        {
            _waitFetchTimeout = timeout;
            return this;
        }

        /// <summary>
        /// 配置删除校验执行间隔。
        /// </summary>
        public EntityCacheOptions<TEntity, TPk> WithDeleteCheckInterval(TimeSpan interval)
        {
            _deleteCheckInterval = interval;
            return this;
        }

        /// <summary>
        /// 配置重排阈值（插入次数达到阈值时会重建有序结构）。
        /// </summary>
        public EntityCacheOptions<TEntity, TPk> WithReorderThreshold(int threshold)
        {
            _reorderThreshold = threshold;
            return this;
        }

        /// <summary>
        /// 配置 keyset 分页查询主键的页大小。
        /// </summary>
        public EntityCacheOptions<TEntity, TPk> WithKeysetPageSize(int pageSize)
        {
            _keysetPageSize = pageSize;
            return this;
        }

        /// <summary>
        /// 配置按主键批量拉取的批大小。
        /// </summary>
        public EntityCacheOptions<TEntity, TPk> WithFetchByKeysBatchSize(int batchSize)
        {
            _fetchByKeysBatchSize = batchSize;
            return this;
        }

        /// <summary>
        /// 配置远端统计信息复用窗口（用于节流/复用远端 stats 查询）。
        /// </summary>
        public EntityCacheOptions<TEntity, TPk> WithRemoteStatsReuseWindow(TimeSpan reuseWindow)
        {
            _remoteStatsReuseWindow = reuseWindow;
            return this;
        }

        /// <summary>
        /// 构建不可变配置（内部使用）。
        /// </summary>
        internal EntityCacheConfig<TEntity, TPk> Build()
        {
            if (_primaryKeySelector == null)
            {
                throw new InvalidOperationException($"未配置主键字段: {typeof(TEntity).FullName}");
            }

            return EntityCacheConfig<TEntity, TPk>.Create(
                _primaryKeySelector,
                _updatedAtSelector,
                _deletedAtSelector,
                cacheKey: _cacheKey,
                cacheDuration: _cacheDuration ?? _global.CacheDuration,
                waitFetchTimeout: _waitFetchTimeout ?? _global.WaitForFetchingTimeout,
                deleteCheckInterval: _deleteCheckInterval ?? _global.DeleteCheckInterval,
                reorderThreshold: _reorderThreshold ?? _global.ReorderThreshold,
                keysetPageSize: _keysetPageSize ?? _global.KeysetPageSize,
                fetchByKeysBatchSize: _fetchByKeysBatchSize ?? _global.FetchByKeysBatchSize,
                remoteStatsReuseWindow: _remoteStatsReuseWindow ?? _global.RemoteStatsReuseWindow,
                cacheKeyPrefix: _global.CacheKeyPrefix,
                enableDiagnosticsLogging: _global.EnableDiagnosticsLogging,
                diagnosticsLogSink: _global.DiagnosticsLogSink
            );
        }
    }

    /// <summary>
    /// 实体缓存的不可变配置（由 <see cref="EntityCacheOptions{TEntity, TPk}"/> 构建）。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <typeparam name="TPk">主键类型。</typeparam>
    internal sealed class EntityCacheConfig<TEntity, TPk>
        where TPk : struct, IComparable<TPk>
    {
        /// <summary>
        /// 创建实体缓存不可变配置（统一由 <see cref="Create"/> 工厂方法构建）。
        /// </summary>
        private EntityCacheConfig(
            Expression<Func<TEntity, TPk>> primaryKeySelector,
            Func<TEntity, DateTimeOffset?>? updatedAtGetter,
            Expression<Func<TEntity, bool>>? notDeletedPredicate,
            Func<TEntity, bool>? isDeleted,
            string? cacheKey,
            string cacheKeyPrefix,
            TimeSpan cacheDuration,
            TimeSpan waitFetchTimeout,
            TimeSpan deleteCheckInterval,
            int reorderThreshold,
            int keysetPageSize,
            int fetchByKeysBatchSize,
            TimeSpan remoteStatsReuseWindow,
            bool enableDiagnosticsLogging,
            Action<CacheLogEntry>? diagnosticsLogSink)
        {
            PrimaryKeySelector = primaryKeySelector;
            PrimaryKeyGetter = primaryKeySelector.Compile();

            UpdatedAtGetter = updatedAtGetter;
            NotDeletedPredicate = notDeletedPredicate;
            IsDeleted = isDeleted;

            CacheKey = cacheKey;
            CacheKeyPrefix = cacheKeyPrefix;
            CacheDuration = cacheDuration;
            WaitFetchTimeout = waitFetchTimeout;
            DeleteCheckInterval = deleteCheckInterval;
            ReorderThreshold = reorderThreshold;
            KeysetPageSize = keysetPageSize;
            FetchByKeysBatchSize = fetchByKeysBatchSize;
            RemoteStatsReuseWindow = remoteStatsReuseWindow;
            EnableDiagnosticsLogging = enableDiagnosticsLogging;
            DiagnosticsLogSink = diagnosticsLogSink;
        }

        /// <summary>
        /// 主键选择表达式（用于诊断与外部可读性）。
        /// </summary>
        public Expression<Func<TEntity, TPk>> PrimaryKeySelector { get; }

        /// <summary>
        /// 主键获取器（由 <see cref="PrimaryKeySelector"/> 编译得到）。
        /// </summary>
        public Func<TEntity, TPk> PrimaryKeyGetter { get; }

        /// <summary>
        /// 更新时间获取器（可选；启用后可进行“按更新时间增量拉取”）。
        /// </summary>
        public Func<TEntity, DateTimeOffset?>? UpdatedAtGetter { get; }

        /// <summary>
        /// “未删除”谓词（可选；当配置了删除时间字段时自动生成）。
        /// </summary>
        public Expression<Func<TEntity, bool>>? NotDeletedPredicate { get; }

        /// <summary>
        /// 是否删除判断器（可选；当配置了删除时间字段时自动生成）。
        /// </summary>
        public Func<TEntity, bool>? IsDeleted { get; }

        /// <summary>
        /// 用户配置的缓存键（可为空；为空时将使用实体类型名作为缓存键）。
        /// </summary>
        public string? CacheKey { get; }

        /// <summary>
        /// 缓存键前缀（用于命名空间隔离）。
        /// </summary>
        public string CacheKeyPrefix { get; }

        /// <summary>
        /// 缓存条目有效期。
        /// </summary>
        public TimeSpan CacheDuration { get; }

        /// <summary>
        /// 查询前等待共享拉取任务的超时时间。
        /// </summary>
        public TimeSpan WaitFetchTimeout { get; }

        /// <summary>
        /// 删除校验执行间隔。
        /// </summary>
        public TimeSpan DeleteCheckInterval { get; }

        /// <summary>
        /// 触发内部重排的插入阈值。
        /// </summary>
        public int ReorderThreshold { get; }

        /// <summary>
        /// Keyset 分页查询主键的页大小。
        /// </summary>
        public int KeysetPageSize { get; }

        /// <summary>
        /// 按主键批量拉取的批大小。
        /// </summary>
        public int FetchByKeysBatchSize { get; }

        /// <summary>
        /// 远端统计信息复用窗口（用于节流/复用 stats 查询）。
        /// </summary>
        public TimeSpan RemoteStatsReuseWindow { get; }

        /// <summary>
        /// 是否启用调试/信息级缓存诊断日志。
        /// </summary>
        public bool EnableDiagnosticsLogging { get; }

        /// <summary>
        /// 诊断日志接收器。
        /// </summary>
        public Action<CacheLogEntry>? DiagnosticsLogSink { get; }

        /// <summary>
        /// 类型级缓存 key（当配置了 CacheKey 时使用）。
        /// </summary>
        public string TypeCacheKey => $"{CacheKeyPrefix}{CacheKey}";

        /// <summary>
        /// 最终生效的缓存 key（空 CacheKey 时回退到实体类型名）。
        /// </summary>
        public string EffectiveCacheKey => string.IsNullOrWhiteSpace(CacheKey)
            ? $"{CacheKeyPrefix}{typeof(TEntity).Name}"
            : TypeCacheKey;

        /// <summary>
        /// 构建实体缓存配置，并对更新时间/删除时间表达式进行适配与编译。
        /// </summary>
        public static EntityCacheConfig<TEntity, TPk> Create(
            Expression<Func<TEntity, TPk>> primaryKeySelector,
            LambdaExpression? updatedAtSelector,
            LambdaExpression? deletedAtSelector,
            string? cacheKey,
            string cacheKeyPrefix,
            TimeSpan cacheDuration,
            TimeSpan waitFetchTimeout,
            TimeSpan deleteCheckInterval,
            int reorderThreshold,
            int keysetPageSize,
            int fetchByKeysBatchSize,
            TimeSpan remoteStatsReuseWindow,
            bool enableDiagnosticsLogging,
            Action<CacheLogEntry>? diagnosticsLogSink)
        {
            Func<TEntity, DateTimeOffset?>? updatedAtGetter = null;
            if (updatedAtSelector != null)
            {
                if (updatedAtSelector.ReturnType == typeof(DateTimeOffset?))
                {
                    updatedAtGetter = (Func<TEntity, DateTimeOffset?>)updatedAtSelector.Compile();
                }
                else if (updatedAtSelector.ReturnType == typeof(DateTime?))
                {
                    var dtGetter = (Func<TEntity, DateTime?>)updatedAtSelector.Compile();
                    updatedAtGetter = entity =>
                    {
                        var dt = dtGetter(entity);
                        return dt == null ? null : new DateTimeOffset(dt.Value);
                    };
                }
                else
                {
                    throw new InvalidOperationException($"更新时间字段类型不支持: {updatedAtSelector.ReturnType.FullName}");
                }
            }

            Expression<Func<TEntity, bool>>? notDeletedPredicate = null;
            Func<TEntity, bool>? isDeleted = null;

            if (deletedAtSelector != null)
            {
                var param = deletedAtSelector.Parameters[0];
                var body = Expression.Equal(deletedAtSelector.Body, Expression.Constant(null, deletedAtSelector.ReturnType));
                notDeletedPredicate = Expression.Lambda<Func<TEntity, bool>>(body, param);

                if (deletedAtSelector.ReturnType == typeof(DateTimeOffset?))
                {
                    var getter = (Func<TEntity, DateTimeOffset?>)deletedAtSelector.Compile();
                    isDeleted = entity => getter(entity) != null;
                }
                else if (deletedAtSelector.ReturnType == typeof(DateTime?))
                {
                    var getter = (Func<TEntity, DateTime?>)deletedAtSelector.Compile();
                    isDeleted = entity => getter(entity) != null;
                }
                else
                {
                    throw new InvalidOperationException($"删除时间字段类型不支持: {deletedAtSelector.ReturnType.FullName}");
                }
            }

            return new EntityCacheConfig<TEntity, TPk>(
                primaryKeySelector: primaryKeySelector,
                updatedAtGetter: updatedAtGetter,
                notDeletedPredicate: notDeletedPredicate,
                isDeleted: isDeleted,
                cacheKey: cacheKey,
                cacheKeyPrefix: cacheKeyPrefix ?? string.Empty,
                cacheDuration: cacheDuration,
                waitFetchTimeout: waitFetchTimeout,
                deleteCheckInterval: deleteCheckInterval,
                reorderThreshold: reorderThreshold,
                keysetPageSize: keysetPageSize,
                fetchByKeysBatchSize: fetchByKeysBatchSize,
                remoteStatsReuseWindow: remoteStatsReuseWindow,
                enableDiagnosticsLogging: enableDiagnosticsLogging,
                diagnosticsLogSink: diagnosticsLogSink
            );
        }
    }
}
