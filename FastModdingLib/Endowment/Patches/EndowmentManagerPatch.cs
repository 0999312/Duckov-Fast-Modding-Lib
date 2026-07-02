using Duckov.Endowment;
using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace FastModdingLib.Endowment.Patches
{
    /// <summary>
    /// Harmony 补丁：EndowmentManager 生命周期注入。
    /// Awake Postfix 将 FML 注册的自定义天赋注入到 EndowmentManager.entries 列表；
    /// SelectIndex Prefix 确保自定义 EndowmentIndex（≥10）不被拦截。
    /// </summary>
    [HarmonyPatch(typeof(EndowmentManager))]
    public static class EndowmentManagerPatch
    {
        /// <summary>
        /// Awake Postfix：遍历 EndowmentRegistry，为每个注册的天赋创建
        /// EndowmentEntry GameObject，分配 EndowmentIndex（≥10），注入到 entries 列表。
        /// </summary>
        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        public static void Awake_Postfix(EndowmentManager __instance)
        {
            // 通过反射访问 EndowmentUtils 的私有 Registry 属性
            var registryProp = typeof(EndowmentUtils).GetProperty("Registry",
                BindingFlags.Static | BindingFlags.NonPublic);
            var registry = registryProp?.GetValue(null) as EndowmentRegistry;
            if (registry == null) return;

            // 获取 EndowmentManager.entries（private List）
            var entriesField = typeof(EndowmentManager).GetField("entries",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var entries = entriesField?.GetValue(__instance) as IList;
            if (entries == null) return;

            foreach (var kvp in registry)
            {
                var entry = kvp.Value;
                if (entry == null) continue;

                // 分配 EndowmentIndex
                var idx = registry.AllocateIndex(kvp.Key);

                // 通过反射设置 EndowmentEntry.index 字段
                var indexField = typeof(EndowmentEntry).GetField("index",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                indexField?.SetValue(entry, idx);

                // 注入到 entries 列表（不重复添加）
                if (!entries.Contains(entry))
                    entries.Add(entry);
            }
        }

        /// <summary>
        /// SelectIndex Prefix：确保自定义 EndowmentIndex（≥10）走原生逻辑。
        /// 游戏原生实现可能对 index >= _Count 做限制，但 FML 自定义天赋
        /// 已通过 Awake Postfix 注入到 entries 列表，原生 SelectIndex 应能正常处理。
        /// 此 Prefix 仅确保不被早期返回语句拦截。
        /// </summary>
        [HarmonyPatch("SelectIndex")]
        [HarmonyPrefix]
        public static bool SelectIndex_Prefix(EndowmentIndex index)
        {
            // 让原生方法处理（包括自定义 index ≥10）
            // 原生方法会检查 entries 列表和 currentIndex 字段
            return true;
        }
    }
}
