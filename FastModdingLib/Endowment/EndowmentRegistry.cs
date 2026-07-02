using Duckov.Endowment;
using FastModdingLib.Register;
using FastModdingLib.Utils;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FastModdingLib
{
    /// <summary>
    /// 天赋注册表。维护 Identifier → EndowmentEntry 主映射，
    /// 以及 Identifier → EndowmentIndex 的内部映射表。
    /// OnRemoved 时清理游戏原生侧残留并处理当前选中状态降级。
    /// </summary>
    public sealed class EndowmentRegistry : SimpleRegistry<EndowmentEntry>
    {
        /// <summary>Identifier → EndowmentIndex 内部映射（仅 FML 内部可见）。</summary>
        private readonly Dictionary<Identifier, EndowmentIndex> _indexMap = new Dictionary<Identifier, EndowmentIndex>();

        /// <summary>自定义天赋从 10 开始分配枚举值（0–4 为游戏原生）。</summary>
        private int _nextIndex = 10;

        /// <summary>为指定 Identifier 分配一个新的 EndowmentIndex（≥10）。</summary>
        internal EndowmentIndex AllocateIndex(Identifier id)
        {
            var idx = (EndowmentIndex)_nextIndex++;
            _indexMap[id] = idx;
            return idx;
        }

        /// <summary>查询 Identifier 对应的 EndowmentIndex。</summary>
        internal bool TryGetIndex(Identifier id, out EndowmentIndex index)
        {
            return _indexMap.TryGetValue(id, out index);
        }

        /// <summary>从 EndowmentIndex 反查 Identifier。</summary>
        internal bool TryGetIdentifier(EndowmentIndex index, out Identifier id)
        {
            foreach (var kvp in _indexMap)
            {
                if (EqualityComparer<EndowmentIndex>.Default.Equals(kvp.Value, index))
                {
                    id = kvp.Key;
                    return true;
                }
            }
            id = default;
            return false;
        }

        /// <summary>获取所有注册的 EndowmentEntry（供 Patch 层遍历）。</summary>
        internal IEnumerable<EndowmentEntry> GetAllEntries()
        {
            foreach (var kvp in this)
            {
                if (kvp.Value != null)
                    yield return kvp.Value;
            }
        }

        protected override void OnRemoved(Identifier id, EndowmentEntry value, string? modid)
        {
            // 如果当前选中的是这个天赋且正在被卸载 → 重置为 None
            if (_indexMap.TryGetValue(id, out var idx))
            {
                // 检查 EndowmentManager.CurrentIndex
                var currentField = typeof(EndowmentManager).GetField("currentIndex",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var current = currentField?.GetValue(EndowmentManager.Instance);
                if (current != null && EqualityComparer<EndowmentIndex>.Default.Equals((EndowmentIndex)current, idx))
                {
                    typeof(EndowmentManager).GetMethod("SelectIndex",
                        BindingFlags.NonPublic | BindingFlags.Instance)
                        ?.Invoke(EndowmentManager.Instance, new object[] { EndowmentIndex.None });
                }
                _indexMap.Remove(id);
            }

            // 不回收 _nextIndex——避免卸载后重装同一 mod 时 index 漂移
            // 销毁对应的 GameObject
            if (value != null && value.gameObject != null)
                Object.Destroy(value.gameObject);
        }
    }
}
