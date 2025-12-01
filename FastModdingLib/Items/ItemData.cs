using Duckov.Buffs;
using ItemStatsSystem;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
        public abstract string Type { get; }
    }
    public class FoodData : UsageBehaviorData
    {
        public float energyValue;
        public float waterValue;
        public override string Type { get; } = "FoodDrink";
    }

    public class HealData : UsageBehaviorData
    {
        public int healValue;
        public override string Type { get; } = "Drug";
    }

    public class AddBuffData : UsageBehaviorData
    {
        public int buff;
        public float chance = 1f;
        public override string Type { get; } = "AddBuff";

        public static Buff FindBuff(int id)
        {
            Buff[] allBuffs = Resources.FindObjectsOfTypeAll<Buff>();
            Buff buffPrefab = allBuffs.FirstOrDefault(b => b != null && b.ID == id);
            return buffPrefab;
        }
    }

    public class RemoveBuffData : UsageBehaviorData
    {
        public int buffID;
        public int removeLayerCount = 2;
        public override string Type { get; } = "RemoveBuff";
    }
}
