using Duckov.Economy;
using FastModdingLib.Register;
using FastModdingLib.Utils;
using System.Linq;
using UnityEngine;

namespace FastModdingLib
{
    public static class CraftingUtils
    {
        public static readonly CraftingFormulaRegistry craftingFormulaRegistry;
        public static readonly DecomposeRegistry decomposeRegistry;

        static CraftingUtils()
        {
            craftingFormulaRegistry = new CraftingFormulaRegistry();
            decomposeRegistry = new DecomposeRegistry();
            RegistryManager.Instance.Registry.Set(
                new Identifier("fastmoddinglib", "crafting_formula"), craftingFormulaRegistry);
            RegistryManager.Instance.Registry.Set(
                new Identifier("fastmoddinglib", "decompose"), decomposeRegistry);
        }

        // ═══════════════════════════════════════════════════════════════
        //  新 API — struct 驱动
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// 添加合成配方（推荐）。
        /// </summary>
        public static void AddCraftingFormula(CraftingFormulaData data)
        {
            string owner = data.Id.Domain;
            string formulaId = data.Id.Path;
            var tags = data.Tags ?? new[] { "WorkBenchAdvanced" };

            AddCraftingFormulaInternal(
                id: data.Id,
                formulaId: formulaId,
                money: data.Money,
                costItems: ResolveItems(data.CostItems),
                resultItemId: data.Result.ResolveTypeId(),
                resultItemAmount: data.Result.Amount,
                tags: tags,
                requirePerk: data.RequirePerk ?? "",
                unlockByDefault: data.UnlockByDefault,
                hideInIndex: data.HideInIndex,
                lockInDemo: data.LockInDemo,
                owner: owner
            );
        }

        /// <summary>
        /// 添加分解配方（推荐）。
        /// </summary>
        public static void AddDecomposeFormula(DecomposeFormulaData data)
        {
            string owner = data.Id.Domain;
            int sourceTypeId = ItemUtils.ResolveItemRef(data.SourceItemId, data.SourceItemTypeId);

            AddDecomposeFormulaInternal(
                id: data.Id,
                sourceItemId: sourceTypeId,
                money: data.Money,
                resultItems: ResolveItems(data.ResultItems),
                owner: owner
            );
        }

        // ═══════════════════════════════════════════════════════════════
        //  内部实现
        // ═══════════════════════════════════════════════════════════════

        private static (int id, long amount)[] ResolveItems(ItemEntry[]? items)
        {
            if (items == null) return System.Array.Empty<(int, long)>();
            var result = new (int, long)[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                result[i] = (items[i].ResolveTypeId(), items[i].Amount);
            }
            return result;
        }

        private static void AddCraftingFormulaInternal(
            Identifier id, string formulaId, long money,
            (int id, long amount)[] costItems,
            int resultItemId, int resultItemAmount,
            string[] tags, string requirePerk,
            bool unlockByDefault, bool hideInIndex, bool lockInDemo,
            string owner)
        {
            if (craftingFormulaRegistry.TryGet(id, out _))
            {
                Debug.LogWarning("Exist Crafting formula: " + formulaId);
                return;
            }

            CraftingFormulaCollection instance = CraftingFormulaCollection.Instance;
            var list = instance.list;
            var item = new CraftingFormula
            {
                id = formulaId,
                unlockByDefault = unlockByDefault
            };

            var cost = new Cost { money = money };
            var array = new Cost.ItemEntry[costItems.Length];
            for (int i = 0; i < costItems.Length; i++)
            {
                array[i] = new Cost.ItemEntry
                {
                    id = costItems[i].id,
                    amount = costItems[i].amount
                };
            }
            cost.items = array;
            item.cost = cost;

            item.result = new CraftingFormula.ItemEntry
            {
                id = resultItemId,
                amount = resultItemAmount
            };
            item.requirePerk = requirePerk;
            item.tags = tags;
            item.hideInIndex = hideInIndex;
            item.lockInDemo = lockInDemo;

            list.Add(item);
            craftingFormulaRegistry.Set(id, item, owner);
            Debug.Log($"Added crafting formula: {formulaId} (identifier: {id})");
        }

        private static void AddDecomposeFormulaInternal(
            Identifier id, int sourceItemId, long money,
            (int id, long amount)[] resultItems, string owner)
        {
            if (decomposeRegistry.TryGetIdentifier(sourceItemId, out _))
            {
                Debug.LogWarning($"Exist decompose formula: {sourceItemId}");
                return;
            }

            DecomposeDatabase instance = DecomposeDatabase.Instance;
            var item = new DecomposeFormula { item = sourceItemId, valid = true };

            var result = new Cost { money = money };
            var array = new Cost.ItemEntry[resultItems.Length];
            for (int i = 0; i < resultItems.Length; i++)
            {
                array[i] = new Cost.ItemEntry
                {
                    id = resultItems[i].id,
                    amount = resultItems[i].amount
                };
            }
            result.items = array;
            item.result = result;

            decomposeRegistry.Register(sourceItemId, id, item, owner);
            Debug.Log($"Added decompose: {sourceItemId} (identifier: {id})");

            instance.Dic.Add(sourceItemId, item);
            instance.entries = instance.Dic.Values.ToArray();
        }

        // ═══════════════════════════════════════════════════════════════
        //  旧 API — 向后兼容（委托到 struct API）
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// [兼容] 添加合成配方（formulaId 字符串）。
        /// </summary>
        public static void AddCraftingFormula(
            string formulaId, long money,
            (int id, long amount)[] costItems,
            int resultItemId, int resultItemAmount,
            string[] tags = null!, string requirePerk = "",
            bool unlockByDefault = true, bool hideInIndex = false,
            bool lockInDemo = false, string? modid = null)
        {
            string owner = modid ?? RegistryManager.CurrentModid;
            var data = new CraftingFormulaData
            {
                Id = new Identifier(owner, $"crafting_{formulaId}"),
                Money = money,
                CostItems = ConvertTupleItems(costItems),
                Result = ItemEntry.Of(resultItemId, resultItemAmount),
                Tags = tags,
                RequirePerk = requirePerk,
                UnlockByDefault = unlockByDefault,
                HideInIndex = hideInIndex,
                LockInDemo = lockInDemo
            };
            AddCraftingFormula(data);
        }

        /// <summary>
        /// [兼容] 添加合成配方（Identifier + int 产物）。
        /// </summary>
        public static void AddCraftingFormula(
            Identifier id, long money,
            (int id, long amount)[] costItems,
            int resultItemId, int resultItemAmount,
            string[] tags = null!, string requirePerk = "",
            bool unlockByDefault = true, bool hideInIndex = false,
            bool lockInDemo = false)
        {
            AddCraftingFormula(new CraftingFormulaData
            {
                Id = id,
                Money = money,
                CostItems = ConvertTupleItems(costItems),
                Result = ItemEntry.Of(resultItemId, resultItemAmount),
                Tags = tags,
                RequirePerk = requirePerk,
                UnlockByDefault = unlockByDefault,
                HideInIndex = hideInIndex,
                LockInDemo = lockInDemo
            });
        }

        /// <summary>
        /// [兼容] 添加合成配方（Identifier + Identifier 产物）。
        /// </summary>
        public static void AddCraftingFormula(
            Identifier id, long money,
            (int id, long amount)[] costItems,
            Identifier resultItemId, int resultItemAmount,
            string[] tags = null!, string requirePerk = "",
            bool unlockByDefault = true, bool hideInIndex = false,
            bool lockInDemo = false)
        {
            AddCraftingFormula(new CraftingFormulaData
            {
                Id = id,
                Money = money,
                CostItems = ConvertTupleItems(costItems),
                Result = ItemEntry.Of(resultItemId, resultItemAmount),
                Tags = tags,
                RequirePerk = requirePerk,
                UnlockByDefault = unlockByDefault,
                HideInIndex = hideInIndex,
                LockInDemo = lockInDemo
            });
        }

        /// <summary>
        /// [兼容] 添加合成配方（Identifier cost + int 产物）。
        /// </summary>
        public static void AddCraftingFormula(
            Identifier id, long money,
            (Identifier itemId, int amount)[] costItems,
            int resultItemId, int resultItemAmount,
            string[] tags = null!, string requirePerk = "",
            bool unlockByDefault = true, bool hideInIndex = false,
            bool lockInDemo = false)
        {
            AddCraftingFormula(new CraftingFormulaData
            {
                Id = id,
                Money = money,
                CostItems = ConvertTupleItems(costItems),
                Result = ItemEntry.Of(resultItemId, resultItemAmount),
                Tags = tags,
                RequirePerk = requirePerk,
                UnlockByDefault = unlockByDefault,
                HideInIndex = hideInIndex,
                LockInDemo = lockInDemo
            });
        }

        /// <summary>
        /// [兼容] 添加合成配方（Identifier cost + Identifier 产物）。
        /// </summary>
        public static void AddCraftingFormula(
            Identifier id, long money,
            (Identifier itemId, int amount)[] costItems,
            Identifier resultItemId, int resultItemAmount,
            string[] tags = null!, string requirePerk = "",
            bool unlockByDefault = true, bool hideInIndex = false,
            bool lockInDemo = false)
        {
            AddCraftingFormula(new CraftingFormulaData
            {
                Id = id,
                Money = money,
                CostItems = ConvertTupleItems(costItems),
                Result = ItemEntry.Of(resultItemId, resultItemAmount),
                Tags = tags,
                RequirePerk = requirePerk,
                UnlockByDefault = unlockByDefault,
                HideInIndex = hideInIndex,
                LockInDemo = lockInDemo
            });
        }

        /// <summary>
        /// [兼容] 添加分解配方（int source）。
        /// </summary>
        public static void AddDecomposeFormula(
            int itemId, long money,
            (int id, long amount)[] resultItems,
            string? modid = null)
        {
            string owner = modid ?? RegistryManager.CurrentModid;
            AddDecomposeFormula(new DecomposeFormulaData
            {
                Id = new Identifier(owner, $"decompose_{itemId}"),
                SourceItemTypeId = itemId,
                Money = money,
                ResultItems = ConvertTupleItems(resultItems)
            });
        }

        /// <summary>
        /// [兼容] 添加分解配方（Identifier + int source）。
        /// </summary>
        public static void AddDecomposeFormula(
            Identifier id, int sourceItemId, long money,
            (int id, long amount)[] resultItems)
        {
            AddDecomposeFormula(new DecomposeFormulaData
            {
                Id = id,
                SourceItemTypeId = sourceItemId,
                Money = money,
                ResultItems = ConvertTupleItems(resultItems)
            });
        }

        // ── tuple → ItemEntry[] 转换 ──

        private static ItemEntry[] ConvertTupleItems((int id, long amount)[] tuples)
        {
            if (tuples == null) return System.Array.Empty<ItemEntry>();
            var result = new ItemEntry[tuples.Length];
            for (int i = 0; i < tuples.Length; i++)
                result[i] = ItemEntry.Of(tuples[i].id, (int)tuples[i].amount);
            return result;
        }

        private static ItemEntry[] ConvertTupleItems((Identifier itemId, int amount)[] tuples)
        {
            if (tuples == null) return System.Array.Empty<ItemEntry>();
            var result = new ItemEntry[tuples.Length];
            for (int i = 0; i < tuples.Length; i++)
                result[i] = ItemEntry.Of(tuples[i].itemId, tuples[i].amount);
            return result;
        }

        // ═══════════════════════════════════════════════════════════════
        //  卸载
        // ═══════════════════════════════════════════════════════════════

        public static void RemoveAllAddedFormulas(string? modid = null)
            => craftingFormulaRegistry.RemoveAllByOwner(modid ?? RegistryManager.CurrentModid);

        public static void RemoveAllAddedDecomposeFormulas(string? modid = null)
            => decomposeRegistry.RemoveAllByOwner(modid ?? RegistryManager.CurrentModid);
    }
}
