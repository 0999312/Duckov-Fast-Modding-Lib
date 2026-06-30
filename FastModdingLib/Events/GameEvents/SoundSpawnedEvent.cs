namespace FastModdingLib.Events.GameEvents
{
    /// <summary>
    /// 声音生成事件。桥接自游戏原生 <c>AIMainBrain.OnSoundSpawned</c> 静态事件。
    /// 仅观察用途，不支持取消。
    /// </summary>
    public sealed class SoundSpawnedEvent : Event
    {
        /// <summary>
        /// 声音信息。
        /// TODO: 确认原生事件参数类型后替换为强类型（当前 object 兜底保证编译）。
        /// </summary>
        public object? SoundInfo { get; }

        public SoundSpawnedEvent(object? soundInfo)
        {
            SoundInfo = soundInfo;
        }
    }
}
