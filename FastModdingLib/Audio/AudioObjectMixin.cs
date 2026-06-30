using Duckov;
using FastModdingLib.Utils;
using FMOD.Studio;
using FMODUnity;
using HarmonyLib;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace FastModdingLib.Audio
{
    [HarmonyPatch(typeof(AudioObject), nameof(AudioObject.Post))]
    public class AudioObjectMixin
    {
        [HarmonyPrefix]
        public static bool Prefix(AudioObject __instance, ref EventInstance? __result, string eventName, bool doRelease)
        {
            if (!AudioUtil.Instance.dataRegistry.TryGetIdentifier(eventName, out var id) || id is null) return true;
            AudioData data = AudioUtil.Instance.dataRegistry[id];
            string filePath = data.Path;
            // 通过 id.Domain 解析 Mod 目录，将 data.Path 中的相对路径转换为完整文件路径
            if (!string.IsNullOrEmpty(filePath) && !Path.IsPathRooted(filePath))
            {
                var modDir = ModPathResolver.ResolveDirectory(id.Domain);
                if (modDir != null)
                {
                    filePath = Path.Combine(modDir, filePath);
                }
                else
                {
                    Debug.LogWarning($"[Audio] Failed to resolve mod directory for domain '{id.Domain}'. " +
                                     $"Ensure the mod has registered its path via ModPathResolver.Register. Using path as-is: {filePath}");
                }
            }
            if (!File.Exists(filePath))
                Debug.Log("[Audio] File don't exist: " + filePath);
            if (!AudioManager.TryCreateEventInstance("SFX/custom", out var eventInstance))
            {
                __result = null;
                return false;
            }
            __instance.events.Add(eventInstance);
            // GCHandle.Alloc 分配的句柄需在 EVENT_CALLBACK_TYPE.STOPPED 等回调中
            // 通过 GCHandle.FromIntPtr(userData).Free() 释放。当前 AudioObject.CustomSFXCallback
            // 由游戏侧提供，不确定是否释放句柄。若未释放则存在累积内存泄漏。
            // 建议方案：提供 FML 自有回调包装器，先调原始回调再 Free GCHandle。
            // Resolved: AudioObject.CustomSFXCallback does exist free logic.
            GCHandle gcHandle = GCHandle.Alloc(filePath);
            eventInstance.setProperty(EVENT_PROPERTY.MINIMUM_DISTANCE, data.MinDistance);
            eventInstance.setProperty(EVENT_PROPERTY.MAXIMUM_DISTANCE, data.MaxDistance);
            eventInstance.set3DAttributes(__instance.gameObject.transform.position.To3DAttributes());
            __instance.ApplyParameters(eventInstance);
            eventInstance.setUserData(GCHandle.ToIntPtr(gcHandle));
            eventInstance.setCallback(AudioObject.CustomSFXCallback);
            eventInstance.start();
            if (doRelease)
            {
                eventInstance.release();
            }

            __result = eventInstance;
            return false;
        }
    }
}