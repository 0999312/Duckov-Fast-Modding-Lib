using System;

namespace FastModdingLib.Events.GameEvents
{
    /// <summary>
    /// 物品解锁状态变更事件。桥接自游戏原生 <c>EconomyManager.OnItemUnlockStateChanged</c> 静态事件。
    /// 仅观察用途，不支持取消。
    /// </summary>
    public sealed class ItemUnlockStateChangedEvent : Event
    {
        /// <summary>
        /// 物品 ID。
        /// TODO: 确认 ItemID 类型与命名空间后替换为强类型（当前 object 兜底保证编译）。
        /// </summary>
        public object ItemId { get; }

        /// <summary>物品是否已解锁。</summary>
        public bool Unlocked { get; }

        public ItemUnlockStateChangedEvent(object itemId, bool unlocked)
        {
            ItemId = itemId ?? throw new ArgumentNullException(nameof(itemId));
            Unlocked = unlocked;
        }
    }
}
