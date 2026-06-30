using Duckov.Buildings;
using Duckov.Utilities;
using FastModdingLib.Register;
using FastModdingLib.Utils;
using System.Collections.Generic;

namespace FastModdingLib
{
    /// <summary>
    /// 建筑注册表。维护 Identifier → BuildingInfo 主映射和 id → prefab 字典。
    /// OnRemoved 时从 <see cref="BuildingDataCollection"/> 的 infos/prefabs 列表移除。
    /// </summary>
    public sealed class BuildingRegistry : SimpleRegistry<BuildingInfo>
    {
        private readonly Dictionary<string, Building> _prefabs;

        public BuildingRegistry()
        {
            _prefabs = new Dictionary<string, Building>();
        }

        /// <summary>注册建筑（写入主字典 + owner 索引 + prefab 索引）。</summary>
        public void Register(Identifier id, BuildingInfo info, Building prefab, string modid)
        {
            Set(id, info, modid);
            _prefabs[info.id] = prefab;
        }

        /// <summary>按建筑 id 查找预制体。</summary>
        public bool TryGetPrefab(string buildingId, out Building prefab)
        {
            return _prefabs.TryGetValue(buildingId, out prefab);
        }

        protected override void OnRemoved(Identifier id, BuildingInfo value, string? modid)
        {
            var collection = GameplayDataSettings.BuildingDataCollection;
            if (collection == null) return;

            // 从 infos 列表移除
            collection.infos?.Remove(value);

            // 从 prefabs 列表移除
            if (_prefabs.TryGetValue(value.id, out var prefab))
            {
                collection.prefabs?.Remove(prefab);
                _prefabs.Remove(value.id);
            }
        }

        public new void Clear()
        {
            _prefabs.Clear();
            base.Clear();
        }
    }
}
