using Duckov.Buffs;
using Duckov.Utilities;
using FastModdingLib.Register;
using FastModdingLib.Utils;

namespace FastModdingLib
{
    public static class BuffUtils
    {
        private static readonly BuffRegistry _buffRegistry = new BuffRegistry();
        private static bool _initialized;

        internal static BuffRegistry Registry => _buffRegistry;

        /// <summary>
        /// 初始化：将 BuffRegistry 注册到 <see cref="RegistryManager.Registry"/> 元表。
        /// 由 <c>RegisterBootstrap.Init()</c> 调用。
        /// </summary>
        internal static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            var meta = RegistryManager.Instance.Registry;
            var id = new Identifier(FMLConstants.Domain, "buff");
            // 幂等：已有则跳过（Audio/Crafting 等已自注册，用 SetIfAbsent）
            if (meta is NonAlterableSimpleRegistry<ERegistry> nonAlt)
                nonAlt.SetIfAbsent(id, _buffRegistry, RegistryManager.CurrentModid);
            else
                meta.Set(id, _buffRegistry, RegistryManager.CurrentModid);
        }

        /// <summary>
        /// 注册自定义 Buff 预制体到游戏 <c>allBuffs</c> 列表 + FML Registry。
        /// modid 从 <see cref="Identifier.Domain"/> 推导。
        /// </summary>
        public static void RegisterBuff(Identifier id, Buff buffPrefab)
        {
            Init();
            var allBuffs = GameplayDataSettings.Buffs.allBuffs;
            if (allBuffs != null && !allBuffs.Contains(buffPrefab))
            {
                allBuffs.Add(buffPrefab);
            }
            _buffRegistry.Set(id, buffPrefab, id.Domain);
        }

        /// <summary>按 Identifier 移除已注册的 Buff，触发 native 善后。</summary>
        public static bool UnregisterBuff(Identifier id) => _buffRegistry.Remove(id);

        /// <summary>批量卸载指定 mod 注册的全部 Buff。</summary>
        public static int UnregisterAllBuffs(string modid) => _buffRegistry.RemoveAllByOwner(modid);

        /// <summary>
        /// 查找 Buff（自定义 + 游戏内置）。优先查 FML Registry，
        /// 再回退到 <see cref="GameplayDataSettings.Buffs.allBuffs"/>。
        /// </summary>
        public static Buff? FindBuff(int buffID)
        {
            // 先查 FML Registry
            foreach (var kvp in _buffRegistry)
            {
                if (kvp.Value != null && kvp.Value.id == buffID)
                    return kvp.Value;
            }
            // 回退到游戏内置列表
            var allBuffs = GameplayDataSettings.Buffs.allBuffs;
            return allBuffs?.Find(b => b != null && b.id == buffID);
        }
    }
}
