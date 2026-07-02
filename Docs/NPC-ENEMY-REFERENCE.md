# NPC / 敌人 双形态数据参考

> 基于 `duckov_assembly/assembly_0625/ExportedProject` 解包数据整理
> 最后更新：2026-07-01

---

## 1. 概述

游戏中有若干角色同时拥有**友善 NPC 形态**和**敌对 Boss 形态**，通过不同的 `CharacterRandomPreset` 实例切换。所有形态使用**同一个 C# 类**（`CharacterRandomPreset`，ScriptableObject，脚本 GUID: `d551df320acceeb317a9e97502ade12f`），通过字段值差异实现行为分化。

### 已确认的双形态角色

| 角色 | 友善形态 | 敌对形态 | 商店/场景变体 |
|------|---------|---------|-------------|
| **Jeff** | `EnemyPreset_Merchant_Jeff` | `EnemyPreset_Boss_Island_NPC_Jeff` | `HiddenWarehouse_Jeff` / `HiddenWarehouse_Jeff_Snow` |
| **XiaoMing** | `EnemyPreset_QuestGiver_XiaoMing` | `EnemyPreset_Boss_Island_NPC_XiaoMing` + `Boss_NPC_XiaoMing` | 有 `Building_Merchant_Ming` 商店 |
| **Fo** | `EnemyPreset_QuestGiver_Fo` | `EnemyPreset_Boss_Island_NPC_Fo` | — |
| **Alex** | `EnemyPreset_QuestGiver_Alex` | `EnemyPreset_Boss_Alex` | — |

### 其他 NPC/商人（无双形态）

| 预设 | 类型 | 备注 |
|------|------|------|
| `EnemyPreset_Boss_Island_NPC_Mud` | Boss NPC | Island 地图 |
| `EnemyPreset_Boss_Island_NPC_Wu` | Boss NPC | Island 地图 |
| `EnemyPreset_Boss_Island_NPC_Orange` | Boss NPC | Island 地图 |
| `EnemyPreset_Merchant_Myst` / `Myst0` / `MystIsland` | 商人 | 有多地图变体 |
| `EnemyPreset_Merchant_Test` | 测试商人 | — |

---

## 2. 形态分化关键字段

下表列出友善 NPC 与敌对 Boss 之间的字段差异。**这些字段决定了 AI 行为、UI 显示和装备生成策略。**

| 字段 | 友善/NPC 典型值 | 敌对/Boss 典型值 | 说明 |
|------|----------------|-----------------|------|
| `team` | **0** (player) 或 **5** (middle) | **1** (scav) | 决定阵营敌对关系 |
| `isBoss` | **0** | **1** | 是否标记为 Boss |
| `showHealthBar` | **0** | **1** | 是否显示血条 |
| `showName` | **0** | **1** | 是否显示名称 |
| `characterIconType` | **0** (none) | **3** (boss) | 地图标记图标类型 |
| `defaultWeaponOut` | **0** | **1** | 是否默认掏武器 |
| `dropBoxOnDead` | **0** (merchant) | **1** | 死亡是否掉落战利品箱 |
| `hasCashChance` | **0** (或 1 for merchant) | **1** | 是否携带现金 |
| `itemsToGenerate` | **[]** 或 chance=0 | 完整装备列表 | 生成的初始装备 |
| `facePreset` | 设置自定义脸部 | 视情况 | 自定义脸部预设 |
| `voiceType` | **0** | **4** | 语音类型 |
| `footstepMaterialType` | **0** | **2** | 脚步声材质 |
| `specialAttachmentBases` | 有商店/任务附件 | 通常为空 | 特殊行为附件 |
| `health` | 55-800 | 350-800 | 生命值 |
| `damageMultiplier` | 1.5-5.0 | 1.0-1.2 | 伤害倍率 |
| `aiCombatFactor` | 1.0-2.0 | 2.0 | AI 战斗因子 |

### 2.1 `specialAttachmentBases`（特殊附件）

这是 NPC 形态的关键差异化组件。不同附件决定了角色的交互行为：

| 附件 | 关联形态 | 推测功能 |
|------|---------|---------|
| `SpecialAttachment_Jeff` | `Merchant_Jeff` | 商店交互（打开 Jeff 的商店） |
| `SpecialAttachment_XiaoMing` | `QuestGiver_XiaoMing` | 任务交互（打开 Ming 的任务） |
| `SpecialAttachment_Fo` | `QuestGiver_Fo` | 任务交互 |
| `SpecialAttachment_Alex` (GUID: `22879480...`) | `QuestGiver_Alex` | 任务交互 |
| `SpecialAttachment_Merchant_Myst` | `Merchant_Myst` | 商店交互 |

> **关键发现**：`AISpecialAttachmentBase` 子类（如 `AISpecialAttachment_Shop`、`AISpecialAttachment_Hackable`、`AISpecialAttachment_Horse`）在 `DecompiledDLL/Core/` 中有定义。它们是挂载到 `CharacterRandomPreset.specialAttachmentBases` 列表中的组件，提供角色特殊行为。

---

## 3. 装备/战利品生成系统

### 3.1 `itemsToGenerate` 结构

每个 `ItemGenerationEntry` 包含：

| 字段 | 类型 | 说明 |
|------|------|------|
| `comment` | string | 注释（如 "枪"、"护甲"、"头盔"） |
| `chance` | float (0-1) | 生成概率 |
| `randomCount` | Vector2 | 随机数量范围 |
| `controlDurability` | bool | 是否控制耐久度 |
| `durability` | Vector2 | 耐久度范围（0-1 比例） |
| `durabilityIntegrity` | Vector2 | 耐久完整性范围 |
| `randomFromPool` | bool | 是否从物品池随机 |
| `itemPool` | ItemPool | 物品 TypeID 池（带权重） |
| `tags` | TagList | 标签筛选（带权重） |
| `addtionalRequireTags` | Tag[] | 额外必须标签 |
| `excludeTags` | Tag[] | 排除标签 |
| `qualities` | QualityList | 品质分布（带权重） |

### 3.2 装备生成示例（Jeff Boss 形态）

```
itemsToGenerate:
  # 1. 主武器 - 霰弹枪 (typeID 683, 权重1)
  - comment: 枪
    chance: 1.0
    durability: 0.4~0.5
    durabilityIntegrity: 0.5~0.6
    qualities: 1(w:10) 2(w:20) 3(w:30) 4(w:100) 5(w:40)
    excludeTags: [某种标签]

  # 2. 护甲（后续条目）...

  # 3. 头盔...

  # 4. 背包...

  # 5. 子弹（自动匹配口径）
  bulletQualityDistribution: 品质4(w:40)
  bulletFilter: caliber=SHT, minQuality=3, maxQuality=3
  bulletCountRange: 0.5~1.0
```

### 3.3 友善 NPC 装备策略

友善 NPC 的装备配置有两种模式：
- **完全不生成**：`itemsToGenerate: []`（如 Jeff Merchant）
- **生成但不使用**：`chance: 0`（如 XiaoMing/Fo QuestGiver，配置了武器池但概率为 0）

---

## 4. 商店系统

### 4.1 商店建筑 Prefab

| Prefab | 用途 |
|--------|------|
| `Building_Merchant_Normal.prefab` | 普通商人商店 |
| `Building_Merchant_Weapon.prefab` | 武器商人商店 |
| `Building_Merchant_Equipment.prefab` | 装备商人商店 |
| `Building_Merchant_Ming.prefab` | **Ming 的专属商店** |
| `IslandShop.prefab` | Island 地图商店 |

### 4.2 StockShopDatabase 商人条目

| merchantID | 条目数（估算） | 说明 |
|------------|--------------|------|
| `Merchant_Normal` | ~500 条 | 普通商人（最大库存） |
| `Merchant_Ming` | ~260 条 | Ming 的商店 |
| `Merchant_Weapon` | ~1040 条 | 武器商人 |
| `Merchant_Equipment` | ~2500 条 | 装备商人 |

### 4.3 Ming 商店示例条目

```
Merchant_Ming:
  typeID 336  maxStock:2  forceUnlock  priceFactor:1
  typeID 8    maxStock:6  forceUnlock  priceFactor:2
  typeID 298  maxStock:4  forceUnlock  priceFactor:2
  typeID 746  maxStock:9  forceUnlock  priceFactor:1
  typeID 338  maxStock:2  priceFactor:1.5
  typeID 754  maxStock:3  lockInDemo
  typeID 290  maxStock:1
  typeID 749  maxStock:1  lockInDemo
  typeID 873  maxStock:1
  ... (共约 260 条)
```

每个条目结构：`typeID`, `maxStock`, `forceUnlock`, `priceFactor`, `possibility`, `lockInDemo`

---

## 5. 商人巡逻 AI

`Patrol_Merchant.asset` 是一个 NodeCanvas `BehaviourTree`（不同脚本 GUID: `6f0bd83e...`），专门用于商人 NPC 的巡逻行为。包含：
- 状态重置（Noticed/Alert 设为 false）
- 瞄准重置
- 等待 + 换弹
- 与 Leader 的关联检查
- 朝向玩家瞄准

这与战斗 AI（`Combat_*.asset`）是**不同的行为树**。

---

## 6. 对 FML 的影响和建议

### 6.1 `EnemyPresetData` DTO 需扩展

当前 Wave 2 设计的 `EnemyPresetData` 缺少以下关键能力：

| 缺失能力 | 建议 |
|---------|------|
| 装备配置 | 新增 `EquipmentConfig` 子结构：武器池、护甲池、头盔池、背包池、物品池，每个带品质/耐久度/排除标签 |
| 子弹匹配 | 新增 `BulletConfig`：自动从武器口径推导子弹类型和品质 |
| 战利品配置 | 新增 `LootConfig`：是否掉落战利品箱、现金范围 |
| 阵营设置 | `team` 字段应暴露（支持 player/scav/middle 等） |
| NPC 标记 | `npcRole` 枚举：Enemy / Merchant / QuestGiver / Neutral |

### 6.2 `EnemyUtils` 应重命名为 `EntityUtils` 或新增 `NpcUtils`

- 当前 `RegisterEnemy` 命名暗示所有实体都是敌对
- 需要支持 `RegisterFriendlyNpc` 或统一为 `RegisterEntity` + `EntityRole` 参数
- 友善 NPC 的 `specialAttachmentBases` 是关键——FML 需要知道如何挂载商店/任务附件

### 6.3 商店集成

- Ming 已有独立商店建筑 (`Building_Merchant_Ming`) 和商店数据
- FML 的 `ShopUtils` 已支持 `AddGoods` + `CreateMerchantProfile`
- 对于自定义 NPC 商店，modder 需要能：
  1. 创建新商人 profile（已有 `CreateMerchantProfile`）
  2. 给 NPC 挂载 `AISpecialAttachment_Shop` 指向该 profile
  3. 或者让 NPC 交互时直接打开已有的 StockShopView

### 6.4 标签驱动的物品需求

从 `ItemGenerationEntry` 的结构可以看出，游戏原生已支持：
- 按标签筛选物品（`tags` + `addtionalRequireTags` + `excludeTags`）
- 按品质筛选（`qualities`）
- 按物品池筛选（`itemPool`）
- 耐久度控制（`controlDurability` + `durability` + `durabilityIntegrity`）

FML 的 `CraftingUtils` 和 `QuestUtils` 应借鉴此模式，在 `CraftingFormulaData.CostItems` 和 `TaskData` 中支持：
- `ItemEntry.ByTag("Food", 3)` — 任意食物标签物品 ×3
- 耐久度折算逻辑

---

## 7. 角色完整清单

### 7.1 双形态角色

| 角色 | 友善形态文件 | 敌对形态文件 | 模型 Prefab |
|------|------------|------------|------------|
| Jeff | `EnemyPreset_Merchant_Jeff.asset` | `EnemyPreset_Boss_Island_NPC_Jeff.asset` | `0_CharacterModel_Boss_Jeff.prefab` / `0_CharacterModel_Duck_Jeff.prefab` |
| XiaoMing | `EnemyPreset_QuestGiver_XiaoMing.asset` | `EnemyPreset_Boss_Island_NPC_XiaoMing.asset` + `EnemyPreset_Boss_NPC_XiaoMing.asset` | `CharacterModel_Custom_XiaoMing.prefab` |
| Fo | `EnemyPreset_QuestGiver_Fo.asset` | `EnemyPreset_Boss_Island_NPC_Fo.asset` | 来自 Fo 的脸部预设 GUID |
| Alex | `EnemyPreset_QuestGiver_Alex.asset` | `EnemyPreset_Boss_Alex.asset` | 来自 Alex 的脸部预设 GUID |

### 7.2 纯商人/NPC

| 角色 | 文件 | AI 控制器 |
|------|------|----------|
| Myst | `EnemyPreset_Merchant_Myst.asset` + `Myst0` + `MystIsland` | `AIController_Merchant_Myst.prefab` |
| Test | `EnemyPreset_Merchant_Test.asset` | — |

### 7.3 Island 地图 NPC（敌人）

| 角色 | 文件 |
|------|------|
| Mud | `EnemyPreset_Boss_Island_NPC_Mud.asset` |
| Wu | `EnemyPreset_Boss_Island_NPC_Wu.asset` |
| Orange | `EnemyPreset_Boss_Island_NPC_Orange.asset` |

---

*本文档供 FML Phase 5 规划参考。当前修复计划（Wave 1-3）不涉及此内容。*
