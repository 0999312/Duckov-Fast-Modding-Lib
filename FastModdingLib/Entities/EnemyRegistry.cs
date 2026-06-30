using Duckov.Utilities;
using FastModdingLib.Register;
using FastModdingLib.Utils;
using System.Collections.Generic;

namespace FastModdingLib.Entities
{
    /// <summary>
    /// 敌人注册表。维护 Identifier → CharacterRandomPreset 主映射，
    /// 并额外存储 IStateConfig 供 AI 初始化补丁注入。
    /// OnRemoved 时从 <see cref="GameplayDataSettings.CharacterRandomPresetData.presets"/> 移除。
    /// </summary>
    public sealed class EnemyRegistry : SimpleRegistry<CharacterRandomPreset>
    {
        /// <summary>追踪注入到 preset 列表的预设体引用，用于 OnRemoved 时移除。</summary>
        private readonly Dictionary<Identifier, CharacterRandomPreset> _injectedPresets =
            new Dictionary<Identifier, CharacterRandomPreset>();

        /// <summary>按 Identifier 存储敌人 AI 状态机配置。</summary>
        private readonly Dictionary<Identifier, IStateConfig> _aiConfigs =
            new Dictionary<Identifier, IStateConfig>();

        /// <summary>
        /// 注册敌人 preset 到 FML Registry 并注入到游戏 preset 列表。
        /// modid 从 <see cref="Identifier.Domain"/> 推导。
        /// </summary>
        /// <param name="aiConfig">AI 状态机配置，可为 null（不使用自定义 AI）。</param>
        public void RegisterPreset(Identifier id, CharacterRandomPreset preset, IStateConfig? aiConfig = null)
        {
            string modid = id.Domain;
            Set(id, preset, modid);
            _injectedPresets[id] = preset;

            if (aiConfig != null)
            {
                _aiConfigs[id] = aiConfig;
            }

            var presets = GameplayDataSettings.CharacterRandomPresetData.presets;
            if (presets != null && !presets.Contains(preset))
            {
                presets.Add(preset);
            }
        }

        /// <summary>按 Identifier 查询已注册的 AI 状态机配置。</summary>
        public bool TryGetAiConfig(Identifier id, out IStateConfig? config)
        {
            return _aiConfigs.TryGetValue(id, out config);
        }

        protected override void OnRemoved(Identifier id, CharacterRandomPreset value, string? modid)
        {
            _injectedPresets.Remove(id);
            _aiConfigs.Remove(id);
        }

        public new void Clear()
        {
            _injectedPresets.Clear();
            _aiConfigs.Clear();
            base.Clear();
        }
    }
}
