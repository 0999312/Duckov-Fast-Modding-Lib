using System;

namespace FastModdingLib.Events.GameEvents
{
    /// <summary>
    /// 实体受伤事件。桥接自游戏原生 <c>Health.OnHurt</c> 静态事件。
    /// 标记 [Cancelable]：可叫停后续 FML handler，但游戏侧伤害效果已应用（仅观察 + 后续链路 gating）。
    /// </summary>
    [Cancelable]
    public sealed class HurtEvent : Event
    {
        /// <summary>
        /// 受伤的目标角色。
        /// TODO: 确认 CharacterMainControl 的命名空间后替换为强类型（当前 object 兜底保证编译）。
        /// </summary>
        public object Target { get; }

        /// <summary>
        /// 伤害信息。
        /// TODO: 确认 DamageInfo 的命名空间后替换为强类型（当前 object 兜底保证编译）。
        /// </summary>
        public object Info { get; }

        public HurtEvent(object target, object info)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Info = info ?? throw new ArgumentNullException(nameof(info));
        }
    }
}