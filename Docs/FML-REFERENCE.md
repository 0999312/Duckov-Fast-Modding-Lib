# FML 开发框架参考手册

> 整合游戏侧架构分析 + FML API 设计 + 实际案例方案
> 最后更新：2026-07-01

---

## 0. 核心设计理念

### 0.1 Identifier 优先

所有 FML public API 统一使用 `Identifier("domain", "path")`。modder 永远不接触游戏原生数字 ID。

```csharp
// ✅ 正确
ItemUtils.CreateCustomItem(new Identifier("mymod", "coffee"), itemData);
EndowmentUtils.SelectEndowment(new Identifier("mymod", "assassin"));

// ❌ 禁止
ItemUtils.CreateCustomItem(150001, itemData);
```

### 0.2 代码端优先

modder 通过纯 C# 代码完成 90% 的工作。Unity 编辑器仅在需要自定义 3D 模型/复杂 UI 布局时才使用。

### 0.3 自动卸载

所有通过 FML API 注册的资源自动绑定到 modid，卸载时无需手动清理。

---

## 1. 游戏侧架构速查

### 1.1 角色系统

游戏统一使用 `CharacterRandomPreset`（ScriptableObject）配置所有角色——包括敌人、NPC、商人、任务给予者。

**角色通过以下字段组合决定行为**：

| 字段 | 友善 NPC | 敌对 Boss | 说明 |
|------|---------|----------|------|
| `team` | 0 (player) / 5 (middle) | 1 (scav) | `Teams` 枚举 |
| `isBoss` | false | true | Boss 标记 |
| `showHealthBar` | false | true | 血条显示 |
| `defaultWeaponOut` | false | true | 是否掏武器 |
| `canTalk` | true | true | 对话能力 |
| `specialAttachmentBases` | 商店/任务附件 | 通常空 | 特殊行为组件 |
| `itemsToGenerate` | 空或概率0 | 完整装备链 | 初始装备 |
| `facePreset` | 自定义脸部 | 视情况 | `CustomFacePreset` |
| `aiController` | 不同 AI 预制体 | 战斗 AI 预制体 | `AICharacterController` |

**关键附件类型**（位于 `AISpecialAttachmentBase` 子类）：
- `AISpecialAttachment_Shop` — 商店交互
- `AISpecialAttachment_Hackable` — 可黑客
- `AISpecialAttachment_Horse` — 坐骑

### 1.2 装备/战利品生成

每个 `CharacterRandomPreset` 通过 `itemsToGenerate` 列表定义初始装备：

```
ItemGenerationEntry:
  comment: "主武器"
  chance: 1.0              # 生成概率
  itemPool: [{typeID:683, weight:1}]  # 物品 TypeID 池
  tags: [{tag, weight}]     # 标签筛选
  excludeTags: [...]        # 排除标签
  qualities: [{value, weight}]  # 品质分布
  durability: 0.4~0.5       # 耐久度范围
  durabilityIntegrity: 0.5~0.6  # 耐久完整性
  controlDurability: true
```

**子弹自动匹配**：通过 `bulletQualityDistribution` + `bulletFilter` 自动生成匹配的子弹。

### 1.3 商店系统

商店通过 `StockShopDatabase` 管理，每个商人有独立的 `merchantID` 和商品列表。

```
StockShopDatabase 条目:
  merchantID: "Merchant_Ming"
  entries:
    - typeID: 336, maxStock: 2, forceUnlock: true, priceFactor: 1.0
    - typeID: 8,   maxStock: 6, forceUnlock: true, priceFactor: 2.0
```

建筑系统中的 `Building_Merchant_*` 预制体在基地中放置后启用对应商人。

### 1.4 合成/任务系统

- `CraftingFormulaData` — 配方数据：金钱、材料、产物、工作台标签、前置技能
- `QuestData` — 任务数据：包含 `TaskData`（需求）和 `RewardData`（奖励）
- `TaskRequireItem` — 提交物品任务
- `TaskKillCount` — 击杀任务（`requireEnemy`, `weaponTypeID`, `requireHeadshot`）

---

## 2. FML 模块地图

### 2.1 核心层

| 模块 | 文件 | 关键 API |
|------|------|---------|
| **Identifier** | `Utils/Identifier.cs` | `new Identifier("domain", "path")`, `Parse()`, `TryParse()` |
| **Register** | `Register/` | `IRegistry<T>`, `SimpleRegistry<T>`, `RegistryManager` |
| **EventBus** | `Events/` | `EventBusManager.Sync.Register<T>()`, 15 个游戏事件 |

### 2.2 内容模块

| 模块 | 关键 API | 状态 |
|------|---------|------|
| **ItemUtils** | `CreateCustomItem()`, `RegisterGun()`, `LoadSprite()` | ✅ 完备 |
| **CraftingUtils** | `AddCraftingFormula()`, `AddDecomposeFormula()` | ✅ 完备 |
| **QuestUtils** | `RegisterQuest()`, `AddQuestRelation()` | ✅ 完备 |
| **ShopUtils** | `AddGoods()`, `CreateMerchantProfile()` | ✅ 完备 |
| **BuildingUtils** | `RegisterBuilding()`, `PlaceBuilding()` | ✅ 完备 |
| **PerkTreeUtils** | `AddPerk()`, `ConnectPerks()`, `RegisterPerkTree()` | ✅ 完备 |
| **EndowmentUtils** | `RegisterEndowment()`, `SelectEndowment()` | ✅ 完备 |
| **EnemyUtils** | `RegisterEnemy()`, `SpawnEnemy()` | ✅ 基础完备 |
| **EconomyUtils** | `AddMoney()`, `UnlockItem()` | ✅ 完备 |
| **BuffUtils** | `RegisterBuff()`, `FindBuff()` | ✅ 完备 |
| **AudioUtil** | `PlayBGM()`, `SetMasterVolume()` | ✅ 完备 |

### 2.3 待实现

| 组件 | 用途 | 优先级 |
|------|------|--------|
| ✅ `EnemyPresetData` DTO | 纯代码配置角色（装备、AI、外观） | 已完成 |
| ✅ `CreateSimpleBuilding()` | 代码端创建 Building GameObject | 已完成 |
| ✅ `SimpleViewBuilder` | 代码端创建 Canvas UI | 已完成 |
| UI 注入辅助 | 在已有 View 中注入按钮/条目 | P1 |
| 标签物品需求 | 合成/任务支持按标签匹配物品 | ✅ 基础实现（`ItemEntry.ByTag()` / `WithDurabilityCost()` + `TagCostRegistry`；运行时验收待 Phase 5） |

---

## 3. 案例方案设计

### 案例 1：饮品商人

**场景**：modder 创建"饮品商人"——建造咖啡机后在基地生成商人 NPC，提供饮品配方解锁任务。

#### 步骤 1：创建饮品物品

```csharp
// 创建咖啡物品
var coffeeData = new ItemData
{
    itemId = 200001,
    localizationKey = "item_coffee",
    weight = 0.3f, value = 500,
    maxStackCount = 5, quality = 3,
    spritePath = "coffee_icon.png",
    tags = new List<string> { "Drink", "Food" },
    usages = new UsageData
    {
        useTime = 2f,
        behaviors = new List<UsageBehaviorData>
        {
            new FoodData { energyValue = 30, waterValue = 20 }
        }
    }
};
ItemUtils.CreateCustomItem(new Identifier("drinkmod", "coffee"), coffeeData);

// 同样创建茶、果汁等
```

#### 步骤 2：创建合成配方

```csharp
// 咖啡配方
CraftingUtils.AddCraftingFormula(new CraftingFormulaData
{
    Id = new Identifier("drinkmod", "coffee_recipe"),
    Money = 50,
    CostItems = new[] {
        ItemEntry.Of("drinkmod:coffee_beans", 3),
        ItemEntry.Of("drinkmod:water_bottle", 1)  // 需要水
    },
    Result = ItemEntry.Of("drinkmod:coffee", 1),
    Tags = new[] { "WorkBenchAdvanced" },
    RequirePerk = "cooking"  // 需要烹饪技能
});
```

#### 步骤 3：创建任务

```csharp
var questData = new QuestData
{
    ID = 5001,
    displayName = "quest_drink_master",
    description = "quest_drink_master_desc",
    questGiver = QuestGiverID.Fence,
    requireLevel = 1,
    tasks = new List<TaskData>
    {
        new TaskRequireItem
        {
            id = 1,
            itemIdentifier = new Identifier("drinkmod", "coffee"),
            requiredAmount = 3
        },
        new TaskKillCount
        {
            id = 2,
            requireAmount = 5,
            requireEnemy = "Scav",
            weaponIdentifier = new Identifier("drinkmod", "pistol"),
            requireHeadshot = true
        }
    },
    rewards = new List<RewardData>
    {
        new RewardEXP { id = 1, amount = 500 },
        new RewardUnlockItem { id = 2, itemIdentifier = new Identifier("drinkmod", "juice_recipe_bp") }
    }
};
QuestUtils.RegisterQuest(questData, "drinkmod");
```

#### 步骤 4：注册建筑 + 建造后回调

```csharp
// 注册咖啡机建筑
var buildingInfo = new BuildingInfo
{
    id = "CoffeeMachine",
    prefabName = "Building_CoffeeMachine",
    maxAmount = 1,
    cost = new Cost(money: 3000),
    iconReference = ItemUtils.LoadSprite("coffee_machine_icon", 0)
};
var building = BuildingUtils.CreateSimpleBuilding(
    new Identifier("drinkmod", "coffee_machine"),
    new Vector2Int(2, 2));
BuildingUtils.RegisterBuilding(
    new Identifier("drinkmod", "coffee_machine"), buildingInfo, building);

// 建造后生成商人 NPC
BuildingUtils.OnBuildingBuilt(
    new Identifier("drinkmod", "coffee_machine"),
    (building) =>
    {
        // 获取建筑位置
        var pos = building.transform.position + Vector3.right * 3;
        // 生成商人 ⏳ Phase 5
        // 以下 EnemyUtils.SpawnFriendlyNpc 为 Phase 5 计划 API，当前未实现
        // 替代方案：使用 EnemyUtils.SpawnEnemy() + CharacterRandomPreset 配置 NpcRole + Team
        EnemyUtils.SpawnFriendlyNpc(
            new Identifier("drinkmod", "drink_merchant"),
            new EnemyPresetData
            {
                NameKey = "NPC_Drink_Merchant",
                Team = Teams.middle,
                Health = 300,
                CanTalk = true,
                NpcRole = NpcRole.Merchant,
                ShopProfile = "Merchant_Drink",
                Model = ModelRef.GamePrefab("CharacterModel_Duck_Jeff")
            },
            pos);
    },
    "drinkmod");
```

#### 步骤 5：注册商人商品

```csharp
// 给商人添加商品
ShopUtils.CreateMerchantProfile("Merchant_Drink");
ShopUtils.AddGoods(new ShopGoodsData
{
    merchantProfileID = "Merchant_Drink",
    itemIdentifier = new Identifier("drinkmod", "coffee_beans"),
    maxStock = 10, priceFactor = 1.0f
}, "drinkmod");
```

---

### 案例 2：给已有敌人分配武器

**场景**：modder 添加几种新武器，并让特定的已有敌人（如 Scav、Boss）有概率使用。

#### 步骤 1：创建武器

```csharp
// 从 AssetBundle 注册新步枪
var bundle = AssetUtil.LoadBundle(new Identifier("weaponmod", "weapons"));
ItemUtils.RegisterGun(new Identifier("weaponmod", "ak_custom"),
    bundle, "AK_Custom_Prefab", originGunID: 654);  // 基于 AK 模板

ItemUtils.RegisterGun(new Identifier("weaponmod", "pistol_custom"),
    bundle, "Pistol_Custom_Prefab", originGunID: 151);  // 基于手枪模板
```

#### 步骤 2：创建子弹

```csharp
var bulletData = new BulletData
{
    itemId = 300101,
    localizationKey = "bullet_556_custom",
    Caliber = "5.56x45",
    damageMultiplier = 1.3f,
    ArmorPiercingGain = 0.4f
};
ItemUtils.CreateCustomBullet(new Identifier("weaponmod", "bullet_556_custom"), bulletData);
```

#### 步骤 3：给已有敌人分配武器（FML 新能力）

```csharp
// ⏳ Phase 5 API — 以下方法当前未实现，仅作设计参考
// 方案 A：通过 EnemyUtils.ModifyExistingPreset 给已有敌人注入武器池
EnemyUtils.AddWeaponToPreset(
    enemyPresetId: "EnemyPreset_Scav",  // 游戏原生 Scav 预设
    new WeaponConfig
    {
        WeaponPool = new[] {
            ItemEntry.Of("weaponmod:ak_custom", 1)
        },
        Chance = 0.3f,  // 30% 概率替换默认武器
        Qualities = new QualityRange { Min = 2, Max = 4 },
        Durability = new Vector2(0.4f, 0.7f),
        WithMatchingAmmo = true  // 自动匹配 5.56 口径子弹
    },
    "weaponmod");

// 方案 B：给 Boss 添加特殊掉落武器
EnemyUtils.AddLootToPreset(
    "EnemyPreset_Boss_Red",
    ItemEntry.Of("weaponmod:ak_custom", 1),
    0.15f,  // 15% 概率
    "weaponmod");
```

#### 步骤 4：设置武器标签（供任务系统引用）

```csharp
// 武器自动继承 originGunID 的标签，也可以通过 ItemUtils 追加
// 这样 TaskKillCount 的 weaponIdentifier 可以通过标签匹配：
// "任意手枪标签武器" → weaponTag: "Pistol"
```

---

### 案例 3：标签驱动的合成与任务

**场景**：配方接受"任意水标签物品"而非指定 typeID；任务要求"使用手枪击杀 N 个敌人"。

#### 步骤 1：标签配方

```csharp
// 饮品配方 —— 接受任意水标签物品
CraftingUtils.AddCraftingFormula(new CraftingFormulaData
{
    Id = new Identifier("drinkmod", "juice"),
    Money = 30,
    CostItems = new[] {
        ItemEntry.ByTag("Fruit", 3),         // 任意水果标签 ×3
        ItemEntry.ByTag("Water", 1),         // 任意水标签 ×1
    },
    Result = ItemEntry.Of("drinkmod:juice", 1),
    Tags = new[] { "WorkBenchAdvanced" }
});

// 弹药配方 —— 接受任意火药标签物品
CraftingUtils.AddCraftingFormula(new CraftingFormulaData
{
    Id = new Identifier("weaponmod", "bullet_craft"),
    Money = 100,
    CostItems = new[] {
        ItemEntry.ByTag("Gunpowder", 2),     // 任意火药
        ItemEntry.ByTag("Metal", 5),         // 任意金属
    },
    Result = ItemEntry.Of("weaponmod:bullet_556_custom", 30),
    Tags = new[] { "WorkBenchAdvanced" }
});
```

#### 步骤 2：标签击杀任务

```csharp
var questData = new QuestData
{
    ID = 5002,
    displayName = "quest_pistol_master",
    description = "quest_pistol_master_desc",
    questGiver = QuestGiverID.Fence,
    requireLevel = 3,
    tasks = new List<TaskData>
    {
        new TaskKillByTagData
        {
            id = 1,
            requireAmount = 10,
            requireEnemyName = "Scav",
            // 使用标签匹配武器，而非指定 typeID
            weaponTag = "Pistol",           // 🆕 新能力
            requireHeadShot = false
        },
        new TaskKillByTagData
        {
            id = 2,
            requireAmount = 3,
            requireEnemyName = "Boss",
            weaponTag = "Sniper",           // 🆕 任意狙击枪标签
            requireHeadShot = true
        }
    },
    rewards = new List<RewardData>
    {
        new RewardEXP { id = 1, amount = 1000 },
        new RewardMoney { id = 2, amount = 5000 }
    }
};
QuestUtils.RegisterQuest(questData, "weaponmod");
```

#### 步骤 3：耐久度折算的配方消耗

```csharp
// 修理配方 —— 消耗按耐久度比例折算
CraftingUtils.AddCraftingFormula(new CraftingFormulaData
{
    Id = new Identifier("weaponmod", "repair_kit"),
    Money = 200,
    CostItems = new[] {
        ItemEntry.ByTag("Armor", 1)
            .WithDurabilityCost(true),  // 🆕 按耐久度折算：满耐久=1个，50%=0.5个
        ItemEntry.Of("Metal", 10)
    },
    Result = ItemEntry.Of("weaponmod:repair_kit", 1),
    Tags = new[] { "WorkBenchAdvanced" }
});
```

---

## 4. FML 需要新增的 API

基于以上三个案例，以下 API 需要在 Wave 2/3 修复和后续 Phase 5 中实现：

### 4.1 P0 — 当前修复计划 (Wave 2)

| API | 用途 | 案例 |
|-----|------|------|
| `EnemyPresetData` + `ModelRef` | 纯代码配置角色 | 案例1-步骤4（商人 NPC） |
| `CreateSimpleBuilding()` | 代码端创建 Building | 案例1-步骤4（咖啡机建筑） |
| `BuildingUtils.OnBuildingBuilt()` 真回调 | 建筑建成后逻辑 | 案例1-步骤4（生成商人） |

### 4.2 P1 — 后续 Phase 5

| API | 用途 | 案例 |
|-----|------|------|
| `EnemyUtils.AddWeaponToPreset()` | 给已有敌人加武器 | 案例2-步骤3 |
| `EnemyUtils.AddLootToPreset()` | 给已有敌人加掉落 | 案例2-步骤3 |
| `EnemyUtils.SpawnFriendlyNpc()` | 生成友善 NPC | 案例1-步骤4 |
| `EntityUtils` 重命名 | 统一实体管理 | 全局 |
| `ItemEntry.ByTag()` | 标签匹配物品 | 案例3-步骤1 |
| `TaskKillByTagData.weaponTag` | 标签击杀要求 | 案例3-步骤2 |
| `ItemEntry.WithDurabilityCost()` | 耐久度折算 | 案例3-步骤3 |

---

## 5. 实现建议

### 5.1 实现顺序

```
Wave 1 (文档修复, ~55行) ─── 即刻可做
  └── 所有 B/C/D 类文档修复

Wave 2 (代码实现基础, ~250 LOC) ─── 解锁案例1基础
  ├── EnemyPresetData + ModelRef
  ├── CreateSimpleBuilding + SetBuildingModel
  └── OnBuildingBuilt 真回调

Phase 5 (能力扩展, ~400 LOC) ─── 解锁案例2+3
  ├── EnemyUtils 扩展 (武器分配/友善NPC)
  ├── 标签物品需求 (ItemEntry.ByTag)
  └── UI 注入辅助
```

### 5.2 标签系统实现思路

游戏原生 `ItemGenerationEntry` 已支持标签筛选。FML 可以在 `ItemUtils.TryResolveTypeId()` 基础上扩展：

```csharp
// 新增 ItemUtils 方法
public static int[] FindItemsByTag(string tag, int? minQuality = null);

// ItemEntry 扩展
public static ItemEntry ByTag(string tag, int amount, int? minQuality = null);
```

### 5.3 武器分配实现思路

游戏 `CharacterRandomPreset.presets` 列表在 `GameplayDataSettings.CharacterRandomPresetData` 中。FML 通过反射修改已有 preset 的 `itemsToGenerate` 列表来注入新武器：

```csharp
// Harmony Postfix CharacterRandomPreset.CreateCharacterAsync
// 在角色生成前检查是否有 FML 注入的武器配置
// 有则追加到 itemsToGenerate 或替换
```

---

*本文档为开发参考，随实施进展更新。*
