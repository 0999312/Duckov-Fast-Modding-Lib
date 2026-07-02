# 文档与代码差异 — 修复计划 v1

> 基于 2026-07-01 审计结果 + 游戏解包源码分析
> 计划文档留存于 `Docs/FIX-PLAN-v1.md`

---

## 0. 游戏侧源码验证结论

在编写修复计划前，对 `DecompiledDLL/` 中的游戏反编译源码进行了审计，确认以下关键事实：

| 游戏类型 | 关键发现 |
|----------|---------|
| `Building` (MonoBehaviour) | 4 个 `[SerializeField] private` 字段：`id`(string), `dimensions`(Vector2Int), `graphicsContainer`(GameObject), `functionContainer`(GameObject)。可通过反射设置 → **CreateSimpleBuilding 可行** |
| `BuildingInfo` (struct) | 所有字段 public：`id`, `prefabName`, `maxAmount`, `cost`, `requireBuildings`, `alternativeFor`, `requireQuests`, `iconReference` |
| `BuildingManager.BuyAndPlace` | `internal static`，FML 已通过反射包装 → 无需修改 |
| `BuildingManager.OnBuildingBuiltComplex` | `public static event Action<int, BuildingInfo>` → **OnBuildingBuilt 可正式实现** |
| `BuildingDataCollection` | `infos`/`prefabs` 均为 `[SerializeField] private List<>` → FML 通过 Publicizer + Patch 已可访问 |
| `CharacterRandomPreset` | ScriptableObject，~80 字段，`characterModel` 为 `[SerializeField] private` → **EnemyPresetData DTO 可行**（通过 ScriptableObject.CreateInstance + 反射） |
| `EndowmentEntry.ModifierDescription` | **嵌套 struct**（非独立的 `ItemStatsSystem.ModifierDescription`），字段：`statKey`(string), `type`(ModifierType), `value`(float) |
| `EndowmentManager.SelectIndex` | `internal void` → 可通过 Publicizer 或反射访问 |
| `EndowmentManager.UnlockEndowment` | `public static bool` → 可直接调用 |
| `EndowmentManager.entries` | `[SerializeField] private List<EndowmentEntry>` → 反射访问 |
| `ModifierDescription` 命名冲突 | 存在两个同名类型：`ItemStatsSystem.ModifierDescription`（物品用类）vs `EndowmentEntry.ModifierDescription`（天赋用嵌套 struct）→ FML 的 `object[]` 方案**是正确的技术选择** |

---

## 1. 修复分类

### 🔴 A 类 — 代码缺失（CRITICAL）

#### A1. `EnemyPresetData` + `ModelRef`

**状态**: 完全不存在  
**游戏侧验证**: `CharacterRandomPreset` 是 ScriptableObject，可通过 `ScriptableObject.CreateInstance` + 反射创建。`characterModel` 字段为 `[SerializeField] private`。  

**修复方案**: 新建 `FastModdingLib/Entities/EnemyPresetData.cs`，实现：
- `EnemyPresetData` 类：包装 20+ 个最常用字段（health, team, sightDistance, damageMultiplier, moveSpeedFactor, exp, hasCashChance, cashRange 等）
- `ModelRef` struct：两个静态工厂 `GamePrefab(string name)` 和 `FromBundle(string bundle, string path)`
- `ToNative()` 方法：内部通过 `ScriptableObject.CreateInstance<CharacterRandomPreset>()` + 反射创建

**与 PLAN-Phase4 §14 的偏离**: 原设计 ModelRef 在 `EnemyPresetData.ToNative()` 中尝试加载 `CharacterModel`。实际游戏侧 `CharacterModel` 是 Unity prefab 引用，无法通过字符串名称在运行时加载。**修正方案**: ModelRef.GamePrefab 引用游戏已有 `CharacterModel_*` prefab 的 Transform.Find/Resource 路径；`FromBundle` 从 AssetBundle 加载。如果两者都不可用，fallback 到默认 CharacterModel。

**预估**: ~150 LOC，1 个新文件

---

#### A2. `CreateSimpleBuilding()` + `SetBuildingModel()`

**状态**: 完全不存在  
**游戏侧验证**: `Building` 的 `id`/`dimensions`/`graphicsContainer`/`functionContainer` 均为 `[SerializeField] private`。`Building.Awake()` 有 fallback 逻辑（`transform.Find("Graphics")` / `transform.Find("Function")`）。  

**修复方案**: 在 `BuildingUtils.cs` 中新增两个方法：
- `CreateSimpleBuilding(Identifier id, Vector2Int dimensions, string? existingPrefabName = null)`: 
  - 若 `existingPrefabName` 提供 → `BuildingDataCollection.GetPrefab(name)` → `Instantiate` 克隆，反射修改 id/dimensions
  - 否则 → `new GameObject()` → `AddComponent<Building>()` → 反射 set id/dimensions → 创建 Graphics 子对象（含 Cube primitive + MeshRenderer）→ 创建 Function 子对象（含 BoxCollider trigger）→ 反射 set graphicsContainer/functionContainer
- `SetBuildingModel(Identifier buildingId, GameObject modelPrefab, bool replaceExisting = true)`:
  - 从 Registry 查找 Building prefab → 获取 `graphicsContainer` → 清理旧子对象 → `Instantiate(modelPrefab, graphicsContainer.transform)`

**与 PLAN-Phase4 §15 的偏离**: 原设计尝试克隆已有 Building prefab 的 "Graphics 部分"，但 Building 组件没有公开 `graphicsContainer` getter。**修正**: 使用反射读取 `graphicsContainer`，如果反射失败则走纯代码创建路径（Cube primitive）。

**预估**: ~100 LOC，修改 `BuildingUtils.cs`

---

#### A3. `SimpleViewBuilder`

**状态**: 完全不存在  
**游戏侧验证**: 游戏使用标准 Unity Canvas 系统（Canvas, CanvasScaler, GraphicRaycaster, FadeGroup）。View 基类有 `exitButton`/`viewTabs`/`sfx_Open`/`sfx_Close` 序列化字段。  

**修复方案**: 新建 `FastModdingLib/UI/SimpleViewBuilder.cs`，提供链式 API：
- `SimpleViewBuilder.Create(string viewName)` → 创建 Canvas GameObject + Canvas + CanvasScaler + GraphicRaycaster + FadeGroup
- `.AddTitle(string, int)` → 添加标题 Text
- `.AddButton(string, Action)` → 添加按钮
- `.AddText(string)` → 添加文本
- `.AddCloseButton()` → 添加关闭按钮
- `.Build<T>() where T : View` → 挂载 View 子类，反射设置 exitButton/fadeGroup 引用

**与 PLAN-Phase4 §16.2 的偏离**: 原设计使用 `AddScrollList<T>`。**修正**: 首版不实现 ScrollList（复杂度高且大多数 mod 不需要），留作未来扩展。

**预估**: ~120 LOC，1 个新文件

---

### 🟠 B 类 — API 签名错误（HIGH）

#### B1. MIGRATION.md §11 PerkTreeUtils API

**问题**: 展示的是旧版 `[Obsolete]` string 签名  
**修复**: 替换为 Identifier 版本，与 USAGE.md §14 保持一致：

```csharp
// 修复后：
Perk perk = PerkTreeUtils.AddPerk(
    new Identifier("mymod", "ExtraHealth"), req, icon);

PerkTreeUtils.ConnectPerks(
    new Identifier("mymod", "ExtraHealth"),
    new Identifier("mymod", "IronWill"));

PerkTreeUtils.ForceUnlock(new Identifier("mymod", "ExtraHealth"));
PerkTreeUtils.RemoveAllPerks("mymod");
```

**预估**: ~10 行修改

---

#### B2. MIGRATION.md §11 BuildingUtils API

**问题**: 展示过时的 `string` API 签名 + 缺少 `PlaceBuilding`  
**修复**: 
- `GetBuildingInfo("building_id")` → `GetBuildingInfo(new Identifier("mymod", "workbench"))`
- `List<string> GetAllBuildingIds()` → `IReadOnlyList<Identifier> GetAllBuildingIds()`
- 新增 `PlaceBuilding` 示例

**预估**: ~15 行修改

---

#### B3. MIGRATION.md §12 基类错误

**问题**: `class MyGunMod : FastModdingLib.ModBehaviour` 与 §1 指导矛盾  
**修复**: 改为 `class MyGunMod : Duckov.Modding.ModBehaviour, IHasModid`，移除 `base.OnAfterSetup()` 调用，显式调用 `new Harmony(...).PatchAll(...)`

**预估**: ~10 行修改

---

### 🟡 C 类 — 文档内不一致（MEDIUM）

#### C1. PLAN.md §6 索引

**修复**: 第 328 行 `🚧 待实施` → `✅ 已完成`

---

#### C2. PLAN.md §4 覆盖率矩阵

**修复**: 第 305-308 行 `(Phase 4)` → `(Phase 5)`

---

#### C3. PROGRESS.md 遗漏未实现项

**修复**: 在 Phase 4 节末尾新增 "**未实现的 PLAN-Phase4 设计项**" 小节，列出 A1-A3 三项及其状态。

---

### 🟢 D 类 — 细节问题（LOW）

#### D1. USAGE.md §15 注释

**修复**: 第 1011 行注释改为明确说明 `object[]` 的用途和 `EndowmentEntry.ModifierDescription` 的嵌套 struct 性质。

---

#### D2. PLAN.md 日期

**修复**: 第 4 行 `2026-06-28` → `2026-07-01`

---

## 2. 实施优先级

| 优先级 | 项目 | 工作量 | 实施阶段 |
|--------|------|--------|----------|
| **P0** | B1/B2/B3 (MIGRATION.md 修复) | 35 行 | Wave 1 — 文档修复 |
| **P0** | C1/C2/D2 (PLAN.md 修复) | 6 行 | Wave 1 — 文档修复 |
| **P0** | C3 (PROGRESS.md 补充) | ~10 行 | Wave 1 — 文档修复 |
| **P0** | D1 (USAGE.md 注释) | ~5 行 | Wave 1 — 文档修复 |
| **P1** | A2 (CreateSimpleBuilding + SetBuildingModel) | ~100 LOC | Wave 2 — 代码实现 |
| **P1** | A1 (EnemyPresetData + ModelRef) | ~150 LOC | Wave 2 — 代码实现 |
| **P2** | A3 (SimpleViewBuilder) | ~120 LOC | Wave 3 — 代码实现 |

**并行策略**: Wave 1 全部 6 项可一次性完成（仅文档编辑）。Wave 2 中 A1 和 A2 互不依赖可并行。Wave 3 在 Wave 2 之后。

---

## 3. A1 详细设计：EnemyPresetData

```csharp
// FastModdingLib/Entities/EnemyPresetData.cs
namespace FastModdingLib
{
    /// <summary>模型引用：代码端优先，AssetBundle 作为高级选项。</summary>
    public struct ModelRef
    {
        public string GamePrefabName { get; set; }
        public string BundleName { get; set; }
        public string AssetPath { get; set; }

        public static ModelRef GamePrefab(string name)
            => new() { GamePrefabName = name };
        public static ModelRef FromBundle(string bundle, string path)
            => new() { BundleName = bundle, AssetPath = path };
    }

    public class EnemyPresetData
    {
        // 必填
        public string NameKey { get; set; }
        
        // 基础属性
        public Teams Team { get; set; } = Teams.scav;
        public float Health { get; set; } = 100f;
        public int Exp { get; set; } = 100;
        public bool IsBoss { get; set; }
        public bool ShowHealthBar { get; set; } = true;
        public bool HasSoul { get; set; } = true;
        
        // AI
        public float SightDistance { get; set; } = 17f;
        public float SightAngle { get; set; } = 100f;
        public float ReactionTime { get; set; } = 0.2f;
        public float HearingAbility { get; set; } = 1f;
        public float PatrolRange { get; set; } = 8f;
        public bool CanDash { get; set; }
        public float DamageMultiplier { get; set; } = 1f;
        public float MoveSpeedFactor { get; set; } = 1f;
        public bool CanTalk { get; set; } = true;
        
        // 掉落
        public bool DropBoxOnDead { get; set; } = true;
        public float HasCashChance { get; set; }
        public Vector2Int CashRange { get; set; }
        
        // 模型
        public ModelRef Model { get; set; } = ModelRef.GamePrefab("CharacterModel_Default");
        
        // 抗性
        public float ElementFactor_Physics { get; set; } = 1f;
        public float ElementFactor_Fire { get; set; } = 1f;
        // (其余抗性字段省略，完整列表见实现)
        
        internal CharacterRandomPreset ToNative()
        {
            var preset = ScriptableObject.CreateInstance<CharacterRandomPreset>();
            // 反射设置所有公共字段...
            // ModelRef 处理：尝试 GamePrefabName → Resources/AssetBundle 加载
            return preset;
        }
    }
}
```

### EnemyUtils 新增重载

```csharp
// EnemyUtils.cs 新增：
public static void RegisterEnemy(Identifier id, IStateConfig aiConfig, 
    EnemyPresetData data, string? modid = null)
{
    var preset = data.ToNative();
    RegisterEnemy(id, aiConfig, preset, modid ?? id.Domain);
}
```

---

## 4. A2 详细设计：CreateSimpleBuilding

```csharp
// BuildingUtils.cs 新增：
public static Building CreateSimpleBuilding(
    Identifier id, Vector2Int dimensions, string? existingPrefabName = null)
{
    Building building;
    
    if (existingPrefabName != null)
    {
        var existingPrefab = BuildingDataCollection.GetPrefab(existingPrefabName);
        if (existingPrefab != null)
        {
            var clone = UnityEngine.Object.Instantiate(existingPrefab);
            SetBuildingField(clone, "id", id.Path);
            SetBuildingField(clone, "dimensions", dimensions);
            return clone;
        }
    }
    
    // 纯代码创建
    var go = new GameObject($"Building_{id.Path}");
    building = go.AddComponent<Building>();
    SetBuildingField(building, "id", id.Path);
    SetBuildingField(building, "dimensions", dimensions);
    
    // graphicsContainer
    var graphics = new GameObject("Graphics");
    graphics.transform.SetParent(go.transform);
    var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
    cube.transform.SetParent(graphics.transform);
    cube.transform.localScale = new Vector3(dimensions.x, 2f, dimensions.y);
    SetBuildingField(building, "graphicsContainer", graphics);
    
    // functionContainer
    var func = new GameObject("Function");
    func.transform.SetParent(go.transform);
    func.layer = 8; // Interact
    func.AddComponent<BoxCollider>().isTrigger = true;
    SetBuildingField(building, "functionContainer", func);
    
    return building;
}

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
        foreach (Transform child in graphics.transform)
            UnityEngine.Object.Destroy(child.gameObject);
    }
    
    var model = UnityEngine.Object.Instantiate(modelPrefab, graphics.transform);
    model.transform.localPosition = Vector3.zero;
}
```

---

## 5. A3 详细设计：SimpleViewBuilder

```csharp
// FastModdingLib/UI/SimpleViewBuilder.cs
namespace FastModdingLib.UI
{
    public class SimpleViewBuilder
    {
        private readonly GameObject _root;
        private readonly FadeGroup _fadeGroup;
        private Button? _exitButton;

        public static SimpleViewBuilder Create(string viewName)
        {
            var go = new GameObject(viewName);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            go.AddComponent<GraphicRaycaster>();
            var fadeGroup = go.AddComponent<FadeGroup>();
            return new SimpleViewBuilder(go, fadeGroup);
        }

        // 链式方法省略，详见实现
        
        public T Build<T>() where T : View
        {
            var view = _root.AddComponent<T>();
            // 反射设置 fadeGroup / exitButton 字段
            return view;
        }
    }
}
```

---

## 6. 遗留问题处理

PROGRESS.md 中的 3 项遗留问题在此修复中一并处理：

| 遗留问题 | 本修复是否处理 | 说明 |
|----------|---------------|------|
| `OnBuildingBuilt` 占位实现 | ✅ **本修复处理** | 订阅 `BuildingManager.OnBuildingBuiltComplex` 事件实现真回调 |
| Endowment IL2CPP 兼容性 | ❌ 不处理 | 需要运行时环境验证，非代码层面可修复 |
| PerkTree 反射字段版本漂移 | ❌ 不处理 | 需要游戏版本升级时验证 |

### OnBuildingBuilt 实现

```csharp
// BuildingUtils.cs 中 OnBuildingBuilt/OffBuildingBuilt 改为：
private static Dictionary<int, (Identifier buildingId, Action<Building> callback)> _buildingCallbacks = new();

public static void OnBuildingBuilt(Identifier buildingId, Action<Building> callback, string modid)
{
    _buildingCallbacks[buildingId.GetHashCode()] = (buildingId, callback);
    BuildingManager.OnBuildingBuiltComplex += OnBuildingBuiltHandler;
}

private static void OnBuildingBuiltHandler(int guid, BuildingInfo info)
{
    foreach (var kvp in _buildingCallbacks)
    {
        if (kvp.Value.buildingId.Path == info.id)
            kvp.Value.callback?.Invoke(info.Prefab);
    }
}
```

---

## 7. 验收标准

### Wave 1 验收
- [x] `MIGRATION.md` PerkTreeUtils 示例换为 Identifier 版本
- [x] `MIGRATION.md` BuildingUtils 示例换为 Identifier 版本  
- [x] `MIGRATION.md` §12 基类修正
- [x] `PLAN.md` §6 索引 ✅ / §4 矩阵 Phase 5 / 日期更新
- [x] `PROGRESS.md` 新增未实现项说明
- [x] `USAGE.md` §15 注释修正

### Wave 2 验收
- [x] `EnemyPresetData.cs` + `ModelRef` 编译通过
- [x] `EnemyUtils.RegisterEnemy(Identifier, IStateConfig, EnemyPresetData)` 新重载可用
- [x] `BuildingUtils.CreateSimpleBuilding()` 可创建含 Cube 模型 + 交互碰撞体的 Building
- [x] `BuildingUtils.SetBuildingModel()` 可注入自定义模型
- [x] `BuildingUtils.OnBuildingBuilt()` 从占位改为真回调
- [x] `dotnet build` 通过（0 错误）

### Wave 3 验收
- [x] `SimpleViewBuilder` 编译通过，链式 API 可用
- [x] `USAGE.md` 补充 SimpleViewBuilder 文档

---

*本计划将在实施前提交审核。实施按 Wave 1 → Wave 2 → Wave 3 顺序执行。*

---

## 附录 A — 已识别的能力缺口（非 Bug，需后续 Phase 覆盖）

以下能力缺口不在当前修复范围内（非文档/代码不一致问题），但经审查确认了其重要性并记录于此，供后续 Phase 规划参考。

### A.1 角色装备/物品/战利品管理

**现状问题**：
- `EnemyUtils.RegisterEnemy` 无法为 NPC 指定装备（武器、护甲、头盔、背包）
- 无法为已有实体（包括游戏原生实体）运行时替换手持物品或随机更换装备
- 战利品掉落配置不完整——`CharacterRandomPreset` 中虽有 `itemsToGenerate` 字段（含 `ItemGenerationEntry`：池、标签、品质、概率、耐久度范围），但 FML 未封裝

**游戏侧基础**：
- `CharacterRandomPreset.itemsToGenerate` — `List<ItemGenerationEntry>`，支持按物品池/标签/品质筛选生成初始装备
- `CharacterRandomPreset.bulletQualityDistribution` + `bulletCountRange` + `bulletFilter` — 自动为武器生成同口径子弹
- `CharacterMainControl` 有 `PrimWeaponSlot()`、`MeleeWeaponSlot()`、`ArmorSlot()`、`HelmatSlot()`、`BackpackSlot()` 等装备槽
- `CharacterMainControl.CharacterItem.Inventory` — 完整物品背包管理

**需求**：
- `EnemyPresetData` 需扩展 `EquipmentConfig`：武器池 (ItemPool + 标签筛选)、护甲池、头盔池、背包池、随机品质/耐久度范围
- 支持为游戏原生实体（如已有 Scav）运行时替换装备
- 枪械自动匹配同口径随机子弹（复用 `bulletQualityDistribution` 机制）

### A.2 友善 NPC 交互

**现状问题**：
- `EnemyUtils` 假设所有注册实体为敌对 AI（`RegisterEnemy` 命名、`IStateConfig` 战斗状态机）
- 无"友善 NPC"概念——商人、任务给予者等需要不同的交互模式
- `CharacterRandomPreset.canTalk` 字段已存在，但 FML 未暴露

**游戏侧基础**：
- `CharacterRandomPreset.team = Teams.middle` — 设为中立阵营可实现非敌对
- `CharacterRandomPreset.canTalk = true` — 启用对话能力
- `InteractableBase` — 世界交互基类，NPC 可通过挂载子类实现交互
- `AICharacterController.defaultWeaponOut = false` — NPC 不掏武器

**需求**：
- `EnemyUtils` 重命名为 `EntityUtils` 或新增 `NpcUtils`
- 提供 `RegisterFriendlyNpc(Identifier, NpcConfig)` — 团队中立/友好 + 对话交互
- NPC 交互模板：对话 → 打开商店 View / 任务 View / 自定义 View

### A.3 游戏原生 UI 复用

**现状问题**：
- `SimpleViewBuilder`（Wave 3）仅能创建基本 Canvas 面板，无法复用游戏已有的复杂 UI
- Modder 最需要的 UI 能力是"在已有界面加按钮/条目/过滤标签"（注入模式），而非从头建 Canvas
- Modder 不希望手动在 Unity 编辑器搭建 UI——复杂、易出错、跨版本兼容性差

**游戏侧基础**：
- `StockShopView` — 商店交易界面，通过 `SetupAndShow(merchantData)` 打开
- `CraftView` — 合成台界面，有 `entryTemplate`（列表条目模板）、`craftButton`、`InventoryDisplay`
- `BuilderView` + `BuildingSelectionPanel` — 建造界面，可注入自定义建筑按钮
- `PerkTreeView` — 技能树界面
- `EndowmentSelectionPanel` — 天赋选择界面
- 所有 View 通过 `View.GetViewInstance<T>()` / `GameplayUIManager.GetViewInstance<T>()` 查找

**需求**：
- **注入模式优先**（覆盖 80% 场景）：Harmony Postfix 已有 View 的 `Setup()`，在其中注入自定义条目/按钮
- 提供 `UIInjectionHelper` 工具类：`InjectButton(View, string, Action)`、`InjectFilterTag(View, string, string)`、`InjectListEntry<T>(View, T, Func<T, GameObject>)`
- `SimpleViewBuilder` 降级为辅助角色（覆盖 15% 场景），且文档明确说明何时用注入模式、何时用代码 Canvas

### A.4 标签驱动的物品需求

**现状问题**：
- `CraftingFormulaData.CostItems` 和 `QuestData` 的任务物品需求均使用具体 `ItemEntry`（typeID 或 Identifier）
- 无法表达"任意 5.56 口径子弹 ×30"或"任意食物标签物品 ×3"
- `CraftingUtils` 和 `QuestUtils` 的消耗逻辑无法处理按耐久度折算

**游戏侧基础**：
- `ItemAssetsCollection.Search(BulletFilter)` — 支持按口径/标签/品质搜索物品
- `Item.StackCount` — 物品堆叠数
- `Item.GetStat("Durability")` — 耐久度属性
- `GameplayDataSettings` 持有物品标签系统（`Tag`、`TagCollection`）

**需求**：
- `ItemEntry` 扩展 `TagRequirement` 模式：`ItemEntry.ByTag("Food", 3)`
- `CraftingUtils.AddCraftingFormula` 支持标签成本：消耗时从玩家背包搜索匹配标签的物品
- 耐久度折算：若需求为"任意护甲×1"，消耗时优先取低耐久度物品；若需求数量按耐久度比例折算（如每 20% 耐久度 = 0.2 个物品）
- Quest 任务目标同样支持标签匹配

---

## 附录 B — 缺口优先级与 Phase 归属

| 缺口 | 建议 Phase | 优先级 | 依赖 |
|------|-----------|--------|------|
| A.1 角色装备/战利品 | Phase 5 | P0 | EnemyPresetData (Wave 2) |
| A.2 友善 NPC | Phase 5 | P1 | EnemyPresetData + InteractableBase |
| A.3 UI 注入辅助 | Phase 5 | P0 | 无（纯 Harmony Postfix 模式） |
| A.4 标签物品需求 | Phase 5 | P1 | ItemUtils + CraftingUtils |

> 这些缺口将在当前 Wave 1-3 修复完成后，以新的 `PLAN-Phase5-*.md` 形式详细设计和实施。当前修复计划（Wave 1-3）仅覆盖文档/代码一致性问题。
