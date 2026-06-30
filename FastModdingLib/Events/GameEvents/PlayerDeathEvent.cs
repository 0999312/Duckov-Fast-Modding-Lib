namespace FastModdingLib.Events.GameEvents
{
    /// <summary>
    /// 玩家死亡事件。桥接自游戏原生 <c>LevelManager.OnMainCharacterDead</c> 静态事件。
    /// 仅观察用途，不支持取消。
    /// 原生签名：Action&lt;DamageInfo&gt;（DamageInfo 为值类型，装箱后以 object 透传）。
    /// </summary>
    public sealed class PlayerDeathEvent : Event
    {
        /// <summary>
        /// 原生 DamageInfo（boxed object）。因 Krafs.Publicizer 公开化副本与原始程序集
        /// 存在二义性（见 <see cref="Adapters.GameEventAdapters"/> 类注释），此处以 object 透传；
        /// 需读取具体字段时请反射访问。
        /// </summary>
        public object? Info { get; }

        public PlayerDeathEvent(object? info)
        {
            Info = info;
        }
    }
}
