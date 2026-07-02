# Phase 4 — Building / PerkTree / Endowment / UI 深化计划 ✅ 已实施完成

> 路线归属：`PLAN.md` Phase 4（原 Phase 3 已实现基础框架，本计划针对四个系统进行针对性编码和优化）
> 声明：本计划**不走 GSD 工作流**；按本文直接进入实现。
> 最后更新：2026-07-01 — ✅ 代码已全部落地，本文件转为架构参考存档

---

## 0. 设计原则：Identifier 优先

> 本计划所有新增/修改的 public API **必须**遵守以下原则：

### 0.1 核心原则

| 原则 | 说明 |
|------|------|
| **public API 全用 Identifier** | 所有注册、查询、卸载、选择的公开方法，统一使用 `Identifier` 作为资源标识符。modder 看到的永远是 `Identifier`，永远不碰数字 ID |
| **数字 ID 内部化** | 游戏原生的数字 ID（如 `EndowmentIndex` 枚举值、`Item.typeID`、`PerkTree` 的内部 int id）由 FML 内部自动分配或从 Identifier 映射。数字 ID 的生成/冲突检测对 modder 完全透明 |
| **兜底仅内部** | 如确需强指定数字 ID（例如与既有游戏内容保持兼容），通过 `Identifier` 的可选元数据或内部配置表处理，**不暴露在 public API 签名中** |
| **继承现有模式** | `ItemUtils`、`EnemyUtils`、`AudioUtil` 等已有模块全部使用 `Identifier` → `Register` 模式，新模块必须一致 |

### 0.2 反例（本计划禁止）

```csharp
// ❌ 禁止：public API 接受裸 string/数字 ID
public static Perk AddPerk(string treeId, string perkName, ...);
public static void SelectEndowment(EndowmentIndex index);
public static BuildingInfo? GetBuildingInfo(string buildingId);

// ✅ 正确：全部走 Identifier
public static Perk AddPerk(Identifier id, ...);
public static void SelectEndowment(Identifier id);
public static BuildingInfo? GetBuildingInfo(Identifier id);
```

### 0.3 数字 ID 内部映射策略

FML 内部维护 `Identifier ↔ 游戏原生数字ID` 的映射表：

```
┌──────────────────────────────────────────────────────┐
│ FML Registry (Identifier 命名空间)                    │
│                                                      │
│  Identifier "mymod:assassin" ──→ 内部映射 ──→ (EndowmentIndex)10  │
│  Identifier "mymod:workshop"  ──→ 内部映射 ──→ BuildingInfo.id = "mymod:workshop"  │
│  Identifier "mymod:perk_str"   ──→ 内部映射 ──→ Perk GameObject.name = "mymod:perk_str" │
│                                                      │
│  游戏原生 API 调用时，FML 从 Registry 反查对应的       │
│  Identifier → 取内部映射的数字ID/字符串 → 传入原生方法  │
└──────────────────────────────────────────────────────┘
```

---

## 0.4 定位与动机

Phase 3 已完成 Building、PerkTree 的基础注册 API 和 ModOptions 设置面板。但当前实现存在以下功能缺口：

| 系统 | 现存问题 |
|------|---------|
| **Building** | `PlaceBuilding()` 直接抛 `NotSupportedException`；`RegisterBuilding` 无后续 Harmony patch 保证自定义建筑在 BuilderView UI 中可见；`UnregisterBuilding` 未清理 `BuildingDataCollection.infos` |
| **PerkTree** | `ConnectPerks` 用 try/catch 包装反射调用，极度脆弱；缺少 `PerkBehaviour` 创建辅助；缺少自动注册 PerkTree 到 `LevelConfig` 的 patch |
| **Endowment** | **完全缺失**—无 `EndowmentUtils`，无 `EndowmentRegistry`，modder 无法注册自定义天赋 |
| **UI 交互** | `ModOptions` 已完备，但 Building/PerkTree/Endowment 系统仍缺少对游戏原生 UI（BuilderView / PerkTreeView / EndowmentSelectionPanel）的注入能力 |

**本计划目标**：补齐四个系统的缺口，使其从"API 存在但不可用"升级为"modder 可完整走通注册→运行时生效→卸载回滚全流程"。

---

## 1. 各模块现状速览

### 1.1 Building (`FastModdingLib/Buildings/`)

| 方法 | 状态 | 问题 |
|------|------|------|
| `RegisterBuilding(Identifier, BuildingInfo, Building)` | ✅ 可用 | 直接 `Add` 到 `BuildingDataCollection.infos/prefabs` |
| `UnregisterBuilding(Identifier)` | ⚠️ 部分 | 仅从 Registry 移除，未从 `BuildingDataCollection` 清理 |
| `GetBuildingInfo(Identifier)` | ✅ 可用 | 当前签名为 `GetBuildingInfo(string)`——需改为 `Identifier` |
| `GetAllBuildingIds()` | ⚠️ 待改 | 应返回 `IReadOnlyList<Identifier>` 而非 `List<string>` |
| `PlaceBuilding(Identifier, Identifier, Vector2Int, BuildingRotation)` | ❌ 不可用 | 抛 `NotSupportedException` — `BuildingManager.BuyAndPlace` 是 `internal`；当前签名为 `PlaceBuilding(string,string,...)`——需改为 `Identifier` |

**缺失**：
- 无 Harmony patch 拦截 `BuildingDataCollection.GetInfo/GetPrefab` 以回退到 Mod 注册表
- 无 `BuildingSelectionPanel.GetBuildingsToDisplay` Postfix 注入自定义建筑到 UI
- `Unregister` 未做 native 侧善后

### 1.2 PerkTree (`FastModdingLib/PerkTrees/`)

| 方法 | 状态 | 问题 |
|------|------|------|
| `AddPerk(Identifier id, PerkRequirement req, Sprite icon, string modid)` | ⚠️ 待改 | 当前签名为 `AddPerk(treeId, perkName, ...)`——需改为 `Identifier`；`treeId` 从 `id.Domain` 推导，`perkName` 从 `id.Path` 推导 |
| `ConnectPerks(Identifier fromPerk, Identifier toPerk)` | ⚠️ 脆弱 + 待改 | try/catch 反射；当前签名为 `ConnectPerks(treeId, from, to)`——需改为 `Identifier`，treeId 可从 Perk 反查 |
| `ForceUnlock(Identifier perkId)` | ⚠️ 待改 | 当前签名为 `ForceUnlock(treeId, perkName)`——需改为 `Identifier` |
| `RemovePerk(Identifier)` | ✅ 可用 | |
| `RemoveAllPerks(string modid)` | ✅ 可用 | |

**缺失**：
- `ConnectPerks` 需改造为可靠性更高的实现
- 无 `AddPerkBehaviour<T>(Identifier perkId)` 辅助方法
- 无 Harmony patch `LevelConfig.IsPerkTreeEnabled` 让自定义 PerkTree 生效
- 无 patch 防止 `PerkTree.Collect()` 清空运行时注入的 Perk

### 1.3 Endowment（完全缺失，需遵守 Identifier 优先）

| 应有内容 | 状态 | 备注 |
|---------|------|------|
| `EndowmentUtils.cs` | ❌ 不存在 | `Identifier` → Endowment 的映射由 FML 内部管理；`EndowmentIndex` 枚举值由 FML 自动分配（模10起），modder 不直接接触 |
| `EndowmentRegistry.cs` | ❌ 不存在 | `SimpleRegistry<EndowmentEntry>` + `OnRemoved` |
| 完整 API（Register/Unregister/Select/Query） | ❌ 不存在 | 全部走 `Identifier`：`SelectEndowment(Identifier)` 内部映射到 `EndowmentIndex` |

**关键约束**：`EndowmentIndex` 是游戏原生 `enum`（`None=0, Surviver=1, Porter=2, Berserker=3, Marksman=4, _Count=5`）。FML 自定义天赋使用 ≥10 的枚举值，由 FML 内部从 `Identifier` 自动分配序号，modder 永远不写 `(EndowmentIndex)10`。

### 1.4 UI (`FastModdingLib/Options/`)

| 组件 | 状态 |
|------|------|
| `ModOptionsRegistry` | ✅ 完备 — `RegisterPanel`/`UnregisterPanel`/持久化 |
| `ModOptionsBuilder` | ✅ 完备 — Toggle/Slider/Dropdown/Button/Header/Space |

**缺失**：
- 无可复用的"与游戏原生交互入口"辅助（如 `InteractableBase` 子类模板）

---

## 2. 实施阶段拆分

```
B1 (Building Patch 层) ──┐
                          ├── B2 (Building 完善) ──┐
P1 (PerkTree 稳健化) ────┤                          ├── U1 (UI 交互) ── E1 (Endowment)
                          └── (B1/P1 可并行)        │
                                                     └── E1 可与 U1 并行
```

| 阶段 | 范围 | 依赖 | 预估 LOC | 验收点 |
|------|------|------|----------|--------|
| **B1** | Building Harmony Patch 层：Postfix `BuildingDataCollection.GetInfo/GetPrefab` + Postfix `BuildingSelectionPanel.GetBuildingsToDisplay` + 反射公开 `BuyAndPlace` | 无 | ~120 | `PlaceBuilding` 不再抛异常；自定义建筑在 BuilderView UI 中可见 |
| **B2** | Building 完善：`RegisterBuilding` 改为先注入 registry 再延迟注入 game collection；`UnregisterBuilding` 补 native 清理；`BuildingRegistry.OnRemoved` 完整实现 | B1 | ~60 | Register→Unregister 往返不残留数据 |
| **P1** | PerkTree 稳健化：`ConnectPerks` 改为非反射方案（直接操作 NodeCanvas Graph API）；新增 `AddPerkBehaviour<T>`；新增 `RegisterPerkTree`（patch `LevelConfig` + guard `Collect`） | 无 | ~150 | `ConnectPerks` 不再抛 try/catch warning；自定义树在 PerkTreeView UI 中可选 |
| **E1** | Endowment 完整实现：`EndowmentUtils` + `EndowmentRegistry` + `RegisterEndowment` + `UnregisterEndowment` + `SelectEndowment` + Harmony patch `EndowmentManager.Awake` Postfix 注入 | 无 | ~200 | modder 可注册自定义天赋并在 EndowmentSelectionPanel 中选择 |
| **U1** | UI 交互辅助：提供 `InteractableBase` 子类模板 + Building/PerkTree/Endowment 交互入口指导文档 | B2, P1, E1 | ~50（+ 文档） | Building/PerkTree/Endowment 各有一个示范交互入口 |

> **并行策略**：B1/P1/E1 互不依赖，可完全并行。U1 在系统都完成后做集成验证。

---

## 3. B1 — Building Harmony Patch 层

### 3.1 新建文件：`FastModdingLib/Buildings/Patches/BuildingCollectionPatch.cs`

```
~80 LOC
├── [HarmonyPatch(typeof(BuildingDataCollection), "GetInfo")]
│   [HarmonyPostfix]
│   static void GetInfo_Postfix(string id, ref BuildingInfo __result)
│       → 若 __result.Valid == false，从 BuildingRegistry 按 id 查找
│
├── [HarmonyPatch(typeof(BuildingDataCollection), "GetPrefab")]
│   [HarmonyPostfix]
│   static void GetPrefab_Postfix(string prefabName, ref Building __result)
│       → 若 __result == null，从 BuildingRegistry 按 prefabName 查找
│
└── [HarmonyPatch(typeof(BuildingSelectionPanel), "GetBuildingsToDisplay")]
    [HarmonyPostfix]
    static void GetBuildingsToDisplay_Postfix(ref BuildingInfo[] __result)
        → __result = __result.Concat(从 BuildingRegistry 获取全部 BuildingInfo).ToArray()
```

### 3.2 `PlaceBuilding` 反射公开 + Identifier 化

```csharp
// BuildingUtils 中修改签名（string → Identifier）：
private static readonly MethodInfo? _buyAndPlaceMethod = typeof(BuildingManager)
    .GetMethod("BuyAndPlace", BindingFlags.NonPublic | BindingFlags.Static);

/// <summary>
/// 放置建筑。areaId 和 buildingId 均为 Identifier。
/// FML 内部将 Identifier.Path 映射为游戏原生的 string 建筑 ID。
/// </summary>
public static BuildingBuyAndPlaceResults PlaceBuilding(
    Identifier areaId, Identifier buildingId,
    Vector2Int coord, BuildingRotation rotation)
{
    if (_buyAndPlaceMethod == null)
        throw new InvalidOperationException("BuildingManager.BuyAndPlace not found via reflection.");
    // Identifier.Path → 游戏原生 string areaID / buildingID
    return (BuildingBuyAndPlaceResults)_buyAndPlaceMethod.Invoke(null,
        new object[] { areaId.Path, buildingId.Path, coord, rotation });
}
```

### 3.3 验收
- `PlaceBuilding(Identifier("base","area1"), Identifier("mymod","workshop"), (0,0), Rot0)` 不再抛异常
- BuilderView UI 中可见自定义建筑（`GetBuildingsToDisplay` Postfix 生效）

---

## 4. B2 — Building 注册完善

### 4.1 `RegisterBuilding` 改进

现状是直接 `Add` 到 `collection.infos/prefabs`。改进为：
1. 先登入 `BuildingRegistry`
2. Patch 层的 `GetInfo/GetPrefab` Postfix 自动回退
3. **删除直接 `Add` 到 collection 的逻辑**——让 Patch 层统一处理注入

### 4.2 `BuildingRegistry.OnRemoved` 完善

```csharp
protected override void OnRemoved(Identifier id, BuildingInfo info, string? modid)
{
    // 从 BuildingDataCollection 清理
    var collection = GameplayDataSettings.BuildingDataCollection;
    // info.id 是游戏原生 string ID（= Identifier.Path 或 FML 内部映射的值）
    collection?.infos?.RemoveAll(i => i.id == info.id);
    collection?.prefabs?.RemoveAll(p => p != null && p.name == info.prefabName);
}
```

### 4.3 文件布局

```
FastModdingLib/Buildings/
├── BuildingUtils.cs          (修改：PlaceBuilding 签名为 Identifier + 反射实现，
│                              GetBuildingInfo 签名为 Identifier，
│                              RegisterBuilding 改为不直写 collection)
├── BuildingRegistry.cs       (修改：OnRemoved 补全 native 清理)
└── Patches/
    └── BuildingCollectionPatch.cs  (新增 ~80 LOC：Postfix 三个方法)
```

### 4.4 验收
- `RegisterBuilding(id, info, prefab) → PlaceBuilding(areaId, id, ...) → UnregisterBuilding(id)` 完整流程无异常
- 卸载后 `BuildingDataCollection.infos` 中无残留条目
- 所有 public API 接受 `Identifier`（无裸 string ID）

---

## 5. P1 — PerkTree 稳健化

### 5.1 `ConnectPerks` 重写（Identifier 化）

当前用 try/catch + 反射查找 `ConnectTo`/`AddConnection`，极度脆弱。重写为直接调用 NodeCanvas Graph API：

```csharp
/// <summary>
/// 建立 Perk 前置关系：fromPerk 是 toPerk 的前置条件。
/// 两个参数均为 Identifier——FML 从 Registry 反查对应的 Perk 实例。
/// </summary>
public static void ConnectPerks(Identifier fromPerkId, Identifier toPerkId)
{
    // 从 Registry 查找 Perk 实例
    if (!_perkRegistry.TryGet(fromPerkId, out var fromPerk)) return;
    if (!_perkRegistry.TryGet(toPerkId, out var toPerk)) return;
    
    // 两者应在同一棵树上——从 master 反查验证
    if (fromPerk.Master == null || toPerk.Master == null) return;
    if (fromPerk.Master != toPerk.Master) return;
    
    var graph = fromPerk.Master.relationGraphOwner?.graph as PerkRelationGraph;
    if (graph == null) return;
    
    // 确保两者在图中都有节点
    var fromNode = graph.GetRelatedNode(fromPerk) ?? graph.AddNode<PerkRelationNode>(Vector2.zero);
    fromNode.relatedNode = fromPerk;
    var toNode = graph.GetRelatedNode(toPerk) ?? graph.AddNode<PerkRelationNode>(Vector2.zero);
    toNode.relatedNode = toPerk;
    
    // NodeCanvas 标准 API：Node.ConnectTo(target)
    fromNode.ConnectTo(toNode);
}
```

### 5.2 `AddPerkBehaviour<T>` 新增（Identifier 化）

```csharp
/// <summary>在已有 Perk 上挂载自定义 PerkBehaviour。perkId 为 Identifier。</summary>
public static T AddPerkBehaviour<T>(Identifier perkId) where T : PerkBehaviour
{
    if (!_perkRegistry.TryGet(perkId, out var perk)) return null;
    return perk.gameObject.AddComponent<T>();
}
```

### 5.3 `RegisterPerkTree` 新增（Identifier 化）

```csharp
/// <summary>完整注册一棵自定义 PerkTree，含 LevelConfig patch。</summary>
/// <param name="id">Identifier——Domain=modid, Path=treeID</param>
public static PerkTree RegisterPerkTree(Identifier id, bool horizontal = false)
{
    // 1. 创建 PerkTree GameObject + 挂组件（treeID = id.Path）
    // 2. 创建 PerkRelationGraph（ScriptableObject）+ PerkTreeRelationGraphOwner
    // 3. 注入到 PerkTreeManager.perkTrees
    // 4. 通过新 patch 将 id.Path 注册到 LevelConfig.IsPerkTreeEnabled
}
```

`AddPerk` 签名也同步改为 Identifier 驱动：

```csharp
/// <summary>
/// 在技能树上注册新 Perk。
/// id.Domain → 推导 treeId（如 "DefaultPerkTree"）或从已注册的 PerkTree 查
/// id.Path → perk 唯一名称（兼作 GameObject.name，影响存档 key）
/// </summary>
public static Perk AddPerk(Identifier id, PerkRequirement req, Sprite icon, string modid)
{
    // treeId 推导策略：
    //   1) 若 id.Domain 匹配某个已注册 PerkTree 的 Identifier.Domain → 用该树
    //   2) 否则尝试 id.Domain 作为原生 treeId（如 "DefaultPerkTree"）
    var treeId = ResolveTreeId(id.Domain);
    var tree = PerkTreeManager.GetPerkTree(treeId);
    // ... 后续逻辑与现有一致，perkName = id.Path
}
```

### 5.4 新增 Patch 文件

```
FastModdingLib/PerkTrees/Patches/
├── PerkTreeEnablePatch.cs    (新增 ~40 LOC)
│   └── [HarmonyPatch(typeof(LevelConfig), "IsPerkTreeEnabled")]
│       [HarmonyPrefix] → 自定义 treeId 时返回 true
│
└── PerkTreeCollectGuard.cs   (新增 ~30 LOC)
    └── [HarmonyPatch(typeof(PerkTree), "Collect")]
        [HarmonyPrefix] → 若树 ID 来自 FML 注册表，跳过 Collect 清空
```

### 5.5 文件布局变更

```
FastModdingLib/PerkTrees/
├── PerkTreeUtils.cs          (修改：ConnectPerks 重写 + AddPerkBehaviour + RegisterPerkTree)
├── PerkTreeRegistry.cs       (不变)
└── Patches/
    ├── PerkTreeEnablePatch.cs    (新增)
    └── PerkTreeCollectGuard.cs   (新增)
```

### 5.6 验收
- `ConnectPerks` 不再输出 try/catch Error 日志
- 自定义 PerkTree 在游戏内 PerkTreeView 中可选，Behaviour 可正常触发
- `RegisterPerkTree → AddPerk → AddPerkBehaviour → ConnectPerks → ForceUnlock` 全流程可用

---

## 6. E1 — Endowment 完整实现

### 6.1 新建文件布局

```
FastModdingLib/
├── Endowment/
│   ├── EndowmentUtils.cs      (~120 LOC)  公共 API
│   ├── EndowmentRegistry.cs   (~50 LOC)   SimpleRegistry<EndowmentEntry> + OnRemoved
│   └── Patches/
│       └── EndowmentManagerPatch.cs (~50 LOC)  Awake Postfix 注入 + SelectIndex Prefix
```

### 6.2 `EndowmentUtils` API 契约（全部 Identifier 优先）

```csharp
namespace FastModdingLib
{
    public static class EndowmentUtils
    {
        // —— 生命周期 ——
        internal static void Init();    // 幂等，注册到 RegistryManager 元表

        // —— 注册/卸载（Identifier 优先） ——
        /// <summary>
        /// 注册自定义天赋。FML 内部自动分配 EndowmentIndex（≥10，按注册顺序递增），
        /// 并建立 Identifier → EndowmentIndex 的内部映射。modder 不接触枚举值。
        /// </summary>
        public static void RegisterEndowment(Identifier id, EndowmentEntry endowment, string modid);
        public static bool UnregisterEndowment(Identifier id);
        public static int  UnregisterAllEndowments(string modid);

        // —— 查询（全部走 Identifier） ——
        public static EndowmentEntry? GetEndowment(Identifier id);
        public static bool TryGetEndowment(Identifier id, out EndowmentEntry entry);
        /// <summary>列出指定 mod 注册的全部天赋 Identifier。</summary>
        public static IReadOnlyList<Identifier> GetAllEndowments(string modid);

        // —— 状态操作（Identifier → 内部映射到 EndowmentIndex） ——
        /// <summary>查询天赋是否已解锁。内部从 Identifier 映射到 EndowmentIndex 后调原生 API。</summary>
        public static bool IsEndowmentUnlocked(Identifier id);
        /// <summary>解锁天赋。内部从 Identifier 映射到 EndowmentIndex 后调原生 UnlockEndowment。</summary>
        public static bool UnlockEndowment(Identifier id);
        /// <summary>选择/激活天赋。内部从 Identifier 映射到 EndowmentIndex 后调原生 SelectIndex。</summary>
        public static void SelectEndowment(Identifier id);

        // —— 存档（返回值仍是游戏原生枚举——modder 可用 Identifier 反查） ——
        public static Identifier? GetCurrentSelection();  // 返回当前选中的天赋 Identifier
    }
}
```

**关键设计决策**：`EndowmentIndex` 枚举值由 FML 内部自动分配（从 10 开始递增），modder 只通过 `Identifier` 操作天赋。内部映射表 `Dictionary<Identifier, EndowmentIndex>` 在 `RegisterEndowment` 时建立，在 `UnregisterEndowment` 时回收（被回收的枚举值不会立即复用，避免存档错乱）。

### 6.3 `EndowmentRegistry`

```csharp
public sealed class EndowmentRegistry : SimpleRegistry<EndowmentEntry>
{
    // 内部映射：Identifier → EndowmentIndex（仅 FML 内部可见）
    private readonly Dictionary<Identifier, EndowmentIndex> _indexMap = new();
    private int _nextIndex = 10;  // 自定义天赋从 10 开始分配

    internal EndowmentIndex AllocateIndex(Identifier id)
    {
        var idx = (EndowmentIndex)_nextIndex++;
        _indexMap[id] = idx;
        return idx;
    }

    internal bool TryGetIndex(Identifier id, out EndowmentIndex index)
        => _indexMap.TryGetValue(id, out index);

    internal bool TryGetIdentifier(EndowmentIndex index, out Identifier id)
    {
        foreach (var kvp in _indexMap)
            if (kvp.Value == index) { id = kvp.Key; return true; }
        id = default;
        return false;
    }

    protected override void OnRemoved(Identifier id, EndowmentEntry value, string? modid)
    {
        // 如果当前选中的是这个天赋且正在被卸载 → 重置为 None
        if (_indexMap.TryGetValue(id, out var idx) && EndowmentManager.CurrentIndex == idx)
        {
            typeof(EndowmentManager).GetMethod("SelectIndex",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?.Invoke(EndowmentManager.Instance, new object[] { EndowmentIndex.None });
        }
        _indexMap.Remove(id);
        // 不回收 _nextIndex——避免卸载后重装同一 mod 时 index 漂移
        if (value != null && value.gameObject != null)
            UnityEngine.Object.Destroy(value.gameObject);
    }
}
```

### 6.4 Harmony Patch：`EndowmentManagerPatch.cs`

```
├── [HarmonyPatch(typeof(EndowmentManager), "Awake")]
│   [HarmonyPostfix]
│   → 遍历 EndowmentRegistry，为每个注册的天赋：
│      1) 创建 EndowmentEntry GameObject
│      2) 通过反射设置 index 字段（使用 AllocateIndex 分配的枚举值）
│      3) 注入到 EndowmentManager.entries（反射访问 private List）
│
└── [HarmonyPatch(typeof(EndowmentManager), "SelectIndex")]
    [HarmonyPrefix]
    → 接受自定义 EndowmentIndex（≥10），走相同逻辑（不拦截，仅确保不抛异常）
```

### 6.5 数字 ID 兜底机制

仅在以下极端场景启用数字 ID 强指定（兜底），且走独立的重载方法（非默认路径）：

```csharp
/// <summary>
/// 【兜底】使用强指定的 EndowmentIndex 注册天赋。
/// 仅在需要与既有游戏内容共享枚举空间时使用。触发此 API 即表示 modder 已自行处理冲突。
/// 正常情况下请使用 RegisterEndowment(Identifier, EndowmentEntry, string)。
/// </summary>
public static void RegisterEndowmentWithIndex(Identifier id, EndowmentEntry endowment,
    EndowmentIndex explicitIndex, string modid)
{
    // 不自动分配，直接使用 explicitIndex
    // 若 explicitIndex 已被占用 → 抛 InvalidOperationException 明确报错
}
```

### 6.6 验收
- `RegisterEndowment → UnlockEndowment → SelectEndowment` 完整流程可用
- 自定义天赋在 EndowmentSelectionPanel UI 中可见
- 卸载后 EndowmentManager.entries 中无残留条目

---

## 7. U1 — UI 交互辅助

### 7.1 目标

为 Building/PerkTree/Endowment 系统提供"游戏内交互入口"的创建辅助。不创建新的 UI 系统，而是复用游戏现有交互模式。

### 7.2 内容

#### 7.2.1 交互入口模板（文档性指导）

```
FastModdingLib/UI/InteractTemplates.cs (~40 LOC)

/// Building 系统交互入口：挂载到 Building Prefab 的 functionContainer 上
public class BuildingInteractTemplate : InteractableBase
{
    [SerializeField] private string targetViewType; // "BuilderView" / custom
    protected override void OnInteractFinished() { /* ... */ }
}

/// PerkTree 系统交互入口：挂载到场景物件上
public class PerkTreeInteractTemplate : InteractableBase
{
    [SerializeField] private string perkTreeID;  // 对应 Identifier.Path
    protected override void OnInteractFinished()
    {
        PerkTreeView.Show(PerkTreeManager.GetPerkTree(perkTreeID));
    }
}

/// Endowment 系统交互入口：挂载到基地物件上
public class EndowmentInteractTemplate : InteractableBase
{
    protected override void OnInteractFinished()
    {
        EndowmentSelectionPanel.Show();
    }
}
```

#### 7.2.2 Modding UI 指导文档（新增 Docs 条目）

在 `DecompiledDLL/Docs/` 下新增或更新 `08-ui-system-guide.md` 附录，加入"Building/PerkTree/Endowment 交互入口最佳实践"部分。

### 7.3 验收
- `InteractableBase` 三个子类模板编译通过
- Building/PerkTree/Endowment 文档中各有交互入口示例

---

## 8. ModBehaviour 集成

B1/B2/P1/E1 各阶段的 Harmony patch 统一在 `ModBehaviour.OnAfterSetup()` 中通过 `harmony.PatchAll()` 自动应用。卸载时各模块 `TearDown(modid)` 自动调用 `RemoveAllByOwner`。

```csharp
// FMLBootstrap.cs 或 RegisterBootstrap.cs 扩展：
public static void Init()
{
    // ...既有代码...
    BuildingUtils.Init();    // 幂等
    PerkTreeUtils.Init();    // 幂等
    EndowmentUtils.Init();   // 幂等
}
```

---

## 9. 验收清单（DOD）

### B1/B2 — Building ✅
- [x] `PlaceBuilding(Identifier, Identifier, ...)` 不再抛 `NotSupportedException`
- [x] 自定义建筑在 BuilderView UI 中可见（`GetBuildingsToDisplay` Postfix）
- [x] `GetInfo/GetPrefab` Postfix 正确回退到 BuildingRegistry
- [x] `RegisterBuilding → PlaceBuilding → UnregisterBuilding` 往返后 `BuildingDataCollection.infos` 干净（OnRemoved 清理）
- [x] `GetBuildingInfo` 签名已改为 `Identifier`（旧 `string` 签名标记 `[Obsolete]`）
- [x] `dotnet build` 通过（0 错误）

### P1 — PerkTree ✅
- [x] `ConnectPerks(Identifier, Identifier)` 不再使用 try/catch 包装
- [x] `AddPerkBehaviour<T>(Identifier)` 可用
- [x] `RegisterPerkTree(Identifier)` 可创建完整自定义技能树（含 LevelConfig patch）
- [x] `AddPerk(Identifier, ...)` 内部正确推导 treeId（从 Domain 或注册表）
- [x] 自定义 PerkTree 在 PerkTreeView UI 中可见，Behaviour 正常触发（patch 生效）
- [x] `PerkTree.Collect` 不清空运行时注入的 Perk（patch guard 生效）
- [x] `dotnet build` 通过（0 错误）

### E1 — Endowment ✅
- [x] `EndowmentUtils` 完整 API 可用——所有 public 方法接受 `Identifier`，无裸 `EndowmentIndex` 暴露
- [x] `EndowmentRegistry` 内部自动分配 `EndowmentIndex`（≥10），映射表正确
- [x] `SelectEndowment(Identifier)` → 内部查映射表 → 调原生 `SelectIndex`
- [x] `EndowmentManager.Awake` Postfix 注入自定义天赋到列表
- [x] 自定义天赋在 EndowmentSelectionPanel 中可见可选
- [x] 卸载后残留去除，`CurrentIndex` 安全降级为 None
- [x] 兜底 `RegisterEndowmentWithIndex(...)` 可用
- [x] `dotnet build` 通过（0 错误）

### U1 — UI 交互 ✅
- [x] 三个 `InteractableBase` 子类模板编译通过
- [x] 文档更新（USAGE.md 新增 §15 EndowmentUtils，更新 §13 BuildingUtils、§14 PerkTreeUtils）

### 全量 ✅
- [x] `dotnet build` 通过（0 错误）；不新增第三方包引用
- [x] 不修改 `0Harmony.dll` 引用方式（版本硬性锁定 2.4.1.0）
- [x] **所有新增/修改的 public API 签名均使用 `Identifier`——无裸 `string ID` 或 `int/enum ID` 参数**
- [x] PLAN.md 覆盖率矩阵更新

---

## 10. 不在本计划范围

- **不**实现完整的自定义 UI View（如新的 BuilderView 子类）——仅做 Harmony patch 注入现有 UI
- **不**创建 Endowment 的 AssetBundle prefab 加载路径——`EndowmentEntry` 通过代码创建
- **不**做 PerkTree 与 Quest 系统的深度集成——Quest 条件引用 Perk 的路径已有游戏原生支持
- **不**实现 BuildingEffect 的声明式 API——modder 通过 Prefab 挂 `BuildingEffect` 组件即可
- **不**修改 PLAN.md 主文档 Phase 编号

---

## 11. 风险与对策

| 风险 | 说明 | 对策 |
|------|------|------|
| `BuildingSelectionPanel.GetBuildingsToDisplay` 可能被混淆或签名变更 | static 方法 Patch 对方法名敏感 | Postfix 签名用 Harmony 的 `__result` 参数 + `HarmonyArgument` 确保兼容 |
| NodeCanvas `Node.ConnectTo` 在运行时可能不可用 | 取决于游戏打包的 NodeCanvas 版本 | 如 `ConnectTo` 不可用，回退到 `graph.ConnectNodes(from, to)` |
| `EndowmentManager.entries` 是 `private List` | 反射写入可能被 IL2CPP 阻断 | 仅 Editor/Mono 运行时支持；IL2CPP 发布版无法使用自定义天赋（这是已知限制） |
| PerkTree 的 `perks` 是 `internal List` | Publicizer 已处理 `TeamSoda.Duckov.Core`，但运行时行为需验证 | 已通过 Publicizer 公开；运行时注入后调 `tree.Collect()` 重新扫描 |
| `LevelConfig.IsPerkTreeEnabled` 可能检查硬编码的 PerkTreeIDList | 自定义 treeId 不在 SO 中 | Prefix patch 拦截自定义 treeId 直接返回 true |
| **Identifier 命名空间冲突** | 两个 mod 使用相同 `Identifier` 注册资源 | `NonAlterableSimpleRegistry.SetIfAbsent` 默认拒绝重复 key；冲突时抛明确异常 |
| **数字 ID 内部映射漂移** | Endowment 卸载后重装，`EndowmentIndex` 可能变化导致存档不匹配 | 卸载时回收枚举值但不上移 `_nextIndex`；存档中存储 `Identifier` 字符串而非数字 ID，读档时通过 Registry 反查 |
| **旧 API 兼容** | Building/PerkTree 当前 public API 使用 `string` 参数，改为 `Identifier` 后破坏调用方 | 库未发布 v1，无外部调用方；仅本仓内调用点一次性迁移。旧 `string` 签名可保留为 `[Obsolete]` 重载过渡，内部转调新 API |

---

## 12. 后续可演进项（future）

- **Endowment 的 AssetBundle prefab 加载**——modder 用 Unity 制作 EndowmentEntry prefab 通过 AssetRef 引用
- **PerkTree UI 连线可视化**——自定义 PerkTree 的 Graph 节点布局自动计算（避免所有节点叠在 (0,0)）
- **Building 的 BuildingEffect 声明式 API**——FML 包装 `ModifierDescription` DTO，自动创建 BuildingEffect 组件
- **Endowment 与 PerkTree 的解锁联动**——Perk 解锁后自动解锁对应 Endowment

---

## 13. 案例驱动设计验证

以下三个真实 modder 场景用于验证 Phase 4 API 设计是否满足端到端需求。每个案例标注：
- ✅ 已有接口满足
- 🆕 需新增接口
- 🎨 需 Unity 编辑器制作

---

### 案例 A：新商店 → Building 集成 → 基地放置 → NPC 商人 → 任务 + PerkTree

**场景**：modder 创建一家"赏金商人"商店，作为可建造的建筑放置在基地中，该建筑有自己的 3D 模型和渲染。建造后生成一个商人 NPC（带自定义外观），商人提供专属任务和 PerkTree。

#### A.1 完整流水线

```
步骤 1 ── 创建商店数据
├── ShopUtils.AddGoods(Identifier("bountymod","shop"), ShopGoodsData)
│   └── 接口状态：✅ ShopUtils 已有 AddGoods
│
步骤 2 ── 注册建筑
├── BuildingUtils.RegisterBuilding(Identifier("bountymod","shop_bld"), buildingInfo, prefab)
│   └── 接口状态：✅ BuildingUtils.RegisterBuilding 已存在
│   └── 🎨 Unity 编辑器：需制作 Building Prefab（含 3D 模型 + Building 组件 + functionContainer）
│
步骤 3 ── 建造建筑（玩家在基地 BuilderView 中操作）
├── BuildingUtils.PlaceBuilding(Identifier("base","area1"), Identifier("bountymod","shop_bld"), coord, rot)
│   └── 接口状态：⚠️ B1 待实现（当前抛 NotSupportedException）
│
步骤 4 ── 建筑建成后生成商人 NPC
├── 监听 BuildingManager.OnBuildingBuilt 事件 → 当 guid 匹配时 spawn NPC
│   └── 接口状态：✅ EventBus 已桥接 GameEventAdapters
│   └── 🆕 需新增：FML 内建的 "建筑建成回调" 便捷包装（见 A.4）
│
步骤 5 ── 创建商人 NPC
├── EnemyUtils.RegisterEnemy(Identifier("bountymod","merchant"), aiConfig, preset)
├── EnemyUtils.SpawnEnemy(Identifier("bountymod","merchant"), buildingPosition)
│   └── 接口状态：✅ EnemyUtils 已实现
│   └── 🎨 Unity 编辑器：需制作 CharacterRandomPreset（ScriptableObject）、
│       CharacterModel prefab（3D 模型 + 骨骼）
│
步骤 6 ── 给商人添加任务
├── QuestUtils.RegisterQuest(questData, "bountymod")
├── QuestUtils.AddQuestRelation(questId, before, after)
│   └── 接口状态：✅ QuestUtils 已有完整 API
│   └── 🎨 Unity 编辑器（可选）：Quest 数据可在代码中构造或 ScriptableObject 加载
│
步骤 7 ── 给商人添加 PerkTree
├── PerkTreeUtils.RegisterPerkTree(Identifier("bountymod","merchant_perks"))
├── PerkTreeUtils.AddPerk(Identifier("bountymod","merchant_perks/perk_fast_trade"), req, icon, modid)
├── PerkTreeUtils.ConnectPerks(
│       Identifier("bountymod","merchant_perks/perk_root"),
│       Identifier("bountymod","merchant_perks/perk_fast_trade"))
│   └── 接口状态：⚠️ P1 待实现（RegisterPerkTree + AddPerk Identifier 化）
│   └── 🎨 Unity 编辑器（可选）：Perk 图标 Sprite
│
步骤 8 ── 卸载回滚
├── ModBehaviour.OnBeforeDeactivate → RegistryManager.RemoveAllByOwner("bountymod")
│   └── 自动卸载：商店 → 建筑 → NPC → 任务 → Perk → 一键清理
│   └── 接口状态：✅ Register 一体化已完成
```

#### A.2 接口满足度检查

| 步骤 | 所需 FML API | 状态 | 备注 |
|------|-------------|------|------|
| 1 | `ShopUtils.AddGoods(Identifier, ShopGoodsData, modid)` | ✅ | 已存在 |
| 2 | `BuildingUtils.RegisterBuilding(Identifier, BuildingInfo, Building)` | ✅ | 已存在 |
| 3 | `BuildingUtils.PlaceBuilding(Identifier, Identifier, Vector2Int, BuildingRotation)` | 🆕 | B1 实现 |
| 4 | 「建筑建成 → spawn NPC」便捷包装 | 🆕 | 见 A.4 |
| 5 | `EnemyUtils.RegisterEnemy` + `SpawnEnemy` | ✅ | 已存在 |
| 6 | `QuestUtils.RegisterQuest` + `AddQuestRelation` | ✅ | 已存在 |
| 7 | `PerkTreeUtils.RegisterPerkTree` + `AddPerk` + `ConnectPerks` | 🆕 | P1 实现 |
| 8 | `RemoveAllByOwner("bountymod")` | ✅ | 已存在 |

#### A.3 需要 Unity 编辑器制作的内容

| 资产 | 工具 | 说明 |
|------|------|------|
| Building Prefab | Unity Editor | 3D 模型 + `Building` 组件（id, dimensions, graphicsContainer, functionContainer）。functionContainer 挂交互脚本 |
| CharacterRandomPreset | Unity Editor（ScriptableObject） | 商人 NPC 配置：health, team, AI 行为树等 |
| CharacterModel Prefab | Unity Editor | 商人 NPC 3D 模型 + 骨骼 + 动画 |
| Perk 图标 Sprite | Unity Editor / 外部工具 | PNG → Sprite |
| 全部 Unity 资产打包为 AssetBundle | Unity Editor | `.bundle` 文件，随 mod 分发 |

#### A.4 🆕 需新增接口：「建筑建成回调」便捷包装

```csharp
// BuildingUtils 新增：
/// <summary>
/// 注册建筑建成回调。当指定 buildingId 的建筑建造完成时触发。
/// FML 内部订阅 BuildingManager.OnBuildingBuiltComplex，按 guid 匹配。
/// </summary>
public static void OnBuildingBuilt(Identifier buildingId, Action<Building> callback, string modid);
public static void OffBuildingBuilt(Identifier buildingId, Action<Building> callback);
```

> 该接口是便捷包装，非必须——modder 可直接订阅 `BuildingManager.OnBuildingBuiltComplex`。
> 但考虑到"建筑建成 → 生成 NPC"是常见模式，FML 提供此包装可减少样板代码。

---

### 案例 B：新工作台 → Building 集成 → 基地放置

**场景**：modder 创建"附魔工作台"，作为可建造建筑放置在基地中。交互时打开自定义工作台 UI（复用 CraftView 模式），支持消耗材料给装备附加 Effect。

#### B.1 完整流水线

```
步骤 1 ── 注册建筑
├── BuildingUtils.RegisterBuilding(Identifier("enchantmod","enchant_table"), buildingInfo, prefab)
│   └── ✅ 已存在
│   └── 🎨 Unity 编辑器：Building Prefab（functionContainer 挂 InteractableBase 子类）
│
步骤 2 ── 注册工作台配方（如果有专属配方）
├── CraftingUtils.AddCraftingFormula(formulaId, money, costItems, resultItemId, ...)
│   └── ✅ 已存在（CraftingData 声明式 API）
│
步骤 3 ── 工作台交互入口
├── Building Prefab 的 functionContainer 挂 EnchantWorkbench : InteractableBase
│   └── OnInteractFinished() → 打开自定义 View（如 EnchantView）
│   └── 🎨 Unity 编辑器：需在 Prefab 上挂载脚本组件
│
步骤 4 ── 自定义工作台 View
├── 新建 EnchantView : View（继承游戏原生 View 基类）
│   └── 接口状态：❌ FML 不提供 View 创建——modder 自行在 Unity 中制作 Canvas Prefab
│   └── 🎨 Unity 编辑器：Canvas + View 组件 + 物品栏显示 + 按钮
│
步骤 5 ── 附魔逻辑（消耗材料 → 给装备加 Effect）
├── ItemStatsSystem.Effect（游戏原生）或 ItemUtils 辅助
│   └── 接口状态：⚠️ FML 无 Effect 创建封装——modder 需直接操作游戏 API
│   └── 🆕 建议新增：ItemUtils.AddEffect(Item, EffectData)（见 B.3）
```

#### B.2 接口满足度检查

| 步骤 | 所需 FML API | 状态 | 备注 |
|------|-------------|------|------|
| 1 | `BuildingUtils.RegisterBuilding` | ✅ | |
| 2 | `CraftingUtils.AddCraftingFormula` | ✅ | |
| 3 | `InteractableBase` 子类（modder 自行实现） | ⚠️ | FML 提供 U1 模板，modder 按模板写 |
| 4 | 自定义 View（Canvas Prefab） | ❌ | FML 不封装——modder 在 Unity 中制作并通过 AssetBundle 加载 |
| 5 | `ItemUtils` Effect 封装 | 🆕 | 见 B.3 |

#### B.3 🆕 建议新增接口：物品 Effect 辅助

```csharp
// ItemUtils 新增（低优先级，非本 Phase 强制交付）：
/// <summary>给物品添加一个 Effect 组件。</summary>
public static void AddEffect(Item item, EffectData effect);

/// <summary>从物品移除所有来自指定 mod 的 Effect。</summary>
public static void RemoveEffects(Item item, string modid);
```

> 这不是 Phase 4 的强制交付——modder 可直接操作 `ItemStatsSystem.Effect` 游戏原生 API。
> 若后续 Phase 中此模式反复出现，再提升优先级。

#### B.4 需要 Unity 编辑器制作的内容

| 资产 | 说明 |
|------|------|
| Building Prefab（附魔台 3D 模型） | 含 Building 组件 + functionContainer（挂 EnchantWorkbench 脚本） |
| EnchantWorkbench.cs | InteractableBase 子类——C# 代码（modder 编写） |
| EnchantView Canvas Prefab | 工作台 UI——Unity Canvas + View 组件（modder 制作） |
| AssetBundle 打包 | 含 Building Prefab + Canvas Prefab |

---

### 案例 C：新 Endowment（天赋）

**场景**：modder 创建"暗杀者"天赋——移动速度 +15%，最大生命 -10%。需完成特定任务后解锁，在 EndowmentSelectionPanel 中可选。

#### C.1 完整流水线

```
步骤 1 ── 注册天赋
├── EndowmentUtils.RegisterEndowment(Identifier("assassinmod","assassin"), entry, modid)
│   └── 接口状态：🆕 E1 实现
│   └── entry 通过代码创建：new GameObject → AddComponent<EndowmentEntry> → 反射设字段
│
步骤 2 ── 设置天赋效果（ModifierDescription）
├── 通过反射设置 EndowmentEntry.modifiers 字段
│   └── 内容：[{statKey="moveSpeed", type=PercentageAdd, value=0.15f},
│             {statKey="maxHealth", type=PercentageAdd, value=-0.1f}]
│   └── 接口状态：⚠️ 当前需反射——🆕 建议新增 RegisterEndowment 接受 ModifierDescription[] 参数
│
步骤 3 ── 设置解锁条件
├── 任务完成时调 EndowmentUtils.UnlockEndowment(Identifier("assassinmod","assassin"))
│   └── 接口状态：🆕 E1 实现（内部映射到 EndowmentIndex → 调原生 UnlockEndowment）
│
步骤 4 ── 玩家选择天赋
├── EndowmentSelectionPanel 中可见"暗杀者"选项（Postfix 注入生效后）
├── 玩家确认 → EndowmentUtils.SelectEndowment(Identifier("assassinmod","assassin"))
│   └── 接口状态：🆕 E1 实现
│
步骤 5 ── 卸载回滚
├── EndowmentUtils.UnregisterEndowment(Identifier("assassinmod","assassin"))
│   └── 若当前选中此天赋 → 自动重置为 None
│   └── 接口状态：🆕 E1 实现
```

#### C.2 接口满足度检查

| 步骤 | 所需 FML API | 状态 | 备注 |
|------|-------------|------|------|
| 1 | `EndowmentUtils.RegisterEndowment(Identifier, EndowmentEntry, modid)` | 🆕 | E1 |
| 2 | ModifierDescription 设置 | 🆕 | 建议 RegisterEndowment 接受 `ModifierDescription[]` 参数而非强制反射 |
| 3 | `EndowmentUtils.UnlockEndowment(Identifier)` | 🆕 | E1 |
| 4 | `EndowmentUtils.SelectEndowment(Identifier)` | 🆕 | E1 |
| 5 | `EndowmentUtils.UnregisterEndowment(Identifier)` | 🆕 | E1 |

#### C.3 🆕 API 设计修正

基于案例 C，`RegisterEndowment` 应直接接受效果描述数组：

```csharp
/// <summary>
/// 注册自定义天赋。
/// </summary>
/// <param name="id">Identifier，如 ("assassinmod","assassin")</param>
/// <param name="modifiers">效果描述数组。FML 内部设置到 EndowmentEntry.modifiers。</param>
/// <param name="unlockedByDefault">是否默认解锁（默认 false，需任务解锁）。</param>
/// <param name="requirementText">解锁条件提示文本（本地化 key）。</param>
/// <param name="modid">注册者 mod 标识。</param>
public static void RegisterEndowment(
    Identifier id,
    ModifierDescription[] modifiers,
    bool unlockedByDefault = false,
    string requirementText = "",
    string modid = "")
```

这样 modder 无需反射设置 `EndowmentEntry` 字段，完全通过 FML API 完成。

#### C.4 需要 Unity 编辑器制作的内容

| 资产 | 说明 |
|------|------|
| 天赋图标 Sprite（可选） | 如果 modder 想自定义图标，制作 PNG → Sprite → AssetBundle |
| 无需 Building Prefab | Endowment 是纯数据 + 代码，不涉及 3D 模型 |
| 无需 NPC | Endowment 在基地 UI 中操作 |

> **注意**：Endowment 系统的游戏原生 UI（`EndowmentSelectionPanel`）已经存在，FML 通过 patch 注入自定义天赋即可——modder **不需要**制作新的 Canvas Prefab。

---

### 案例汇总：接口缺口清单

| 缺口 | 所属阶段 | 触发案例 | 类型 |
|------|---------|---------|------|
| `PlaceBuilding(Identifier, Identifier, ...)` 反射公开 | B1 | A 步骤 3, B 步骤 2 | 🆕 实现 |
| `OnBuildingBuilt` 便捷回调 | B2 | A 步骤 4 | 🆕 新增 |
| `RegisterPerkTree(Identifier)` | P1 | A 步骤 7 | 🆕 新增 |
| `AddPerk(Identifier, ...)` Identifier 化 | P1 | A 步骤 7 | 🆕 修改签名 |
| `ConnectPerks(Identifier, Identifier)` | P1 | A 步骤 7 | 🆕 修改签名 |
| `EndowmentUtils` 全部 API | E1 | C 全部步骤 | 🆕 从零实现 |
| `RegisterEndowment` 接受 `ModifierDescription[]` | E1 | C 步骤 2 | 🆕 设计修正 |
| `ItemUtils.AddEffect` 封装 | 后续 | B 步骤 5 | 🔮 建议后续 |
| **`EnemyPresetData` DTO 封装 CharacterRandomPreset** | **§14** | **A 步骤 5** | **🆕 新增** |

### 案例汇总：必须 Unity 编辑器制作

| 资产类型 | 涉及案例 | 制作方式 | 参考章节 |
|---------|---------|---------|---------|
| Building 自定义 3D 模型（可选） | A, B | Unity Editor → AssetBundle → `SetBuildingModel()` 注入 | §15.2 |
| CharacterModel 自定义模型（可选） | A | Unity Editor → AssetBundle → `ModelRef.FromBundle()` | §14.2 |
| Canvas Prefab 复杂 UI（可选） | B | Unity Editor → AssetBundle | §16.6 |
| **Building 图标** | A, B | 🟢 `ItemUtils.LoadSprite("name", 0)` → `assets/textures/name.png` | §15.3 |
| **Perk/Skill 图标** | A | 🟢 `ItemUtils.LoadSprite("name", 0)` | §15.3 |
| **Endowment 图标（可选）** | C | 🟢 `ItemUtils.LoadSprite("name", 0)` | §15.3 |

> **图标零 Unity 编辑器依赖**：所有 Sprite 图标通过 FML 已有的 `ItemUtils.LoadSprite` 从 `assets/textures/` 目录的 PNG 文件运行时加载，modder 直接将 PNG 放入 mod 文件夹即可。**不需要 Unity 编辑器导入 Sprite。**

---

## 14. CharacterRandomPreset 封装（代码端优先）

### 14.1 设计原则：modder 不应被迫使用 Unity 编辑器

当前 `EnemyUtils.RegisterEnemy` 直接接受 `CharacterRandomPreset preset`——modder 必须在 Unity 中通过 `[CreateAssetMenu]` 创建 `.asset` 文件。这违反了 FML 的代码端优先原则。

> **核心理念**：FML 应让 modder **纯代码**完成 90% 的配置工作。仅在需要自定义 3D 模型/材质/动画时才涉及 Unity 编辑器。

### 14.2 封装方案：`EnemyPresetData` DTO

新建 `FastModdingLib/Entities/EnemyPresetData.cs`：

```csharp
namespace FastModdingLib
{
    public class EnemyPresetData
    {
        // ===== 必填标识 =====
        public string NameKey { get; set; }     // 本地化 key（兼 kill counter key）

        // ===== 基础属性 =====
        public Teams Team { get; set; } = Teams.scav;
        public float Health { get; set; } = 100f;
        public int Exp { get; set; } = 100;
        public bool IsBoss { get; set; }
        public bool ShowHealthBar { get; set; } = true;
        public bool HasSoul { get; set; } = true;

        // ===== AI 战斗参数 =====
        public float SightDistance { get; set; } = 17f;
        public float SightAngle { get; set; } = 100f;
        public float ReactionTime { get; set; } = 0.2f;
        public float HearingAbility { get; set; } = 1f;
        public float PatrolRange { get; set; } = 8f;
        public bool CanDash { get; set; }
        public float DamageMultiplier { get; set; } = 1f;
        public float MoveSpeedFactor { get; set; } = 1f;
        public bool CanTalk { get; set; } = true;

        // ===== 掉落 =====
        public bool DropBoxOnDead { get; set; } = true;
        public float HasCashChance { get; set; }
        public Vector2Int CashRange { get; set; }

        // ===== 🆕 模型引用（代码端优先——引用已有模型，不需要 Unity 编辑器） =====
        /// <summary>
        /// CharacterModel 来源。
        /// - 若为字符串：游戏已有 CharacterModel 的 prefab 名称
        ///   （如 "CharacterModel_Default"、"CharacterModel_Scav"）
        /// - 若为 AssetBundle + 路径：自定义模型（需 Unity 编辑器制作）
        /// 默认使用游戏通用 Scav 模型。
        /// </summary>
        public ModelRef Model { get; set; } = ModelRef.GamePrefab("CharacterModel_Default");

        // ===== 元素抗性 =====
        public float ElementFactor_Physics { get; set; } = 1f;
        public float ElementFactor_Fire { get; set; } = 1f;
        // ...

        internal CharacterRandomPreset ToNative() { /* 内部转换，同前 */ }
    }

    /// <summary>模型引用：代码端优先，AssetBundle 作为高级选项。</summary>
    public struct ModelRef
    {
        public string GamePrefabName { get; set; }   // 引用游戏已有 prefab
        public string BundleName { get; set; }        // AssetBundle 名（可选）
        public string AssetPath { get; set; }          // bundle 内路径（可选）

        public static ModelRef GamePrefab(string name)
            => new() { GamePrefabName = name };
        public static ModelRef FromBundle(string bundle, string path)
            => new() { BundleName = bundle, AssetPath = path };
    }
}
```

### 14.3 变更后对比

```csharp
// ❌ 旧：modder 必须在 Unity 中创建 ScriptableObject
var preset = bundle.LoadAsset<CharacterRandomPreset>("MyEnemy");
EnemyUtils.RegisterEnemy(id, aiConfig, preset);

// ✅ 新：纯代码，引用游戏已有模型
var data = new EnemyPresetData
{
    NameKey = "NPC_Merchant_Bounty",
    Team = Teams.middle,
    Health = 500f,
    Model = ModelRef.GamePrefab("CharacterModel_Scav"),  // 复用已有模型
};
EnemyUtils.RegisterEnemy(id, aiConfig, data);

// 🔮 高级：需要自定义 3D 模型时才用 Unity 编辑器
var data2 = new EnemyPresetData
{
    NameKey = "Boss_Custom",
    Model = ModelRef.FromBundle("my_bundle", "CustomBossModel"),
};
```

---

## 15. Building Prefab 制作说明（代码端优先）

### 15.0 代码端 vs Unity 编辑器路径

| 场景 | 推荐路径 | modder 需要做什么 |
|------|---------|------------------|
| **使用游戏已有模型** | 🟢 纯代码 | 引用已有 Building Prefab 名称 |
| **简单立方体建筑** | 🟢 纯代码 | 一行 `CreateSimpleBuilding()` |
| **自定义 3D 模型** | 🟡 Unity 编辑器 | AssetBundle 打包（高级用法） |

> **设计目标**：90% 的 Building mod 可以通过纯代码完成。Unity 编辑器仅在需要独特视觉外观时才使用。

### 15.1 🆕 代码端：`CreateSimpleBuilding`（无需 Unity 编辑器）

```csharp
// BuildingUtils 新增：
/// <summary>
/// 代码端创建简易 Building Prefab（无需 Unity 编辑器）。
/// 自动创建带 Building 组件 + 基础 Cube 模型 + 网格碰撞体的 GameObject。
/// 适用于功能性建筑（商店工作台等），视觉上使用简单几何体或游戏已有模型。
/// </summary>
/// <param name="dimensions">占地尺寸，如 (2,2)。</param>
/// <param name="modelRef">模型来源：
///   - null：使用默认 Cube（1x1 白色立方体）
///   - 字符串：引用游戏已有 prefab 名称（如 "Building_Workbench" 复用其 Graphics）</param>
public static Building CreateSimpleBuilding(
    Identifier id, Vector2Int dimensions,
    string? existingPrefabName = null)
{
    if (existingPrefabName != null)
    {
        // 路径 A：克隆游戏已有 Building Prefab 的 Graphics 部分
        var existingPrefab = BuildingDataCollection.GetPrefab(existingPrefabName);
        if (existingPrefab != null)
        {
            var clone = UnityEngine.Object.Instantiate(existingPrefab);
            // 修改 id 和 dimensions
            typeof(Building).GetField("id", ...).SetValue(clone, id.Path);
            typeof(Building).GetField("dimensions", ...).SetValue(clone, dimensions);
            return clone;
        }
    }

    // 路径 B：纯代码创建（默认 Cube 模型）
    var go = new GameObject($"Building_{id.Path}");
    var building = go.AddComponent<Building>();

    // 创建 graphicsContainer
    var graphics = new GameObject("Graphics");
    graphics.transform.SetParent(go.transform);
    var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
    cube.transform.SetParent(graphics.transform);
    cube.transform.localScale = new Vector3(dimensions.x, 2f, dimensions.y);

    // 创建 functionContainer（空容器，modder 后续挂脚本）
    var func = new GameObject("Function");
    func.transform.SetParent(go.transform);
    func.layer = 8; // Interact layer
    var collider = func.AddComponent<BoxCollider>();
    collider.size = new Vector3(dimensions.x, 2f, dimensions.y);
    collider.isTrigger = true;

    // 反射设置 Building 字段
    SetBuildingField(building, "id", id.Path);
    SetBuildingField(building, "dimensions", dimensions);
    SetBuildingField(building, "graphicsContainer", graphics);
    SetBuildingField(building, "functionContainer", func);
    return building;
}
```

### 15.2 🆕 自定义模型注入：`SetBuildingModel`（无需 Unity 编辑器重建 Prefab）

当 modder 有自定义 3D 模型（通过 AssetBundle 加载）时，无需重建整个 Building Prefab——只需将模型注入到已有 Building 的 `graphicsContainer` 中：

```csharp
// BuildingUtils 新增：
/// <summary>
/// 将自定义 3D 模型注入到 Building 的 graphicsContainer 中。
/// 替换（或追加到）graphicsContainer 下的子物体。
/// </summary>
/// <param name="buildingId">已注册的建筑 Identifier。</param>
/// <param name="modelPrefab">自定义模型 prefab（可从 AssetBundle 加载）。</param>
/// <param name="replaceExisting">是否替换 graphicsContainer 下现有子物体（默认 true）。</param>
public static void SetBuildingModel(
    Identifier buildingId, GameObject modelPrefab, bool replaceExisting = true)
{
    if (!_buildingRegistry.TryGet(buildingId, out var info)) return;

    var prefab = _buildingRegistry.TryGetPrefab(buildingId.Path, out var b) ? b : null;
    if (prefab == null) return;

    var graphics = prefab.graphicsContainer;
    if (graphics == null) return;

    // 清理旧模型
    if (replaceExisting)
    {
        foreach (Transform child in graphics.transform)
            UnityEngine.Object.Destroy(child.gameObject);
    }

    // 注入新模型
    var model = UnityEngine.Object.Instantiate(modelPrefab, graphics.transform);
    model.transform.localPosition = Vector3.zero;
    model.transform.localRotation = Quaternion.identity;
    model.transform.localScale = Vector3.one;
}
```

**完整流程：代码端 Building + 自定义模型（无 Unity 编辑器创建 Prefab）**：

```csharp
// 1. 从 AssetBundle 加载自定义 3D 模型（唯一需要 Unity 编辑器的步骤）
var bundle = AssetUtil.LoadBundle(modPath, "my_models");
var shopModel = bundle.LoadAsset<GameObject>("BountyShop_Model");  // 仅 3D 模型，非完整 Prefab

// 2. 代码端创建 Building（复用 Workbench 结构）
var building = BuildingUtils.CreateSimpleBuilding(
    Identifier("bountymod", "shop_bld"),
    new Vector2Int(3, 2),
    existingPrefabName: "Building_Workbench"  // 复用其 Function 容器（交互区域）
);

// 3. 注入自定义模型
BuildingUtils.SetBuildingModel(
    Identifier("bountymod", "shop_bld"), shopModel);

// 4. 图标从 PNG 加载（无需 Unity 编辑器导入 Sprite）
var icon = ItemUtils.LoadSprite("bounty_shop_icon", 0);
// 文件路径: {mod}/assets/textures/bounty_shop_icon.png

// 5. 配置并注册
var info = new BuildingInfo
{
    id = "BountyShop",
    prefabName = "Building_BountyShop",
    maxAmount = 1,
    cost = new Cost(money: 5000),
    requireBuildings = new[] { "Workbench_A" },
    iconReference = icon   // ← 从 PNG 加载的 Sprite
};
BuildingUtils.RegisterBuilding(Identifier("bountymod", "shop_bld"), info, building);
```

> **相比原方案的改进**：modder 不需要在 Unity 编辑器中创建完整 Building Prefab——只需要制作 3D 模型（FBX → AssetBundle），Building 的结构和交互由 FML 代码端管理。

### 15.3 图标加载：使用已有 `ItemUtils.LoadSprite`

FML 已有 `ItemUtils.LoadSprite`（位于 `FastModdingLib/Items/ItemUtils.cs`）：

```csharp
// 自动推导 modid（从调用方程序集名）
public static Sprite? LoadSprite(string resourceName, int NEW_ITEM_ID);

// 显式指定 Identifier
public static Sprite? LoadSprite(Identifier id, int NEW_ITEM_ID);

// 直接指定目录
public static Sprite? LoadSpriteFromDir(string modDirectory, string resourceName, int NEW_ITEM_ID);
```

**加载约定**：文件必须放在 `{mod 目录}/assets/textures/{resourceName}.png`。

```csharp
// 示例：加载 Perk 图标
var perkIcon = ItemUtils.LoadSprite("perk_fast_trade", 0);
// → 文件路径: {mod}/assets/textures/perk_fast_trade.png

// 示例：加载 Building 图标
var buildingIcon = ItemUtils.LoadSprite("bounty_shop", 0);
// → 文件路径: {mod}/assets/textures/bounty_shop.png
```

> 此方法已于 Phase 1 实现。Phase 4 中 Building/Perk/Endowment 的 Sprite 加载统一使用此 API，**无需 Unity 编辑器导入 Sprite**。

### 15.4 官方 Building 组件结构参考

基于 `DecompiledDLL/Core/Duckov.Buildings/Building.cs` + 逆向 Prefab 实测：

```
Building : MonoBehaviour, IDrawGizmos
├── [SerializeField] string id              ← 建筑唯一 ID，如 "Workbench"
├── [SerializeField] Vector2Int dimensions  ← 占地尺寸（实测：Workbench {0,0}, Kitchen {3,3}）
├── [SerializeField] GameObject graphicsContainer   ← 美术层（模型/渲染器）
├── [SerializeField] GameObject functionContainer   ← 功能层（交互碰撞体）
├── bool unlockAchievement                  ← 建成时是否解锁成就
│
├── 只读属性: GUID, ID, Dimensions, DisplayName, DisplayNameKey
│
└── 运行时: BuildingManager 管理创建/销毁；
    graphicsContainer 在预览模式关碰撞体；functionContainer 在预览模式设 inactive
```

**实测 dimensions 值**（来自官方 Prefab 逆向）：

| Prefab | dimensions | Graphics 子对象数 | Function 交互点数 |
|--------|-----------|------------------|------------------|
| Building_Workbench | {0,0} | 8（模型+灯光） | 3（Crafting + Formular + Decompose） |
| Building_WorkbenchAdvance | {3,2} | 复杂层级 | 3+ |
| Building_Kitchen | {3,3} | 多个模型子对象 | 1（Cooking 交互） |

### 15.5 Unity 编辑器路径（高级，仅自定义 3D 模型时需要）

> 以下步骤仅在 modder 需要**完全自定义的 3D 模型外观**时才需要。日常开发使用 §15.1-§15.2 的代码端路径即可。

**步骤 1**：Unity 中创建包含 3D 模型的 Building Prefab，层级如下：

```
BuildingRoot (GameObject)
├── [Building] 组件    ← id, dimensions, 引用两个子容器
├── Graphics (GameObject)
│   └── 3D 模型 (MeshFilter + MeshRenderer + 可选 Collider, Layer=19)
└── Function (GameObject, Layer=8)
    └── 交互区域 (BoxCollider(isTrigger) + Interactable 子类)
```

**步骤 2**：打包为 AssetBundle → 代码中加载 → 注册。

> 完整的 Unity 编辑器制作检查清单见原 §15.4。

基于 `DecompiledDLL/Core/Duckov.Buildings/Building.cs` 反编译：

```
Building : MonoBehaviour
├── [SerializeField] string id              ← 建筑唯一 ID，与 BuildingInfo.id 一致
├── [SerializeField] Vector2Int dimensions  ← 占地尺寸，如 (2,2) = 2×2 格
├── [SerializeField] GameObject graphicsContainer   ← 3D 模型子物体（放置时显示）
├── [SerializeField] GameObject functionContainer   ← 功能子物体（交互逻辑挂载点）
├── bool unlockAchievement                  ← 建成时是否解锁成就（Key: Building_{id}）
│
├── 只读属性:
│   ├── int GUID          ← 运行时唯一 ID（BuildingManager 分配）
│   ├── string ID         ← 返回 id
│   ├── Vector2Int Dimensions ← 返回 dimensions
│   ├── string DisplayNameKey → "Building_" + ID
│   └── string DisplayName → DisplayNameKey.ToPlainText()
│
└── 关键行为:
    ├── BuildingManager 管理生命周期（创建/销毁）
    ├── graphicsContainer 在预览模式下碰撞体被禁用
    └── functionContainer 在预览模式下设为 inactive
```

##### 官方 BuilderView 结构参考

基于 `DecompiledDLL/Core/Duckov.Buildings.UI/BuilderView.cs` 和 `Duckov.UI/View.cs`：

```
BuilderView : View (继承 ManagedUIElement : MonoBehaviour)
├── 内部模式: enum { None, Placing, Destroying }
│
├── [SerializeField] 关键组件:
│   ├── BuildingSelectionPanel selectionPanel    ← 左侧建筑列表
│   ├── BuildingContextMenu contextMenu          ← 右键菜单
│   ├── GridDisplay gridDisplay                  ← 网格预览
│   └── CinemachineVirtualCamera virtualCamera   ← 建造视角摄像机
│
├── View 基类字段 (Duckov.UI.View):
│   ├── ViewTabs viewTabs        ← 标签页
│   ├── Button exitButton        ← 关闭按钮（自动绑定 Close()）
│   ├── string sfx_Open          ← 打开音效
│   └── string sfx_Close         ← 关闭音效
│
├── View 生命周期:
│   ├── Awake(): 绑定 exitButton + UIInput 导航事件
│   ├── Open(): 设置 ActiveView, DisableInput, Post sfx_Open
│   └── Close(): ActiveView=null, EnableInput, Post sfx_Close
│
└── 交互流程:
    玩家与 BuildingArea 交互 → BuilderViewInvoker.OnInteractFinished()
    → BuilderView.Show(targetArea) → 生成建筑列表 → 放置模式 → BuyAndPlace()
```

##### Unity 编辑器 Building Prefab 制作步骤

#### 步骤 1：创建 Unity 场景

1. 使用与游戏相同的 Unity 版本（参考 `ProjectSettings/ProjectVersion.txt`）
2. 导入游戏 DLL 引用（`TeamSoda.Duckov.Core.dll` 等）
3. 创建空场景用于 Prefab 制作

#### 步骤 2：制作 3D 模型

```
1. 导入建筑 3D 模型（FBX/OBJ）
   ├── 设置 Material（使用游戏兼容的 Shader——URP Lit）
   ├── 添加 MeshCollider（用于放置时的碰撞检测，预览时自动禁用）
   └── 调整 Scale/Rotation 匹配游戏世界比例
```

#### 步骤 3：组装 Building Prefab

```
BuildingRoot (GameObject)                        ← 根节点
├── [Building] 组件                               ← 挂载 Building 脚本
│   ├── Id: "YourBuildingID"                     ← 与 BuildingInfo.id 一致
│   ├── Dimensions: (2, 2)                       ← 占地格数
│   ├── Graphics Container: → graphicsContainer 子物体引用
│   └── Function Container: → functionContainer 子物体引用
│
├── graphicsContainer (GameObject)                ← 美术层
│   ├── 3D 模型 (MeshRenderer + MeshFilter)
│   ├── 碰撞体 (MeshCollider)
│   └── 可选：动画 (Animator)
│
└── functionContainer (GameObject)                ← 功能层
    ├── [InteractableBase 子类]                   ← 交互脚本
    │   └── 如 BountyShopInteract : InteractableBase
    ├── [BuildingEffect]（可选）                   ← 全局效果
    └── 其他功能组件
```

#### 步骤 4：挂载交互脚本（C# 代码）

```csharp
// modder 编写的交互脚本
public class BountyShopInteract : InteractableBase
{
    protected override void OnInteractFinished()
    {
        // 打开自定义商店 View 或复用 StockShopView
        StockShopView.SetupAndShow(myShopData);
    }
}
```

#### 步骤 5：打包 AssetBundle

```
1. 选中 BuildingRoot → Inspector 底部 AssetBundle 标签
   └── 设置 assetBundleName: "bounty_buildings"
2. Unity 菜单: Window → AssetBundle Browser → Build
3. 输出: Assets/AssetBundles/bounty_buildings.bundle
```

#### 步骤 6：modder 代码中加载和注册

```csharp
// ModBehaviour.OnAfterSetup()
var bundle = AssetBundle.LoadFromFile(modPath + "/bounty_buildings.bundle");
var prefab = bundle.LoadAsset<GameObject>("BuildingRoot").GetComponent<Building>();

var info = new BuildingInfo
{
    id = "BountyShop",
    prefabName = "BuildingRoot",
    maxAmount = 1,
    cost = new Cost(money: 5000),
    requireBuildings = new[] { "Workbench_A" },
    iconReference = bundle.LoadAsset<Sprite>("BountyShop_Icon")
};

BuildingUtils.RegisterBuilding(
    Identifier("bountymod", "shop_bld"), info, prefab);
```

##### Building Prefab 检查清单（Unity 编辑器路径）

| 检查项 | 说明 |
|--------|------|
| ✅ Building 组件挂载 | 在根节点，id 字段非空 |
| ✅ graphicsContainer 引用 | 引用到美术子物体 |
| ✅ functionContainer 引用 | 引用到功能子物体 |
| ✅ dimensions 匹配模型 | 2×2 建筑应设 (2,2) |
| ✅ 碰撞体在 graphicsContainer | 不在根节点（预览时禁用碰撞体）|
| ✅ InteractableBase 子类在 functionContainer | 交互逻辑挂载点 |
| ✅ 无脚本丢失（Missing Script） | AssetBundle 打包时所有脚本 DLL 已引用 |
| ✅ AssetBundle 命名 | 不含中文/特殊字符 |

---

## 16. View Canvas 制作说明 + View 封装评估（代码端优先）

### 16.0 代码端 vs Unity 编辑器 vs 注入模式

| 场景 | 推荐路径 | modder 需要做什么 |
|------|---------|------------------|
| **在已有 View 中添加内容** | 🟢 注入模式 | Harmony patch 已有 View 的 `Setup()` 方法 |
| **简单面板（按钮+文字）** | 🟡 代码端 Canvas | 用 FML 提供的 `SimpleViewBuilder` 纯代码构建 |
| **复杂 UI 布局** | 🔴 Unity 编辑器 | AssetBundle 打包（最后手段） |

> **核心理念**：大多数 mod 的 UI 需求是"在已有界面加一个按钮/条目"。注入模式覆盖 80% 的场景。简单面板代码端覆盖 15%。仅 5% 的复杂 UI 才需要 Unity 编辑器。

### 16.1 🆕 注入模式：在已有 View 中添加内容（无需 Unity 编辑器）

```csharp
// 场景：给 BuilderView 的建筑列表加一个自定义建筑按钮
[HarmonyPatch(typeof(BuildingSelectionPanel), "Setup")]
[HarmonyPostfix]
static void Setup_Postfix(BuildingSelectionPanel __instance)
{
    // 在现有列表中注入自定义条目
    var myEntry = BuildingBtnEntry.Create(
        icon: SpriteLoader.FromPng(modPath + "/icon.png"),  // 从 PNG 加载
        name: "赏金商店",
        onClick: () => BuildingUtils.PlaceBuilding(...)
    );
    __instance.AddEntry(myEntry);
}

// 场景：给 CraftView 加一个自定义过滤标签
[HarmonyPatch(typeof(CraftView), "Setup")]
[HarmonyPostfix]
static void CraftView_Setup_Postfix(CraftView __instance)
{
    __instance.AddFilterTag("MyMod_Weapons", "自定义武器");
}
```

> 注入模式不需要任何 Unity 编辑器操作——纯 Harmony patch + 代码端资源加载。

### 16.2 🆕 代码端 Canvas：`SimpleViewBuilder`（无需 Unity 编辑器）

```csharp
// FML 提供（新建 FastModdingLib/UI/SimpleViewBuilder.cs）：
public class SimpleViewBuilder
{
    private readonly GameObject _root;
    private readonly RectTransform _content;

    /// <summary>创建基础 Canvas（Screen Space Overlay, 1920×1080）。</summary>
    public static SimpleViewBuilder Create(string viewName)
    {
        var go = new GameObject(viewName);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        go.AddComponent<GraphicRaycaster>();
        // FadeGroup 作为主容器
        var fadeGroup = go.AddComponent<FadeGroup>();
        return new SimpleViewBuilder(go, fadeGroup);
    }

    public SimpleViewBuilder AddTitle(string text, int fontSize = 24) { /* ... */ return this; }
    public SimpleViewBuilder AddButton(string text, Action onClick) { /* ... */ return this; }
    public SimpleViewBuilder AddText(string text) { /* ... */ return this; }
    public SimpleViewBuilder AddCloseButton() { /* 自动绑定 View.Close() */ return this; }
    public SimpleViewBuilder AddScrollList<T>(IEnumerable<T> items, Func<T, string> display) { /* ... */ return this; }

    /// <summary>挂载指定 View 子类脚本并返回。</summary>
    public T Build<T>() where T : View
    {
        var view = _root.AddComponent<T>();
        // 反射设 fadeGroup / exitButton 等序列化字段
        return view;
    }
}

// modder 使用：
var myView = SimpleViewBuilder.Create("BountyShopView")
    .AddTitle("赏金商店")
    .AddText("欢迎光临！")
    .AddButton("交易", () => OpenTrade())
    .AddCloseButton()
    .Build<MyBountyShopView>();
```

> `SimpleViewBuilder` 让 modder 用纯代码创建基本 Canvas UI，无需在 Unity 编辑器中手动拖拽组件。

### 16.3 View 是否需要 FML 封装 → 评估结论

| 维度 | 评估 | 结论 |
|------|------|------|
| **View 基类** | modder 需继承游戏 `View` 来创建自定义面板 | **不封装基类**——`View` 已有完整生命周期 |
| **View 实例发现** | 需 Harmony patch `GameplayUIManager.GetViewInstance<T>()` | **B1 阶段已规划** |
| **代码端 View 创建** | `SimpleViewBuilder` 覆盖基本面板需求 | **🆕 新增** |
| **注入模式** | 在已有 View 中加内容（按钮/条目/过滤标签） | **文档指导**——无新代码，纯 Harmony 模式 |
| **复杂 UI 布局** | 仍需要 Unity 编辑器 | **高级路径**——§16.5 作为参考 |

> **结论：View 基类不封装。** 提供三个层次的解决方案：注入模式（80%）→ `SimpleViewBuilder`（15%）→ Unity 编辑器（5%）。

### 16.4 官方 View 结构参考

基于 `DecompiledDLL/Core/Duckov.UI/ManagedUIElement.cs` (80 LOC) + `View.cs` (151 LOC)：

```
ManagedUIElement : MonoBehaviour                   ← UI 元素基类
├── bool open { get; }                            ← 是否打开
├── ManagedUIElement parent                       ← 父级 UI 元素
├── static event onOpen / onClose                 ← 全局事件
├── Open(ManagedUIElement parent)                 ← 公开方法
├── Close()                                       ← 公开方法
└── virtual OnOpen() / OnClose()                  ← 子类重写

View : ManagedUIElement                           ← 面板基类
├── static View ActiveView                        ← 当前激活面板（单例模式）
├── ViewTabs viewTabs                             ← 标签页
├── Button exitButton                             ← 关闭按钮
├── string sfx_Open / sfx_Close                   ← 音效
│
├── Awake(): 绑定 exitButton + UIInput 导航
├── OnOpen(): ActiveView.Close() + DisableInput + Post sfx
├── OnClose(): ActiveView=null + EnableInput + Post sfx
│
├── OnNavigate(UIInputEventData)                  ← 手柄/键盘 UI 导航
├── OnConfirm(UIInputEventData)
├── OnCancel(UIInputEventData)
│
└── static GetViewInstance<T>() → T               ← 单例查找
    └── 委托至 GameplayUIManager.GetViewInstance<T>()
```

### 16.5 官方 CraftView 参考（列表+选择模式）

基于 `DecompiledDLL/Core/CraftView.cs` (366 LOC)：

```
CraftView : View, ISingleSelectionMenu<CraftView_ListEntry>
├── [SerializeField] FadeGroup fadeGroup                    ← 淡入淡出动画
├── [SerializeField] CraftView_ListEntry entryTemplate      ← 列表条目模板
├── [SerializeField] ItemDetailsDisplay details             ← 物品详情面板
├── [SerializeField] CostDisplay costDisplay                ← 费用显示
├── [SerializeField] Button craftButton                     ← 制作按钮
├── [SerializeField] InventoryDisplay playerInventoryDisplay ← 玩家背包
│
├── PrefabPool<CraftView_ListEntry> _entryPool              ← 列表对象池
├── CraftView_ListEntry selectedEntry                       ← 当前选中
│
├── SetupAndShow(): 配置数据 → Open()
├── Setup(): 填充列表 + 配置背包
├── RefreshList(): 按过滤条件刷新
├── SetSelection(entry): 选中条目 → 刷新详情
│
└── 异步制作: UniTask CraftTask() → 扣费 → 创建物品 → 通知
```

### 16.6 Unity 编辑器路径：制作自定义 View Canvas（高级）

#### 步骤 1：创建 Canvas Prefab

```
1. Unity 中新建 Canvas（GameObject → UI → Canvas）
   ├── Canvas Scaler: Scale With Screen Size (1920×1080)
   ├── Graphic Raycaster: 启用
   └── 挂载你的 View 脚本（继承 View）
```

#### 步骤 2：组装 View 层级

```
MyCustomView (Canvas 根)                            ← 挂载你的 View 脚本
├── [FadeGroup] fadeGroup                            ← 主淡入淡出容器
│
├── TitleBar                                         ← 标题栏
│   ├── Text (标题文本，支持本地化)
│   └── Button (关闭按钮 → 绑定到 View.exitButton)
│
├── ContentPanel                                     ← 主内容区域
│   ├── ListPanel (左侧列表)
│   │   ├── ScrollRect + Mask
│   │   └── entryTemplate (列表条目 Prefab，初始 inactive)
│   │
│   ├── DetailPanel (右侧详情)
│   │   ├── ItemDetailsDisplay (物品详情，可复用游戏组件)
│   │   ├── CostDisplay (费用显示)
│   │   └── Text (描述文本)
│   │
│   └── InventoryPanel (可选：玩家背包)
│       └── InventoryDisplay (复用游戏组件)
│
└── BottomBar                                        ← 底部操作栏
    └── Button (确认/操作按钮)
```

#### 步骤 3：编写 View 脚本

```csharp
namespace MyMod.UI
{
    public class MyCustomView : View, ISingleSelectionMenu<MyListEntry>
    {
        [SerializeField] private FadeGroup fadeGroup;
        [SerializeField] private MyListEntry entryTemplate;
        [SerializeField] private ItemDetailsDisplay details;
        [SerializeField] private CostDisplay costDisplay;
        [SerializeField] private Button actionButton;

        private PrefabPool<MyListEntry> _entryPool;
        private MyListEntry selectedEntry;

        // 单例获取（必须——游戏通过 GameplayUIManager 管理 View 实例）
        public static MyCustomView Instance => View.GetViewInstance<MyCustomView>();

        protected override void OnOpen()
        {
            base.OnOpen();
            fadeGroup.Show();
        }

        protected override void OnClose()
        {
            base.OnClose();
            fadeGroup.Hide();
        }

        // 外部调用入口
        public static void Show(MyTarget target)
        {
            if (Instance != null)
                Instance.SetupAndShow(target);
        }

        internal void SetupAndShow(MyTarget target)
        {
            // 1. 清理选中
            ItemUIUtilities.Select(null);
            SetSelection(null);
            // 2. 配置数据
            Setup(target);
            // 3. 打开面板
            Open();
        }

        private void Setup(MyTarget target) { /* 填充列表 */ }
        public MyListEntry GetSelection() => selectedEntry;
        public bool SetSelection(MyListEntry sel) { /* 选中逻辑 */ }
    }
}
```

#### 步骤 4：注册到 GameplayUIManager

```csharp
// 方案 A：通过 AssetBundle 加载 + Harmony Patch
[HarmonyPatch(typeof(GameplayUIManager), "GetViewInstance")]
[HarmonyPostfix]
static void GetViewInstance_Postfix(ref View __result)
{
    if (__result == null && typeof(T) == typeof(MyCustomView))
        __result = MyCustomView.Instance;  // 从 AssetBundle 预实例化的引用
}
```

#### 步骤 5：打包 AssetBundle

```
1. 将 Canvas Prefab 标记 assetBundleName
2. Build AssetBundle
3. 随 mod 分发 .bundle 文件
```

### 16.7 View 制作检查清单（Unity 编辑器路径）

| 检查项 | 说明 |
|--------|------|
| ✅ 继承 `View`（非 `MonoBehaviour`） | 必须 |
| ✅ Canvas Scaler 正确 | Scale With Screen Size, 1920×1080 |
| ✅ `FadeGroup` 作为主容器 | 用于 `Show()`/`Hide()` 动画 |
| ✅ `static Instance` 属性 | 通过 `GetViewInstance<T>()` |
| ✅ `static void Show(Target)` 入口 | 外部调用约定 |
| ✅ `entryTemplate` 初始 inactive | `PrefabPool` 需要 |
| ✅ 按钮绑定在 `Awake()`、移除在 `OnDestroy()` | 防止泄漏 |
| ✅ 事件订阅在 `OnEnable()`、退订在 `OnDisable()` | UI 层事件 |
| ✅ 本地化用 `[LocalizationKey]` | 标记所有文本字段 |
| ✅ AssetBundle 不丢脚本引用 | 打包前确认无 Missing Script |

---

## 17. 减少 Unity 编辑器依赖——综合策略

### 17.1 问题陈述

当前 FML 的多个系统要求 modder 使用 Unity 编辑器制作资产（Building Prefab、CharacterRandomPreset、Canvas View）。但大多数 FML modder 是 C# 程序员，**不使用 Unity 编辑器**。要求他们安装 Unity、学习 Prefab 制作、打包 AssetBundle 是显著的入门障碍。

### 17.2 分层策略

```
┌─────────────────────────────────────────────────────────┐
│ 层级 0：纯代码（80% 场景）— 零 Unity 编辑器依赖          │
│                                                         │
│  • EnemyPresetData DTO                 (§14)             │
│  • CreateSimpleBuilding() 代码端 Building    (§15.1)     │
│  • 引用游戏已有模型/材质                   (§14.2 ModelRef) │
│  • 注入已有 View（Harmony Postfix）        (§16.1)        │
│  • SimpleViewBuilder 代码端 Canvas         (§16.2)        │
│  • SpriteLoader.FromPng() 从 PNG 加载图标  (§16.1)        │
│                                                         │
├─────────────────────────────────────────────────────────┤
│ 层级 1：Unity 编辑器（15% 场景）— 自定义视觉资产          │
│                                                         │
│  • 自定义 3D Building 模型               (§15.4)         │
│  • 自定义 CharacterModel（NPC 外观）         (§14.2 ModelRef.FromBundle) │
│  • 复杂 Canvas UI 布局                   (§16.5)         │
│  • AssetBundle 打包                      AssetUtil.LoadBundle │
│                                                         │
├─────────────────────────────────────────────────────────┤
│ 层级 2：DLL 引用（5% 场景）— 直接操作游戏类型            │
│                                                         │
│  • 高级用户绕过 FML 封装，直接使用 Publicizer 公开的类型  │
│  • 不推荐——失去 FML 卸载/隔离/兼容性保证                  │
└─────────────────────────────────────────────────────────┘
```

### 17.3 各系统 Unity 编辑器依赖对比

| 系统 | 原依赖 | 优化后 |
|------|--------|--------|
| **EnemyUtils** | 必须 Unity 创建 CharacterRandomPreset.asset | 🟢 `EnemyPresetData` DTO 纯代码配置；模型引用游戏已有 prefab |
| **Building** | 必须 Unity 创建 Building Prefab | 🟢 `CreateSimpleBuilding()` 纯代码 + 克隆游戏已有 prefab |
| **Endowment** | 无（纯数据） | 🟢 始终纯代码 |
| **PerkTree** | 无（纯数据） | 🟢 始终纯代码 |
| **View** | 必须 Unity 创建 Canvas Prefab | 🟢 注入模式（80%）+ `SimpleViewBuilder`（15%）+ Unity（5%） |
| **Sprites/Icons** | 必须 Unity 导入 PNG→Sprite | 🟢 `SpriteLoader.FromPng()` 运行时加载 PNG（利用 `Texture2D.LoadImage` + `Sprite.Create`） |

### 17.4 🆕 需新增的 FML 基础设施

| 新增组件 | 位置 | 用途 | 优先级 |
|---------|------|------|--------|
| `EnemyPresetData` | `FastModdingLib/Entities/` | CharacterRandomPreset 的 DTO 封装 | P0 |
| `ModelRef` | `FastModdingLib/Entities/` | 模型引用（游戏已有 / AssetBundle） | P0 |
| `CreateSimpleBuilding()` | `BuildingUtils` | 代码端创建 Building GameObject | P0 |
| `SetBuildingModel()` | `BuildingUtils` | 将自定义模型注入已有 Building | P0 |
| `SimpleViewBuilder` | `FastModdingLib/UI/` | 代码端创建 Canvas + 基础 UI 组件 | P1 |
| `SpriteLoader` | **已有** `ItemUtils.LoadSprite` | 运行时 PNG → Sprite 转换（路径 `assets/textures/`） | ✅ 已有 |
| **注入模式文档** | Docs | Harmony Postfix 已有 View 的示例代码 | P1 |

### 17.5 对案例的影响

| 案例 | 原 Unity 依赖 | 优化后 |
|------|-------------|--------|
| **A：赏金商店** | Building Prefab + CharacterRandomPreset.asset + NPC 模型 | Building: `CreateSimpleBuilding("Building_Workbench")` 克隆 + NPC: `EnemyPresetData` + `ModelRef.GamePrefab("CharacterModel_Scav")` |
| **B：附魔工作台** | Building Prefab + Canvas Prefab | Building: `CreateSimpleBuilding()` + View: 注入模式扩展现有 CraftView |
| **C：暗杀者天赋** | （无） | 始终纯代码 |

> **关键认知**：FML 的目标是让 modder **写 C# 代码即可完成 mod 开发**。Unity 编辑器是可选的高级工具，不是强制性依赖。

---

*本计划为 Building / PerkTree / Endowment / UI 四个系统的深化实施文档。*
