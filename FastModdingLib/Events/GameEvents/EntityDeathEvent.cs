using System;

namespace FastModdingLib.Events.GameEvents
{
    /// <summary>
    /// 实体死亡事件。桥接自游戏原生 <c>Health.OnDead</c> 静态事件。
    /// 仅观察用途，不支持取消。
    /// 注意：原生 OnDead 实际签名为 <c>Action&lt;Health, DamageInfo&gt;</c>（2 个参数），
    /// 故本事件携带 Victim（Health 组件）与 Info（DamageInfo）两个字段。
    /// TODO: 确认 Health / DamageInfo 命名空间后可替换为强类型（当前 object 兜底保证编译）。
    /// </summary>
    public sealed class EntityDeathEvent : Event
    {
        /// <summary>死亡实体的 Health 组件。</summary>
        public object Victim { get; }

        /// <summary>致死的伤害信息。</summary>
        public object Info { get; }

        public EntityDeathEvent(object victim, object info)
        {
            Victim = victim ?? throw new ArgumentNullException(nameof(victim));
            Info = info ?? throw new ArgumentNullException(nameof(info));
        }
    }
}