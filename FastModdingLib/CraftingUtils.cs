using Duckov.Economy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace FastModdingLib
{
    public static class CraftingUtils 
    {
        public static List<string> addedFormulaIds = new List<string>();

        public static List<int> addedFormulaResults = new List<int>();

        public static List<int> addedDecomposeItemIds = new List<int>();
        public static void AddDecomposeFormula(int itemId, long money, (int id, long amount)[] resultItems)
        {
            DecomposeDatabase instance = DecomposeDatabase.Instance;
            FieldInfo field = typeof(DecomposeDatabase).GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic);
            DecomposeFormula[] collection = (DecomposeFormula[])field.GetValue(instance);
            List<DecomposeFormula> list = new List<DecomposeFormula>(collection);
            foreach (DecomposeFormula item2 in list)
            {
                if (item2.item == itemId && item2.result.items.Any())
                {
                    Debug.LogWarning($"Existed decompose formula, item: {itemId}");
                    foreach(var itemResult in item2.result.items){
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
            field.SetValue(instance, list.ToArray());
            if (!addedDecomposeItemIds.Contains(itemId))
            {
                addedDecomposeItemIds.Add(itemId);
            }
            Debug.Log($"Added decompose: {itemId}");
            typeof(DecomposeDatabase).GetMethod("RebuildDictionary", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(instance, null);
        }

        public static void RemoveAllAddedDecomposeFormulas()
        {
            try
            {
                DecomposeDatabase instance = DecomposeDatabase.Instance;
                FieldInfo field = typeof(DecomposeDatabase).GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic);
                DecomposeFormula[] collection = (DecomposeFormula[])field.GetValue(instance);
                List<DecomposeFormula> list = new List<DecomposeFormula>(collection);
                int num = 0;
                for (int num2 = list.Count - 1; num2 >= 0; num2--)
                {
                    if (addedDecomposeItemIds.Contains(list[num2].item))
                    {
                        Debug.Log($"Remove decompose formula: {list[num2].item}");
                        list.RemoveAt(num2);
                        num++;
                    }
                }
                field.SetValue(instance, list.ToArray());
                addedDecomposeItemIds.Clear();
                typeof(DecomposeDatabase).GetMethod("RebuildDictionary", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(instance, null);
                Debug.Log($"Removed {num} decompose formulas");
            }
            catch (Exception arg)
            {
                Debug.LogError($"Exception at removing decompose formula: {arg}");
            }
        }


        public static void AddCraftingFormula(string formulaId, long money, (int id, long amount)[] costItems, int resultItemId, int resultItemAmount, string[] tags = null, string requirePerk = "", bool unlockByDefault = true, bool hideInIndex = false, bool lockInDemo = false)
        {
            CraftingFormulaCollection instance = CraftingFormulaCollection.Instance;
            FieldInfo field = typeof(CraftingFormulaCollection).GetField("list", BindingFlags.Instance | BindingFlags.NonPublic);
            List<CraftingFormula> list = (List<CraftingFormula>)field.GetValue(instance);
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
            if (!addedFormulaIds.Contains(formulaId))
            {
                addedFormulaIds.Add(formulaId);
                addedFormulaResults.Add(resultItemId);
            }
            Debug.Log("Added crafting formula: " + formulaId);
            FieldInfo field2 = typeof(CraftingFormulaCollection).GetField("_entries_ReadOnly", BindingFlags.Instance | BindingFlags.NonPublic);
            field2.SetValue(instance, null);
        }

        public static void RemoveAllAddedFormulas()
        {
            try
            {
                CraftingFormulaCollection instance = CraftingFormulaCollection.Instance;
                FieldInfo field = typeof(CraftingFormulaCollection).GetField("list", BindingFlags.Instance | BindingFlags.NonPublic);
                List<CraftingFormula> list = (List<CraftingFormula>)field.GetValue(instance);
                int num = 0;
                for (int num2 = list.Count - 1; num2 >= 0; num2--)
                {
                    if (addedFormulaIds.Contains(list[num2].id))
                    {
                        Debug.Log("Remove Formula: " + list[num2].id);
                        list.RemoveAt(num2);
                        num++;
                    }
                }
                addedFormulaIds.Clear();
                FieldInfo field2 = typeof(CraftingFormulaCollection).GetField("_entries_ReadOnly", BindingFlags.Instance | BindingFlags.NonPublic);
                field2.SetValue(instance, null);
                Debug.Log($"Removed {num} crafting formulas");
            }
            catch (Exception arg)
            {
                Debug.LogError($"Exception at removing crafting formula: {arg}");
            }
        }
    }
}
