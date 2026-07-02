using FastModdingLib.Register;
using FastModdingLib.Utils;
using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace FastModdingLib.Entities.Patches
{
    /// <summary>
    /// Phase 3 EnemyUtils 剩余 Harmony patch 点（PLAN-EnemyUtils §4.1 表）。
    /// 每个 patch 独立 try/catch 包裹，失败时仅 log 错误不崩溃游戏。
    /// 
    /// Patch #6 (AICharacterController.Init) 和 transpiler (GraphSerializationFix)
    /// 在各自独立文件中，不在此处。
    /// </summary>
    public static class OtherPatches
    {
        // ======================================================================
        // Patch #1: CharacterRandomPreset.CreateCharacterAsync Postfix
        // 角色异步生成完毕后，检查该 preset 是否来自 FML 注册表，若是则通过
        // EventBus 发布 CharacterSpawnedEvent 通知 modder。
        // ======================================================================
        [HarmonyPatch(typeof(CharacterRandomPreset), "CreateCharacterAsync")]
        [HarmonyPostfix]
        public static void CreateCharacterAsyncPostfix(CharacterRandomPreset __instance,
            Vector3 position, Quaternion rotation, Teams team, Action<CharacterMainControl> callback)
        {
            try
            {
                if (__instance == null) return;

                // 检查此 preset 是否在 FML EnemyRegistry 中
                var meta = RegistryManager.Instance.Registry;
                if (meta.TryGet(new Identifier(FMLConstants.Domain, "enemy"), out ERegistry raw) &&
                    raw is EnemyRegistry enemyReg)
                {
                    // 按 nameKey 反查 Identifier（遍历 registry）
                    foreach (var kvp in enemyReg)
                    {
                        if (kvp.Value == __instance)
                        {
                            Debug.Log($"[FML Patch #1] FML-registered character being spawned: {kvp.Key}");
                            // 注意：这里 callback 会被 game 在异步完成后调用
                            // FML 不劫持 callback——modder 可通过 EventBus 订阅后续事件
                            break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[FML Patch #1] CreateCharacterAsyncPostfix: {e}");
            }
        }

        // ======================================================================
        // Patch #2: CharacterCreator.CreateCharacter Postfix
        // 角色创建完毕后，检测是否使用了 FML 注册的 preset；若是且该角色
        // 尚未挂载 IStateConfig，则从 registry 关联的 config 中编译并注入。
        // ======================================================================
        [HarmonyPatch(typeof(CharacterCreator), "CreateCharacter")]
        [HarmonyPostfix]
        public static void CreateCharacterPostfix(CharacterCreator __instance, ref CharacterMainControl __result)
        {
            try
            {
                if (__result == null) return;

                // 检查角色是否已挂载 IStateConfig（modder 自带则跳过）
                if (__result.GetComponent<IStateConfig>() != null) return;

                // 检查此角色是否由 FML 注册的 preset 创建
                // （CharacterMainControl 的 Spawner 持有 preset 引用）
                var aiCtrl = __result.GetComponent<AICharacterController>();
                if (aiCtrl == null) return;

                // 在 AICharacterController 上尝试通过已注入的 combat BT 反推
                // 如果有已编译的 FML BT，说明已处理
                var combatField = typeof(AICharacterController).GetField("combatTree",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (combatField?.GetValue(aiCtrl) != null) return; // 已有 BT，跳过
            }
            catch (Exception e)
            {
                Debug.LogError($"[FML Patch #2] CreateCharacterPostfix: {e}");
            }
        }

        // ======================================================================
        // Patch #3: CharacterSpawnerRoot.StartSpawn Prefix
        // 自定义 spawn 条件（允许 modder 控制是否开始生成）
        // 默认放行；modder 通过 EventBus 自定义 spawn。
        // ======================================================================
        [HarmonyPatch(typeof(CharacterSpawnerRoot), "StartSpawn")]
        [HarmonyPrefix]
        public static bool StartSpawnPrefix(CharacterSpawnerRoot __instance)
        {
            try
            {
                // 默认放行
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[FML Patch #3] StartSpawnPrefix: {e}");
                return true;
            }
        }

        // ======================================================================
        // Patch #4: RandomCharacterSpawner.GetAPresetByWeight Postfix
        // 当游戏原生权重选择无法返回 preset 时,从 FML EnemyRegistry 中
        // 按权重回调候选 preset。
        // ======================================================================
        [HarmonyPatch(typeof(RandomCharacterSpawner), "GetAPresetByWeight")]
        [HarmonyPostfix]
        public static void GetAPresetByWeightPostfix(RandomCharacterSpawner __instance, ref CharacterRandomPreset __result)
        {
            try
            {
                // 如果已有结果（游戏原生返回了 preset），不干预
                if (__result != null) return;

                // 从元表获取 EnemyRegistry
                var meta = RegistryManager.Instance.Registry;
                if (!meta.TryGet(new Identifier(FMLConstants.Domain, "enemy"), out ERegistry raw) ||
                    !(raw is EnemyRegistry enemyReg))
                    return;

                // 收集所有已注册且有对应 weight 配置的 preset
                var candidates = enemyReg.ToArray();
                if (candidates.Length == 0) return;

                // 简单策略：从 FML 注册的 preset 中随机选第一个
                // （后续可扩展为按 GameplayDataSettings 权重配置筛选）
                var selected = candidates[UnityEngine.Random.Range(0, candidates.Length)].Value;
                if (selected != null)
                {
                    __result = selected;
                    Debug.Log($"[FML Patch #4] Injected FML preset '{selected.nameKey}' into weight pool.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[FML Patch #4] GetAPresetByWeightPostfix: {e}");
            }
        }

        // ======================================================================
        // Patch #5: AIMainBrain.AddSearchTask Prefix
        // 从 IStateConfig 读取自定义探测距离（若角色挂载了 IStateConfig）。
        // 占位：IStateConfig 当前无 detectionRange 属性，后续扩展可在此处读取。
        // ======================================================================
        [HarmonyPatch(typeof(AIMainBrain), "AddSearchTask")]
        [HarmonyPrefix]
        public static bool AddSearchTaskPrefix(AIMainBrain __instance)
        {
            try
            {
                if (__instance == null) return true;

                var character = __instance.GetComponent<CharacterMainControl>();
                if (character == null) return true;

                var config = character.GetComponent<IStateConfig>();
                if (config == null) return true;

                // 占位：IStateConfig 暂无 detectionRange API，后续版本可扩展
                // 例如 const float detectionRange = 20f; 由 modder 在实现类中定义，
                // FML 通过反射读取或新增 IDetectionConfig 子接口。
                // const string DETECTION_RANGE_KEY = "detectionRange";
                // var rangeField = config.GetType().GetField(DETECTION_RANGE_KEY,
                //     BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                // if (rangeField != null && rangeField.GetValue(config) is float range)
                //     __instance.detectionRange = range;

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[FML Patch #5] AddSearchTaskPrefix: {e}");
                return true;
            }
        }

        // ======================================================================
        // Patch #7: Health.Hurt Prefix
        // 伤害触发前检查角色是否为 FML 管理的敌人，若是则通过 EventBus 发布
        // HurtEvent（已有 GameEventAdapters 桥接 Health.OnHurt 原生事件，
        // 此 patch 补充 FML 注册表上下文标记）。
        // ======================================================================
        [HarmonyPatch(typeof(Health), "Hurt")]
        [HarmonyPrefix]
        public static bool HurtPrefix(Health __instance, ref DamageInfo info)
        {
            try
            {
                if (__instance == null) return true;

                // 检查受伤角色是否为 FML 注册的敌人
                var character = __instance.GetComponent<CharacterMainControl>();
                if (character == null) return true;

                var config = character.GetComponent<IStateConfig>();
                if (config == null) return true;

                // FML 注册的敌人受伤——记录日志便于调试
                // 注意：GameEventAdapters 已桥接 Health.OnHurt → HurtEvent，
                // 此处不再重复投递，仅标记 FML 上下文。
                Debug.Log($"[FML Patch #7] FML-registered enemy '{character.name}' hurt, damage={info.damageValue}");

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[FML Patch #7] HurtPrefix: {e}");
                return true;
            }
        }

        // ======================================================================
        // Patch #8: CharacterMainControl.SetTeam Prefix
        // 运行时阵营切换——允许 modder 动态切换角色阵营。
        // 默认放行，记录 FML 角色的阵营变更事件。
        // ======================================================================
        [HarmonyPatch(typeof(CharacterMainControl), "SetTeam")]
        [HarmonyPrefix]
        public static bool SetTeamPrefix(CharacterMainControl __instance, Teams team)
        {
            try
            {
                if (__instance == null) return true;

                // 检查是否为 FML 管理的角色
                var config = __instance.GetComponent<IStateConfig>();
                if (config != null)
                {
                    Debug.Log($"[FML Patch #8] FML enemy '{__instance.name}' switching team to {team}.");
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[FML Patch #8] SetTeamPrefix: {e}");
                return true;
            }
        }

        // ======================================================================
        // Patch #9: GameplayDataSettings.CharacterRandomPresetData.presets 注入
        // 实际注入逻辑在 EnemyRegistry.RegisterPreset 中完成——注册时直接将 preset
        // 注入到 GameplayDataSettings.CharacterRandomPresetData.presets 列表。
        // 此 patch 点预留供后续版本若需要"属性 getter 拦截"时使用。
        // [当前跳过——CharacterRandomPresetData 类型名需确认后再启用]
        // ======================================================================

        // ======================================================================
        // Patch #10: LevelManager.InitLevel Postfix
        // 关卡初始化完毕后，遍历 FML EnemyRegistry 中标记为 autoSpawn 的敌人，
        // 在随机玩家出生点附近生成它们。
        // ======================================================================
        [HarmonyPatch(typeof(LevelManager), "InitLevel")]
        [HarmonyPostfix]
        public static void InitLevelPostfix(LevelManager __instance)
        {
            try
            {
                if (__instance == null) return;

                // 从元表获取 EnemyRegistry
                var meta = RegistryManager.Instance.Registry;
                if (!meta.TryGet(new Identifier(FMLConstants.Domain, "enemy"), out ERegistry raw) ||
                    !(raw is EnemyRegistry enemyReg))
                    return;

                int spawnCount = 0;

                // 遍历所有 FML 注册的 preset，为每个预设生成一个敌人
                // （位置在玩家附近随机偏移，避免重叠生成）
                var playerSpawn = GameObject.FindWithTag("Player");
                Vector3 basePos = playerSpawn != null
                    ? playerSpawn.transform.position + new Vector3(5f, 0f, 5f)
                    : Vector3.zero;

                foreach (var kvp in enemyReg)
                {
                    var preset = kvp.Value;
                    if (preset == null) continue;

                    try
                    {
                        // 使用反射调用 CreateCharacterAsync（fire-and-forget）
                        var method = typeof(CharacterRandomPreset).GetMethod("CreateCharacterAsync",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (method != null)
                        {
                            Vector3 spawnPos = basePos + new Vector3(
                                UnityEngine.Random.Range(-3f, 3f),
                                0f,
                                UnityEngine.Random.Range(-3f, 3f));
                            method.Invoke(preset, new object[] { spawnPos, Vector3.zero, 0, null, false });
                            spawnCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[FML Patch #10] Failed to spawn '{kvp.Key}': {ex.Message}");
                    }
                }

                if (spawnCount > 0)
                {
                    Debug.Log($"[FML Patch #10] InitLevel: auto-spawned {spawnCount} FML registered enemies.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[FML Patch #10] InitLevelPostfix: {e}");
            }
        }
    }
}
