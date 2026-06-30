using FastModdingLib.Utils;
using System;
using System.Collections.Generic;

namespace FastModdingLib.Register
{
    /// <summary>
    /// 有 native 键（如 Audio eventName、Item TypeID）的 registry。
    /// 在 <see cref="SimpleRegistry{T}"/> 基础上维护 native key → <see cref="Identifier"/> 反向索引，
    /// 写入 / 删除 / 批量卸载三条路径均同步反向字典，避免出现两份真相。
    /// </summary>
    /// <remarks>
    /// §11 风险对策：关键变异方法用 <see langword="new"/> + <see langword="sealed"/> 杜绝
    /// 二次继承错失反向索引；native 侧善后仍走基类 <c>OnRemoved</c> protected virtual 扩展点。
    /// </remarks>
    public sealed class ReverseLookupRegistry<T, TKey> : SimpleRegistry<T>
    {
        private readonly Dictionary<TKey, Identifier> _byNativeKey;
        private readonly Func<T, TKey> _nativeKeySelector;

        /// <param name="nativeKeySelector">从 value 提取 native key 的函数（如 <c>data => data.Eventname</c>）。</param>
        public ReverseLookupRegistry(Func<T, TKey> nativeKeySelector)
        {
            _nativeKeySelector = nativeKeySelector ?? throw new ArgumentNullException(nameof(nativeKeySelector));
            _byNativeKey = new Dictionary<TKey, Identifier>();
        }

        /// <summary>
        /// 写入主字典并同步反向索引。等价于 <see cref="SimpleRegistry{T}.Set(Identifier, T, string)"/>
        /// 后追加 <c>_byNativeKey[nativeKey] = id</c>。
        /// </summary>
        public void Register(TKey nativeKey, Identifier id, T value, string modid)
        {
            Set(id, value, modid);
            _byNativeKey[nativeKey] = id;
        }

        /// <summary>按 native key 反查 <see cref="Identifier"/>；不存在返回 false。</summary>
        public bool TryGetIdentifier(TKey nativeKey, out Identifier? id)
        {
            return _byNativeKey.TryGetValue(nativeKey, out id);
        }

        /// <summary>
        /// 移除指定 entry 并同步清反向索引。先快照 value 以提取 nativeKey，再调基类
        /// <see cref="SimpleRegistry{T}.Remove(Identifier)"/>（触发 <c>OnRemoved</c>），最后清反向字典。
        /// </summary>
        new public bool Remove(Identifier id)
        {
            if (!TryGet(id, out var value))
            {
                return false;
            }
            TKey nativeKey = _nativeKeySelector(value);
            bool removed = base.Remove(id);
            if (removed)
            {
                _byNativeKey.Remove(nativeKey);
            }
            return removed;
        }

        /// <summary>
        /// 按 owner 批量移除并同步清反向索引。先快照 (id, nativeKey) 对，再调基类
        /// <see cref="SimpleRegistry{T}.RemoveAllByOwner"/>（逐条触发 <c>OnRemoved</c>），
        /// 最后从反向字典移除对应 nativeKey。
        /// </summary>
        new public int RemoveAllByOwner(string modid)
        {
            var ids = GetAllByOwner(modid);
            var nativeKeys = new List<TKey>(ids.Count);
            foreach (var id in ids)
            {
                if (TryGet(id, out var value))
                {
                    nativeKeys.Add(_nativeKeySelector(value));
                }
            }
            int removed = base.RemoveAllByOwner(modid);
            foreach (var nativeKey in nativeKeys)
            {
                _byNativeKey.Remove(nativeKey);
            }
            return removed;
        }

        /// <summary>清空主字典与反向索引；不触发 <c>OnRemoved</c> 回调（与基类语义一致）。</summary>
        new public void Clear()
        {
            base.Clear();
            _byNativeKey.Clear();
        }
    }
}