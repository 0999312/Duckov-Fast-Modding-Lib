using Duckov.Quests;
using Duckov.Quests.Rewards;
using Duckov.Quests.Tasks;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.VisualScripting;
using UnityEngine;
using static Duckov.Utilities.GameplayDataSettings;

namespace FastModdingLib
{
    public class QuestData
    {
        public string displayName = string.Empty;
        public string description = string.Empty;
        public int ID;
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
        public int requiredAmount;
        public override Task SetTask(Quest quest) {
            GameObject gameObject = new GameObject($"SubmitItems_{quest.id}_{this.id}");
            gameObject.transform.SetParent(quest.transform);
            SubmitItems submit = gameObject.AddComponent<SubmitItems>();
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
            GameObject gameObject = new GameObject($"SubmitMoney_{quest.id}_{this.id}");
            gameObject.transform.SetParent(quest.transform);
            QuestTask_SubmitMoney task = gameObject.AddComponent<QuestTask_SubmitMoney>();
            task.id = id;
            task.money = money;
            task.master = quest;
            return task;
        }
    }

    public class TaskRequireUseItem : TaskData
    {
        public int itemTypeID;
        public int amount;
        public override Task SetTask(Quest quest)
        {
            GameObject gameObject = new GameObject($"UseItem_{quest.id}_{this.id}");
            gameObject.transform.SetParent(quest.transform);
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
        public int buffTypeID = -1;
        public bool requireHeadshot = false;
        public bool withoutHeadShot = false;
        public string requireEnemy = string.Empty;
        public override Task SetTask(Quest quest)
        {
            GameObject gameObject = new GameObject($"KillCount_{quest.id}_{this.id}");
            gameObject.transform.SetParent(quest.transform);
            
            QuestTask_KillCount task = gameObject.AddComponent<QuestTask_KillCount>();
            task.id = id;
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

            if(requireEnemy != string.Empty)
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
        public int amount;
        public override Reward SetReward(Quest quest)
        {
            GameObject gameObject = new GameObject($"RewardItem_{quest.id}_{this.id}");
            gameObject.transform.SetParent(quest.transform);
            gameObject.SetActive(false);
            RewardItem reward = gameObject.AddComponent<RewardItem>();
            reward.id = id;
            reward.itemTypeID = itemTypeID;
            reward.amount = amount;
            reward.master = quest;
            gameObject.SetActive(true);
            return reward;
        }
    }
    public class RewardEXP : RewardData
    {
        public int amount;
        public override Reward SetReward(Quest quest)
        {
            GameObject gameObject = new GameObject($"RewardEXP_{quest.id}_{this.id}");
            gameObject.transform.SetParent(quest.transform);
            gameObject.SetActive(false);
            QuestReward_EXP reward = gameObject.AddComponent<QuestReward_EXP>();
            reward.id = id;
            reward.amount = amount;
            reward.master = quest;
            gameObject.SetActive(true);
            return reward;
        }
    }

    public class RewardMoney : RewardData
    {
        public int amount;
        public override Reward SetReward(Quest quest)
        {
            GameObject gameObject = new GameObject($"RewardMoney_{quest.id}_{this.id}");
            gameObject.transform.SetParent(quest.transform);
            gameObject.SetActive(false);
            QuestReward_Money reward = gameObject.AddComponent<QuestReward_Money>();
            reward.id = id;
            reward.amount = amount;
            reward.master = quest;
            gameObject.SetActive(true);
            return reward;
        }
    }
    public class RewardUnlockItem : RewardData
    {
        public int itemTypeID;
        public override Reward SetReward(Quest quest)
        {
            GameObject gameObject = new GameObject($"RewardUnlockItem_{quest.id}_{this.id}");
            gameObject.transform.SetParent(quest.transform);
            gameObject.SetActive(false);
            QuestReward_UnlockStockItem reward = gameObject.AddComponent<QuestReward_UnlockStockItem>();
            reward.id = id;
            reward.unlockItem = itemTypeID;
            reward.master = quest;
            gameObject.SetActive(true);
            return reward;
        }
    }
    
}
