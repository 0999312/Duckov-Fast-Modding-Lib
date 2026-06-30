using Duckov.Buildings;
using Duckov.Utilities;
using FastModdingLib.Register;
using FastModdingLib.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FastModdingLib
{
    public static class BuildingUtils
    {
        private static readonly BuildingRegistry _buildingRegistry = new BuildingRegistry();
        private static bool _initialized;

        internal static BuildingRegistry Registry => _buildingRegistry;

        /// <summary>初始化：将 BuildingRegistry 注册到 RegistryManager 元表。</summary>
        internal static void Init()
        {
            if (_initialized) return;
            _initialized = true;
            var id = new Identifier("fastmoddinglib", "building");
            var meta = RegistryManager.Instance.Registry;
            if (meta is NonAlterableSimpleRegistry<ERegistry> nonAlt)
                nonAlt.SetIfAbsent(id, _buildingRegistry, RegistryManager.CurrentModid);
            else
                meta.Set(id, _buildingRegistry, RegistryManager.CurrentModid);
        }

        /// <summary>
        /// 注册自定义建筑。将 <see cref="BuildingInfo"/> 和对应 <see cref="Building"/>
        /// prefab 注入到 <see cref="GameplayDataSettings.BuildingDataCollection"/> 的
        /// infos / prefabs 列表，同时登入 FML Registry 以便按 modid 卸载。
        /// </summary>
        /// <summary>
        /// 注册自定义建筑。modid 从 <see cref="Identifier.Domain"/> 推导。
        /// </summary>
        public static void RegisterBuilding(Identifier id, BuildingInfo info, Building prefab)
        {
            Init();
            var collection = GameplayDataSettings.BuildingDataCollection;
            if (collection == null)
                throw new InvalidOperationException("BuildingDataCollection not available.");

            collection.infos ??= new List<BuildingInfo>();
            collection.infos.Add(info);
            collection.prefabs ??= new List<Building>();
            collection.prefabs.Add(prefab);

            _buildingRegistry.Register(id, info, prefab, id.Domain);
        }

        /// <summary>按 Identifier 移除已注册的建筑。</summary>
        public static bool UnregisterBuilding(Identifier id) => _buildingRegistry.Remove(id);

        /// <summary>批量卸载指定 mod 注册的全部建筑。</summary>
        public static int UnregisterAllBuildings(string modid) => _buildingRegistry.RemoveAllByOwner(modid);

        /// <summary>按建筑 id 查询 BuildingInfo。</summary>
        public static BuildingInfo? GetBuildingInfo(string buildingId)
        {
            var collection = GameplayDataSettings.BuildingDataCollection;
            foreach (var info in collection?.infos ?? System.Linq.Enumerable.Empty<BuildingInfo>())
            {
                if (info.id == buildingId)
                    return info;
            }
            return null;
        }

        /// <summary>获取所有已注册的建筑 id。</summary>
        public static List<string> GetAllBuildingIds()
        {
            var result = new List<string>();
            foreach (var info in GameplayDataSettings.BuildingDataCollection?.infos ?? System.Linq.Enumerable.Empty<BuildingInfo>())
            {
                result.Add(info.id);
            }
            return result;
        }

        /// <summary>
        /// 放置建筑。封装 <see cref="BuildingManager.BuyAndPlace"/> 调用。
        /// TODO: BuildingManager.BuyAndPlace 是 internal 方法——需通过 Harmony 或反射公开。
        /// 当前实现直接抛 NotSupportedException 占位。
        /// </summary>
        public static void PlaceBuilding(string areaID, string buildingID, Vector2Int coord, BuildingRotation rotation)
        {
            // TODO: 用 Harmony 公开 BuildingManager.BuyAndPlace 或用反射调用
            throw new NotSupportedException("PlaceBuilding requires Harmony patch to access BuildingManager.BuyAndPlace (internal).");
        }
    }
}
