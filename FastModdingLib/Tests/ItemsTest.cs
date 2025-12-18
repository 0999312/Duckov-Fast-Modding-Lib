using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace FastModdingLib.Tests
{
    public static class ItemsTest
    {
        public static ItemData drink01 = new ItemData
        {
            itemId = 150001,
            order = 11,
            localizationKey = "Coffee",
            localizationDesc = "Coffee_Desc",
            weight = 0.6f,
            value = 400,
            maxDurability = 1,
            quality = 3,
            displayQuality = ItemStatsSystem.DisplayQuality.White,
            tags = { "Food", "Drink" },
            spritePath = "items/drink_01.png",
            usages = new UsageData
            {
                actionSound = "SFX/Item/use_drink",
                useSound = string.Empty,
                useTime = 2.5f,
                behaviors = new List<UsageBehaviorData>
                {
                    new FoodData
                    {
                        energyValue = 20f,
                        waterValue = 50f
                    },
                    new ReturnItemData
                    {
                        itemTypeID = 131
                    }
                }
            }
        };

        public static void TestItem()
        {
            ItemUtils.CreateCustomItem(Assembly.GetExecutingAssembly().Location, drink01);
        }
    }
}
