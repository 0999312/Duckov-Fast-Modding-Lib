using Duckov.Buffs;
using Duckov.Utilities;
using FastModdingLib.Register;
using FastModdingLib.Utils;

namespace FastModdingLib
{
    /// <summary>
    /// Buff 注册表。维护 Identifier → Buff 主映射，OnRemoved 时从
    /// <see cref="GameplayDataSettings.Buffs.allBuffs"/> 移除并 Destroy 预制体。
    /// </summary>
    public sealed class BuffRegistry : SimpleRegistry<Buff>
    {
        protected override void OnRemoved(Identifier id, Buff value, string? modid)
        {
            var allBuffs = GameplayDataSettings.Buffs.allBuffs;
            if (allBuffs != null)
            {
                allBuffs.Remove(value);
            }
        }
    }
}
