using System;

namespace TaiChi.Cache
{
    /// <summary>
    /// TaiChi.Cache 全局配置项（影响所有实体缓存实例的默认行为）。
    /// </summary>
    public sealed class TaiChiCacheOptions
    {
        /// <summary>
        /// 缓存条目默认有效期。
        /// </summary>
        public TimeSpan CacheDuration { get; set; } = TimeSpan.FromDays(3650);

        /// <summary>
        /// 查询前等待共享拉取任务的超时时间。
        /// </summary>
        public TimeSpan WaitForFetchingTimeout { get; set; } = TimeSpan.FromSeconds(3);

        /// <summary>
        /// 删除校验执行间隔（用于发现远端删除导致的本地多余数据）。
        /// </summary>
        public TimeSpan DeleteCheckInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// 当插入次数达到阈值时触发内部数据结构重排，以维持按主键有序。
        /// </summary>
        public int ReorderThreshold { get; set; } = 200;

        /// <summary>
        /// Keyset 分页查询主键时的默认页大小。
        /// </summary>
        public int KeysetPageSize { get; set; } = 512;

        /// <summary>
        /// 按主键批量拉取的默认批大小。
        /// </summary>
        public int FetchByKeysBatchSize { get; set; } = 128;

        /// <summary>
        /// 远端统计信息复用窗口；小于等于 0 表示禁用节流/复用。
        /// </summary>
        public TimeSpan RemoteStatsReuseWindow { get; set; } = TimeSpan.FromSeconds(2);

        /// <summary>
        /// 为空表示默认使用 Type.Name 作为类型级缓存 key。
        /// </summary>
        public string CacheKeyPrefix { get; set; } = string.Empty;

        /// <summary>
        /// 是否启用调试/信息级缓存诊断日志。
        /// 警告与错误级日志在配置日志接收器后仍会继续输出。
        /// </summary>
        public bool EnableDiagnosticsLogging { get; set; }

        /// <summary>
        /// 缓存诊断日志接收器。
        /// </summary>
        public Action<CacheLogEntry>? DiagnosticsLogSink { get; set; }
    }
}
