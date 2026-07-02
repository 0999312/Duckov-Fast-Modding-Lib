using Duckov.PerkTrees;
using HarmonyLib;

namespace FastModdingLib.PerkTrees.Patches
{
    /// <summary>
    /// LevelConfig.IsPerkTreeEnabled 前缀补丁。
    /// 自定义 PerkTree（由 FML <see cref="PerkTreeUtils.RegisterPerkTree"/> 注册）的
    /// treeId 不在游戏原生 LevelConfig 的硬编码列表中，原生方法返回 false 导致自定义树不可选。
    /// 此补丁拦截 FML 注册的 treeId，直接返回 true。
    /// </summary>
    [HarmonyPatch(typeof(LevelConfig), "IsPerkTreeEnabled")]
    public static class PerkTreeEnablePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(string treeId, ref bool __result)
        {
            // 检查 treeId 是否对应 FML 注册的 PerkTree
            if (PerkTreeUtils.IsFMLTree(treeId))
            {
                __result = true;
                return false; // 跳过原生方法
            }
            return true; // 非 FML 管理，走原生逻辑
        }
    }
}
