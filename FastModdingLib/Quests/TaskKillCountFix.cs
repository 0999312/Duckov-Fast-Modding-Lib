using Duckov.Quests.Tasks;

namespace FastModdingLib.Quests
{
    public class TaskKillCountFix : QuestTask_KillCount
    {

        public override void SetupSaveData(object data)
        {
            base.SetupSaveData(data);
            if (!this.CheckFinished())
            {
                if (!this.isActiveAndEnabled)
                {
                    this.gameObject.SetActive(true);
                }
            }
        }
    }
}
