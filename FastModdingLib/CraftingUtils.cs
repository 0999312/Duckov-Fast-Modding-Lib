using Duckov.Economy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace FastModdingLib
{
    public static class CraftingUtils
    {
        public static Dictionary<string, string> addedFormulaIds = new Dictionary<string, string>();

        public static Dictionary<int, string> addedFormulaResults = new Dictionary<int, string>();

        public static Dictionary<int, string> addedDecomposeItemIds = new Dictionary<int, string>();
        public static void AddDecomposeFormula(int itemId, long money, (int id, long amount)[] resultItems, string modid = "old_fml_version")
        {
            DecomposeDatabase instance = DecomposeDatabase.Instance;

            DecomposeFormula[] collection = instance.entries;
            List<DecomposeFormula> list = new List<DecomposeFormula>(collection);
            foreach (DecomposeFormula item2 in list)
            {
                if (item2.item == itemId && item2.result.items.Any())
                {
                    Debug.LogWarning($"Existed decompose formula, item: {itemId}");
                    foreach (var itemResult in item2.result.items)
                    {
                        Debug.LogWarning($"itemResult: {itemResult.id} : {itemResult.amount}");
                    }
                    return;
                }
            }
            DecomposeFormula item = new DecomposeFormula
            {
                item = itemId,
                valid = true
            };
            Cost result = new Cost
            {
                money = money
            };
            Cost.ItemEntry[] array = new Cost.ItemEntry[resultItems.Length];
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
            list.Add(item);
            collection = list.ToArray();
            if (!addedDecomposeItemIds.ContainsKey(itemId))
            {
                addedDecomposeItemIds.Add(itemId, modid);
            }
            Debug.Log($"Added decompose: {itemId}");
            typeof(DecomposeDatabase).GetMethod("RebuildDictionary", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(instance, null);
        }

        public static void RemoveAllAddedDecomposeFormulas(string modid = "old_fml_version")
        {
            try
            {
                DecomposeDatabase instance = DecomposeDatabase.Instance;

                DecomposeFormula[] collection = instance.entries;
                List<DecomposeFormula> list = new List<DecomposeFormula>(collection);
                int num = 0;
                for (int num2 = list.Count - 1; num2 >= 0; num2--)
                {
                    if (addedDecomposeItemIds.ContainsKey(list[num2].item))
                    {
                        Debug.Log($"Remove decompose formula: {list[num2].item}");
                        list.RemoveAt(num2);
                        num++;
                    }
                }
                collection = list.ToArray();
                addedDecomposeItemIds.Clear();
                typeof(DecomposeDatabase).GetMethod("RebuildDictionary", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(instance, null);
                Debug.Log($"Removed {num} decompose formulas");
            }
            catch (Exception arg)
            {
                Debug.LogError($"Exception at removing decompose formula: {arg}");
            }
        }
        public static void AddCraftingFormula(string formulaId, long money, (int id, long amount)[] costItems, int resultItemId, int resultItemAmount, string[] tags = null, string requirePerk = "", bool unlockByDefault = true, bool hideInIndex = false, bool lockInDemo = false, string modid = "old_fml_version")
        {
            CraftingFormulaCollection instance = CraftingFormulaCollection.Instance;
            List<CraftingFormula> list = instance.list;
            foreach (CraftingFormula item2 in list)
            {
                if (item2.id == formulaId)
                {
                    Debug.LogWarning("Exist Crafting formula: " + formulaId);
                    return;
                }
            }
            CraftingFormula item = new CraftingFormula
            {
                id = formulaId,
                unlockByDefault = unlockByDefault
            };
            Cost cost = new Cost
            {
                money = money
            };
            Cost.ItemEntry[] array = new Cost.ItemEntry[costItems.Length];
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
            CraftingFormula.ItemEntry result = new CraftingFormula.ItemEntry
            {
                id = resultItemId,
                amount = resultItemAmount
            };
            item.result = result;
            item.requirePerk = requirePerk;
            item.tags = tags ?? new string[1] { "WorkBenchAdvanced" };
            item.hideInIndex = hideInIndex;
            item.lockInDemo = lockInDemo;
            list.Add(item);
            if (!addedFormulaIds.ContainsKey(formulaId))
            {
                addedFormulaIds.Add(formulaId, modid);
            }
            Debug.Log("Added crafting formula: " + formulaId);
        }
        public static void RemoveAllAddedFormulas(string modid = "old_fml_version")
        {
            try
            {
                CraftingFormulaCollection instance = CraftingFormulaCollection.Instance;
                List<CraftingFormula> list = instance.list;
                int num = 0;
                for (int num2 = list.Count - 1; num2 >= 0; num2--)
                {
                    if (addedFormulaIds.ContainsKey(list[num2].id))
                    {
                        Debug.Log("Remove Formula: " + list[num2].id);
                        list.RemoveAt(num2);
                        num++;
                    }
                }
                addedFormulaIds.Clear();
                Debug.Log($"Removed {num} crafting formulas");
            }
            catch (Exception arg)
            {
                Debug.LogError($"Exception at removing crafting formula: {arg}");
            }
        }
    }
}
