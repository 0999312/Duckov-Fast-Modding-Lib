using Duckov.Modding;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FastModdingLib.Modding
{
    /// <summary>
    /// fml.json 加载器与缓存。在 ModManager.Rescan 后调用 <see cref="LoadAll"/> 批量加载，
    /// 之后通过 <see cref="Get"/> / <see cref="TryGet"/> 按 mod name 查询。
    /// </summary>
    public static class ModMetaCache
    {
        private static readonly Dictionary<string, ModMeta> _cache = new Dictionary<string, ModMeta>();

        /// <summary>按 mod name 获取元数据。未缓存时返回 <see cref="ModMeta.Default"/>。</summary>
        public static ModMeta Get(string modName)
        {
            return _cache.TryGetValue(modName, out var meta) ? meta : ModMeta.Default;
        }

        /// <summary>按 mod name 安全获取元数据。</summary>
        public static bool TryGet(string modName, out ModMeta meta)
        {
            return _cache.TryGetValue(modName, out meta);
        }

        /// <summary>清空全部缓存。</summary>
        public static void Clear()
        {
            _cache.Clear();
        }

        /// <summary>
        /// 遍历 <paramref name="modInfos"/>，为每个 mod 加载其根目录下的 fml.json。
        /// 幂等：已缓存的不重复加载。
        /// </summary>
        public static void LoadAll(List<ModInfo> modInfos)
        {
            foreach (var info in modInfos)
            {
                if (_cache.ContainsKey(info.name)) continue;
                _cache[info.name] = LoadFromPath(info.path, info.name);
            }
        }

        /// <summary>
        /// 从指定 mod 目录加载并解析 fml.json。modid 必须与 <paramref name="expectedName"/>
        /// （ModInfo.name）一致，否则视为无效。
        /// </summary>
        private static ModMeta LoadFromPath(string modPath, string expectedName)
        {
            string jsonPath = Path.Combine(modPath, "fml.json");
            if (!File.Exists(jsonPath))
            {
                return ModMeta.Default;
            }

            try
            {
                string json = File.ReadAllText(jsonPath);
                JObject obj = JObject.Parse(json);

                // modid 为必填字段
                if (!obj.TryGetValue("modid", out var modIdToken) || string.IsNullOrWhiteSpace(modIdToken.ToString()))
                {
                    Debug.LogWarning($"[FML ModMetaCache] {jsonPath}: missing or empty 'modid' field. fml.json ignored.");
                    return ModMeta.Default;
                }

                string modId = modIdToken.Value<string>();
                // 校验 modid 必须与 ModInfo.name 一致
                if (modId != expectedName)
                {
                    Debug.LogWarning($"[FML ModMetaCache] {jsonPath}: modid '{modId}' does not match info.ini name '{expectedName}'. fml.json ignored.");
                    return ModMeta.Default;
                }

                var deps = System.Array.Empty<ModDependency>();
                if (obj.TryGetValue("dependencies", out var d))
                {
                    var list = new System.Collections.Generic.List<ModDependency>();
                    if (d is Newtonsoft.Json.Linq.JArray arr)
                    {
                        foreach (var token in arr)
                        {
                            if (token.Type == Newtonsoft.Json.Linq.JTokenType.String)
                            {
                                var name = token.Value<string>() ?? "";
                                if (!string.IsNullOrEmpty(name))
                                    list.Add(new ModDependency { Name = name });
                            }
                            else if (token.Type == Newtonsoft.Json.Linq.JTokenType.Object)
                            {
                                var name = token["name"]?.Value<string>() ?? "";
                                var wid = token["workshopId"]?.Value<string>() ?? "";
                                if (!string.IsNullOrEmpty(name) || !string.IsNullOrEmpty(wid))
                                    list.Add(new ModDependency { Name = name, WorkshopId = wid });
                            }
                        }
                    }
                    deps = list.ToArray();
                }

                return new ModMeta
                {
                    ModId = modId,
                    Priority = obj.TryGetValue("priority", out var p) ? p.Value<int>() : int.MaxValue,
                    Dependencies = deps,
                    LoadAfter = obj.TryGetValue("loadAfter", out var la)
                        ? la.ToObject<string[]>() ?? System.Array.Empty<string>()
                        : System.Array.Empty<string>(),
                    AutoActivate = obj.TryGetValue("autoActivate", out var a) && a.Value<bool>(),
                    Loaded = true
                };
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[FML ModMetaCache] Failed to parse {jsonPath}: {e.Message}");
                return ModMeta.Default;
            }
        }
    }
}
