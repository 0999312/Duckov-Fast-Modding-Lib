using Duckov.Quests;
using ItemStatsSystem;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FastModdingLib.Quests
{
    public class FMLTask_SubmitItemByTag : Task
    {
        [SerializeField] private string itemTag = "";
        [SerializeField] private int requireAmount = 1;
        [SerializeField] private int? minQuality;
        [SerializeField] private bool durabilityCost;
        [SerializeField] private int submitted;

        public string ItemTag { get => itemTag; internal set => itemTag = value ?? ""; }
        public int RequireAmount { get => requireAmount; internal set => requireAmount = value; }
        public int? MinQuality { get => minQuality; internal set => minQuality = value; }
        public bool DurabilityCost { get => durabilityCost; internal set => durabilityCost = value; }
        public int Submitted => submitted;

        protected override bool CheckFinished() => submitted >= requireAmount;
        public override object GenerateSaveData() => submitted;
        public override void SetupSaveData(object data) { if (data is int n) submitted = n; }

        public bool TrySubmitFromInventory()
        {
            var items = EnumeratePlayerItems();
            if (items == null) return false;

            int needed = requireAmount - submitted;
            if (needed <= 0) return false;

            var candidates = new List<(Item item, float effective)>();
            foreach (var item in items)
            {
                if (item == null) continue;
                if (!string.IsNullOrEmpty(itemTag) && !ItemUtils.HasTag(item, itemTag)) continue;
                if (minQuality.HasValue && item.Quality < minQuality.Value) continue;
                candidates.Add((item, GetEffective(item, durabilityCost)));
            }
            candidates.Sort((a, b) => a.effective.CompareTo(b.effective));

            float acc = 0f;
            var consume = new List<Item>();
            foreach (var (item, eff) in candidates)
            {
                if (acc >= needed) break;
                acc += eff;
                consume.Add(item);
            }
            if (acc < needed) return false;

            foreach (var item in consume)
            {
                item.StackCount--;
                if (item.StackCount <= 0) item.DestroyTree();
            }
            submitted += needed;
            ReportStatusChanged();
            return true;
        }

        private static float GetEffective(Item item, bool useDurability)
        {
            if (!useDurability) return item.StackCount;
            try
            {
                var m = typeof(Item).GetMethod("GetStat", new[] { typeof(int) });
                var s = m?.Invoke(item, new object[] { "Durability".GetHashCode() });
                if (s != null)
                {
                    var baseProp = s.GetType().GetProperty("BaseValue");
                    var curProp = s.GetType().GetProperty("Value");
                    if (baseProp == null || curProp == null) return item.StackCount;
                    var bvObj = baseProp.GetValue(s);
                    var cvObj = curProp.GetValue(s);
                    if (bvObj == null || cvObj == null) return item.StackCount;
                    float bv = (float)bvObj;
                    float cv = (float)cvObj;
                    if (bv > 0) return item.StackCount * (cv / bv);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FMLTask_SubmitItemByTag.GetEffective] Reflection failed: {e.Message}");
            }
            return item.StackCount;
        }

        private static List<Item>? EnumeratePlayerItems()
        {
            var inv = CharacterMainControl.Main?.CharacterItem?.Inventory;
            if (inv == null) return null;
            var r = new List<Item>();
            try
            {
                var slots = inv.GetType().GetProperty("AllSlots")?.GetValue(inv) as System.Collections.IEnumerable;
                if (slots == null) return r;
                foreach (var s in slots)
                {
                    var item = s.GetType().GetProperty("Content")?.GetValue(s) as Item;
                    if (item != null) r.Add(item);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FMLTask_SubmitItemByTag.EnumeratePlayerItems] Reflection failed: {e.Message}");
            }
            return r;
        }
    }
}
