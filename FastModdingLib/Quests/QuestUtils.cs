using Duckov.Quests;
using Duckov.Quests.Relations;
using Duckov.Quests.Tasks;
using Duckov.Utilities;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using static Unity.VisualScripting.Member;

namespace FastModdingLib
{
    public static class QuestUtils
    {
        public static Dictionary<Quest, string> addedQuests = new Dictionary<Quest, string>();
        public static void RegisterQuest(QuestData data, string modid = "FastModdingLib")
        {
            Quest quest = new GameObject($"Quest_{data.displayName}").AddComponent<Quest>();
            UnityEngine.Object.DontDestroyOnLoad(quest);
            quest.DisplayNameRaw = data.displayName;
            quest.DescriptionRaw = data.description;
            quest.ID = data.ID;
            quest.QuestGiverID = data.questGiver;
            quest.requireLevel = data.requireLevel;
            
            if(data.requireScene != null && data.requireScene != "")
            {
                quest.requireSceneID = data.requireScene;
            }

            if (data.requireItemID != -1)
            {
                quest.requiredItemID = data.requireItemID;
                quest.requiredItemCount = 1;
            }
            
            foreach (var taskData in data.tasks)
            {
                Task task = taskData.SetTask(quest);
                if (task != null)
                {
                    quest.tasks.Add(task);
                }
            }

            foreach (var rewardData in data.rewards)
            {
                Reward reward = rewardData.SetReward(quest);
                if (reward != null)
                {
                    quest.rewards.Add(reward);
                }
            }
            GameplayDataSettings.QuestCollection.Add(quest);
            quest.needInspection = true;
            addedQuests.Add(quest, modid);
            Debug.Log($"[FML] Registered quest: {data.displayName} (ID: {data.ID}) from mod: {modid}");
            foreach (var item in quest.tasks)
            {
                Debug.Log($"[FML] Task:{item.gameObject.name}, {item.master.name}");
            }
            
        }

        public static void UnregisterQuest(int ID)
        {
            foreach (var item in addedQuests)
            {
                if (item.Key.id == ID)
                {
                    GameplayDataSettings.QuestCollection.Remove(item.Key);
                    UnityEngine.Object.Destroy(item.Key.gameObject);
                    addedQuests.Remove(item.Key);
                    break;
                }
            }
        }

        public static void UnregisterQuestAll(string modID)
        {
            foreach (var item in addedQuests)
            {
                if (item.Value.Equals(modID))
                {
                    GameplayDataSettings.QuestCollection.Remove(item.Key);
                    UnityEngine.Object.Destroy(item.Key.gameObject);
                    addedQuests.Remove(item.Key);
                    break;
                }
            }
        }

        public static void AddQuestRelation(int id, int before = -1, int after = -1)
        {
            QuestRelationNode item = new QuestRelationNode
            {
                questID = id
            };
            if(before != -1)
            {
                QuestRelationNode source = new QuestRelationNode
                {
                    questID = before
                };
                item.inConnections.Add(QuestRelationConnection.Create(source, item));
            }

            if(after != -1)
            {
                QuestRelationNode target = new QuestRelationNode
                {
                    questID = after
                };
                item.outConnections.Add(QuestRelationConnection.Create(item, target));
            }

            GameplayDataSettings.QuestRelation.allNodes.Add(item);
        }

    }
}