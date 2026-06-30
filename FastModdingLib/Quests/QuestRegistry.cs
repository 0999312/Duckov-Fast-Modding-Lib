using Duckov.Quests;
using Duckov.Utilities;
using FastModdingLib.Register;
using FastModdingLib.Utils;
using UnityEngine;

namespace FastModdingLib
{
    /// <summary>
    /// 管理 <see cref="Quest"/> 注册表的 native 清理。
    /// <see cref="SimpleRegistry{T}.OnRemoved"/> 在 registry 删除 entry 时善后：
    /// 从 <see cref="GameplayDataSettings.QuestCollection"/> 移除、<see cref="Object.Destroy"/> 游戏对象、
    /// 清理 <see cref="GameplayDataSettings.QuestRelation"/> 节点。
    /// </summary>
    public class QuestRegistry : SimpleRegistry<Quest>
    {
        protected override void OnRemoved(Identifier id, Quest value, string? modid)
        {
            GameplayDataSettings.QuestCollection.Remove(value);
            Object.Destroy(value.gameObject);
            GameplayDataSettings.QuestRelation.RemoveNode(
                GameplayDataSettings.QuestRelation.GetNode(value.ID));
        }
    }
}
