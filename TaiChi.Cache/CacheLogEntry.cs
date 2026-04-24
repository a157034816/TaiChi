using System;
using System.Collections.Generic;

namespace TaiChi.Cache
{
    /// <summary>
    /// TaiChi.Cache 结构化诊断日志事件。
    /// </summary>
    public sealed class CacheLogEntry
    {
        /// <summary>
        /// 创建一条缓存诊断日志事件。
        /// </summary>
        /// <param name="level">日志级别。</param>
        /// <param name="eventName">事件名（用于检索与聚合）。</param>
        /// <param name="entityType">实体类型（用于标识缓存归属）。</param>
        /// <param name="cacheKey">缓存键（用于区分同一实体的不同缓存实例）。</param>
        /// <param name="message">日志消息。</param>
        /// <param name="exception">可选异常信息。</param>
        /// <param name="fields">可选结构化字段。</param>
        public CacheLogEntry(
            CacheLogLevel level,
            string eventName,
            Type entityType,
            string cacheKey,
            string message,
            Exception? exception = null,
            IReadOnlyDictionary<string, object?>? fields = null)
        {
            Level = level;
            EventName = eventName ?? throw new ArgumentNullException(nameof(eventName));
            EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
            CacheKey = cacheKey ?? throw new ArgumentNullException(nameof(cacheKey));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Exception = exception;
            Fields = fields;
        }

        /// <summary>
        /// 日志级别。
        /// </summary>
        public CacheLogLevel Level { get; }

        /// <summary>
        /// 事件名（建议使用稳定的点分命名，例如 <c>cache.refresh.start</c>）。
        /// </summary>
        public string EventName { get; }

        /// <summary>
        /// 该日志对应的实体类型。
        /// </summary>
        public Type EntityType { get; }

        /// <summary>
        /// 该日志对应的缓存键。
        /// </summary>
        public string CacheKey { get; }

        /// <summary>
        /// 日志消息文本。
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 可选异常信息。
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// 可选结构化字段。
        /// </summary>
        public IReadOnlyDictionary<string, object?>? Fields { get; }
    }
}
