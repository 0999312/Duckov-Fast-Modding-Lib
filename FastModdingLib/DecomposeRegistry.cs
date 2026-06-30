using FastModdingLib.Register;
using FastModdingLib.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FastModdingLib
{
    /// <summary>
    /// 分解配方注册表。反向 key = <see cref="DecomposeFormula"/>.item (int itemId)；
    /// <see cref="OnRemoved"/> 从 <see cref="DecomposeDatabase"/>.Instance.Dic 移除 native 条目
    /// 并触发 <c>RebuildDictionary</c> 反射（PLAN-Register §5.5 R7 要求保留该反射路径）。
    /// <para>
    /// 直接继承 <see cref="SimpleRegistry{T}"/> 并手维护反向索引，而非继承
    /// <see cref="ReverseLookupRegistry{T, TKey}"/>——后者在 R3 被声明为 sealed，
    /// 无法被子类化以 override <see cref="SimpleRegistry{T}.OnRemoved"/>。
    /// </para>
    /// </summary>
    public class DecomposeRegistry : SimpleRegistry<DecomposeFormula>
    {
        private readonly Dictionary<int, Identifier> _byItemId;

        public DecomposeRegistry()
        {
            _byItemId = new Dictionary<int, Identifier>();
        }

        /// <summary>
        /// 写入主字典并同步反向索引。等价于
        /// <see cref="SimpleRegistry{T}.Set(Identifier, T, string)"/> 后追加
        /// <c>_byItemId[itemId] = id</c>。
        /// </summary>
        public void Register(int itemId, Identifier id, DecomposeFormula value, string modid)
        {
            Set(id, value, modid);
            _byItemId[itemId] = id;
        }

        /// <summary>按 native itemId 反查 <see cref="Identifier"/>；不存在返回 false。</summary>
        public bool TryGetIdentifier(int itemId, out Identifier? id)
        {
            return _byItemId.TryGetValue(itemId, out id);
        }

        /// <summary>
        /// 移除指定 entry 并同步清反向索引。先快照 value 以提取 nativeKey，
        /// 再调基类 <see cref="SimpleRegistry{T}.Remove(Identifier)"/>（触发 <c>OnRemoved</c>），
        /// 最后清反向字典。
        /// </summary>
        public override bool Remove(Identifier id)
        {
            if (!TryGet(id, out var value))
            {
                return false;
            }
            int nativeKey = value.item;
            bool removed = base.Remove(id);
            if (removed)
            {
                _byItemId.Remove(nativeKey);
            }
            return removed;
        }

        /// <summary>清空主字典与反向索引；不触发 <c>OnRemoved</c>（与基类语义一致）。</summary>
        public override void Clear()
        {
            base.Clear();
            _byItemId.Clear();
        }

        protected override void OnRemoved(Identifier id, DecomposeFormula value, string? modid)
        {
            var instance = DecomposeDatabase.Instance;
            instance.Dic.Remove(value.item);
            instance.entries = instance.Dic.Values.ToArray();
            typeof(DecomposeDatabase).GetMethod(
                "RebuildDictionary",
                BindingFlags.Instance | BindingFlags.NonPublic
            )?.Invoke(instance, null);
        }
    }
}