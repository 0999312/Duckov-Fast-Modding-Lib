using FastModdingLib.Events;
using FastModdingLib.Events.GameEvents;
using FastModdingLib.Utils;
using Newtonsoft.Json.Linq;
using SodaCraft.Localizations;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

namespace FastModdingLib
{
    public static class I18n
    {
        public static Dictionary<SystemLanguage, string> localizedNames = new Dictionary<SystemLanguage, string>() {
            { SystemLanguage.English, "en_us.json" },
            { SystemLanguage.ChineseSimplified, "zh_cn.json" },
            { SystemLanguage.ChineseTraditional, "zh_tw.json" },
            { SystemLanguage.Japanese, "ja_jp.json" },
            { SystemLanguage.Russian, "ru_ru.json" },
            { SystemLanguage.Korean, "ko_kr.json" },
            { SystemLanguage.Italian, "it_it.json" },
            { SystemLanguage.French, "fr_fr.json" },
            { SystemLanguage.Swedish, "sv_se.json" }
        };
        private static string _modDirectory = string.Empty;

        /// <summary>
        /// 初始化 I18n。mod 目录从 <see cref="ModPathResolver"/> 自动探测。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void InitI18n(string modid = "FastModdingLib")
        {
            if (modid != "FastModdingLib")
            {
                string? modPath = ModPathResolver.ResolveDirectory(modid);
                _modDirectory = modPath != null ? modPath : Assembly.GetExecutingAssembly().Location;
            }
            else
            {
                _modDirectory = Assembly.GetExecutingAssembly().Location;
            }

            EventBusManager.Instance.Sync.Register<LanguageChangedEvent>(OnLanguageChanged);

            // 首次加载：游戏原生 LocalizationManager.OnSetLanguage 仅在玩家主动切换语言时触发，
            // 游戏启动 / mod 首次加载时不会自动触发，因此在此显式读取当前语言文件一次。
            SystemLanguage currentLang = LocalizationManager.CurrentLanguage;
            string initFile = localizedNames.TryGetValue(currentLang, out var f) ? f : localizedNames[SystemLanguage.English];
            LoadLanguageFile($"/{initFile}");
        }

        /// <summary>
        /// 将 <see cref="SystemLanguage"/> 枚举转换为语言代码字符串（如 "zh_cn"、"en_us"）。
        /// 与 <see cref="LanguageChangedEvent.LangCode"/> 的格式约定一致。
        /// </summary>
        public static string GetLangCode(SystemLanguage lang)
        {
            if (localizedNames.TryGetValue(lang, out var fileName))
            {
                return Path.GetFileNameWithoutExtension(fileName);
            }
            return Path.GetFileNameWithoutExtension(localizedNames[SystemLanguage.English]);
        }

        /// <summary>
        /// 根据语言代码（如 "zh_cn"）解析对应的语言文件名，未匹配时回退到英语。
        /// </summary>
        private static string ResolveLanguageFile(string langCode)
        {
            string fileName = $"{langCode}.json";
            return localizedNames.ContainsValue(fileName) ? fileName : localizedNames[SystemLanguage.English];
        }

        private static void OnLanguageChanged(LanguageChangedEvent evt)
        {
            LoadLanguageFile($"/{ResolveLanguageFile(evt.LangCode)}");
        }

        /// <summary>
        /// 从已注册的 mod 目录加载语言 JSON 文件，注入到游戏本地化管理器。
        /// </summary>
        /// <param name="loc">语言文件名（如 "/en_us.json"）。</param>
        public static void LoadLanguageFile(string loc)
        {
            if (string.IsNullOrEmpty(_modDirectory))
            {
                Debug.LogError("[I18n] Mod directory not set. Call InitI18n() first.");
                return;
            }
            StringBuilder assetLoc = new StringBuilder($"assets/lang");
            assetLoc.Append(loc);
            string fileLoc = Path.Combine(_modDirectory, assetLoc.ToString());

            if (File.Exists(fileLoc))
            {
                string jsonContent = File.ReadAllText(fileLoc, Encoding.UTF8);
                JObject jObj = JObject.Parse(jsonContent);
                foreach (var item in jObj)
                {
                    if (item.Value == null) continue;

                    string key = item.Key;
                    string value = item.Value.ToString();
                    LocalizationManager.SetOverrideText(key, value);
                }
            }
            else
            {
                string englishLoc = localizedNames[SystemLanguage.English];
                string englishFile = Path.Combine(_modDirectory, $"assets/lang/{englishLoc}");
                if (!File.Exists(englishFile))
                {
                    Debug.LogError($"[I18n] No language files found at assets/lang/ in {_modDirectory}. Report to modder.");
                    return;
                }
                Debug.LogWarning($"[I18n] Language file {loc} not found, fallback to en_us.json");
                LoadLanguageFile($"/{englishLoc}");
            }
        }
    }
}
