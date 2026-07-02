using Duckov.Buildings;
using Duckov.Utilities;
using FastModdingLib.Register;
using FastModdingLib.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FastModdingLib
{
    public static class BuildingUtils
    {
        private static readonly BuildingRegistry _buildingRegistry = new BuildingRegistry();
        private static bool _initialized;

        /// <summary>用于反射调用 <c>BuildingManager.BuyAndPlace</c> 的 MethodInfo 缓存。</summary>
        private static readonly MethodInfo? _buyAndPlaceMethod = typeof(BuildingManager)
            .GetMethod("BuyAndPlace", BindingFlags.NonPublic | BindingFlags.Static);

        internal static BuildingRegistry Registry => _buildingRegistry;

        /// <summary>初始化：将 BuildingRegistry 注册到 RegistryManager 元表。</summary>
        internal static void Init()
        {
            if (_initialized) return;
            _initialized = true;
            var id = new Identifier(FMLConstants.Domain, "building");
            var meta = RegistryManager.Instance.Registry;
            if (meta is NonAlterableSimpleRegistry<ERegistry> nonAlt)
                nonAlt.SetIfAbsent(id, _buildingRegistry, RegistryManager.CurrentModid);
            else
                meta.Set(id, _buildingRegistry, RegistryManager.CurrentModid);
        }

        // ===== 注册 / 卸载 =====

        /// <summary>
        /// 注册自定义建筑。将 <see cref="BuildingInfo"/> 和对应 <see cref="Building"/>
        /// prefab 登入 FML Registry。<see cref="BuildingCollectionPatch"/> 的 Harmony Postfix
        /// 自动回退到 Registry 查找，无需直接写入 <c>BuildingDataCollection</c>。
        /// modid 从 <see cref="Identifier.Domain"/> 推导。
        /// </summary>
        public static void RegisterBuilding(Identifier id, BuildingInfo info, Building prefab)
        {
            Init();
            var collection = GameplayDataSettings.BuildingDataCollection;
            if (collection == null)
                throw new InvalidOperationException("BuildingDataCollection not available.");

            // 写入 native 侧（使游戏内 UI / 查询即时可见）
            collection.infos ??= new List<BuildingInfo>();
            if (!collection.infos.Contains(info))
                collection.infos.Add(info);
            collection.prefabs ??= new List<Building>();
            if (!collection.prefabs.Contains(prefab))
                collection.prefabs.Add(prefab);

            // Registry 侧登记（OnRemoved 自动做 native 善后）
            _buildingRegistry.Register(id, info, prefab, id.Domain);
        }

        /// <summary>按 Identifier 移除已注册的建筑。</summary>
        public static bool UnregisterBuilding(Identifier id) => _buildingRegistry.Remove(id);

        /// <summary>批量卸载指定 mod 注册的全部建筑。</summary>
        public static int UnregisterAllBuildings(string modid) => _buildingRegistry.RemoveAllByOwner(modid);

        // ===== 查询（Identifier 优先） =====

        /// <summary>按 Identifier 查询 BuildingInfo。优先查 Registry，再回退到 native collection。</summary>
        public static BuildingInfo? GetBuildingInfo(Identifier id)
        {
            // 优先查 Registry（覆盖自定义建筑）
            if (_buildingRegistry.TryGet(id, out var info))
                return info;

            // 回退到 native collection
            return GetBuildingInfo(id.Path);
        }

        /// <summary>
        /// 按建筑 id 字符串查询 BuildingInfo。
        /// </summary>
        [Obsolete("Use GetBuildingInfo(Identifier) instead.")]
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

        /// <summary>获取所有已注册的建筑 Identifier 列表。</summary>
        public static IReadOnlyList<Identifier> GetAllBuildingIds()
        {
            var result = new List<Identifier>();
            foreach (var kvp in _buildingRegistry)
            {
                result.Add(kvp.Key);
            }
            return result;
        }

        /// <summary>
        /// 获取所有已注册的建筑 id 字符串列表（旧版）。
        /// </summary>
        [Obsolete("Use GetAllBuildingIds() (returns IReadOnlyList<Identifier>) instead.")]
        public static List<string> GetAllBuildingIdStrings()
        {
            var result = new List<string>();
            foreach (var info in GameplayDataSettings.BuildingDataCollection?.infos ?? System.Linq.Enumerable.Empty<BuildingInfo>())
            {
                result.Add(info.id);
            }
            return result;
        }

        // ===== 放置建筑 =====

        /// <summary>
        /// 放置建筑。通过反射调用 <see cref="BuildingManager.BuyAndPlace"/>（该方法是 internal）。
        /// areaId 和 buildingId 均为 Identifier——FML 内部将其 Path 映射为游戏原生 string ID。
        /// </summary>
        public static BuildingBuyAndPlaceResults PlaceBuilding(
            Identifier areaId, Identifier buildingId,
            Vector2Int coord, BuildingRotation rotation)
        {
            if (_buyAndPlaceMethod == null)
                throw new InvalidOperationException("BuildingManager.BuyAndPlace not found via reflection.");

            return (BuildingBuyAndPlaceResults)_buyAndPlaceMethod.Invoke(null,
                new object[] { areaId.Path, buildingId.Path, coord, rotation });
        }

        /// <summary>
        /// 放置建筑（旧版 string 签名）。
        /// </summary>
        [Obsolete("Use PlaceBuilding(Identifier, Identifier, Vector2Int, BuildingRotation) instead.")]
        public static BuildingBuyAndPlaceResults PlaceBuilding(
            string areaID, string buildingID,
            Vector2Int coord, BuildingRotation rotation)
        {
            return PlaceBuilding(
                new Identifier(RegistryManager.CurrentModid, areaID),
                new Identifier(RegistryManager.CurrentModid, buildingID),
                coord, rotation);
        }

        // ===== 便捷回调 =====

        private static readonly Dictionary<string, List<(Identifier buildingId, Action<Building> callback, string modid)>> _buildingCallbacks
            = new Dictionary<string, List<(Identifier, Action<Building>, string)>>();
        private static bool _buildingEventsHooked;

        /// <summary>
        /// 注册建筑建成回调。当指定 buildingId 的建筑建造完成时触发。
        /// FML 内部订阅 <c>BuildingManager.OnBuildingBuiltComplex</c>，按 buildingInfo.id 匹配。
        /// </summary>
        public static void OnBuildingBuilt(Identifier buildingId, Action<Building> callback, string modid)
        {
            Init();
            HookBuildingEvents();

            var path = buildingId.Path;
            if (!_buildingCallbacks.ContainsKey(path))
                _buildingCallbacks[path] = new List<(Identifier, Action<Building>, string)>();

            _buildingCallbacks[path].Add((buildingId, callback, modid));
        }

        /// <summary>移除建筑建成回调。</summary>
        public static void OffBuildingBuilt(Identifier buildingId, Action<Building> callback)
        {
            if (!_buildingCallbacks.TryGetValue(buildingId.Path, out var list)) return;
            list.RemoveAll(e => e.buildingId.Equals(buildingId) && e.callback == callback);
        }

        private static void HookBuildingEvents()
        {
            if (_buildingEventsHooked) return;
            _buildingEventsHooked = true;

            var evt = typeof(BuildingManager).GetEvent("OnBuildingBuiltComplex",
                BindingFlags.Public | BindingFlags.Static);
            var handlerMethod = typeof(BuildingUtils).GetMethod("OnBuildingBuiltHandler",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (evt != null && handlerMethod != null)
            {
                var handler = Delegate.CreateDelegate(evt.EventHandlerType!, (object?)null, handlerMethod);
                evt.AddEventHandler(null, handler);
            }
        }

        private static void OnBuildingBuiltHandler(int guid, BuildingInfo info)
        {
            if (!_buildingCallbacks.TryGetValue(info.id, out var list)) return;
            var prefab = info.Prefab;
            foreach (var (buildingId, callback, modid) in list)
            {
                try
                {
                    callback?.Invoke(prefab);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[BuildingUtils.OnBuildingBuilt] callback for '{buildingId}' threw: {e}");
                }
            }
        }

        // ===== 代码端创建建筑 =====

        private static readonly BindingFlags _buildingFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        /// <summary>
        /// 纯代码创建简易 Building GameObject（无需 Unity 编辑器）。
        /// 自动创建带 Building 组件 + 基础 Cube 模型 + 网格碰撞体的完整 Prefab 结构。
        /// </summary>
        /// <param name="id">建筑 Identifier。</param>
        /// <param name="dimensions">占地尺寸，如 (2,2)。</param>
        /// <param name="existingPrefabName">
        /// 可选：引用游戏已有 Building Prefab 名称（如 "Building_Workbench"），
        /// 克隆其 graphicsContainer 和 functionContainer 结构。
        /// </param>
        /// <returns>创建好的 Building 组件实例。</returns>
        public static Building CreateSimpleBuilding(
            Identifier id, Vector2Int dimensions, string? existingPrefabName = null)
        {
            // 路径 A：克隆游戏已有 Building Prefab
            if (existingPrefabName != null)
            {
                var existingPrefab = BuildingDataCollection.GetPrefab(existingPrefabName);
                if (existingPrefab != null)
                {
                    var clone = UnityEngine.Object.Instantiate(existingPrefab);
                    clone.name = $"Building_{id.Path}";
                    SetBuildingField(clone, "id", id.Path);
                    SetBuildingField(clone, "dimensions", dimensions);
                    return clone;
                }
            }

            // 路径 B：纯代码创建（默认 Cube 模型 + 交互碰撞体）
            return CreateBuildingFromScratch(id, dimensions);
        }

        private static Building CreateBuildingFromScratch(Identifier id, Vector2Int dimensions)
        {
            var go = new GameObject($"Building_{id.Path}");
            var building = go.AddComponent<Building>();

            // 设置 Building 组件字段
            SetBuildingField(building, "id", id.Path);
            SetBuildingField(building, "dimensions", dimensions);

            // 创建 graphicsContainer（美术层）
            var graphics = new GameObject("Graphics");
            graphics.transform.SetParent(go.transform);
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(graphics.transform);
            cube.transform.localPosition = Vector3.zero;
            cube.transform.localScale = new Vector3(dimensions.x, 2f, dimensions.y);
            cube.name = "Model_Cube";
            SetBuildingField(building, "graphicsContainer", graphics);

            // 创建 functionContainer（功能层——交互碰撞体）
            var func = new GameObject("Function");
            func.transform.SetParent(go.transform);
            func.layer = LayerMask.NameToLayer("Interact") != -1 ? LayerMask.NameToLayer("Interact") : 8;
            var collider = func.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(dimensions.x, 2f, dimensions.y);
            SetBuildingField(building, "functionContainer", func);

            return building;
        }

        /// <summary>
        /// 将自定义 3D 模型注入到 Building 的 graphicsContainer 中。
        /// </summary>
        /// <param name="buildingId">已注册的建筑 Identifier。</param>
        /// <param name="modelPrefab">自定义模型 prefab（可从 AssetBundle 加载）。</param>
        /// <param name="replaceExisting">是否替换 graphicsContainer 下现有子物体（默认 true）。</param>
        public static void SetBuildingModel(
            Identifier buildingId, GameObject modelPrefab, bool replaceExisting = true)
        {
            if (!_buildingRegistry.TryGet(buildingId, out var info)) return;
            var prefab = BuildingDataCollection.GetPrefab(info.prefabName);
            if (prefab == null) return;

            var graphics = GetBuildingField<GameObject>(prefab, "graphicsContainer");
            if (graphics == null) return;

            if (replaceExisting)
            {
                for (int i = graphics.transform.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.Destroy(graphics.transform.GetChild(i).gameObject);
            }

            var model = UnityEngine.Object.Instantiate(modelPrefab, graphics.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
        }

        // ===== Building 反射辅助 =====

        /// <summary>通过反射设置 Building 的 private [SerializeField] 字段。</summary>
        private static void SetBuildingField<T>(Building building, string fieldName, T value)
        {
            var field = typeof(Building).GetField(fieldName, _buildingFlags);
            if (field != null) field.SetValue(building, value);
            else Debug.LogWarning($"[BuildingUtils] Field '{fieldName}' not found on Building.");
        }

        /// <summary>通过反射读取 Building 的 private [SerializeField] 字段。</summary>
        private static T? GetBuildingField<T>(Building building, string fieldName) where T : class
        {
            var field = typeof(Building).GetField(fieldName, _buildingFlags);
            return field?.GetValue(building) as T;
        }
    }
}
