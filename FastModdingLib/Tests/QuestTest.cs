using Duckov.Quests;
using System;
using System.Collections.Generic;
using System.Text;

namespace FastModdingLib.Tests
{
    public static class QuestTest
    {
        public static void TestQuest() {
            BlueprintData blueprint = new BlueprintData
            {
                itemId = 40501,
                order = 35,
                localizationKey = "Example Blueprint",
                localizationDesc = "An example blueprint for demonstration purposes.",
                tags = { "Formula", "Formula_Blueprint" },
                formulaID = "example_formula"
            };
            ItemUtils.CreateCustomBluePrint(blueprint);
            QuestData exampleQuestData = new QuestData
            {
                ID = 114515,
                displayName = "Example Quest",
                description = "This is an example quest added by FastModdingLib.",
                questGiver = QuestGiverID.Jeff,
                requireLevel = 1,
                tasks = {
                    new TaskRequireItem
                    {
                        id = 1,
                        itemTypeID = 13,
                        requiredAmount = 1
                    }
                },
                rewards = {
                    new RewardGiveItem
                    {
                        id = 1,
                        itemTypeID = 40501,
                        amount = 1
                    }
                }
            };

            QuestData exampleQuestData2 = new QuestData
            {
                ID = 114516,
                displayName = "Example Quest 2",
                description = "This is an example quest added by FastModdingLib.",
                questGiver = QuestGiverID.Jeff,
                requireLevel = 1,
                tasks = {
                    new TaskRequireItem
                    {
                        id = 1,
                        itemTypeID = 12,
                        requiredAmount = 1
                    }
                },
                rewards = {
                    new RewardMoney
                    {
                        id = 1,
                        amount = 5000
                    },
                    new RewardEXP
                    {
                        id = 2,
                        amount = 200
                    },
                    new RewardUnlockItem { 
                        id = 3,
                        itemTypeID = 1259
                    }
                }
            };
            QuestUtils.RegisterQuest(exampleQuestData);
            QuestUtils.RegisterQuest(exampleQuestData2);
            CraftingUtils.AddCraftingFormula(
                formulaId: "example_formula",
                money: 100,
                costItems: new (int id, long amount)[] { (13, 1) },
                resultItemId: 12,
                resultItemAmount: 1,
                tags: new string[] { "WorkBenchAdvanced" },
                requirePerk: "",
                unlockByDefault: false,
                hideInIndex: false,
                lockInDemo: false
            );

            QuestUtils.AddQuestRelation(114515, before: 21, after: 114516);
            QuestUtils.AddQuestRelation(114516, before: 114515);
        }
    }
}
