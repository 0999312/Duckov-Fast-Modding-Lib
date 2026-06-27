using Duckov;
using FMOD.Studio;
using HarmonyLib;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using FastModdingLib.Utils;
using FMODUnity;
using UnityEngine;

namespace FastModdingLib.Audio
{
    [HarmonyPatch(typeof(AudioObject), nameof(AudioObject.Post))]
    public class AudioObjectMixin
    {
        [HarmonyPrefix]
        public static bool Prefix(AudioObject __instance, ref EventInstance? __result, string eventName, bool doRelease)
        {
            Debug.Log($"AudioObjectMixin: Post called with eventName: {eventName}");
            Identifier? id = AudioUtil.Instance.GetIdentifier(eventName);
            if (id is null) return true;
            AudioData data = AudioUtil.Instance.dataRegistry[id];
            string filePath = data.Path;
            if (!File.Exists(filePath))
                Debug.Log("[Audio] File don't exist: " + filePath);
            if (!AudioManager.TryCreateEventInstance("SFX/custom", out var eventInstance))
            {
                __result = null;
                return false;
            }
            __instance.events.Add(eventInstance);
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