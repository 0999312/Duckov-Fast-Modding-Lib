using Duckov.Utilities;
using FastModdingLib.Register;
using FastModdingLib.Utils;
using System.Collections.Generic;

namespace FastModdingLib
{
    /// <summary>
    /// Shop 模块注册表。维护 <see cref="Identifier"/> → <see cref="StockShopDatabase.ItemEntry"/>
    /// 主映射、反向 typeID → <see cref="Identifier"/> 索引，以及 id → merchantProfileID 上下文字典。
    /// <see cref="SimpleRegistry{T}.OnRemoved"/> 在 registry 删除 entry 时完成 native 善后：
    /// 从对应 merchant profile 的 <c>entries</c> 列表移除该 <see cref="StockShopDatabase.ItemEntry"/>。
    /// </summary>
    /// <remarks>
    /// PLAN §5.4 原契约写作 <c>ShopRegistry : ReverseLookupRegistry&lt;ItemEntry, int&gt;</c>，
    /// 但 R3 落地的 <see cref="ReverseLookupRegistry{T, TKey}"/> 为 sealed（§11 风险对策：杜绝二次继承
    /// 错失反向索引）。为遵守"不碰 Register/ 目录"约束，本类改为直接继承 <see cref="SimpleRegistry{T}"/>
    /// 并自行维护反向索引与 merchantProfileID 上下文字典，语义与原契约等价。
    /// </remarks>
    public sealed class ShopRegistry : SimpleRegistry<StockShopDatabase.ItemEntry>
    {
        private readonly Dictionary<int, Identifier> _byTypeId;
        private readonly Dictionary<Identifier, string> _merchantProfileIds;
        private readonly Dictionary<string, List<string>> _createdProfiles = new Dictionary<string, List<string>>(); // modid → merchantProfileID[]

        public ShopRegistry()
        {
            _byTypeId = new Dictionary<int, Identifier>();
            _merchantProfileIds = new Dictionary<Identifier, string>();
        }

        /// <summary>
        /// 写入主字典、owner 索引、反向 typeID 索引与 merchantProfileID 上下文。
        /// </summary>
        public void Register(int typeID, Identifier id, StockShopDatabase.ItemEntry value, string merchantProfileID, string modid)
        {
            Set(id, value, modid);
            _byTypeId[typeID] = id;
            _merchantProfileIds[id] = merchantProfileID;
        }

        /// <summary>按 native typeID 反查 <see cref="Identifier"/>；不存在返回 false。</summary>
        public bool TryGetIdentifier(int typeID, out Identifier? id)
        {
            return _byTypeId.TryGetValue(typeID, out id);
        }

        /// <summary>
        /// 按 (merchantProfileID, typeID) 查找已注册条目的 <see cref="Identifier"/>。
        /// 避免调用方手动构造 Identifier（后者依赖 domain 做 key，domain 随 modid 变化）。
        /// </summary>
        /// <returns>找到返回 true，id 为匹配的 Identifier；未找到返回 false。</returns>
        public bool FindIdentifier(string merchantProfileID, int typeID, out Identifier? id)
        {
            if (_byTypeId.TryGetValue(typeID, out id)
                && _merchantProfileIds.TryGetValue(id, out var profile)
                && profile == merchantProfileID)
            {
                return true;
            }
            id = null;
            return false;
        }

        /// <summary>
        /// native 善后：从对应 merchant profile 的 <c>entries</c> 列表移除该 entry，
        /// 并清理反向索引与上下文字典。基类 <see cref="SimpleRegistry{T}.Remove(Identifier)"/>
        /// 已在 try/catch 中调用本方法，单条失败不会中断 <see cref="SimpleRegistry{T}.RemoveAllByOwner"/>。
        /// </summary>
        protected override void OnRemoved(Identifier id, StockShopDatabase.ItemEntry value, string? modid)
        {
            if (_merchantProfileIds.TryGetValue(id, out var merchantProfileID))
            {
                var profile = GameplayDataSettings.StockshopDatabase.GetMerchantProfile(merchantProfileID);
                profile?.entries.Remove(value);
                _merchantProfileIds.Remove(id);
            }
            _byTypeId.Remove(value.typeID);
        }

        /// <summary>追踪通过 CreateMerchantProfile 创建的商人 profile，供卸载时清理。</summary>
        public void RegisterCreatedProfile(string modid, string merchantProfileID)
        {
            if (!_createdProfiles.TryGetValue(modid, out var list))
            {
                list = new List<string>();
                _createdProfiles[modid] = list;
            }
            list.Add(merchantProfileID);
        }

        /// <summary>按 mod 批量移除通过 CreateMerchantProfile 创建的商人 profile。</summary>
        public int RemoveProfilesByOwner(string modid)
        {
            if (!_createdProfiles.TryGetValue(modid, out var list)) return 0;
            var profiles = GameplayDataSettings.StockshopDatabase.merchantProfiles;
            int count = 0;
            // 逆序移除以避免索引偏移
            for (int i = profiles.Count - 1; i >= 0; i--)
            {
                if (list.Contains(profiles[i].merchantID))
                {
                    profiles.RemoveAt(i);
                    count++;
                }
            }
            _createdProfiles.Remove(modid);
            return count;
        }
    }
}