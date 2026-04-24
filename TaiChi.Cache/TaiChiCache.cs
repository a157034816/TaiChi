using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using TaiChi.Cache.Abstractions;
using ZiggyCreatures.Caching.Fusion;

namespace TaiChi.Cache
{
    /// <summary>
    /// TaiChi 缓存根对象：负责创建/复用实体缓存实例，并提供全局失效能力。
    /// </summary>
    /// <remarks>
    /// - 同一实体类型在不同 cacheKey 下会产生不同缓存实例；
    /// - 对同一 (EntityType, cacheKey) 的重复注册会复用同一个缓存实例。
    /// </remarks>
    public sealed class TaiChiCache
    {
        /// <summary>
        /// 底层 FusionCache，用于承载缓存条目与失效。
        /// </summary>
        private readonly IFusionCache _fusionCache;

        /// <summary>
        /// 全局默认选项。
        /// </summary>
        private readonly TaiChiCacheOptions _options;

        /// <summary>
        /// 已注册的实体缓存实例集合（按实体类型与缓存键区分）。
        /// </summary>
        private readonly ConcurrentDictionary<(Type EntityType, string CacheId), object> _entityCaches = new ConcurrentDictionary<(Type EntityType, string CacheId), object>();

        /// <summary>
        /// 创建缓存根对象。
        /// </summary>
        /// <param name="fusionCache">FusionCache 实例。</param>
        /// <param name="options">可选全局选项；为空则使用默认值。</param>
        public TaiChiCache(IFusionCache fusionCache, TaiChiCacheOptions? options = null)
        {
            _fusionCache = fusionCache ?? throw new ArgumentNullException(nameof(fusionCache));
            _options = options ?? new TaiChiCacheOptions();
        }

        /// <summary>
        /// 获取指定实体类型的缓存实例（必要时创建并注册）。
        /// </summary>
        /// <typeparam name="TEntity">实体类型。</typeparam>
        /// <typeparam name="TPk">主键类型。</typeparam>
        /// <param name="remote">远端数据源。</param>
        /// <param name="configure">缓存配置回调（必须设置主键）。</param>
        /// <returns>实体缓存实例。</returns>
        /// <exception cref="InvalidOperationException">当同一注册键下泛型参数不一致时抛出。</exception>
        public EntityCache<TEntity, TPk> For<TEntity, TPk>(
            IRemoteStore<TEntity, TPk> remote,
            Action<EntityCacheOptions<TEntity, TPk>> configure)
            where TPk : struct, IComparable<TPk>
        {
            if (remote == null) throw new ArgumentNullException(nameof(remote));
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            var options = new EntityCacheOptions<TEntity, TPk>(_options);
            configure(options);

            var config = options.Build();
            var registrationKey = (typeof(TEntity), config.EffectiveCacheKey);

            var cache = _entityCaches.GetOrAdd(registrationKey, _ =>
            {
                return new EntityCache<TEntity, TPk>(_fusionCache, remote, config);
            });

            if (cache is EntityCache<TEntity, TPk> typed)
            {
                return typed;
            }

            throw new InvalidOperationException($"缓存已注册，但类型参数不一致: {typeof(TEntity).FullName}");
        }

        /// <summary>
        /// 触发所有已注册缓存实例的全量失效。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        public async Task InvalidateAllAsync(CancellationToken ct = default)
        {
            foreach (var item in _entityCaches.Values)
            {
                if (item is ICacheInvalidationTarget target)
                {
                    await target.InvalidateAsync(CacheInvalidateScope.Full, ct).ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>
    /// 内部接口：用于统一调用实体缓存的失效能力。
    /// </summary>
    internal interface ICacheInvalidationTarget
    {
        Task InvalidateAsync(CacheInvalidateScope scope, CancellationToken ct);
    }
}
