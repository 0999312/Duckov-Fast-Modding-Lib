using FastModdingLib.Utils;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

namespace FastModdingLib
{
    public static class AssetUtil
    {
        public static Dictionary<string, AssetBundle> loadedBundles = new Dictionary<string, AssetBundle>();

        /// <summary>
        /// 从调用方 mod 目录加载 AssetBundle。modid 从调用方程序集名自动推导。
        /// 要求调用方已通过 <see cref="ModPathResolver.Register"/> 注册路径。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static AssetBundle? LoadBundle(string bundleName)
        {
            var callingAssembly = Assembly.GetCallingAssembly();
            var id = new Identifier(callingAssembly.GetName().Name, bundleName);
            return LoadBundle(id);
        }

        /// <summary>
        /// 从调用方 mod 目录加载 AssetBundle。mod 目录从 <see cref="ModPathResolver"/> 自动探测。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static AssetBundle? LoadBundle(Identifier identifier)
        {
            var modDir = ModPathResolver.ResolveDirectory(identifier.Domain);
            return LoadBundleFromDir(modDir, identifier.Path);
        }

        /// <summary>
        /// 从指定 mod 目录加载 AssetBundle。
        /// </summary>
        /// <param name="modDirectory">mod 根目录（DLL 所在目录）。</param>
        public static AssetBundle? LoadBundleFromDir(string modDirectory, string bundleName)
        {
            string resourceLoc = $"{modDirectory}:{bundleName}";
            if (loadedBundles.ContainsKey(resourceLoc))
            {
                Debug.Log($"AssetBundle {bundleName} is already loaded from {modDirectory}.");
                return loadedBundles[resourceLoc];
            }
            StringBuilder assetLoc = new StringBuilder($"assets/bundle/");
            assetLoc.Append(bundleName);
            string fileLoc = Path.Combine(modDirectory, assetLoc.ToString());

            var assetBundle = AssetBundle.LoadFromFile(fileLoc);
            if (assetBundle == null)
            {
                Debug.Log($"Failed to load AssetBundle {bundleName} from {fileLoc}!");
                return null;
            }
            Debug.Log($"Loaded AssetBundle {bundleName} from {fileLoc}.");
            loadedBundles.Add(resourceLoc, assetBundle);
            return assetBundle;
        }

        /// <summary>
        /// 从指定 DLL 路径加载 AssetBundle（旧签名，保留兼容）。
        /// </summary>
        public static AssetBundle? LoadBundle(string modPath, string bundleName)
        {
            string modDirectory = Path.GetDirectoryName(modPath);
            return LoadBundleFromDir(modDirectory, bundleName);
        }

        /// <summary>
        /// 卸载并释放指定 AssetBundle 及其加载的全部资源。
        /// </summary>
        /// <param name="modDirectory">mod 根目录（DLL 所在目录）。</param>
        /// <param name="bundleName">AssetBundle 文件名。</param>
        /// <param name="unloadAllLoadedObjects">是否同时销毁 bundle 加载的全部 GameObject/资源。</param>
        public static void UnloadBundle(string modDirectory, string bundleName, bool unloadAllLoadedObjects = true)
        {
            string resourceLoc = $"{modDirectory}:{bundleName}";
            if (loadedBundles.TryGetValue(resourceLoc, out var bundle))
            {
                bundle.Unload(unloadAllLoadedObjects);
                loadedBundles.Remove(resourceLoc);
                Debug.Log($"Unloaded AssetBundle {bundleName}.");
            }
        }

        /// <summary>
        /// 卸载并释放全部已缓存的 AssetBundle。
        /// 通常在 mod 卸载时调用。
        /// </summary>
        public static void UnloadAllBundles(bool unloadAllLoadedObjects = true)
        {
            foreach (var kvp in loadedBundles)
            {
                kvp.Value.Unload(unloadAllLoadedObjects);
            }
            loadedBundles.Clear();
            Debug.Log("Unloaded all cached AssetBundles.");
        }
    }
}
