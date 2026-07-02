using HarmonyLib;
using UnityEngine;

namespace FastModdingLib.Crafting.Patches
{
    /// <summary>
    /// Harmony Prefix 拦截 CraftingManager.Craft。
    /// 仅在有 FML 标签成本注册时生效；无注册则完全放行原生逻辑。
    /// </summary>
    [HarmonyPatch(typeof(CraftingManager), "Craft")]
    internal static class CraftingManagerPatch
    {
        /// <returns>true=放行原生逻辑；false=阻止合成</returns>
        static bool Prefix(CraftingFormula formula)
        {
            if (!TagCostRegistry.TryGet(formula.id, out var entry))
                return true;

            if (!TagCostValidator.Validate(entry.Costs))
            {
                Debug.Log($"[FML] Tag cost validation failed for formula '{formula.id}'");
                return false;
            }

            TagCostValidator.Consume(entry.Costs);
            return true;
        }
    }
}
