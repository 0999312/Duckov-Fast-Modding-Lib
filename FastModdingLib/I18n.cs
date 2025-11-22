using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
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
        public static void InitI18n(string modPath)
        {
            SodaCraft.Localizations.LocalizationManager.OnSetLanguage += (lang) =>
            {
                if (I18n.localizedNames.ContainsKey(lang))
                {
                    I18n.loadFileJson(modPath, $"/{I18n.localizedNames[lang]}");
                }
                else
                {
                    I18n.loadFileJson(modPath, $"/{I18n.localizedNames[SystemLanguage.English]}");
                }
            };
        }

        public static void loadFileJson(string modPath, string loc) {
            string modDirectory = Path.GetDirectoryName(modPath);
            StringBuilder assetLoc = new StringBuilder($"assets/lang");
            assetLoc.Append(loc);
            string fileLoc = Path.Combine(modDirectory, assetLoc.ToString());

            if (File.Exists(fileLoc))
            {
                string jsonContent = File.ReadAllText(fileLoc, Encoding.UTF8);
                JObject jObj = JObject.Parse(jsonContent);
                foreach (var item in jObj)
                {
                    if (item.Value == null) continue;

                    string key = item.Key;
                    string value = item.Value.ToString();
                    SodaCraft.Localizations.LocalizationManager.SetOverrideText(key, value);
                }
            }
            else {
                if (File.Exists(localizedNames[SystemLanguage.English]) == false)
                {
                    Debug.LogError($"[I18n] Location {assetLoc.ToString()} doesn't have any language files, report it to modder");
                    return;
                }
                Debug.LogWarning($"[I18n] Language file {loc} not found, fallback to en_us.json");
                I18n.loadFileJson(modPath, $"/{I18n.localizedNames[SystemLanguage.English]}");
            }
            
        }
    }
}
