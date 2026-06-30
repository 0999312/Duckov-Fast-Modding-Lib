using Duckov.PerkTrees;
using FastModdingLib.Register;
using FastModdingLib.Utils;

namespace FastModdingLib
{
    /// <summary>
    /// Perk 注册表。维护 Identifier → Perk 主映射，
    /// OnRemoved 时清理 Perk 组件和对应 GameObject。
    /// </summary>
    public sealed class PerkTreeRegistry : SimpleRegistry<Perk>
    {
        protected override void OnRemoved(Identifier id, Perk value, string? modid)
        {
            if (value != null && value.gameObject != null)
            {
                // 让 Unity 在下一帧销毁（避免迭代中销毁）
                UnityEngine.Object.Destroy(value.gameObject);
            }
        }
    }
}
