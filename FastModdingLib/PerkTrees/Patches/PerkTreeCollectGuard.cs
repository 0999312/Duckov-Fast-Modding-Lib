using Duckov.PerkTrees;
using HarmonyLib;

namespace FastModdingLib.PerkTrees.Patches
{
    /// <summary>
    /// PerkTree.Collect 前缀补丁。
    /// 游戏原生 <c>PerkTree.Collect()</c> 会重新扫描子 GameObject 并重建
    /// Perk 列表，这会将运行时通过 FML 注入的 Perk 清空。
    /// 此补丁对 FML 注册的 PerkTree 跳过 Collect 调用。
    /// </summary>
    [HarmonyPatch(typeof(PerkTree), "Collect")]
    public static class PerkTreeCollectGuard
    {
        [HarmonyPrefix]
        public static bool Prefix(PerkTree __instance)
        {
            // FML 注册的 PerkTree 的 GameObject 始终以 "PerkTree_" 前缀命名
            // （由 RegisterPerkTree 保证），以此识别自定义树
            if (__instance != null && __instance.name != null &&
                __instance.name.StartsWith(PerkTreeUtils.FML_TREE_PREFIX))
            {
                return false; // 跳过 Collect，保护运行时注入的 Perk
            }
            return true; // 游戏原生树，正常执行 Collect
        }
    }
}
