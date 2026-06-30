namespace FastModdingLib.Events.GameEvents
{
    /// <summary>
    /// 击杀计数变更事件。桥接自游戏原生 <c>SavesCounter.OnKillCountChanged</c> 静态事件。
    /// 仅观察用途，不支持取消。
    /// </summary>
    public sealed class KillCountChangedEvent : Event
    {
        /// <summary>当前总击杀数。</summary>
        public int Total { get; }

        public KillCountChangedEvent(int total)
        {
            Total = total;
        }
    }
}
