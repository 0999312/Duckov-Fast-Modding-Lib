using FastModdingLib.Register;
using FastModdingLib.Utils;

namespace FastModdingLib
{
    /// <summary>
    /// 合成配方注册表。<see cref="Identifier"/>.Path = formulaId；
    /// <see cref="OnRemoved"/> 从 <see cref="CraftingFormulaCollection"/>.Instance.list 移除 native 条目。
    /// <para>PLAN-Register §5.5 R7：替代原 <c>addedFormulaIds</c> 旁路字典。</para>
    /// </summary>
    public class CraftingFormulaRegistry : SimpleRegistry<CraftingFormula>
    {
        protected override void OnRemoved(Identifier id, CraftingFormula value, string? modid)
        {
            CraftingFormulaCollection.Instance.list.Remove(value);
        }
    }
}