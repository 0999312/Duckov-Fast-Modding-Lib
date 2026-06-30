using System;

namespace FastModdingLib.Events.GameEvents
{
    /// <summary>
    /// 合成配方解锁事件。桥接自游戏原生 <c>CraftingManager.OnFormulaUnlocked</c> 静态事件。
    /// 仅观察用途，不支持取消。
    /// </summary>
    public sealed class FormulaUnlockedEvent : Event
    {
        /// <summary>
        /// 解锁的合成配方。
        /// TODO: 确认 CraftingFormula 命名空间后替换为强类型（当前 object 兜底保证编译）。
        /// </summary>
        public object Formula { get; }

        public FormulaUnlockedEvent(object formula)
        {
            Formula = formula ?? throw new ArgumentNullException(nameof(formula));
        }
    }
}
