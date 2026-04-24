using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TaiChi.Cache.Abstractions;
using TaiChi.Cache.Test.Fakes;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace TaiChi.Cache.Test
{
    /// <summary>
    /// <see cref="EntityCache{TEntity, TPk}"/> 的加固测试：聚焦并发、失效与等待策略等边界场景。
    /// </summary>
    public sealed class EntityCacheHardeningTests
    {
        /// <summary>
        /// 用于测试的简单实体模型。
        /// </summary>
        private sealed record TestEntity(int Id, string Name, DateTimeOffset? UpdatedAt = null, DateTimeOffset? DeletedAt = null);

        /// <summary>
        /// 创建一个配置适合单元测试的缓存根对象。
        /// </summary>
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
                    Duration = TimeSpan.FromMinutes(30),
                },
            });
        }

        /// <summary>
        /// 验证：拉取过程中触发全量失效，旧拉取结果不会在完成后“恢复”已失效的快照。
        /// </summary>
        [Fact]
        public async Task Invalidate_full_during_fetch_should_not_allow_stale_fetch_to_restore_snapshot()
        {
            var stalePayload = new[] { new TestEntity(1, "stale") };
            var freshPayload = new[] { new TestEntity(2, "fresh") };

            var fusionCache = CreateFusionCache();
            var remote = new FakeRemoteStore<TestEntity, int>(x => x.Id, x => x.UpdatedAt);
            var firstFetchStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFirstFetch = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            remote.SetFetchAllResultHook(async (call, ct) =>
            {
                if (call == 1)
                {
                    firstFetchStarted.TrySetResult(true);
                    await releaseFirstFetch.Task.WaitAsync(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
                    return stalePayload;
                }

                return freshPayload;
            });

            var db = CreateDb(
                fusionCache,
                options =>
                {
                    options.DeleteCheckInterval = TimeSpan.FromHours(1);
                    options.WaitForFetchingTimeout = TimeSpan.FromMilliseconds(100);
                });
            var cache = db.For<TestEntity, int>(
                remote,
                opt => opt
                    .WithPrimaryKey(x => x.Id)
                    .WithUpdatedAt(x => x.UpdatedAt)
                    .WithDeletedAt(x => x.DeletedAt));

            var refreshTask = cache.RefreshAsync(CacheRefreshMode.Full);
            Assert.True(await firstFetchStarted.Task.WaitAsync(TimeSpan.FromSeconds(2)));

            remote.Seed(freshPayload);
            await cache.InvalidateAsync(CacheInvalidateScope.Full);
            releaseFirstFetch.TrySetResult(true);

            try
            {
                await refreshTask;
            }
            catch (OperationCanceledException)
            {
            }

            var invalidatedState = cache.GetLocalState();
            Assert.Equal(0, invalidatedState.Count);
            Assert.True(invalidatedState.NeedUpdate);

            var refreshed = await cache.Query().Where(x => x.Id > 0).ToListAsync();
            Assert.Equal(new[] { 2 }, refreshed.Select(x => x.Id).ToArray());
        }

        /// <summary>
        /// 验证：等待拉取超时后会停止等待并返回本地结果，但不会取消共享刷新任务。
        /// </summary>
        [Fact]
        public async Task Wait_for_fetching_timeout_should_stop_waiting_without_canceling_shared_refresh()
        {
            var stalePayload = new[] { new TestEntity(1, "stale") };
            var freshPayload = new[] { new TestEntity(1, "fresh") };

            var fusionCache = CreateFusionCache();
            var remote = new FakeRemoteStore<TestEntity, int>(x => x.Id, x => x.UpdatedAt);
            remote.Seed(stalePayload);

            var db = CreateDb(
                fusionCache,
                options =>
                {
                    options.DeleteCheckInterval = TimeSpan.FromHours(1);
                    options.WaitForFetchingTimeout = TimeSpan.FromMilliseconds(100);
                });
            var cache = db.For<TestEntity, int>(
                remote,
                opt => opt
                    .WithPrimaryKey(x => x.Id)
                    .WithUpdatedAt(x => x.UpdatedAt));

            await cache.RefreshAsync(CacheRefreshMode.Full);

            var secondFetchStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseSecondFetch = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            remote.Seed(freshPayload);
            remote.SetFetchAllHook(async (call, ct) =>
            {
                if (call == 2)
                {
                    secondFetchStarted.TrySetResult(true);
                    await releaseSecondFetch.Task.WaitAsync(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
                }
            });

            var refreshTask = cache.RefreshAsync(CacheRefreshMode.Full);
            Assert.True(await secondFetchStarted.Task.WaitAsync(TimeSpan.FromSeconds(2)));

            var stopwatch = Stopwatch.StartNew();
            var queryResult = await cache.Query().Where(x => x.Id == 1).ToListAsync();
            stopwatch.Stop();

            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
            Assert.Equal("stale", Assert.Single(queryResult).Name);
            Assert.Equal(2, remote.FetchAllCallCount);
            Assert.False(refreshTask.IsCompleted);

            releaseSecondFetch.TrySetResult(true);
            await refreshTask;

            Assert.Equal("fresh", Assert.Single(cache.GetLocalSnapshot()).Name);
        }
    }
}
