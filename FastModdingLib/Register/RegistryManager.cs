using FastModdingLib.Utils;
using System;
using UnityEngine;

namespace FastModdingLib.Register
{
    public class RegistryManager : Singleton<RegistryManager>
    {
        // —— CurrentModid 上下文（thread-static 防子线程误读） ——
        [ThreadStatic]
        private static string? s_currentModid;

        /// <summary>
        /// 当前线程的 mod 身份。无显式 <see cref="EnterModScope"/> 时兜底为 "FastModdingLib"。
        /// 写入 API 无 modid 重载时取此值作为 owner。
        /// </summary>
        public static string CurrentModid => s_currentModid ?? "FastModdingLib";

        /// <summary>
        /// 进入指定 mod 的注册作用域。返回 <see cref="IDisposable"/>，Dispose 时还原进入前的 modid。
        /// 仅在主线程使用；异步链上的注册应由调用方显式传 modid。
        /// </summary>
        public static IDisposable EnterModScope(string modid)
        {
            return new ModScope(modid);
        }

        internal static void SetCurrentModid(string? value)
        {
            s_currentModid = value;
        }

        public readonly NonAlterableSimpleRegistry<ERegistry> Registry = new NonAlterableSimpleRegistry<ERegistry>();

        // ItemID: Identifier → ItemTypeID（int），反向 TypeID → Identifier。
        // 升级为 ReverseLookupRegistry<int,int>（R6）以支持 ItemUtils.TryGetCustomItem 反查；
        // nativeKeySelector 为 identity（T=int, TKey=int）。
        public readonly ReverseLookupRegistry<int, int> ItemID = new ReverseLookupRegistry<int, int>(typeID => typeID);

        protected RegistryManager()
        {
            Registry.Set(new Identifier("fastmoddinglib", "itemid"), ItemID);
        }

        /// <summary>
        /// 遍历元表 <see cref="Registry"/> 中所有子注册表，调用各自的
        /// <see cref="ERegistry.RemoveAllByOwner"/> 完成一次性批量卸载。
        /// 单个注册表失败不中断后续。
        /// </summary>
        public void RemoveAllByOwner(string modid)
        {
            foreach (var entry in Registry)
            {
                if (entry.Value is ERegistry reg)
                {
                    try
                    {
                        reg.RemoveAllByOwner(modid);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[RegistryManager.RemoveAllByOwner] modid={modid} registry={entry.Key.Domain}:{entry.Key.Path} threw: {e}");
                    }
                }
            }
        }
    }
}