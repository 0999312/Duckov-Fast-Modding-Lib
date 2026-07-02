using FastModdingLib.Utils;
using System.Collections.Generic;

namespace FastModdingLib.Crafting
{
    /// <summary>标签成本注册表。存储每个 FML 注册配方的标签匹配成本。</summary>
    internal static class TagCostRegistry
    {
        private static readonly Dictionary<string, TagCostEntry> _entries = new Dictionary<string, TagCostEntry>();

        public static void Register(string formulaId, TagCostEntry entry)
        {
            _entries[formulaId] = entry;
        }

        public static bool TryGet(string formulaId, out TagCostEntry entry)
            => _entries.TryGetValue(formulaId, out entry);

        public static bool Remove(string formulaId)
            => _entries.Remove(formulaId);

        public static int RemoveAllByOwner(string modid)
        {
            var toRemove = new List<string>();
            foreach (var kvp in _entries)
                if (kvp.Value.Modid == modid)
                    toRemove.Add(kvp.Key);
            foreach (var key in toRemove)
                _entries.Remove(key);
            return toRemove.Count;
        }
    }

    /// <summary>单个配方的标签成本条目。</summary>
    public class TagCostEntry
    {
        public string FormulaId;
        public TagItemCost[] Costs;
        public string Modid;
    }

    /// <summary>单个标签物品成本。</summary>
    public struct TagItemCost
    {
        public string? Tag;
        public int Amount;
        public int? MinQuality;
        public bool DurabilityCost;
    }
}
