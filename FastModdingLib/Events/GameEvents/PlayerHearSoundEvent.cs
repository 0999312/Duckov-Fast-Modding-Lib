namespace FastModdingLib.Events.GameEvents
{
    /// <summary>
    /// 玩家听到声音事件。桥接自游戏原生 <c>AIMainBrain.OnPlayerHearSound</c> 静态事件。
    /// 仅观察用途，不支持取消。
    /// </summary>
    public sealed class PlayerHearSoundEvent : Event
    {
        /// <summary>
        /// 声音信息。
        /// TODO: 确认原生事件参数类型后替换为强类型（当前 object 兜底保证编译）。
        /// </summary>
        public object? SoundInfo { get; }

        public PlayerHearSoundEvent(object? soundInfo)
        {
            SoundInfo = soundInfo;
        }
    }
}
