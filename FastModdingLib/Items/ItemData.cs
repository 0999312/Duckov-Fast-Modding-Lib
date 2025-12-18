using Duckov.Buffs;
using Duckov.ItemUsage;
using Duckov.Quests;
using Duckov.Utilities;
using ItemStatsSystem;
using ItemStatsSystem.Stats;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

namespace FastModdingLib
{
    public class ItemData
    {
        public int itemId;
        public int order = 0;
        public string localizationKey = string.Empty;
        public string localizationDesc = string.Empty;
        public float weight;
        public int value;
        public int maxStackCount = 1;
        public float maxDurability = 0f;
        public int quality;
        public DisplayQuality displayQuality = DisplayQuality.None;
        public string spritePath = string.Empty;
        public List<string> tags = new List<string>();
        public UsageData? usages;
        public List<ModifierData> modifiers = new List<ModifierData>();
    }

    public class ModifierData 
    {
        public ModifierTarget target;
        public string key = string.Empty;
        public ModifierType type;
        public float value = 1F;
        public bool overrideOrder = false;
        public int overrideOrderValue = 0;
        public bool display = true;

        public ModifierDescription getModifier() {
            ModifierDescription modifierDescription = new ModifierDescription(target, key, type, value, overrideOrder, overrideOrderValue);
            modifierDescription.display = display;
            return modifierDescription;
        }
    }

    public class BlueprintData : ItemData
    {
        public new float weight = 0.1F;
        public new int value = 50;
        public new int maxStackCount = 1;
        public new float maxDurability = 0f;
        public new DisplayQuality displayQuality = DisplayQuality.None;
        public new string spritePath = string.Empty;
        public new UsageData? usages = null;
        public string formulaID = string.Empty;
    }

    public class UsageData
    {
        public string actionSound = string.Empty;
        public string useSound = string.Empty;
        public bool useDurability = false;
        public int durabilityUsage = 1;
        public float useTime = 2;

        public List<UsageBehaviorData> behaviors = new List<UsageBehaviorData>();
    }
    public abstract class UsageBehaviorData
    {
        public abstract UsageBehavior GetBehavior(Item item);
    }
    public class FoodData : UsageBehaviorData
    {
        public float energyValue;
        public float waterValue;
        public override UsageBehavior GetBehavior(Item item) {
            FoodDrink foodDrinkBehavior = item.AddComponent<FoodDrink>();
            foodDrinkBehavior.energyValue = this.energyValue;
            foodDrinkBehavior.waterValue = this.waterValue;
            return foodDrinkBehavior;
        }
    }

    public class HealData : UsageBehaviorData
    {
        public int healValue;
        public override UsageBehavior GetBehavior(Item item)
        {
            Drug drugBehavior = item.AddComponent<Drug>();
            drugBehavior.healValue = this.healValue;
            return drugBehavior;
        }
    }

    public class AddBuffData : UsageBehaviorData
    {
        public int buff;
        public float chance = 1f;
        public override UsageBehavior GetBehavior(Item item)
        {
            AddBuff addBuffBehavior = item.AddComponent<AddBuff>();
            addBuffBehavior.buffPrefab = FindBuff(buff);
            addBuffBehavior.chance = this.chance;
            return addBuffBehavior;
        }

        public static Buff FindBuff(int id)
        {
            return GameplayDataSettings.Buffs.allBuffs.Find(buff=>buff.id == id);
        }
    }

    public class RemoveBuffData : UsageBehaviorData
    {
        public int buffID;
        public int removeLayerCount = 2;
        public override UsageBehavior GetBehavior(Item item)
        {
            RemoveBuff buffBehavior = item.AddComponent<RemoveBuff>();
            buffBehavior.buffID = this.buffID;
            buffBehavior.removeLayerCount = this.removeLayerCount;
            return buffBehavior;
        }
    }

    public class ReturnItemData : UsageBehaviorData
    {
        public int itemTypeID;
        public bool display;
        public override UsageBehavior GetBehavior(Item item)
        {
            ReturnItem behavior = item.AddComponent<ReturnItem>();
            behavior.ItemTypeID = this.itemTypeID;
            behavior.showItemName = this.display;
            return behavior;
        }
    }
}
