using System;
using System.Collections.Generic;
using System.Reflection;
using ItemStatsSystem;
using UnityEngine;

namespace FastModdingLib.Crafting
{
    /// <summary>标签成本验证器：搜索、验证、扣除标签匹配物品。</summary>
    internal static class TagCostValidator
    {
        public static bool Validate(TagItemCost[] costs)
        {
            var items = EnumeratePlayerItems();
            if (items == null) return false;

            foreach (var cost in costs)
            {
                if (CountAvailable(items, cost) < cost.Amount)
                    return false;
            }
            return true;
        }

        public static void Consume(TagItemCost[] costs)
        {
            var items = EnumeratePlayerItems();
            if (items == null) return;

            foreach (var cost in costs)
                ConsumeFromItems(items, cost);
        }

        private static float CountAvailable(List<Item> items, TagItemCost cost)
        {
            float total = 0f;
            foreach (var item in items)
            {
                if (item == null) continue;
                if (!string.IsNullOrEmpty(cost.Tag) && !ItemUtils.HasTag(item, cost.Tag)) continue;
                if (cost.MinQuality.HasValue && item.Quality < cost.MinQuality.Value) continue;
                total += GetEffectiveAmount(item, cost.DurabilityCost);
            }
            return total;
        }

        private static void ConsumeFromItems(List<Item> items, TagItemCost cost)
        {
            var candidates = new List<(Item item, float effective)>();
            foreach (var item in items)
            {
                if (item == null) continue;
                if (!string.IsNullOrEmpty(cost.Tag) && !ItemUtils.HasTag(item, cost.Tag)) continue;
                if (cost.MinQuality.HasValue && item.Quality < cost.MinQuality.Value) continue;
                candidates.Add((item, GetEffectiveAmount(item, cost.DurabilityCost)));
            }
            candidates.Sort((a, b) => a.effective.CompareTo(b.effective));

            float remaining = cost.Amount;
            foreach (var (item, _) in candidates)
            {
                if (remaining <= 0) break;
                int toRemove = Mathf.CeilToInt(Mathf.Min(remaining, (float)item.StackCount));
                item.StackCount -= toRemove;
                if (item.StackCount <= 0) item.DestroyTree();
                remaining -= toRemove;
            }
        }

        private static float GetEffectiveAmount(Item item, bool durabilityCost)
        {
            float stack = item.StackCount;
            if (!durabilityCost) return stack;

            try
            {
                var method = typeof(Item).GetMethod("GetStat", new[] { typeof(int) });
                var stat = method?.Invoke(item, new object[] { "Durability".GetHashCode() });
                if (stat != null)
                {
                    var baseProp = stat.GetType().GetProperty("BaseValue");
                    var curProp = stat.GetType().GetProperty("Value");
                    if (baseProp == null || curProp == null) return stack;
                    var baseValObj = baseProp.GetValue(stat);
                    var curValObj = curProp.GetValue(stat);
                    if (baseValObj == null || curValObj == null) return stack;
                    var baseVal = (float)baseValObj;
                    var curVal = (float)curValObj;
                    if (baseVal > 0) return stack * (curVal / baseVal);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TagCostValidator.GetEffectiveAmount] Reflection failed: {e.Message}");
            }
            return stack;
        }

        private static List<Item>? EnumeratePlayerItems()
        {
            var inv = CharacterMainControl.Main?.CharacterItem?.Inventory;
            if (inv == null) return null;

            var result = new List<Item>();
            try
            {
                var allSlots = inv.GetType().GetProperty("AllSlots")?.GetValue(inv) as System.Collections.IEnumerable;
                if (allSlots == null) return result;

                foreach (var slot in allSlots)
                {
                    var item = slot.GetType().GetProperty("Content")?.GetValue(slot) as Item;
                    if (item != null) result.Add(item);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TagCostValidator.GetEffectiveAmount] Reflection failed: {e.Message}");
            }
            return result;
        }
    }
}
