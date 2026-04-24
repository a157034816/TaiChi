using System;
using System.Collections.Generic;

namespace TaiChi.Cache
{
    /// <summary>
    /// 实体缓存的本地表结构：维护“按主键有序”的实体列表与索引。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <typeparam name="TPk">主键类型。</typeparam>
    /// <remarks>
    /// 不变式：
    /// - <see cref="_keys"/> 始终按升序排列；
    /// - <see cref="_items"/> 与 <see cref="_keys"/> 通过相同索引对齐；
    /// - <see cref="_byKey"/> 提供 O(1) 的按键命中。
    /// </remarks>
    internal sealed class CacheTable<TEntity, TPk>
        where TPk : struct, IComparable<TPk>
    {
        /// <summary>
        /// 按主键顺序存放的实体列表（与 <see cref="_keys"/> 对齐）。
        /// </summary>
        private readonly List<TEntity> _items = new List<TEntity>();

        /// <summary>
        /// 已排序的主键列表（与 <see cref="_items"/> 对齐）。
        /// </summary>
        private readonly List<TPk> _keys = new List<TPk>();

        /// <summary>
        /// 主键到实体的映射（用于快速命中）。
        /// </summary>
        private readonly Dictionary<TPk, TEntity> _byKey = new Dictionary<TPk, TEntity>();

        /// <summary>
        /// 缓存的只读快照（写入时失效）。
        /// </summary>
        /// <remarks>
        /// 设计意图：为高频读取路径提供“稳定引用”的列表快照，避免每次读取都复制 <see cref="_items"/>。
        /// - 快照只复制列表结构（顺序/数量），不对实体对象做深拷贝；
        /// - 一旦表发生结构性变更（增删改/重排），需要将快照失效并在下次读取时重建。
        /// </remarks>
        private IReadOnlyList<TEntity>? _cachedSnapshot;

        /// <summary>
        /// 本地记录的最大更新时间（用于增量拉取阈值）。
        /// </summary>
        public DateTimeOffset? LastMaxUpdatedAt { get; set; }

        /// <summary>
        /// 上次删除校验时间。
        /// </summary>
        public DateTimeOffset? LastDeleteCheckAt { get; set; }

        /// <summary>
        /// 自上次重排以来的插入次数。
        /// </summary>
        public int InsertsSinceReorder { get; set; }

        /// <summary>
        /// 是否已完成“整表快照”构建。
        /// </summary>
        public bool FullSnapshotReady { get; set; }

        /// <summary>
        /// 当前实体数量。
        /// </summary>
        public int Count => _items.Count;

        /// <summary>
        /// 按主键有序的实体列表（只读视图）。
        /// </summary>
        public IReadOnlyList<TEntity> Items => _items;

        /// <summary>
        /// 获取或创建当前表的只读快照。
        /// </summary>
        /// <remarks>
        /// - 首次调用或快照失效后，会复制当前 <see cref="_items"/> 并返回只读包装；
        /// - 在表未发生结构性变更期间，会复用同一快照引用，避免高频分配。
        /// </remarks>
        public IReadOnlyList<TEntity> GetOrCreateSnapshot()
        {
            if (_cachedSnapshot != null)
            {
                return _cachedSnapshot;
            }

            var copy = new List<TEntity>(_items);
            _cachedSnapshot = copy.AsReadOnly();
            return _cachedSnapshot;
        }

        /// <summary>
        /// 使缓存的只读快照失效。
        /// </summary>
        private void InvalidateSnapshot()
        {
            _cachedSnapshot = null;
        }

        /// <summary>
        /// 按升序排列的主键列表（只读视图）。
        /// </summary>
        public IReadOnlyList<TPk> Keys => _keys;

        /// <summary>
        /// 判断是否包含指定主键。
        /// </summary>
        public bool ContainsKey(TPk key)
        {
            return _byKey.ContainsKey(key);
        }

        /// <summary>
        /// 尝试按主键获取实体。
        /// </summary>
        public bool TryGetValue(TPk key, out TEntity entity)
        {
            if (_byKey.TryGetValue(key, out entity!))
            {
                return true;
            }

            entity = default!;
            return false;
        }

        /// <summary>
        /// 清空本地表及相关元数据。
        /// </summary>
        public void Clear()
        {
            _items.Clear();
            _keys.Clear();
            _byKey.Clear();
            InvalidateSnapshot();
            FullSnapshotReady = false;
            LastMaxUpdatedAt = null;
            LastDeleteCheckAt = null;
            InsertsSinceReorder = 0;
        }

        /// <summary>
        /// 按主键删除一条记录（若不存在则无操作）。
        /// </summary>
        public void RemoveByKey(TPk key)
        {
            if (!_byKey.Remove(key))
            {
                return;
            }

            int index = _keys.BinarySearch(key);
            if (index >= 0)
            {
                _keys.RemoveAt(index);
                _items.RemoveAt(index);
            }

            InvalidateSnapshot();
        }

        /// <summary>
        /// 获取当前最小与最大主键。
        /// </summary>
        public (TPk? Min, TPk? Max) GetMinMaxKeys()
        {
            if (_keys.Count == 0)
            {
                return (null, null);
            }

            return (_keys[0], _keys[_keys.Count - 1]);
        }

        /// <summary>
        /// 批量 Upsert：新增/更新实体，并按需要执行删除与更新时间维护。
        /// </summary>
        /// <param name="entities">实体列表。</param>
        /// <param name="keyGetter">主键获取器。</param>
        /// <param name="updatedAtGetter">可选更新时间获取器（用于维护 <see cref="LastMaxUpdatedAt"/>）。</param>
        /// <param name="isDeleted">可选删除判断器（为 true 时将从本地表移除）。</param>
        /// <param name="reorderThreshold">触发重排的插入阈值。</param>
        public void UpsertMany(
            IReadOnlyList<TEntity> entities,
            Func<TEntity, TPk> keyGetter,
            Func<TEntity, DateTimeOffset?>? updatedAtGetter,
            Func<TEntity, bool>? isDeleted,
            int reorderThreshold)
        {
            foreach (var entity in entities)
            {
                var key = keyGetter(entity);

                if (isDeleted != null && isDeleted(entity))
                {
                    RemoveByKey(key);
                    continue;
                }

                UpsertOne(entity, key, reorderThreshold);

                if (updatedAtGetter != null)
                {
                    var updatedAt = updatedAtGetter(entity);
                    if (updatedAt != null && (LastMaxUpdatedAt == null || updatedAt > LastMaxUpdatedAt))
                    {
                        LastMaxUpdatedAt = updatedAt;
                    }
                }
            }
        }

        /// <summary>
        /// 插入或更新单条实体，并在插入次数达到阈值时重排。
        /// </summary>
        private void UpsertOne(TEntity entity, TPk key, int reorderThreshold)
        {
            int index = _keys.BinarySearch(key);
            if (index >= 0)
            {
                _items[index] = entity;
                _byKey[key] = entity;
                InvalidateSnapshot();
                return;
            }

            int insertIndex = ~index;
            _keys.Insert(insertIndex, key);
            _items.Insert(insertIndex, entity);
            _byKey[key] = entity;
            InvalidateSnapshot();

            InsertsSinceReorder++;
            if (reorderThreshold > 0 && InsertsSinceReorder >= reorderThreshold)
            {
                Reorder();
            }
        }

        /// <summary>
        /// 通过字典重建有序列表，修复由于多次插入导致的局部无序与碎片。
        /// </summary>
        public void Reorder()
        {
            if (_keys.Count <= 1)
            {
                InsertsSinceReorder = 0;
                return;
            }

            InvalidateSnapshot();

            var pairs = new List<KeyValuePair<TPk, TEntity>>(_byKey.Count);
            foreach (var kv in _byKey)
            {
                pairs.Add(kv);
            }

            pairs.Sort((a, b) => a.Key.CompareTo(b.Key));

            _keys.Clear();
            _items.Clear();

            foreach (var kv in pairs)
            {
                _keys.Add(kv.Key);
                _items.Add(kv.Value);
            }

            InsertsSinceReorder = 0;
        }
    }
}
