using Duckov.Modding;
using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace FastModdingLib.Modding
{
    /// <summary>
    /// Harmony 补丁集合。Hook <c>ModManager</c> 的排序与激活逻辑，
    /// 注入 fml.json 声明的优先级、依赖排序与自激活策略。
    /// </summary>
    public static class ModManagerPatches
    {
        private static bool _sortPatchApplied;

        /// <summary>
        /// 确保补丁已应用。在 FML 自身 OnAfterSetup 中调用一次即可。
        /// 幂等：重复调用不重复 patch。
        /// </summary>
        public static void EnsurePatched()
        {
            if (_sortPatchApplied) return;
            _sortPatchApplied = true;

            var harmony = new Harmony("FastModdingLib.ModOrdering");
            harmony.Patch(
                original: typeof(ModManager).GetMethod("SortModInfosByPriority",
                    BindingFlags.NonPublic | BindingFlags.Static),
                prefix: new HarmonyMethod(typeof(ModManagerPatches), nameof(SortModInfosByPriority_Prefix)));
            harmony.Patch(
                original: typeof(ModManager).GetMethod("ShouldActivateMod",
                    BindingFlags.NonPublic | BindingFlags.Instance),
                postfix: new HarmonyMethod(typeof(ModManagerPatches), nameof(ShouldActivateMod_Postfix)));
            harmony.Patch(
                original: typeof(ModManager).GetMethod(nameof(ModManager.Reorder),
                    BindingFlags.Public | BindingFlags.Static),
                postfix: new HarmonyMethod(typeof(ModManagerPatches), nameof(Reorder_Postfix)));
        }

        /// <summary>
        /// 前缀替换 <c>SortModInfosByPriority</c>：加载 fml.json 元数据，
        /// 按声明式 priority + 拓扑依赖排序，跳过原方法。
        /// </summary>
        public static bool SortModInfosByPriority_Prefix()
        {
            // 每次 Rescan 后 modInfos 已重置，需重新加载所有 fml.json
            ModMetaCache.Clear();
            ModMetaCache.LoadAll(ModManager.modInfos);
            ModDependencyResolver.Sort(ModManager.modInfos);
            return false; // skip original
        }

        /// <summary>
        /// 后置增强 <c>ShouldActivateMod</c>：
        /// 若游戏侧未要求激活（玩家未手动开启），但 fml.json 声明 autoActivate=true
        /// 且所有依赖均已激活/存在，则改为返回 true。
        /// </summary>
        public static void ShouldActivateMod_Postfix(ModInfo info, ref bool __result)
        {
            if (__result) return; // 已激活，不干预

            if (!ModMetaCache.TryGet(info.name, out var meta) || !meta.Loaded || !meta.AutoActivate)
                return;

            // 检查依赖是否全部就绪
            if (meta.Dependencies != null)
            {
                foreach (var dep in meta.Dependencies)
                {
                    if (dep.IsEmpty) continue;
                    ModInfo? depInfo = null;
                    if (ModManager.modInfos != null)
                    {
                        foreach (var m in ModManager.modInfos)
                            if (dep.Matches(m)) { depInfo = m; break; }
                    }
                    if (depInfo is ModInfo d && ModManager.IsModActive(d, out var _))
                        continue;
                    // 依赖不存在或未激活，不自动激活（Name 优先 + WorkshopId 兜底均未命中即视为未装载）
                    Debug.Log($"[FML] Auto-activate '{info.name}' skipped: dependency '{dep}' not active.");
                    return;
                }
            }

            Debug.Log($"[FML] Auto-activating mod: {info.name} (priority={meta.Priority})");
            __result = true;
        }

        /// <summary>
        /// 后置修正 <c>Reorder</c>：玩家在 UI 中拖拽排序后，
        /// <c>RegeneratePriorities</c> 会用索引值覆盖 fml.json 声明的 priority。
        /// 此 Postfix 重新按 fml.json priority + 拓扑依赖排序，确保声明式优先级始终生效。
        /// </summary>
        public static void Reorder_Postfix()
        {
            ModMetaCache.Clear();
            ModMetaCache.LoadAll(ModManager.modInfos);
            ModDependencyResolver.Sort(ModManager.modInfos);
            var onReorder = typeof(ModManager)
                .GetField("OnReorder", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null) as System.Action;
            onReorder?.Invoke();
        }
    }
}
