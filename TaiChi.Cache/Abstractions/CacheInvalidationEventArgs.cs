using System;
using System.Collections.Generic;

namespace TaiChi.Cache.Abstractions
{
    /// <summary>
    /// 缓存失效事件参数。
    /// </summary>
    public sealed class CacheInvalidationEventArgs : EventArgs
    {
        /// <summary>
        /// 创建缓存失效事件参数。
        /// </summary>
        /// <param name="scope">失效范围。</param>
        /// <param name="entityType">实体类型。</param>
        /// <param name="keys">可选：主键集合（仅在按键失效时有意义）。</param>
        /// <param name="reason">可选：失效原因（用于诊断）。</param>
        public CacheInvalidationEventArgs(CacheInvalidateScope scope, Type entityType, IReadOnlyCollection<object>? keys = null, string? reason = null)
        {
            Scope = scope;
            EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
            Keys = keys;
            Reason = reason;
        }

        /// <summary>
        /// 失效范围。
        /// </summary>
        public CacheInvalidateScope Scope { get; }

        /// <summary>
        /// 对应实体类型。
        /// </summary>
        public Type EntityType { get; }

        /// <summary>
        /// 可选：失效主键集合（元素类型为 object，具体由调用方决定）。
        /// </summary>
        public IReadOnlyCollection<object>? Keys { get; }

        /// <summary>
        /// 可选：失效原因。
        /// </summary>
        public string? Reason { get; }
    }
}

