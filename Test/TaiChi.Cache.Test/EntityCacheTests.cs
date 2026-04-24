using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaiChi.Cache.Abstractions;
using TaiChi.Cache.Test.Fakes;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace TaiChi.Cache.Test
{
    /// <summary>
    /// <see cref="EntityCache{TEntity, TPk}"/> 的核心行为测试：缺口补齐、并发拉取复用、失效与多实例注册等。
    /// </summary>
    public sealed class EntityCacheTests
    {
        /// <summary>
        /// 用于测试的简单实体模型。
        /// </summary>
        private sealed record TestEntity(int Id, string Name, DateTimeOffset? UpdatedAt = null, DateTimeOffset? DeletedAt = null);

        /// <summary>
        /// 创建一个配置适合单元测试的缓存根对象。
        /// </summary>
        /// <param name="fusionCache">底层 FusionCache。</param>
        /// <param name="configure">可选：覆盖默认测试选项。</param>
        private static TaiChiCache CreateDb(IFusionCache fusionCache, Action<TaiChiCacheOptions>? configure = null)
        {
            var options = new TaiChiCacheOptions
            {
                CacheDuration = TimeSpan.FromMinutes(30),
                DeleteCheckInterval = TimeSpan.FromSeconds(30),
                WaitForFetchingTimeout = TimeSpan.FromMilliseconds(200),
                KeysetPageSize = 2,
                FetchByKeysBatchSize = 2,
                ReorderThreshold = 1,
                RemoteStatsReuseWindow = TimeSpan.Zero,
            };

            configure?.Invoke(options);
            return new TaiChiCache(fusionCache, options);
        }

        /// <summary>
        /// 创建用于测试的 FusionCache 实例。
        /// </summary>
        private static IFusionCache CreateFusionCache()
        {
            return new FusionCache(new FusionCacheOptions
            {
                DefaultEntryOptions = new FusionCacheEntryOptions
                {
                    Duration = TimeSpan.FromMinutes(30)
                }
            });
        }

        /// <summary>
        /// 验证：当本地数量与远端统计不一致时，会通过 keyset 分页补齐缺失数据。
        /// </summary>
        [Fact]
        public async Task Query_CountMismatch_UsesKeysetToFillMissing()
        {
            var fusionCache = CreateFusionCache();
            var remote = new FakeRemoteStore<TestEntity, int>(x => x.Id, x => x.UpdatedAt);
            remote.Seed(
                new TestEntity(1, "a"),
                new TestEntity(2, "b"),
                new TestEntity(3, "c")
            );

            var db = CreateDb(fusionCache);
            var cache = db.For<TestEntity, int>(
                remote,
                opt => opt
                    .WithPrimaryKey(x => x.Id)
                    .WithUpdatedAt(x => x.UpdatedAt)
                    .WithDeletedAt(x => x.DeletedAt)
            );

            var list = await cache.Query()
                .Where(x => x.Id > 0)
                .ToListAsync();

            Assert.Equal(3, list.Count);
            Assert.Equal(new[] { 1, 2, 3 }, list.Select(x => x.Id).ToArray());
            Assert.Equal(0, remote.FetchAllCallCount);
            Assert.True(remote.QueryKeysCallCount > 0);
            Assert.True(remote.FetchByKeysCallCount > 0);
        }

        /// <summary>
        /// 验证：查询等待拉取超时后，会复用已有拉取任务，不会取消并重启拉取。
        /// </summary>
        [Fact]
        public async Task Query_WhenFetchingTimeout_ReusesExistingFetchWithoutCanceling()
        {
            var fusionCache = CreateFusionCache();
            var remote = new FakeRemoteStore<TestEntity, int>(x => x.Id, x => x.UpdatedAt);
            remote.Seed(
                new TestEntity(1, "a"),
                new TestEntity(2, "b")
            );

            var firstFetchStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFetch = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            remote.SetFetchAllHook(async (call, ct) =>
            {
                if (call == 1)
                {
                    firstFetchStarted.TrySetResult(true);
                    await releaseFetch.Task.WaitAsync(ct).ConfigureAwait(false);
                }
            });

            var db = CreateDb(
                fusionCache,
                options =>
                {
                    options.WaitForFetchingTimeout = TimeSpan.FromMilliseconds(100);
                    options.DeleteCheckInterval = TimeSpan.FromHours(1);
                }
            );

            var cache = db.For<TestEntity, int>(remote, opt => opt.WithPrimaryKey(x => x.Id));

            var refreshTask = cache.RefreshAsync(CacheRefreshMode.Full);
            Assert.True(await firstFetchStarted.Task.WaitAsync(TimeSpan.FromSeconds(2)));

            var listTask = cache.Query().Where(x => x.Id > 0).ToListAsync();

            await Task.Delay(TimeSpan.FromMilliseconds(250));

            Assert.Equal(1, remote.FetchAllCallCount);
            Assert.False(refreshTask.IsCanceled);

            releaseFetch.TrySetResult(true);

            var list = await listTask;

            await refreshTask;

            Assert.Equal(2, list.Count);
            Assert.Equal(1, remote.FetchAllCallCount);
        }

        /// <summary>
        /// 验证：拉取过程中触发失效，旧拉取结果不会覆盖失效后的新状态。
        /// </summary>
        [Fact]
        public async Task Invalidate_DuringFetch_PreventsStaleFetchFromApplying()
        {
            var fusionCache = CreateFusionCache();
            var remote = new FakeRemoteStore<TestEntity, int>(x => x.Id, x => x.UpdatedAt);

            var firstFetchStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseStaleFetch = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            remote.SetFetchAllResultHook(async (call, ct) =>
            {
                if (call == 1)
                {
                    firstFetchStarted.TrySetResult(true);
                    await releaseStaleFetch.Task.ConfigureAwait(false);
                    return new[] { new TestEntity(1, "stale") };
                }

                ct.ThrowIfCancellationRequested();
                return new[] { new TestEntity(2, "fresh") };
            });

            var db = CreateDb(
                fusionCache,
                options =>
                {
                    options.WaitForFetchingTimeout = TimeSpan.FromMilliseconds(100);
                    options.DeleteCheckInterval = TimeSpan.FromHours(1);
                }
            );

            var cache = db.For<TestEntity, int>(remote, opt => opt.WithPrimaryKey(x => x.Id));

            var refreshTask = cache.RefreshAsync(CacheRefreshMode.Full);
            Assert.True(await firstFetchStarted.Task.WaitAsync(TimeSpan.FromSeconds(2)));

            await cache.InvalidateAsync(CacheInvalidateScope.Full);

            var refreshedList = await cache.Query().Where(x => x.Id > 0).ToListAsync();
            Assert.Equal(new[] { 2 }, refreshedList.Select(x => x.Id).ToArray());

            releaseStaleFetch.TrySetResult(true);
            await refreshTask;

            Assert.True(remote.FetchAllCallCount >= 2);
            Assert.Equal(new[] { 2 }, cache.GetLocalSnapshot().Select(x => x.Id).ToArray());
        }

        /// <summary>
        /// 验证：同一实体类型在不同 cacheKey 下允许注册多个缓存实例，且同一 key 会复用实例。
        /// </summary>
        [Fact]
        public async Task For_AllowsMultipleInstancesForSameEntity_WhenCacheKeyDiffers()
        {
            var fusionCache = CreateFusionCache();
            var remoteA = new FakeRemoteStore<TestEntity, int>(x => x.Id, x => x.UpdatedAt);
            var remoteB = new FakeRemoteStore<TestEntity, int>(x => x.Id, x => x.UpdatedAt);

            remoteA.Seed(new TestEntity(1, "alpha"), new TestEntity(2, "beta"));
            remoteB.Seed(new TestEntity(10, "gamma"));

            var db = CreateDb(fusionCache);
            var defaultCache = db.For<TestEntity, int>(remoteA, opt => opt.WithPrimaryKey(x => x.Id));
            var namedCache = db.For<TestEntity, int>(remoteB, opt => opt.WithPrimaryKey(x => x.Id).WithCacheKey("named"));
            var namedCacheAgain = db.For<TestEntity, int>(remoteB, opt => opt.WithPrimaryKey(x => x.Id).WithCacheKey("named"));

            var defaultList = await defaultCache.Query().Where(x => x.Id > 0).ToListAsync();
            var namedList = await namedCache.Query().Where(x => x.Id > 0).ToListAsync();

            Assert.NotSame(defaultCache, namedCache);
            Assert.Same(namedCache, namedCacheAgain);
            Assert.Equal(new[] { 1, 2 }, defaultList.Select(x => x.Id).ToArray());
            Assert.Equal(new[] { 10 }, namedList.Select(x => x.Id).ToArray());
        }

        /// <summary>
        /// 验证：全量刷新返回空数据时，会清理本地元数据（更新时间、删除校验时间、重排计数等）。
        /// </summary>
        [Fact]
        public async Task FullRefresh_WithEmptyPayload_ClearsMetadata()
        {
            var now = DateTimeOffset.UtcNow;
            var fusionCache = CreateFusionCache();
            var remote = new FakeRemoteStore<TestEntity, int>(x => x.Id, x => x.UpdatedAt);
            remote.Seed(
                new TestEntity(1, "alpha", now.AddMinutes(-5)),
                new TestEntity(2, "beta", now.AddMinutes(-1))
            );

            var db = CreateDb(
                fusionCache,
                options =>
                {
                    options.DeleteCheckInterval = TimeSpan.Zero;
                    options.RemoteStatsReuseWindow = TimeSpan.FromMinutes(1);
                }
            );
            var cache = db.For<TestEntity, int>(
                remote,
                opt => opt
                    .WithPrimaryKey(x => x.Id)
                    .WithUpdatedAt(x => x.UpdatedAt)
            );

            await cache.RefreshAsync(CacheRefreshMode.Full);
            await cache.Query().Where(x => x.Id > 0).ToListAsync();

            var warmedState = cache.GetLocalState();
            Assert.Equal(2, warmedState.Count);
            Assert.NotNull(warmedState.LastMaxUpdatedAt);
            Assert.NotNull(warmedState.LastDeleteCheckAt);

            remote.Seed();

            await cache.RefreshAsync(CacheRefreshMode.Full);

            var clearedState = cache.GetLocalState();
            Assert.Equal(0, clearedState.Count);
            Assert.Null(clearedState.LastMaxUpdatedAt);
            Assert.Null(clearedState.LastDeleteCheckAt);
            Assert.Equal(0, clearedState.InsertsSinceReorder);
        }

        /// <summary>
        /// 验证：全量失效采用 unique tag 精确清理，但 legacy tag 仍能清理所有实例以兼容旧行为。
        /// </summary>
        [Fact]
        public async Task FullInvalidate_UsesUniqueTag_ButLegacyTagStillClearsAll()
        {
            var fusionCache = CreateFusionCache();
            var remoteA = new FakeRemoteStore<TestEntity, int>(x => x.Id, x => x.UpdatedAt);
            var remoteB = new FakeRemoteStore<TestEntity, int>(x => x.Id, x => x.UpdatedAt);

            remoteA.Seed(new TestEntity(1, "alpha"));
            remoteB.Seed(new TestEntity(10, "gamma"));

            var db = CreateDb(fusionCache);
            var defaultCache = db.For<TestEntity, int>(remoteA, opt => opt.WithPrimaryKey(x => x.Id));
            var namedCache = db.For<TestEntity, int>(remoteB, opt => opt.WithPrimaryKey(x => x.Id).WithCacheKey("named"));

            await defaultCache.Query().Where(x => x.Id > 0).ToListAsync();
            await namedCache.Query().Where(x => x.Id > 0).ToListAsync();

            Assert.True(defaultCache.TryGetLocal(1, out var defaultEntity));
            Assert.Equal("alpha", defaultEntity.Name);
            Assert.True(namedCache.TryGetLocal(10, out var namedEntity));
            Assert.Equal("gamma", namedEntity.Name);

            await defaultCache.InvalidateAsync(CacheInvalidateScope.Full);

            Assert.Empty(defaultCache.GetLocalSnapshot());
            Assert.Single(namedCache.GetLocalSnapshot());

            fusionCache.RemoveByTag(typeof(TestEntity).Name);

            Assert.Empty(defaultCache.GetLocalSnapshot());
            Assert.Empty(namedCache.GetLocalSnapshot());
        }

        /// <summary>
        /// 验证：<see cref="EntityCache{TEntity, TPk}.GetLocalSnapshot"/> 会缓存只读快照引用，且在表发生变更后失效并重建。
        /// </summary>
        /// <remarks>
        /// 该行为用于高频本地读取场景：
        /// - 连续读取不应重复分配整表列表；
        /// - 写入/刷新后应返回新快照，同时旧快照内容保持不变。
        /// </remarks>
        [Fact]
        public async Task GetLocalSnapshot_CachesSnapshotReference_AndInvalidatesOnMutation()
        {
            var fusionCache = CreateFusionCache();
            var remote = new FakeRemoteStore<TestEntity, int>(x => x.Id, x => x.UpdatedAt);
            remote.Seed(new TestEntity(1, "alpha"), new TestEntity(2, "beta"));

            var db = CreateDb(
                fusionCache,
                options =>
                {
                    options.DeleteCheckInterval = TimeSpan.FromSeconds(-1);
                }
            );

            var cache = db.For<TestEntity, int>(remote, opt => opt.WithPrimaryKey(x => x.Id));

            await cache.RefreshAsync(CacheRefreshMode.Full);

            var snapshot1 = cache.GetLocalSnapshot();
            var snapshot2 = cache.GetLocalSnapshot();

            Assert.Same(snapshot1, snapshot2);
            Assert.Equal(new[] { 1, 2 }, snapshot1.Select(x => x.Id).ToArray());
            Assert.Equal(new[] { "alpha", "beta" }, snapshot1.Select(x => x.Name).ToArray());

            remote.Seed(new TestEntity(1, "alpha2"), new TestEntity(2, "beta"), new TestEntity(3, "gamma"));

            await cache.RefreshAsync(CacheRefreshMode.Full);

            var snapshot3 = cache.GetLocalSnapshot();

            Assert.NotSame(snapshot1, snapshot3);
            Assert.Equal(new[] { 1, 2, 3 }, snapshot3.Select(x => x.Id).ToArray());
            Assert.Equal(new[] { "alpha2", "beta", "gamma" }, snapshot3.Select(x => x.Name).ToArray());

            // 旧快照应保持不变（不随刷新后的表内容改变）。
            Assert.Equal(new[] { 1, 2 }, snapshot1.Select(x => x.Id).ToArray());
            Assert.Equal(new[] { "alpha", "beta" }, snapshot1.Select(x => x.Name).ToArray());
        }

        /// <summary>
        /// 验证：当实体级别覆盖启用远端统计复用窗口时，同一谓词的 stats 查询会在窗口内被复用。
        /// </summary>
        [Fact]
        public async Task Query_ReusesRemoteStats_WhenEntityOverrideEnabled()
        {
            var fusionCache = CreateFusionCache();
            var remote = new FakeRemoteStore<TestEntity, int>(x => x.Id, x => x.UpdatedAt);
            remote.Seed(new TestEntity(1, "alpha"), new TestEntity(2, "beta"));

            var db = CreateDb(
                fusionCache,
                options =>
                {
                    options.DeleteCheckInterval = TimeSpan.FromSeconds(-1);
                    options.RemoteStatsReuseWindow = TimeSpan.Zero;
                }
            );
            var cache = db.For<TestEntity, int>(
                remote,
                opt => opt
                    .WithPrimaryKey(x => x.Id)
                    .WithRemoteStatsReuseWindow(TimeSpan.FromMinutes(1))
            );

            var first = await cache.Query().Where(x => x.Id > 0).ToListAsync();
            var second = await cache.Query().Where(x => x.Id > 0).ToListAsync();
            var count = await cache.Query().Where(x => x.Id > 0).CountAsync();

            Assert.Equal(2, first.Count);
            Assert.Equal(2, second.Count);
            Assert.Equal(2, count);
            Assert.Equal(1, remote.GetStatsCallCount);
        }
    }
}
