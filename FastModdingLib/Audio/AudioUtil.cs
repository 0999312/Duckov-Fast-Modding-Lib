using Duckov;
using FastModdingLib.Register;
using FastModdingLib.Utils;
using UnityEngine;

namespace FastModdingLib.Audio
{
    public class AudioUtil : Singleton<AudioUtil>
    {
        public ReverseLookupRegistry<AudioData, string> dataRegistry;

        private AudioUtil()
        {
            dataRegistry = new ReverseLookupRegistry<AudioData, string>(data => data.Eventname);
            RegistryManager.Instance.Registry.Set(new Identifier(FMLConstants.Domain, "audio"), dataRegistry);
        }

        public void RegisterAudio(Identifier id, AudioData data)
        {
            dataRegistry.Register(data.Eventname, id, data, RegistryManager.CurrentModid);
        }

        // ─── BGM 控制 ───

        /// <summary>播放内置 BGM（FMOD event: "Music/Loop/{name}"）。</summary>
        public static void PlayBGM(string name) => AudioManager.PlayBGM(name);

        /// <summary>播放自定义 BGM 文件。</summary>
        public static void PlayCustomBGM(string filePath, bool loop = true)
            => AudioManager.PlayCustomBGM(filePath, loop);

        /// <summary>停止当前 BGM。</summary>
        public static void StopBGM() => AudioManager.StopBGM();

        /// <summary>切换 BGM：停止当前，播放新的。</summary>
        public static void SwitchBGM(string name)
        {
            StopBGM();
            PlayBGM(name);
        }

        /// <summary>BGM 是否正在播放。</summary>
        public static bool IsBGMPlaying() => AudioManager.PlayingBGM;

        // ─── 音量总线控制 ───

        /// <summary>获取指定总线的音量（0.0~1.0）。名称："Master" / "Master/Music" / "Master/SFX"。</summary>
        public static float GetBusVolume(string busName)
        {
            var bus = AudioManager.GetBus(busName);
            return bus?.Volume ?? 1f;
        }

        /// <summary>设置指定总线的音量。</summary>
        public static void SetBusVolume(string busName, float volume)
        {
            var bus = AudioManager.GetBus(busName);
            if (bus != null) bus.Volume = Mathf.Clamp01(volume);
        }

        /// <summary>获取指定总线的静音状态。</summary>
        public static bool IsBusMuted(string busName)
        {
            var bus = AudioManager.GetBus(busName);
            return bus?.Mute ?? false;
        }

        /// <summary>设置指定总线的静音状态。</summary>
        public static void SetBusMute(string busName, bool mute)
        {
            var bus = AudioManager.GetBus(busName);
            if (bus != null) bus.Mute = mute;
        }

        // ─── 便捷方法 ───

        public static float GetMasterVolume() => GetBusVolume("Master");
        public static void SetMasterVolume(float volume) => SetBusVolume("Master", volume);
        public static float GetMusicVolume() => GetBusVolume("Master/Music");
        public static void SetMusicVolume(float volume) => SetBusVolume("Master/Music", volume);
        public static float GetSFXVolume() => GetBusVolume("Master/SFX");
        public static void SetSFXVolume(float volume) => SetBusVolume("Master/SFX", volume);

        public static bool IsMasterMuted() => IsBusMuted("Master");
        public static void SetMasterMute(bool mute) => SetBusMute("Master", mute);
        public static bool IsMusicMuted() => IsBusMuted("Master/Music");
        public static void SetMusicMute(bool mute) => SetBusMute("Master/Music", mute);
        public static bool IsSFXMuted() => IsBusMuted("Master/SFX");
        public static void SetSFXMute(bool mute) => SetBusMute("Master/SFX", mute);
    }
}