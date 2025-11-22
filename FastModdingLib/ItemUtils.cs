using Duckov.Buffs;
using Duckov.ItemBuilders;
using Duckov.ItemUsage;
using Duckov.Modding;
using Duckov.Utilities;
using ItemStatsSystem;
using ItemStatsSystem.Items;
using SodaCraft.Localizations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEngine.Rendering.DebugUI;

namespace FastModdingLib
{
    public static class ItemUtils {

        private static void createUsage(Item item, ItemData config) {
            if (config.usages == null)
                return;

            item.AddUsageUtilitiesComponent();
            UsageUtilities usageUtilities = item.UsageUtilities;

            FieldInfo useTimeField = typeof(UsageUtilities).GetField("useTime", BindingFlags.Instance | BindingFlags.NonPublic);
            if (useTimeField != null)
                useTimeField.SetValueOptimized(usageUtilities, config.usages.useTime);
            
            SetPrivateField(item, "usageUtilities", usageUtilities);

            if (config.usages.useSound != string.Empty) {
                usageUtilities.hasSound = true;
                usageUtilities.useSound = config.usages.useSound;
            }
            if (config.usages.actionSound != string.Empty) {
                usageUtilities.hasSound = true;
                usageUtilities.actionSound = config.usages.actionSound;
            }
            if (config.usages.useDurability && config.maxDurability > 0) {
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
                return ;

            switch (behaviorData.Type)
            {
                case "FoodDrink":
                    {
                        FoodData? foodData = behaviorData as FoodData;
                        if (foodData != null)
                        {
                            FoodDrink foodDrinkBehavior = item.AddComponent<FoodDrink>();
                            foodDrinkBehavior.energyValue = foodData.energyValue;
                            foodDrinkBehavior.waterValue = foodData.waterValue;
                            usageUtilities.behaviors.Add(foodDrinkBehavior);
                            return;
                        }
                        break;
                    }
                case "Drug":
                    {
                        HealData? healData = behaviorData as HealData;
                        if (healData != null)
                        {
                            Drug drugBehavior = item.AddComponent<Drug>();
                            drugBehavior.healValue = healData.healValue;
                            usageUtilities.behaviors.Add(drugBehavior);
                            return;
                        }
                        break;
                    }
                case "AddBuff":
                    {
                        AddBuffData? addBuffData = behaviorData as AddBuffData;
                        Buff? buff = addBuffData != null ? AddBuffData.FindBuff(addBuffData.buff) : null;
                        if (addBuffData != null && buff != null)
                        {
                            AddBuff addBuffBehavior = item.AddComponent<AddBuff>();
                            addBuffBehavior.buffPrefab = buff;
                            addBuffBehavior.chance = addBuffData.chance;
                            usageUtilities.behaviors.Add(addBuffBehavior);
                            return;
                        }
                        break;
                    }
                case "RemoveBuff":
                    {
                        RemoveBuffData? removeBuffData = behaviorData as RemoveBuffData;
                        if (removeBuffData != null)
                        {
                            RemoveBuff buffBehavior = item.AddComponent<RemoveBuff>();
                            buffBehavior.buffID = removeBuffData.buffID;
                            buffBehavior.removeLayerCount = removeBuffData.removeLayerCount;
                            usageUtilities.behaviors.Add(buffBehavior);
                            return;
                        }
                        break;
                    }
                default:
                    break;
            }
            Debug.LogError($"unexpected usage type: {behaviorData.Type}");
        }

        public static Sprite? LoadEmbeddedSprite(string modPath, string resourceName, int NEW_ITEM_ID)
        {
            try
            {
                string modDirectory = Path.GetDirectoryName(modPath);
                StringBuilder assetLoc = new StringBuilder($"assets/textures/");
                assetLoc.Append(resourceName);
                string fileLoc = Path.Combine(modDirectory, assetLoc.ToString());
                if(File.Exists(fileLoc) == false)
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

        public static void CreateCustomItem(string modPath, ItemData config)
        {
            Item component = ItemBuilder.New()
                .TypeID(config.itemId)
                .EnableStacking(config.maxStackCount, 1)
                .Icon(ItemUtils.LoadEmbeddedSprite(modPath, config.spritePath, config.itemId))
                .Instantiate();
            UnityEngine.Object.DontDestroyOnLoad (component);
            SetItemProperties(component, config);
            RegisterItem(component);
        }

        public static void SetItemProperties(Item item, ItemData config)
        {
            SetPrivateField(item, "weight", config.weight);
            
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

        public static Tag GetTargetTag(string tagName)
        {
            Tag[] source = Resources.FindObjectsOfTypeAll<Tag>();
            return source.FirstOrDefault((Tag t) => t.name == tagName);
        }
        public static bool SetPrivateField(Item item, string fieldName, object value)
        {
            FieldInfo field = typeof(Item).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValueOptimized(item, value);
                return true;
            }
            Debug.LogWarning($"Couldn't find field: {fieldName}");
            return false;
        }
        public static void RegisterItem(Item item)
        {
            Debug.Log($"Start Register custom item: {item.TypeID} - {item.DisplayName}");
            ItemAssetsCollection.AddDynamicEntry(item);
            Debug.Log($"Registered custom item: {item.TypeID} - {item.DisplayName}");
        }

        public static void RegisterGun(AssetBundle assetBundle, string name) {
            //AUGA3
            RegisterGun(assetBundle, name, 654);
        }

        public static void RegisterGun(AssetBundle assetBundle, string name, int originGunID)
        {
            var gameobject = assetBundle.LoadAsset<GameObject>(name);
            Item prefab = gameobject.GetComponent<Item>();
            Item rifle = ItemAssetsCollection.GetPrefab(originGunID);
            ItemSetting_Gun rifleSetting = rifle.GetComponent<ItemSetting_Gun>();
            ItemSetting_Gun setting = prefab.GetComponent<ItemSetting_Gun>();
            setting.muzzleFxPfb = rifleSetting.muzzleFxPfb;
            setting.bulletPfb = rifleSetting.bulletPfb;

            ItemUtils.RegisterItem(prefab);
        }

        public static void RegisterItemFromBundle(AssetBundle assetBundle, string name)
        {
            var gameobject = assetBundle.LoadAsset<GameObject>(name);
            Item prefab = gameobject.GetComponent<Item>();
            ItemUtils.RegisterItem(prefab);
        }

        public static void UnregisterItem(Item item)
        {
            ItemAssetsCollection.RemoveDynamicEntry(item);
            Debug.Log($"Unregistered custom item: {item.TypeID}");
        }
    }
}  
