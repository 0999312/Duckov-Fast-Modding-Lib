using Duckov.ItemBuilders;
using Duckov.Utilities;
using ItemStatsSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FastModdingLib.Register;
using FastModdingLib.Utils;
using Unity.VisualScripting;
using UnityEngine;

namespace FastModdingLib
{
    public static class ItemUtils
    {
        public static Dictionary<int, string> addedItemIds = new Dictionary<int, string>();
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

            //item.AgentUtilities.CreateAgent();
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

        public static Sprite? LoadEmbeddedSprite(string modPath, string resourceName, int NEW_ITEM_ID)
        {
            try
            {
                string modDirectory = Path.GetDirectoryName(modPath);
                StringBuilder assetLoc = new StringBuilder($"assets/textures/");
                assetLoc.Append(resourceName);
                string fileLoc = Path.Combine(modDirectory, assetLoc.ToString());
                if (File.Exists(fileLoc) == false)
                {
                    Debug.LogError("EmbeddedSprite is missing: " + fileLoc);
                    return null;
                }

                byte[] ImageArray = File.ReadAllBytes(fileLoc);
                Texture2D texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
                if (!texture2D.LoadImage(ImageArray))
                {
                    Debug.LogError($"Invaild sprite image, Resource:{resourceName}");
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

        public static Item GetCustomItem(string modPath, ItemData config, string modid = "old_fml_version")
        {
            ItemBuilder itemBuilder = ItemBuilder.New()
                .TypeID(config.itemId)
                .EnableStacking(config.maxStackCount, 1)
                .Icon(ItemUtils.LoadEmbeddedSprite(modPath, config.spritePath, config.itemId));

            config.modifiers.ForEach(modifier => {
                itemBuilder.Modifier(modifier.getModifier());
            });

            Item component = itemBuilder
                .Instantiate();

            UnityEngine.Object.DontDestroyOnLoad(component);
            SetItemProperties(component, config);

            return component;
        }

        public static void CreateCustomItem(Identifier id, string modPath, ItemData config, string modid = "old_fml_version")
        {
            ItemBuilder itemBuilder = ItemBuilder.New()
                .TypeID(config.itemId)
                .EnableStacking(config.maxStackCount, 1)
                .Icon(ItemUtils.LoadEmbeddedSprite(modPath, config.spritePath, config.itemId));

            config.modifiers.ForEach(modifier => {
                itemBuilder.Modifier(modifier.getModifier());
            });

            Item component = itemBuilder
                .Instantiate();

            UnityEngine.Object.DontDestroyOnLoad(component);
            SetItemProperties(component, config);
            RegisterItem(id, component, modid);
        }

        public static void CreateCustomBluePrint(Identifier id, BlueprintData config, string modid = "old_fml_version")
        {
            Item component = ItemBuilder.New()
                .TypeID(config.itemId)
                .Icon(ItemAssetsCollection.GetPrefab(285).icon)
                .Instantiate();
            UnityEngine.Object.DontDestroyOnLoad(component);
            SetItemProperties(component, config);
            ItemSetting_Formula formula = component.AddComponent<ItemSetting_Formula>();
            formula.formulaID = config.formulaID;
            RegisterItem(id, component, modid);
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
        
        public static void RegisterItem(Identifier id, Item item, string modid = "old_fml_version")
        {
            Debug.Log($"Start Register custom item: {item.TypeID} - {item.DisplayName}");
            if (ItemAssetsCollection.Instance.GetEntry(item.TypeID) != null)
            {
                do
                {
                    lastKnownUsed++;
                } while (ItemAssetsCollection.Instance.GetEntry(lastKnownUsed) != null);

                item.TypeID = lastKnownUsed;
            }
            ItemAssetsCollection.AddDynamicEntry(item);
            ItemUtils.addedItemIds.Add(item.TypeID, modid);
            RegistryManager.Instance.ItemID.Set(id, item.TypeID);
            Debug.Log($"Registered custom item: {item.TypeID} - {item.DisplayName}");
        }

        public static void RegisterGun(Identifier id, AssetBundle assetBundle, string name, int originGunID = 654, string modid = "old_fml_version")
        {
            var gameobject = assetBundle.LoadAsset<GameObject>(name);
            Item prefab = gameobject.GetComponent<Item>();
            //prefab.Slots["1"];
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

            ItemUtils.RegisterItem(id, prefab, modid);
        }

        public static void RegisterItemFromBundle(Identifier id, AssetBundle assetBundle, string name, string modid = "old_fml_version")
        {
            var gameobject = assetBundle.LoadAsset<GameObject>(name);
            Item prefab = gameobject.GetComponent<Item>();
            ItemUtils.RegisterItem(id, prefab, modid);
        }

        public static void UnregisterItem(Item item)
        {
            ItemAssetsCollection.RemoveDynamicEntry(item);
            Debug.Log($"Unregistered custom item: {item.TypeID}");
        }

        public static void UnregisterAllItem(string modid = "old_fml_version")
        {
            foreach (var itemId in ItemUtils.addedItemIds.ToList())
            {
                Item item = ItemAssetsCollection.GetPrefab(itemId.Key);
                if (item != null)
                {
                    ItemUtils.UnregisterItem(item);
                }
                ItemUtils.addedItemIds.Remove(itemId.Key);
            }
        }

        public static void CreateCustomBullet(Identifier id, BulletData config, string modPath, string modid = "TopTierWeaponExpansion")
        {
            Item component = ItemBuilder.New()
                .TypeID(config.itemId)
                .EnableStacking(config.maxStackCount, 1)
                .Icon(ItemUtils.LoadEmbeddedSprite(modPath, config.spritePath, config.itemId))
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
            ItemUtils.RegisterItem(id, component, modid);
        }
    }
}
