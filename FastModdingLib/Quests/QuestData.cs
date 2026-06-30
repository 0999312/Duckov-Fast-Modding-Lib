using Duckov.Quests;
using Duckov.Quests.Rewards;
using Duckov.Quests.Tasks;
using FastModdingLib.Quests;
using FastModdingLib.Utils;
using System.Collections.Generic;

namespace FastModdingLib
{
    public class QuestData
    {
        public string displayName = string.Empty;
        public string description = string.Empty;
        public int ID;

        /// <summary>
        /// 可选：Quest 的 Identifier（domain = modid, path = 任务标识）。
        /// 设置后 Registry 使用此 Identifier 而非硬编码 "fastmoddinglib:quest_{ID}"。
        /// </summary>
        public Identifier? Id;

        public QuestGiverID questGiver;
        public int requireLevel;
        public int requireItemID = -1;
        public string requireScene = string.Empty;
        public List<TaskData> tasks = new List<TaskData>();
        public List<RewardData> rewards = new List<RewardData>();
    }

    public abstract class TaskData
    {
        public abstract Task SetTask(Quest quest);
        public int id;
    }
    public class TaskRequireItem : TaskData
    {
        public int itemTypeID;

        /// <summary>
        /// 可选：物品 Identifier。设置后优先解析为 itemTypeID，
        /// 解析失败时回退到 <see cref="itemTypeID"/>。
        /// </summary>
        public Identifier? itemIdentifier;

        public int requiredAmount;
        public override Task SetTask(Quest quest)
        {
            SubmitItems submit = quest.gameObject.AddComponent<SubmitItems>();
            submit.id = id;
            submit.itemTypeID = itemTypeID;
            submit.requiredAmount = requiredAmount;
            submit.master = quest;
            return submit;
        }
    }
    public class TaskRequireMoney : TaskData
    {
        public int money;

        public override Task SetTask(Quest quest)
        {
            QuestTask_SubmitMoney task = quest.gameObject.AddComponent<QuestTask_SubmitMoney>();
            task.id = id;
            task.money = money;
            task.master = quest;
            return task;
        }
    }

    public class TaskRequireUseItem : TaskData
    {
        public int itemTypeID;

        /// <summary>
        /// 可选：物品 Identifier。设置后优先解析为 itemTypeID。
        /// </summary>
        public Identifier? itemIdentifier;

        public int amount;
        public override Task SetTask(Quest quest)
        {
            QuestTask_UseItem task = quest.gameObject.AddComponent<QuestTask_UseItem>();
            task.id = id;
            task.itemTypeID = itemTypeID;
            task.amount = amount;
            task.master = quest;
            return task;
        }
    }
    public class TaskKillCount : TaskData
    {
        public int requireAmount = 1;
        public int weaponTypeID = -1;

        /// <summary>
        /// 可选：武器 Identifier。设置后优先解析为 weaponTypeID。
        /// </summary>
        public Identifier? weaponIdentifier;

        public int buffTypeID = -1;
        public bool requireHeadshot = false;
        public bool withoutHeadShot = false;
        public string requireEnemy = string.Empty;
        public override Task SetTask(Quest quest)
        {
            TaskKillCountFix task = quest.gameObject.AddComponent<TaskKillCountFix>();
            task.id = id;
            task.resetOnLevelInitialized = false;
            task.requireAmount = requireAmount;
            if (weaponTypeID != -1)
            {
                task.withWeapon = true;
                task.weaponTypeID = weaponTypeID;
            }
            if (buffTypeID != -1)
            {
                task.requireBuff = true;
                task.requireBuffID = buffTypeID;
            }
            task.requireHeadShot = requireHeadshot;
            task.withoutHeadShot = withoutHeadShot;

            if (requireEnemy != string.Empty)
            {
                task.requireEnemyType = EnemyUtils.GetPreset(this.requireEnemy);
            }

            task.master = quest;

            return task;
        }
    }
    public abstract class RewardData
    {
        public abstract Reward SetReward(Quest quest);
        public int id;
    }

    public class RewardGiveItem : RewardData
    {
        public int itemTypeID;

        /// <summary>
        /// 可选：物品 Identifier。设置后优先解析为 itemTypeID。
        /// </summary>
        public Identifier? itemIdentifier;

        public int amount;
        public override Reward SetReward(Quest quest)
        {
            RewardItem reward = quest.gameObject.AddComponent<RewardItem>();
            reward.id = id;
            reward.itemTypeID = itemTypeID;
            reward.amount = amount;
            reward.master = quest;
            return reward;
        }
    }
    public class RewardEXP : RewardData
    {
        public int amount;
        public override Reward SetReward(Quest quest)
        {
            QuestReward_EXP reward = quest.gameObject.AddComponent<QuestReward_EXP>();
            reward.id = id;
            reward.amount = amount;
            reward.master = quest;
            return reward;
        }
    }

    public class RewardMoney : RewardData
    {
        public int amount;
        public override Reward SetReward(Quest quest)
        {
            QuestReward_Money reward = quest.gameObject.AddComponent<QuestReward_Money>();
            reward.id = id;
            reward.amount = amount;
            reward.master = quest;
            return reward;
        }
    }
    public class RewardUnlockItem : RewardData
    {
        public int itemTypeID;

        /// <summary>
        /// 可选：物品 Identifier。设置后优先解析为 itemTypeID。
        /// </summary>
        public Identifier? itemIdentifier;

        public override Reward SetReward(Quest quest)
        {
            QuestReward_UnlockStockItem reward = quest.gameObject.AddComponent<QuestReward_UnlockStockItem>();
            reward.id = id;
            reward.unlockItem = itemTypeID;
            reward.master = quest;
            return reward;
        }
    }

}
