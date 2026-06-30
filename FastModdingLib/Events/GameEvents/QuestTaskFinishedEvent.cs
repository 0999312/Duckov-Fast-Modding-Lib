using System;

namespace FastModdingLib.Events.GameEvents
{
    /// <summary>
    /// 任务完成事件。桥接自游戏原生 <c>QuestManager.OnTaskFinishedEvent</c> 静态事件。
    /// 仅观察用途，不支持取消。
    /// </summary>
    public sealed class QuestTaskFinishedEvent : Event
    {
        /// <summary>
        /// 完成的任务。
        /// TODO: 确认 QuestTask 命名空间后替换为强类型（当前 object 兜底保证编译）。
        /// </summary>
        public object Task { get; }

        public QuestTaskFinishedEvent(object task)
        {
            Task = task ?? throw new ArgumentNullException(nameof(task));
        }
    }
}
