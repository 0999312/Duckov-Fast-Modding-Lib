using HarmonyLib;
using System;
using UnityEngine;

namespace FastModdingLib.Entities.Patches
{
    /// <summary>
    /// Patch #6：<see cref="AICharacterController.Init"/> Postfix。
    /// 在敌人 AI 初始化完毕后，检查该角色是否有已注册的 IStateConfig 状态机。
    /// 如果有，编译状态机并注入为 <c>combatTree</c>（替换原有的 combat BehaviourTree），
    /// 使敌人按 modder 定义的 C# 状态机行为运行。
    /// </summary>
    [HarmonyPatch(typeof(AICharacterController), "Init")]
    public static class AICharacterControllerInitPatch
    {
        [HarmonyPostfix]
        public static void Postfix(AICharacterController __instance)
        {
            try
            {
                if (__instance == null) return;

                // 检查是否已有 FML 注册的状态机（通过角色身上的组件标记）
                var stateConfig = __instance.GetComponent<IStateConfig>();
                if (stateConfig == null) return;

                // 编译状态机为 BehaviourTree
                var bt = StateMachineToBT.Compile(stateConfig);
                if (bt == null)
                {
                    Debug.LogWarning($"[FML] AICharacterController.Init: Compile returned null for {stateConfig.GetType().Name}.");
                    return;
                }

                // 注入到 combatTree 插槽（替换原有 BT）
                // AICharacterController 的 combatTree 字段可能为 public 或非 public
                var combatField = typeof(AICharacterController).GetField("combatTree",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
                if (combatField != null)
                {
                    combatField.SetValue(__instance, bt);
                    Debug.Log($"[FML] Injected compiled BehaviourTree into AICharacterController combatTree.");
                }
                else
                {
                    Debug.LogWarning("[FML] AICharacterController.combatTree field not found via reflection.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[FML AICharacterControllerInitPatch] Error injecting BT: {e}");
            }
        }
    }
}
