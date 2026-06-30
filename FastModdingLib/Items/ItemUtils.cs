using Duckov.ItemBuilders;
using Duckov.Utilities;
using FastModdingLib.Register;
using FastModdingLib.Utils;
using ItemStatsSystem;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Unity.VisualScripting;
using UnityEngine;

namespace FastModdingLib
{
    public static class ItemUtils
    {
        private static void createUsage(Item item, ItemData config)
        {
            if (config.usages == null)
                return;

            item.AddUsageUtilitiesComponent();
            UsageUtilities usageUtilities = item.UsageUtilities;

            usageUtilities.useTime = config.usages.useTime;

            item.usageUtilities = usageUtilities;

            if (config.usages.useSound != string.Empty)
            {
                usageUtilities.hasSound = true;
                usageUtilities.useSound = config.usages.useSound;
            }
            if (config.usages.actionSound != string.Empty)
            {
                usageUtilities.hasSound = true;
                usageUtilities.actionSound = config.usages.actionSound;
            }
            if (config.usages.useDurability && config.maxDurability > 0)
            {
                usageUtilities.useDurability = true;
                usageUtilities.durabilityUsage = config.usages.durabilityUsage;
            }

            foreach (var behavior in config.usages.behaviors)
            {
                createBehavior(item, behavior, usageUtilities);
            }
        }

        public static void createBehavior(Item item, UsageBehaviorData behaviorData, UsageUtilities usageUtilities)
        {
            if (behaviorData == null)
                return;

            usageUtilities.behaviors.Add(behaviorData.GetBehavior(item));
        }

        // ===== Sprite 加载（通用：物品图标 / Perk 图标 / 建筑图标等） =====

        /// <summary>
        /// 从调用方 mod 目录 <c>assets/textures/</c> 加载 Sprite（适用物品图标、Perk 图标等）。
        /// modid 从调用方程序集名自动推导。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Sprite? LoadSprite(string resourceName, int NEW_ITEM_ID)
        {
            var callingAssembly = System.Reflection.Assembly.GetCallingAssembly();
            var id = new Identifier(callingAssembly.GetName().Name, resourceName);
            return LoadSprite(id, NEW_ITEM_ID);
        }

        /// <summary>
        /// 从调用方 mod 目录 <c>assets/textures/</c> 加载 Sprite（适用物品图标、Perk 图标等）。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Sprite? LoadSprite(Identifier id, int NEW_ITEM_ID)
        {
            var modDir = ModPathResolver.ResolveDirectory(id.Domain);
            return LoadSpriteFromDir(modDir, id.Path, NEW_ITEM_ID);
        }

        /// <summary>从指定目录加载 Sprite。适用物品图标、Perk 图标等所有 Sprite 场景。</summary>
        public static Sprite? LoadSpriteFromDir(string modDirectory, string resourceName, int NEW_ITEM_ID)
        {
            try
            {
                StringBuilder assetLoc = new StringBuilder($"assets/textures/");
                assetLoc.Append(resourceName);
                string fileLoc = Path.Combine(modDirectory, assetLoc.ToString());
                if (File.Exists(fileLoc) == false)
                {
                    Debug.LogError("Sprite is missing: " + fileLoc);
                    return null;
                }

                byte[] ImageArray = File.ReadAllBytes(fileLoc);
                Texture2D texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
                if (!texture2D.LoadImage(ImageArray))
                {
                    Debug.LogError($"Invaild sprite image, Resource:{resourceName}");
                    UnityEngine.Object.Destroy(texture2D);
                    return null;
                }
                texture2D.filterMode = FilterMode.Bilinear;
                texture2D.Apply();
                Sprite sprite = Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f), 100f);
                return sprite;
            }
            catch (Exception arg)
            {
                Debug.LogError($"Except on loading sprite: {arg}");
                return null;
            }
        }

        // ===== 物品构造 =====

        /// <summary>
        /// 创建自定义 Item 实例（不注册到 Registry）。modid 从调用方程序集名自动推导。
        /// 要求调用方已通过 <see cref="ModPathResolver.Register"/> 注册路径。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Item GetCustomItem(ItemData config)
        {
            var callingAssembly = System.Reflection.Assembly.GetCallingAssembly();
            string modid = callingAssembly.GetName().Name;
            return GetCustomItem(new Identifier(modid, config.localizationKey), config);
        }

        /// <summary>
        /// 创建自定义 Item 实例（不注册到 Registry）。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Item GetCustomItem(Identifier id, ItemData config)
        {
            var modDir = ModPathResolver.ResolveDirectory(id.Domain);
            ItemBuilder itemBuilder = ItemBuilder.New()
                .TypeID(config.itemId)
                .EnableStacking(config.maxStackCount, 1)
                .Icon(ItemUtils.LoadSpriteFromDir(modDir, config.spritePath, config.itemId));

            config.modifiers.ForEach(modifier =>
            {
                itemBuilder.Modifier(modifier.getModifier());
            });

            Item component = itemBuilder
                .Instantiate();

            UnityEngine.Object.DontDestroyOnLoad(component);
            SetItemProperties(component, config);

            return component;
        }

        /// <summary>
        /// 创建并注册自定义物品。modid 从 <see cref="Identifier.Domain"/> 推导，
        /// mod 目录从 <see cref="ModPathResolver"/> 自动探测。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void CreateCustomItem(Identifier id, ItemData config)
        {
            var modDir = ModPathResolver.ResolveDirectory(id.Domain);
            ItemBuilder itemBuilder = ItemBuilder.New()
                .TypeID(config.itemId)
                .EnableStacking(config.maxStackCount, 1)
                .Icon(ItemUtils.LoadSpriteFromDir(modDir, config.spritePath, config.itemId));

            config.modifiers.ForEach(modifier =>
            {
                itemBuilder.Modifier(modifier.getModifier());
            });

            Item component = itemBuilder
                .Instantiate();

            UnityEngine.Object.DontDestroyOnLoad(component);
            SetItemProperties(component, config);
            RegisterItem(id, component);
        }

        /// <summary>
        /// 创建并注册自定义蓝图。modid 从 <see cref="Identifier.Domain"/> 推导。
        /// </summary>
        public static void CreateCustomBluePrint(Identifier id, BlueprintData config)
        {
            Item component = ItemBuilder.New()
                .TypeID(config.itemId)
                .Icon(ItemAssetsCollection.GetPrefab(285).icon)
                .Instantiate();
            UnityEngine.Object.DontDestroyOnLoad(component);
            SetItemProperties(component, config);
            ItemSetting_Formula formula = component.AddComponent<ItemSetting_Formula>();
            formula.formulaID = config.formulaID;
            RegisterItem(id, component);
        }

        public static void SetItemProperties(Item item, ItemData config)
        {
            item.weight = config.weight;

            item.Order = config.order;
            item.Value = config.value;
            item.Quality = config.quality;

            item.DisplayNameRaw = config.localizationKey;
            item.MaxDurability = config.maxDurability;
            item.Durability = config.maxDurability;
            ItemUtils.createUsage(item, config);
            item.Tags.Clear();
            foreach (string tagName in config.tags)
            {
                item.Tags.Add(GetTargetTag(tagName));
            }
        }

        public static void SetItemGraphic(Item item, AssetBundle assetBundle, string name)
        {
            GameObject graphic = assetBundle.LoadAsset<GameObject>(name);
            item.itemGraphic = graphic.GetComponent<ItemGraphicInfo>();
        }

        public static Tag GetTargetTag(string tagName)
        {
            return GameplayDataSettings.Tags.Get(tagName);
        }

        private static int lastKnownUsed = -1;

        /// <summary>
        /// 注册自定义物品到游戏系统。owner modid 从 <see cref="Identifier.Domain"/> 推导。
        /// </summary>
        public static void RegisterItem(Identifier id, Item item)
        {
            string owner = id.Domain;
            Debug.Log($"Start Register custom item: {item.TypeID} - {item.DisplayName}");
            if (ItemAssetsCollection.Instance.GetEntry(item.TypeID) != null)
            {
                do
                {
                    lastKnownUsed++;
                } while (ItemAssetsCollection.Instance.GetEntry(lastKnownUsed) != null);

                item.TypeID = lastKnownUsed;
            }
            if (RegistryManager.Instance.ItemID.TryGet(id, out _))
            {
                throw new ArgumentException($"ItemID already registered: {id.Domain}:{id.Path}");
            }
            ItemAssetsCollection.AddDynamicEntry(item);
            RegistryManager.Instance.ItemID.Register(item.TypeID, id, item.TypeID, owner);
            Debug.Log($"Registered custom item: {item.TypeID} - {item.DisplayName}");
        }

        /// <summary>
        /// 从 AssetBundle 注册枪支。modid 从 <see cref="Identifier.Domain"/> 推导。
        /// </summary>
        public static void RegisterGun(Identifier id, AssetBundle assetBundle, string name, int originGunID = 654)
        {
            var gameobject = assetBundle.LoadAsset<GameObject>(name);
            Item prefab = gameobject.GetComponent<Item>();
            Item rifle = ItemAssetsCollection.GetPrefab(originGunID);

            prefab.Tags.Clear();
            prefab.Tags.AddRange(rifle.Tags);

            foreach (var slot in prefab.Slots)
            {
                if (slot.Key.Equals("Muzzle") || slot.Key.Equals("Stock") || slot.Key.Equals("Mag"))
                    if (rifle.Slots[slot.Key] != null)
                    {
                        prefab.Slots[slot.Key].requireTags = rifle.Slots[slot.Key].requireTags;
                        prefab.Slots[slot.Key].excludeTags = rifle.Slots[slot.Key].excludeTags;
                    }
            }

            ItemSetting_Gun rifleSetting = rifle.GetComponent<ItemSetting_Gun>();
            ItemSetting_Gun setting = prefab.GetComponent<ItemSetting_Gun>();
            setting.adsAimMarker = rifleSetting.adsAimMarker;
            setting.muzzleFxPfb = rifleSetting.muzzleFxPfb;
            setting.bulletPfb = rifleSetting.bulletPfb;

            ItemUtils.RegisterItem(id, prefab);
        }

        /// <summary>
        /// 从 AssetBundle 注册物品。modid 从 <see cref="Identifier.Domain"/> 推导。
        /// </summary>
        public static void RegisterItemFromBundle(Identifier id, AssetBundle assetBundle, string name)
        {
            var gameobject = assetBundle.LoadAsset<GameObject>(name);
            Item prefab = gameobject.GetComponent<Item>();
            ItemUtils.RegisterItem(id, prefab);
        }

        public static void UnregisterItem(Item item)
        {
            ItemAssetsCollection.RemoveDynamicEntry(item);
            Debug.Log($"Unregistered custom item: {item.TypeID}");
        }

        /// <summary>
        /// 批量卸载指定 mod 注册的全部自定义物品。
        /// modid 未指定时走 <see cref="RegistryManager.CurrentModid"/>。
        /// </summary>
        public static void UnregisterAllItem(string? modid = null)
        {
            RegistryManager.Instance.ItemID.RemoveAllByOwner(modid ?? RegistryManager.CurrentModid);
        }

        /// <summary>
        /// 按 TypeID 反查自定义 Item（R6 新增读路径）。
        /// </summary>
        public static bool TryGetCustomItem(int typeID, out Item? item)
        {
            item = null;
            if (!RegistryManager.Instance.ItemID.TryGetIdentifier(typeID, out var _))
            {
                return false;
            }
            item = ItemAssetsCollection.GetPrefab(typeID);
            return item != null;
        }

        /// <summary>
        /// 将 <see cref="Identifier"/> 解析为物品的 TypeID。
        /// 供 ShopUtils / QuestUtils / CraftingUtils 等模块在注册时将 item Identifier 转为 int typeID。
        /// 查询 <see cref="RegistryManager.Instance.ItemID"/> 中已注册的自定义物品。
        /// </summary>
        /// <returns>找到对应 typeID 返回 true；未找到返回 false。</returns>
        public static bool TryResolveTypeId(Identifier id, out int typeId)
        {
            return RegistryManager.Instance.ItemID.TryGet(id, out typeId);
        }

        /// <summary>
        /// 解析 item 引用：若 <paramref name="identifier"/> 有值则解析为 typeID；
        /// 否则回退到 <paramref name="fallbackTypeId"/>。
        /// </summary>
        internal static int ResolveItemRef(Identifier? identifier, int fallbackTypeId)
        {
            if (identifier != null && TryResolveTypeId(identifier, out int resolved))
                return resolved;
            return fallbackTypeId;
        }

        /// <summary>
        /// 创建并注册自定义子弹。modid 从 <see cref="Identifier.Domain"/> 推导，
        /// mod 目录从 <see cref="ModPathResolver"/> 自动探测。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void CreateCustomBullet(Identifier id, BulletData config)
        {
            var modDir = ModPathResolver.ResolveDirectory(id.Domain);
            Item component = ItemBuilder.New()
                .TypeID(config.itemId)
                .EnableStacking(config.maxStackCount, 1)
                .Icon(ItemUtils.LoadSpriteFromDir(modDir, config.spritePath, config.itemId))
                .SetConstant("Caliber", config.Caliber, true)
                .SetConstant("SFX_Put", config.SFX_Put, false)
                .SetConstant("CritDamageFactorGain", config.CritDamageFactorGain, config.CritDamageFactorGain != 0F)
                .SetConstant("damageMultiplier", config.damageMultiplier, config.damageMultiplier != 0F)
                .SetConstant("CritRateGain", config.CritRateGain, config.CritRateGain != 0F)
                .SetConstant("ArmorPiercingGain", config.ArmorPiercingGain, config.ArmorPiercingGain != 0F)
                .SetConstant("ArmorBreakGain", config.ArmorBreakGain, config.ArmorBreakGain != 0F)
                .SetConstant("DurabilityCost", config.DurabilityCost, config.DurabilityCost != 0F)
                .SetConstant("ExplosionRange", config.ExplosionRange, config.ExplosionRange != 0F)
                .SetConstant("ExplosionDamage", config.ExplosionDamage, config.ExplosionDamage != 0F)
                .SetConstant("buffChanceMultiplier", config.buffChanceMultiplier, true)
                .SetConstant("bleedChance", config.bleedChance, true)
                .Instantiate();
            UnityEngine.Object.DontDestroyOnLoad(component);
            ItemUtils.SetItemProperties(component, config);
            ItemSetting_Bullet setting = component.AddComponent<ItemSetting_Bullet>();
            ItemUtils.RegisterItem(id, component);
        }
    }
}
