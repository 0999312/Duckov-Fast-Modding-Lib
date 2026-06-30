using FastModdingLib.Register;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FastModdingLib.Utils
{
    /// <summary>
    /// Mod 目录路径解析器。替代手工传递 <c>dllPath</c> / <c>modPath</c> 的模式。
    /// 
    /// 解析优先级：
    ///   1. 显式注册路径（通过 <see cref="Register"/> 由 ModBehaviour 或 modder 主动注册）
    ///   2. <see cref="RegistryManager.CurrentModid"/> 查已注册的 modid → path 映射
    /// 
    /// 用法：
    ///   string modDir = ModPathResolver.ResolveDirectory("mymod");    // 按 modid 查
    ///   string dllPath = ModPathResolver.Resolve("mymod");            // DLL 完整路径
    /// </summary>
    public static class ModPathResolver
    {
        private static readonly Dictionary<string, string> _registeredPaths =
            new Dictionary<string, string>();

        private static readonly object _lock = new object();

        /// <summary>
        /// 显式注册 modid → DLL 路径映射。通常在 ModBehaviour.Awake() 或 OnAfterSetup() 中调用。
        /// 幂等：同一 modid 重复注册不覆盖已有值。
        /// </summary>
        public static void Register(string modid, string dllPath)
        {
            if (string.IsNullOrEmpty(modid)) throw new ArgumentNullException(nameof(modid));
            if (string.IsNullOrEmpty(dllPath)) throw new ArgumentNullException(nameof(dllPath));
            lock (_lock)
            {
                if (!_registeredPaths.ContainsKey(modid))
                {
                    _registeredPaths[modid] = dllPath;
                    Debug.Log($"[ModPathResolver] Registered mod '{modid}' path: {dllPath}");
                }
            }
        }

        /// <summary>
        /// 解析指定 modid 的 DLL 完整路径。未注册时返回 null。
        /// </summary>
        public static string? Resolve(string modid)
        {
            if (string.IsNullOrEmpty(modid)) return null;
            lock (_lock)
            {
                return _registeredPaths.TryGetValue(modid, out var path) ? path : null;
            }
        }

        /// <summary>
        /// 解析指定 modid 的 Mod 目录（DLL 所在目录）。
        /// 未注册时返回 null。
        /// </summary>
        public static string? ResolveDirectory(string modid)
        {
            var path = Resolve(modid);
            return path != null ? Path.GetDirectoryName(path) : null;
        }

    }
}
