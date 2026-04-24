using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using TaiChi.Cache.Abstractions;
using TaiChi.Cache.Test.Fakes;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace TaiChi.Cache.Test
{
    /// <summary>
    /// 缓存诊断日志相关测试：验证诊断开关对 Debug/Info 输出的影响，以及 Warn 级事件的保底输出。
    /// </summary>
    public sealed class EntityCacheLoggingTests
    {
        /// <summary>
        /// 用于测试的简单实体模型。
        /// </summary>
        private sealed record TestEntity(int Id, string Name, DateTimeOffset? UpdatedAt = null, DateTimeOffset? DeletedAt = null);

        /// <summary>
        /// 验证：启用诊断后会输出 Debug/Info 级别的关键事件（缺口补齐、命中等）。
        /// </summary>
        [Fact]
        public async Task DiagnosticsEnabled_ShouldEmitDebugAndInfoEvents()
        {
            var logs = new ConcurrentQueue<CacheLogEntry>();
            var remote = new FakeRemoteStore<TestEntity, int>(x => x.Id, x => x.UpdatedAt);
            remote.Seed(
                new TestEntity(1, "alpha"),
                new TestEntity(2, "beta"),
                new TestEntity(3, "gamma"));

            var cache = CreateCache(
                logs,
                remote,
                options => options.EnableDiagnosticsLogging = true);

            var list = await cache.Query()
                .Where(entity => entity.Id > 0)
                .ToListAsync();

            var count = await cache.Query()
                .Where(entity => entity.Id > 0)
                .CountAsync();

            Assert.Equal(3, list.Count);
            Assert.Equal(3, count);

            var events = logs.Select(entry => entry.EventName).ToArray();
            Assert.Contains("cache.query.stats-mismatch", events);
            Assert.Contains("cache.fill-missing.start", events);
            Assert.Contains("cache.fill-missing.completed", events);
            Assert.Contains("cache.query.local-hit", events);
        }

        /// <summary>
        /// 验证：禁用诊断时仍会输出“等待超时”相关的 Warn 级事件，但不会输出 Debug/Info。
        /// </summary>
        [Fact]
        public async Task DiagnosticsDisabled_ShouldStillEmitTimeoutWarning()
        {
            var logs = new ConcurrentQueue<CacheLogEntry>();
            var remote = new FakeRemoteStore<TestEntity, int>(x => x.Id, x => x.UpdatedAt);
            remote.Seed(
                new TestEntity(1, "alpha"),
                new TestEntity(2, "beta"));

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

            var cache = CreateCache(
                logs,
                remote,
                options =>
                {
                    options.EnableDiagnosticsLogging = false;
                    options.WaitForFetchingTimeout = TimeSpan.FromMilliseconds(100);
                });

            var refreshTask = cache.RefreshAsync(CacheRefreshMode.Full);
            Assert.True(await firstFetchStarted.Task.WaitAsync(TimeSpan.FromSeconds(2)));

            var listTask = cache.Query().Where(entity => entity.Id > 0).ToListAsync();

            await Task.Delay(TimeSpan.FromMilliseconds(250));

            releaseFetch.TrySetResult(true);

            var list = await listTask;
            await refreshTask;

            Assert.Equal(2, list.Count);

            var events = logs.Select(entry => entry.EventName).ToArray();
            Assert.Contains("cache.prepare.timeout", events);
            Assert.DoesNotContain("cache.prepare.waiting", events);
            Assert.DoesNotContain("cache.query.stats-mismatch", events);
        }

        /// <summary>
        /// 验证：禁用诊断时仍会输出“过期拉取丢弃”相关的 Warn 级事件。
        /// </summary>
        [Fact]
        public async Task DiagnosticsDisabled_ShouldStillEmitStaleDiscardWarning()
        {
            var logs = new ConcurrentQueue<CacheLogEntry>();
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

            var cache = CreateCache(
                logs,
                remote,
                options =>
                {
                    options.EnableDiagnosticsLogging = false;
                    options.WaitForFetchingTimeout = TimeSpan.FromMilliseconds(100);
                });

            var refreshTask = cache.RefreshAsync(CacheRefreshMode.Full);
            Assert.True(await firstFetchStarted.Task.WaitAsync(TimeSpan.FromSeconds(2)));

            await cache.InvalidateAsync(CacheInvalidateScope.Full);

            var refreshedList = await cache.Query().Where(entity => entity.Id > 0).ToListAsync();
            Assert.Equal(new[] { 2 }, refreshedList.Select(entity => entity.Id).ToArray());

            releaseStaleFetch.TrySetResult(true);
            await refreshTask;

            var events = logs.Select(entry => entry.EventName).ToArray();
            Assert.Contains("cache.fetch.stale-discarded", events);
        }

        /// <summary>
        /// 创建一个带日志接收器的缓存实例，便于断言事件输出。
        /// </summary>
        /// <param name="logs">日志队列（作为 DiagnosticsLogSink）。</param>
        /// <param name="remote">测试远端存储。</param>
        /// <param name="configure">可选：覆盖默认测试选项。</param>
        private static EntityCache<TestEntity, int> CreateCache(
            ConcurrentQueue<CacheLogEntry> logs,
            FakeRemoteStore<TestEntity, int> remote,
            Action<TaiChiCacheOptions>? configure = null)
        {
            var options = new TaiChiCacheOptions
            {
                CacheDuration = TimeSpan.FromMinutes(30),
                DeleteCheckInterval = TimeSpan.FromHours(1),
                WaitForFetchingTimeout = TimeSpan.FromMilliseconds(200),
                KeysetPageSize = 2,
                FetchByKeysBatchSize = 2,
                ReorderThreshold = 1,
                RemoteStatsReuseWindow = TimeSpan.Zero,
                DiagnosticsLogSink = logs.Enqueue,
            };

            configure?.Invoke(options);

            var fusionCache = new FusionCache(new FusionCacheOptions
            {
                DefaultEntryOptions = new FusionCacheEntryOptions
                {
                    Duration = TimeSpan.FromMinutes(30)
                }
            });

            var cacheRoot = new TaiChiCache(fusionCache, options);
            return cacheRoot.For<TestEntity, int>(
                remote,
                config => config
                    .WithPrimaryKey(entity => entity.Id)
                    .WithUpdatedAt(entity => entity.UpdatedAt)
                    .WithDeletedAt(entity => entity.DeletedAt));
        }
    }
}
