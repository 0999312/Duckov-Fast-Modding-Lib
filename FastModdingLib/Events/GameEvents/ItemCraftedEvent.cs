using System;

namespace FastModdingLib.Events.GameEvents
{
    /// <summary>
    /// 物品合成事件。桥接自游戏原生 <c>CraftingManager.OnItemCrafted</c> 静态事件。
    /// 仅观察用途，不支持取消。
    /// </summary>
    public sealed class ItemCraftedEvent : Event
    {
        /// <summary>
        /// 合成的物品数据。
        /// TODO: 确认 ItemData 命名空间后替换为强类型（当前 object 兜底保证编译）。
        /// </summary>
        public object ItemData { get; }

        /// <summary>合成数量。</summary>
        public int Count { get; }

        public ItemCraftedEvent(object itemData, int count)
        {
            ItemData = itemData ?? throw new ArgumentNullException(nameof(itemData));
            Count = count;
        }
    }
}
