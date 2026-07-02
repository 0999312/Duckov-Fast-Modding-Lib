using Duckov.Buildings;
using Duckov.Buildings.UI;
using Duckov.Utilities;
using HarmonyLib;
using System.Linq;

namespace FastModdingLib.Buildings.Patches
{
    /// <summary>
    /// Harmony Patch 集合，确保自定义建筑在游戏原生 UI 和数据查询中可见。
    /// 当原生 <see cref="BuildingDataCollection"/> 找不到对应的 Info/Prefab 时，
    /// 回退到 FML 的 <see cref="BuildingRegistry"/> 查找。
    /// </summary>
    [HarmonyPatch]
    public static class BuildingCollectionPatch
    {
        [HarmonyPatch(typeof(BuildingDataCollection), "GetInfo")]
        [HarmonyPostfix]
        static void GetInfo_Postfix(string id, ref BuildingInfo __result)
        {
            // 原生已找到有效结果，不干预（BuildingInfo 是值类型，通过 Valid 属性判断）
            if (__result.Valid)
                return;

            // 从 BuildingRegistry 回退查找
            var registry = BuildingUtils.Registry;
            foreach (var kvp in registry)
            {
                if (kvp.Value.id == id)
                {
                    __result = kvp.Value;
                    return;
                }
            }
        }

        [HarmonyPatch(typeof(BuildingDataCollection), "GetPrefab")]
        [HarmonyPostfix]
        static void GetPrefab_Postfix(string prefabName, ref Building __result)
        {
            // 原生已找到，不干预
            if (__result != null)
                return;

            // 从 BuildingRegistry 的 prefab 字典查找
            var registry = BuildingUtils.Registry;
            if (registry.TryGetPrefab(prefabName, out var prefab))
                __result = prefab;
        }

        [HarmonyPatch(typeof(BuildingSelectionPanel), "GetBuildingsToDisplay")]
        [HarmonyPostfix]
        static void GetBuildingsToDisplay_Postfix(ref BuildingInfo[] __result)
        {
            // 将 BuildingRegistry 中注册的全部 BuildingInfo 追加到显示列表
            var registry = BuildingUtils.Registry;
            var customInfos = registry.GetAllInfos().ToArray();
            if (customInfos.Length == 0)
                return;

            __result = __result.Concat(customInfos).ToArray();
        }
    }
}
