using Duckov.Utilities;
using FastModdingLib.Register;
using FastModdingLib.Utils;
using System;
using System.Collections.Generic;

namespace FastModdingLib
{
    public static class ShopUtils
    {
        private static readonly ShopRegistry _shopRegistry = new ShopRegistry();

        /// <summary>
        /// 暴露 <see cref="_shopRegistry"/> 供 <c>RegisterBootstrap</c> 注册到
        /// <see cref="RegistryManager.Registry"/> 元表（internal：仅同程序集 Bootstrap 访问）。
        /// </summary>
        internal static ShopRegistry Registry => _shopRegistry;

        // —— Identifier 构造（domain=owner modid, path=shop_{merchantProfileID}_{typeID}）——
        private static Identifier MakeIdentifier(string owner, string merchantProfileID, int typeID)
        {
            return new Identifier(owner, $"shop_{merchantProfileID}_{typeID}");
        }

        // —— ItemEntry → ShopGoodsData 转换 ——
        private static ShopGoodsData ToShopGoodsData(StockShopDatabase.ItemEntry entry, string merchantProfileID)
        {
            return new ShopGoodsData
            {
                merchantProfileID = merchantProfileID,
                typeID = entry.typeID,
                maxStock = entry.maxStock,
                forceUnlock = entry.forceUnlock,
                priceFactor = entry.priceFactor,
                possibility = entry.possibility
            };
        }

        /// <summary>
        /// 向指定 merchant profile 追加一个商品 entry，并在 <see cref="_shopRegistry"/> 中登记
        /// 便于后续按 modid 批量卸载。
        /// </summary>
        /// <param name="modid">owner mod 身份；null 时走 <see cref="RegistryManager.CurrentModid"/>。</param>
        public static void AddGoods(ShopGoodsData data, string? modid = null)
        {
            string owner = modid ?? RegistryManager.CurrentModid;

            // 若设置了 itemIdentifier，优先解析为 typeID
            int resolvedTypeId = ItemUtils.ResolveItemRef(data.itemIdentifier, data.typeID);

            var entry = new StockShopDatabase.ItemEntry
            {
                typeID = resolvedTypeId,
                possibility = data.possibility,
                forceUnlock = data.forceUnlock,
                maxStock = data.maxStock,
                priceFactor = data.priceFactor,
                lockInDemo = false
            };

            // native 侧仍需直接 mutate（游戏运行时读取该列表）
            GameplayDataSettings.StockshopDatabase.GetMerchantProfile(data.merchantProfileID).entries.Add(entry);

            // registry 侧登记：Identifier 使用 owner modid 作为 domain
            var id = MakeIdentifier(owner, data.merchantProfileID, resolvedTypeId);
            _shopRegistry.Register(resolvedTypeId, id, entry, data.merchantProfileID, owner);
        }

        /// <summary>
        /// 卸载指定 mod 通过 <see cref="AddGoods"/> 注册的全部商品，并完成 native 善后
        /// （从对应 merchant profile 的 entries 列表移除）。返回实际移除条数。
        /// </summary>
        public static int UnregisterAllGoods(string modid)
        {
            return _shopRegistry.RemoveAllByOwner(modid);
        }

        // —— 单条 goods 操作 ——

        /// <summary>
        /// 按 (merchantProfileID, typeID) 精确移除单个由 <see cref="AddGoods"/> 注册的商品。
        /// 通过 registry 的 <see cref="ShopRegistry.FindIdentifier"/> 找到对应 Identifier，
        /// 再调 <see cref="SimpleRegistry{T}.Remove(Identifier)"/> 触发
        /// <see cref="ShopRegistry.OnRemoved"/> 完成 native 侧 entries 列表善后。
        /// </summary>
        /// <returns>找到并移除返回 true；registry 中无此条目返回 false。</returns>
        public static bool RemoveGoods(string merchantProfileID, int typeID)
        {
            if (_shopRegistry.FindIdentifier(merchantProfileID, typeID, out var id))
                return _shopRegistry.Remove(id!);
            return false;
        }

        /// <summary>
        /// 按 Identifier 移除单个由 <see cref="AddGoods"/> 注册的商品。Identifier 版本。
        /// Identifier 需与 <see cref="AddGoods"/> 注册时内部生成的 id 一致
        /// （格式为 <c>shop_{merchantProfileID}_{typeID}</c>）。
        /// 通常建议使用 <see cref="ShopGoodsData.itemIdentifier"/> 进行查询。
        /// </summary>
        /// <returns>找到并移除返回 true；registry 中无此条目返回 false。</returns>
        public static bool RemoveGoods(Identifier id)
        {
            return _shopRegistry.Remove(id);
        }

        /// <summary>
        /// 修改已注册 goods 的可变属性（maxStock / forceUnlock / priceFactor / possibility）。
        /// 通过 registry 找到已登记的 <see cref="StockShopDatabase.ItemEntry"/> 引用，
        /// 直接 mutate 其字段——同一对象引用同时存在于 native entries 列表，故 native 侧同步生效。
        /// typeID / merchantProfileID 不可经此 API 变更；如需变更身份请先 <see cref="RemoveGoods"/> 再 <see cref="AddGoods"/>。
        /// </summary>
        /// <returns>找到并更新返回 true；registry 中无此条目返回 false。</returns>
        public static bool EditGoods(string merchantProfileID, int typeID, ShopGoodsData newData)
        {
            if (!_shopRegistry.FindIdentifier(merchantProfileID, typeID, out var id)
                || id == null)
            {
                return false;
            }
            if (!_shopRegistry.TryGet(id, out var entry))
            {
                return false;
            }
            entry.maxStock = newData.maxStock;
            entry.forceUnlock = newData.forceUnlock;
            entry.priceFactor = newData.priceFactor;
            entry.possibility = newData.possibility;
            return true;
        }

        /// <summary>
        /// 按 Identifier 修改已注册 goods 的可变属性。Identifier 版本。
        /// </summary>
        /// <returns>找到并更新返回 true；registry 中无此条目返回 false。</returns>
        public static bool EditGoods(Identifier id, ShopGoodsData newData)
        {
            if (!_shopRegistry.TryGet(id, out var entry))
                return false;
            entry.maxStock = newData.maxStock;
            entry.forceUnlock = newData.forceUnlock;
            entry.priceFactor = newData.priceFactor;
            entry.possibility = newData.possibility;
            return true;
        }

        // —— Merchant profile 创建 ——

        /// <summary>
        /// 创建新商人 profile 并追加到 <see cref="StockShopDatabase.merchantProfiles"/> 列表。
        /// 返回传入的 <paramref name="name"/>（即新 profile 的 merchantID）。
        /// 创建的 profile 会被 Registry 追踪，mod 卸载时通过
        /// <see cref="ShopRegistry.RemoveProfilesByOwner"/> 自动移除。
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="name"/> 已存在于 merchantProfiles 列表。</exception>
        public static string CreateMerchantProfile(string name)
        {
            if (GameplayDataSettings.StockshopDatabase.GetMerchantProfile(name) != null)
            {
                throw new ArgumentException($"MerchantProfile '{name}' already exists.", nameof(name));
            }
            var profile = new StockShopDatabase.MerchantProfile
            {
                merchantID = name
            };
            GameplayDataSettings.StockshopDatabase.merchantProfiles.Add(profile);
            _shopRegistry.RegisterCreatedProfile(RegistryManager.CurrentModid, name);
            return name;
        }

        /// <summary>
        /// 移除指定 mod 通过 <see cref="CreateMerchantProfile"/> 创建的全部商人 profile。
        /// </summary>
        public static int RemoveAllProfiles(string modid)
        {
            return _shopRegistry.RemoveProfilesByOwner(modid);
        }

        // —— 批量操作 ——

        /// <summary>
        /// 移除指定商人 profile 下所有由 <see cref="AddGoods"/> 注册的商品（仅 FML 登记条目，
        /// 不影响 vanilla 原生条目）。返回实际移除条数。
        /// </summary>
        public static int RemoveAllGoods(string merchantProfileID)
        {
            var profile = GameplayDataSettings.StockshopDatabase.GetMerchantProfile(merchantProfileID);
            if (profile == null)
            {
                return 0;
            }
            // 快照 typeID 列表——OnRemoved 会 mutate profile.entries，不可直接迭代原列表。
            var typeIDs = new List<int>(profile.entries.Count);
            foreach (var entry in profile.entries)
            {
                typeIDs.Add(entry.typeID);
            }
            int removed = 0;
            foreach (var typeID in typeIDs)
            {
                if (RemoveGoods(merchantProfileID, typeID))
                {
                    removed++;
                }
            }
            return removed;
        }

        // —— 查询 API ——

        /// <summary>
        /// 查询指定商人 profile 下 typeID 对应的单个商品。从 native 侧 entries 列表读取，
        /// 覆盖 vanilla 与 FML 注册条目。
        /// </summary>
        public static bool TryGetGoods(string merchantProfileID, int typeID, out ShopGoodsData data)
        {
            var profile = GameplayDataSettings.StockshopDatabase.GetMerchantProfile(merchantProfileID);
            if (profile == null)
            {
                data = null!;
                return false;
            }
            foreach (var entry in profile.entries)
            {
                if (entry.typeID == typeID)
                {
                    data = ToShopGoodsData(entry, merchantProfileID);
                    return true;
                }
            }
            data = null!;
            return false;
        }

        /// <summary>
        /// 获取指定商人 profile 下的全部商品（vanilla + FML 注册）。从 native 侧 entries 列表读取。
        /// profile 不存在时返回空列表。
        /// </summary>
        public static IReadOnlyList<ShopGoodsData> GetAllGoods(string merchantProfileID)
        {
            var profile = GameplayDataSettings.StockshopDatabase.GetMerchantProfile(merchantProfileID);
            if (profile == null)
            {
                return Array.Empty<ShopGoodsData>();
            }
            var result = new List<ShopGoodsData>(profile.entries.Count);
            foreach (var entry in profile.entries)
            {
                result.Add(ToShopGoodsData(entry, merchantProfileID));
            }
            return result;
        }
    }
}