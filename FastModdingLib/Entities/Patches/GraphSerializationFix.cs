using HarmonyLib;
using NodeCanvas.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace FastModdingLib.Entities.Patches
{
    /// <summary>
    /// NodeCanvas Graph 序列化修复 transpiler（PLAN-EnemyUtils §3.3）。
    /// 
    /// 目标：移除 <c>Graph.OnEnable</c>（或 <c>Deserialize</c>）中的 playmode 序列化 guard：
    /// <code>
    /// if (Threader.applicationIsPlaying || Application.isPlaying)
    ///     return;  // 静默跳过序列化 → 运行时建图 NRE
    /// </code>
    /// 
    /// 使运行时通过 <c>ScriptableObject.CreateInstance&lt;BehaviourTree&gt;()</c> 建图后，
    /// Graph 能正常反序列化其节点结构。
    /// 
    /// 容错：如果目标方法 IL 序列不匹配（NodeCanvas 版本差异），patch 静默失败并 log 警告，
    /// 确保不影响游戏正常运行（PLAN §3.3 风险对策）。
    /// </summary>
    [HarmonyPatch]
    public static class GraphSerializationFix
    {
        /// <summary>
        /// 动态确定目标方法：优先 <c>Graph.OnEnable</c>，回退 <c>Graph.Deserialize</c>。
        /// </summary>
        public static MethodBase TargetMethod()
        {
            var type = typeof(Graph);
            var method = type.GetMethod("OnEnable",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method != null) return method;

            method = type.GetMethod("Deserialize",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method != null) return method;

            Debug.LogWarning("[FML GraphSerializationFix] No target method found (OnEnable/Deserialize). Patch skipped.");
            return null;
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            // 移除序列化 guard：查找包含 "get_applicationIsPlaying" 或 "get_isPlaying" 的 call 指令，
            // 以及紧随其后的条件跳转和 ret 指令。
            //
            // 匹配模式（模糊匹配，不依赖精确 IL 序列）：
            //   call get_applicationIsPlaying
            //   call get_isPlaying
            //   brfalse.s/brtrue.s/brfalse/brtrue  <-- 条件跳转
            //   ret                                 <-- guard 的 return

            var codeList = new List<CodeInstruction>(instructions);
            var filtered = new List<CodeInstruction>();
            int skipUntil = -1;

            for (int i = 0; i < codeList.Count; i++)
            {
                // 如果当前在 skip 窗口内，跳过
                if (i < skipUntil) continue;

                var instr = codeList[i];

                // 检测 call get_applicationIsPlaying 或 get_isPlaying
                if (instr.opcode == OpCodes.Call && instr.operand is MethodInfo method)
                {
                    string methodName = method.Name;
                    if (methodName.Contains("get_applicationIsPlaying") ||
                        methodName.Contains("get_isPlaying"))
                    {
                        // 找到 guard 起点：跳过此 call 及后续 3-4 条指令
                        // (条件跳转 + ret，或另一个 call + 条件跳转 + ret)
                        skipUntil = i + 1;
                        for (int j = i + 1; j < Math.Min(i + 5, codeList.Count); j++)
                        {
                            var next = codeList[j];
                            if (next.opcode == OpCodes.Ret ||
                                next.opcode == OpCodes.Brfalse_S ||
                                next.opcode == OpCodes.Brtrue_S ||
                                next.opcode == OpCodes.Brfalse ||
                                next.opcode == OpCodes.Brtrue)
                            {
                                skipUntil = j + 1;
                            }
                            else if (next.opcode == OpCodes.Call && next.operand is MethodInfo nextMethod &&
                                     (nextMethod.Name.Contains("get_applicationIsPlaying") ||
                                      nextMethod.Name.Contains("get_isPlaying")))
                            {
                                // 第二个 call（两个条件用 || 连接）——继续往下找跳转
                                continue;
                            }
                            else
                            {
                                // 非 guard 序列中的指令——停止扩展 skip 窗口
                                break;
                            }
                        }
                        Debug.Log(
                            $"[FML GraphSerializationFix] Removed playmode serialization guard at IL offset ~{i}..{skipUntil - 1}.");
                        continue;
                    }
                }

                filtered.Add(instr);
            }

            // 如果没有检测到 guard，log 警告但不崩溃
            if (filtered.Count == codeList.Count)
            {
                Debug.LogWarning(
                    "[FML GraphSerializationFix] No playmode serialization guard pattern detected. " +
                    "The NodeCanvas version may have changed. Patch was safely skipped.");
            }

            return filtered;
        }
    }
}
