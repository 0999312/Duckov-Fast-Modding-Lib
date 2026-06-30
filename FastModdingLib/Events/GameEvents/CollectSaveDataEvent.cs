namespace FastModdingLib.Events.GameEvents
{
    /// <summary>
    /// 收集存档数据事件。桥接自游戏原生 <c>SavesSystem.OnCollectSaveData</c> 静态事件。
    /// 仅观察用途，不支持取消。Phase 1 SaveUtils 也基于此事件。
    /// </summary>
    public sealed class CollectSaveDataEvent : Event
    {
        /// <summary>
        /// 存档数据。
        /// TODO: 确认原生事件参数类型后替换为强类型（当前 object 兜底保证编译）。
        /// </summary>
        public object? SaveData { get; }

        public CollectSaveDataEvent(object? saveData)
        {
            SaveData = saveData;
        }
    }
}
