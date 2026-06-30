# Fast-Modding-Lib 开发计划表 (PLAN.md)

> 高层粒度的阶段性路线图。每个阶段进入实施前再细化为代码级子任务与验收标准。
> 最后更新：2026-06-28

---

## 0. 项目定位

**Fast-Modding-Lib (FML)** 是为 Unity 游戏《逃离鸭科夫》(Duckov by TeamSoda) 提供
的 Mod 框架，目标是在原游戏的反编译源码与第三方 Mod 创作者之间架起一层稳定、
声明式、易上手、易卸载的中间层 API。

**当前体量**：~50 个 .cs 文件，~4500 LOC，覆盖 Items / Quests / I18n / EventBus /
Register 一体化 / Crafting / Economy / Buffs / Shop(A-Z) / Audio(SFX+BGM+Bus) /
Buildings / PerkTrees / CustomOptions / Entities(待 EnemyUtils) 等模块。

**技术栈**：.NET Standard 2.1 + Krafs.Publicizer + Harmony (vendored 0Harmony.dll，版本锁定 2.4.1.0)。

---

## 1. 现状评估摘要

### 已完备模块 (Phase 1~2 已落地，Phase 3 大部分落地)
- **Items** — `ItemUtils` + `ItemData` + `BulletData`：物品 / 枪支 / 子弹 / 蓝图程序化创建与 AssetBundle 加载；
  R6 已迁移至 `ReverseLookupRegistry<int,int>`，新增 `TryGetCustomItem(int)` 读路径，
  旁路字典 `addedItemIds` 已删除
- **Quests** — `QuestUtils` + 5 种 Task + 4 种 Reward + `TaskKillCountFix` 持久化修复；
  R5 已迁移至 `QuestRegistry : SimpleRegistry<Quest>`，旁路字典 `addedQuests` 已删除
- **I18n** — 9 语言 JSON 加载；B4 已从直接 hook `OnSetLanguage` 迁移到 EventBus
  （通过 `LanguageChangedEvent`），公共 API 零回归
- **EventBus** — B1~B6 全部落地：`EventBus` + `AsyncEventBus` + `EventBusManager` +
  15 个游戏事件桥接（`GameEventAdapters` 用 `DynamicMethod` 反射订阅）+ `EventBusTest` 7 用例
- **Register 一体化** — R1~R8 全部落地：`IRegistry<T>` 扩能（`Remove`/`Clear`/`IEnumerable`/
  `GetAllByOwner`/`RemoveAllByOwner`）+ `ReverseLookupRegistry<T,TKey>` +
  `RegistryManager.CurrentModid` + `EnterModScope` + 五模块迁移（Audio/Quests/Shop/
  Items/Crafting）+ `RegisterTest` 15 用例
- **Crafting** — R7 已迁移至 `CraftingFormulaRegistry` + `DecomposeRegistry`（2 份
  `ReverseLookupRegistry`），"修缮中"状态已解除；基于 Registry 的 per-formula 管理可用
- **Economy** — `EconomyUtils`：金钱增删查、`SetMoney`、物品解锁/确认/查询、`MoneyChangedEvent` 订阅
- **Buffs** — `BuffUtils` + `BuffRegistry : SimpleRegistry<Buff>`
- **Shop** — `ShopUtils` + `ShopRegistry`：`AddGoods`/`RemoveGoods`/`EditGoods`/
  `CreateMerchantProfile`/`TryGetGoods`/`GetAllGoods`/`UnregisterAllGoods`/`RemoveAllGoods`
- **Audio** — `AudioUtil` BGM 控制（Play/Stop/Switch/IsPlaying）+ FMOD 总线音量
  （Master/Music/SFX 三路 Volume + Mute），R4 已删除 `mapdata` 旁路字典
- **Buildings** — `BuildingUtils` + `BuildingRegistry`（含 `PlaceBuilding` 占位）
- **PerkTrees** — `PerkTreeUtils` + `PerkTreeRegistry`（含 `AddPerk`/`ConnectPerks`/`ForceUnlock`）
- **CustomOptions** — `ModOptionsBuilder` + `ModOptionsRegistry`（Toggle/Slider/Dropdown/Button）
- **Utils / AssetUtil** — 通用基建；`Identifier` 已补 `ToString()`/`Parse()`/`TryParse()`
- **ModBehaviour 生命周期** — `OnAfterSetup` 调 EventBus + Register bootstrap；
  `OnBeforeDeactivate` 调 TearDown + `RemoveAllByOwner` 自动清理

### Stub / 空缺（待补齐）
- **Entities / EnemyUtils** — `EnemyUtils.GetPreset(name)` 18 LOC 单方法（Phase 3 待实现）
- **Achievements** — 空缺（Phase 4）
- **Weather** — 空缺（Phase 4）
- **Fishing** — 空缺（Phase 4）
- **Multi-Scene** — 空缺（Phase 4）
- **不纳入考虑**：
  - `/Duckov.BlackMarkets` —— 黑市系统不依赖 NPC/Character，事件 context 已公开，modder 可直接订阅；天然 mod 兼容，无需 FML 封装
  - `/Duckov.Crops` —— 反编译可见但游戏内未实装，等待官方完成后再评估

### 仓库卫生
- ✅ Phase 0 已完成无害项整理（gitignore / 清缓存 / 删子 sln）
- ⚠️ 待办（未做）：`DuckovPath` 改 env var fallback、README 拼写校对、Tests/ 剥离独立 csproj
- 🚫 **明确不动（版本硬性锁定）**：`0Harmony.dll` vendored 二进制，版本硬性锁定
  **2.4.1.0**（`ProductVersion: 2.4.1.0+789df191bbaf6610232d50e7ef7dddc0d2812549`，
  文件大小 2456064 字节，时间戳 2025-11-03）。
  **禁止改 NuGet `PackageReference Lib.Harmony`** —— NuGet 拉取的版本可能漂移到
  2.x 后续 patch 或 3.x，与游戏运行时实际加载的 Harmony 版本错位会引发
  `MissingMethodException` / patch 失效 / mod 加载崩溃。保持 csproj
  `<Reference Include="0Harmony"><HintPath>0Harmony.dll</HintPath></Reference>`
  现状不变；后续任何子模块计划文档均需遵守此约束。

### 游戏侧已确认架构（来自 `DecompiledDLL/` 反编译调研）

- **统一角色类** `CharacterMainControl : MonoBehaviour` —— 玩家/敌人/NPC 通用，靠 `Teams` 枚举区分
- **AI 大脑** `AICharacterController` —— 持有 4 个 NodeCanvas `BehaviourTree`
  (patrol / alert / combat / combat_Attack_Tree)，public 字段可直接替换
- **角色模板** `CharacterRandomPreset : ScriptableObject` —— 约 80 字段，全套战斗/AI/战利品配置
- **中央 ScriptableObject 注册表** `GameplayDataSettings`
  (从 `Resources/GameplayDataSettings` 加载) —— 持有
  `CharacterRandomPresetData.presets` / `CraftingFormulas` / `QuestCollection` /
  `StockshopDatabase` / `AchievementDatabase` /
  `BuildingDataCollection` 等所有游戏级数据集合
- **生成链**：`CharacterSpawnerRoot → CharacterSpawnerGroup[Selector] →
  RandomCharacterSpawner.GetAPresetByWeight() →
  CharacterRandomPreset.CreateCharacterAsync() → CharacterCreator.CreateCharacter()`
- **敌我判定**：`Teams` 枚举 (player/scav/usec/bear/middle/lab/all/wolf) +
  `Team.IsEnemy(self, target)` 静态方法
- **关键事件钩子**（modder 友好，Phase 1 EventBus 将统一桥接）：
  - `Health.OnHurt` / `Health.OnDead` 静态事件（任意角色受伤/死亡）
  - `AIMainBrain.OnSoundSpawned` / `OnPlayerHearSound`
  - `LevelManager.OnLevelInitialized` / `OnMainCharacterDead` / `OnControllingCharacterChanged`
  - `SavesSystem.OnCollectSaveData`（自定义数据持久化）
  - `SavesCounter.OnKillCountChanged`
  - `EconomyManager.OnMoneyChanged` / `OnItemUnlockStateChanged`
  - `CraftingManager.OnItemCrafted` / `OnFormulaUnlocked`
  - `QuestManager.OnTaskFinishedEvent`
  - `LocalizationManager.OnSetLanguage`（I18n 当前已直接 hook）

### NodeCanvas 关键约束（影响 EnemyUtils 设计）
- 运行时通过 `ScriptableObject.CreateInstance<BehaviourTree>()` 建图本身可行
- 但 **`Graph.cs` line ~67 在 playmode 下静默跳过序列化**，导致运行时建图 NRE
- 修复方案：单次 Harmony transpiler 去掉 `|| Threader.applicationIsPlaying ||
  Application.isPlaying` 子句；此补丁**留到 EnemyUtils 实际启动开发时再决定是否上**

---

## 2. Phase 划分（高层目标）

> 默认推荐顺序：0 → 1 → 2 → 3 → 4 → 5(贯穿)
> 实施时按用户手动调整的优先级编排。

### Phase 0 — 仓库与工程基础整理 ✅
**状态**：全部完成。

- [x] 根 `.gitignore` 写入（覆盖 `DecompiledDLL/` / `.vs/` 等）
- [x] `git rm -r --cached .vs/` 脱离索引
- [x] 删除嵌套子目录 `FastModdingLib/FastModdingLib.sln`
- [x] 🚫 `0Harmony.dll` 改 PackageReference `Lib.Harmony` → **不执行**（版本硬性锁定 2.4.1.0，详见 §1 仓库卫生说明）；保持 vendored `<HintPath>` 现状
- [x] `DuckovPath` 改 env var fallback（新增 `$(DUCKOV_PATH)` 环境变量优先）
- [x] README 文档全面更新（反映全模块 API）
- [x] Tests/ 已通过 `Condition="'$(Configuration)' != 'Debug'"` 控制，非 Debug 配置自动排除

**验收**：`dotnet build` 通过（0 警告 0 错误）。

---

### Phase 1 — 框架内核加固 ✅
**状态**：全部完成。
**内部依赖顺序**：1.A (EventBus) → 1.B (Register 一体化 + ModBehaviour 生命周期)

#### 1.A — EventBus ✅
> 详见 [`PLAN-EventBus.md`](./PLAN-EventBus.md)。已落地 `FastModdingLib/Events/`。

- B1~B6 全部落地：核心总线（修复参考设计两处缺陷——同优先级 handler 不互相覆盖、
  新增 `Unregister` / `UnregisterAll`）+ 15 个游戏事件桥接（14 个用 `DynamicMethod`
  反射订阅绕过 Publicizer 二义性）+ I18n 内部迁移 + `EventBusTest` 7 用例
- 协程由 `EventBusRunner : MonoBehaviour` 宿主

#### 1.B — 其他内核加固 ✅

- **[Register 一体化]** R1~R8 全部落地。详见 [`PLAN-Register.md`](./PLAN-Register.md)。
  - `IRegistry<T>` 扩能 + `ReverseLookupRegistry<T,TKey>` +
    `RegistryManager.CurrentModid` + `EnterModScope`
  - Audio / Quests / Shop / Items / Crafting 五模块旁路字典全部收编
  - `RegisterTest` 15 用例全绿
- **[Crafting 修缮]** 与 Register R7 合流完成。`CraftingFormulaRegistry` +
  `DecomposeRegistry` 已落地，per-formula 管理基于 Registry 可用
- **[ModBehaviour 生命周期]** `OnAfterSetup` 调 EventBus + Register bootstrap；
  `OnBeforeDeactivate` 调 `TearDown` + `RegistryManager.RemoveAllByOwner` 自动清理
- **[技术债已清]** `old_fml_version` 等硬编码默认值已全局替换为 `RegistryManager.CurrentModid`
- NodeCanvas 序列化补丁随 EnemyUtils 实现（Phase 3，方案 A）一并落地

---

### Phase 2 — 头部消费系统（高复用） ✅
**状态**：全部完成。

- ✅ **EconomyUtils**：money 增删查、`SetMoney`、物品解锁/确认/查询、`MoneyChangedEvent` 订阅
- ✅ **BuffUtils** + `BuffRegistry : SimpleRegistry<Buff>`
- ✅ **CustomOptionsUtils**：`ModOptionsBuilder` (Toggle/Slider/Dropdown/Button) + `ModOptionsRegistry`

**验收**：✅ 已在 README 给出 6 行内样例。

---

### Phase 3 — 内容创作系统 ✅
**状态**：全部完成（Shop / Audio / Building / Perk / EnemyUtils）。

- ✅ **ShopUtils 扩充**：`AddGoods`/`RemoveGoods`/`EditGoods`/`CreateMerchantProfile`/
  `TryGetGoods`/`GetAllGoods`/`UnregisterAllGoods`
- ✅ **AudioUtils 扩展**：BGM API（Play/Stop/Switch/IsPlaying）+ FMOD 总线音量
  （Master/Music/SFX Volume + Mute）
- ✅ **PerkTreeUtils**：`AddPerk`/`ConnectPerks`/`ForceUnlock`/`RemovePerk`
- ✅ **BuildingUtils**：`RegisterBuilding`/`GetBuildingInfo`/`UnregisterBuilding`/
  `PlaceBuilding`(占位)
- ✅ **EnemyUtils**：已实现（方案 A — Hybrid，`IStateConfig` → `StateMachineToBT.Compile()`）
  - 详见下文 §3 方案 A + [`PLAN-EnemyUtils.md`](./PLAN-EnemyUtils.md)

> 已撤回子任务确认：CropUtils 仍等官方实装；BlackMarket 不封装

---

### Phase 4 — 长尾幂等系统（视社区需求）
**目标**：低频但完整的系统逐个补，每个约半天到一天。

- AchievementsUtils / WeatherUtils / FishingUtils / MultiSceneUtils
- 优先级由社区反馈决定，未启动前不列入近期里程碑。

---

### Phase 5 — 质量（贯穿每阶段末）
**目标**：把"静态调用样例"升级为可信的回归保护网。

- 引入 NUnit / xUnit
- 将 `Tests/` 静态样例（ItemsTest / QuestTest / ShopTest）转为真单元测试
  （1.A 的 `EventBusTest` 已先行落地，作为首个不依赖 Unity 的纯 C# 测试样板）
- 给每个新增模块补单元测试 + 1 个示例 mod 子项目
- 文档：每 `*Utils` 写中英双语 API doc，更新 README 与示例仓库链接

---

## 3. EnemyUtils 架构方案（待 Phase 3 启动时定夺）

> 三种方案的差异在"modder 写 AI 的姿势"和"运行时是否走 NodeCanvas"两个轴上。

### 方案 A — Hybrid（FML 推荐）
- **modder 视角**：实现 `IStateConfig` C# 接口（state + onUpdate/onEnter/onExit 回调 + transition 条件）
- **FML 内部**：`FML.StateMachineToBT.Compile()` 把 C# 状态机编译为一个 NodeCanvas `BehaviourTree`
- **依赖**：需 Harmony transpiler patch NodeCanvas `Graph.cs` 的 playmode 序列化 guard

| 优点 | 缺点 |
|---|---|
| 复用游戏现有 BT 节点的有用子集 | 需打一个一次性 transpiler 补丁 |
| modder 不需学 NodeCanvas | 运行时图无法在 NC 编辑器里调试（但 C# 日志可工作） |
| 复杂行为个性化的逃生舱：modder 可手建 BT 资产仍然接入 | modder 若想要"纯"代码控制仍需迁就 BT tick 模型 |

### 方案 B — 纯 C# 状态机（绕过 NodeCanvas）
- **modder 视角**：写 `enum EnemyState` + MonoBehaviour 自跑循环
- **FML 内部**：`FML.SimpleAIRunner : MonoBehaviour` 自己 tick，不走任何 NodeCanvas API

| 优点 | 缺点 |
|---|---|
| 无序列化坑、无 transpiler 依赖 | 无法复用游戏既有 Patrol/Chase 等 BT 节点，全部要重写 C# |
| 调试用纯 C# 思路，最小认知负担 | 不能用 NC 编辑器可视化编辑 |
| 即使 NodeCanvas DLL 在发行版被 strip/混淆也能工作 | 与游戏 AI 行为可能产生微妙差异 |

### 方案 C — 裸暴露 NodeCanvas
- **modder 视角**：直接写 `BehaviourTree` 资产或代码建图
- **FML 内部**：仅提供 `EnemyUtils.RegisterGraph(enemy, bt)` + 预制 `ActionTask<AICharacterController>` 库

| 优点 | 缺点 |
|---|---|
| 功能最全、最贴近游戏开发者思路 | modder 学习成本最高（需理解 NodeCanvas） |
| 可在 NC 编辑器里可视化设计 BT | modder 需引用 `NodeCanvas.Framework.dll`（许可摩擦） |
| 与游戏现有 BT 节点完全互通 | 序列化 guard 必须打补丁，且建图仍可能有限制 |

### 10 个已确认的可 patch 点（任一方案都将复用）

> 详细签名见反编译调研报告（由 explore agent 产出，本文件不展开）。

1. `CharacterRandomPreset.CreateCharacterAsync` — Pre/Post 修改 spawn 参数与返回角色
2. `CharacterCreator.CreateCharacter` — 替换 model prefab
3. `CharacterSpawnerRoot.StartSpawn` — 自定义 spawn 条件
4. `RandomCharacterSpawner.GetAPresetByWeight` — 注入自定义 preset 进权重池
5. `AIMainBrain.AddSearchTask` / `DoSearch` — 自定义瞄准/发现距离
6. `AICharacterController.Init` — Postfix 注入自定义 BT / 特殊附件 / AI 参数
7. `Health.Hurt` — Pre 改伤害 / Post 听死亡事件
8. `CharacterMainControl.SetTeam` — 运行时改阵营
9. `GameplayDataSettings.CharacterRandomPresetData.presets` — 注入自定义 preset 进全局列表
10. `LevelManager.InitLevel` Postfix — 关卡初始化完毕后追加 spawn

### NodeCanvas 序列化补丁的决定时机

当前**不列入任何 phase**。**Phase 3 实际启动 EnemyUtils 开发、确定选定方案 A 或 C
后，再决定是否提前在 Phase 1 / 1.5 做此 transpiler**。
若最终选方案 B（纯 C#），则永远不需要打这个补丁。

---

## 4. 已确认覆盖率矩阵（FML vs 游戏）

| 游戏系统 | FML 覆盖 | 状态 |
|---|---|---|---|
| Items / Inventory / Stats | ✅ | 完备（R6: `ReverseLookupRegistry`, `TryGetCustomItem`） |
| Crafting / Decompose | ✅ | 完备（R7: 迁移至 Registry, per-formula 管理） |
| Quests | ✅ | 完备（R5: `QuestRegistry`, `UnregisterQuestAll`） |
| I18n | ✅ | 完备（B4: 内部迁移到 EventBus `LanguageChangedEvent`） |
| Audio (SFX + BGM + Bus) | ✅ | 完备（R4: 删 mapdata, 扩 BGM/bus volume/mute） |
| Shop (A–Z) | ✅ | 完备（Add/Remove/Edit/CreateProfile/Query/Unregister） |
| **EventBus / 事件桥接** | ✅ | 完备（B1–B6: 15 事件桥接 + EventBusTest） |
| **Register 一体化** | ✅ | 完备（R1–R8: 扩能 + 5 模块迁移 + RegisterTest） |
| Economy (money / unlock) | ✅ | Phase 2 完成 |
| Buffs / 状态效果 | ✅ | Phase 2 完成 |
| CustomOptions (设置 UI) | ✅ | Phase 2 完成 |
| Perk Trees | ✅ | Phase 3 完成 |
| Buildings | ✅ | Phase 3 完成 |
| **Entities / AI / Spawner** | ✅ | Phase 3 EnemyUtils 完成（方案 A — Hybrid） |
| ModBehaviour 生命周期 | ✅ | OnAfterSetup + OnBeforeDeactivate 完整卸载链 |
| Save/Load 自定义数据 | ⚠️ | 通过 EventBus `CollectSaveDataEvent` 桥接，声明式基类待补 |
| Crops / Farming | N/A | 反编译可见但游戏未实装，不纳入考虑 |
| Black Market | N/A | 非 NPC 系统，事件 context 公开，不封装 |
| Weather / Seasons | ❌ | 空缺 (Phase 4) |
| Achievements / Statistics | ❌ | 空缺 (Phase 4) |
| Fishing | ❌ | 空缺 (Phase 4) |
| Multi-Scene | ❌ | 空缺 (Phase 4) |

---

## 5. 仓库卫生 ✅

> 全部完成。

- [x] 🚫 `0Harmony.dll` 二进制 vendored → **不改** `PackageReference Lib.Harmony`（版本硬性锁定 2.4.1.0）
- [x] `DuckovPath` 新增 `$(DUCKOV_PATH)` 环境变量优先
- [x] README 全面更新（反映全模块 API）
- [x] `Tests/` 已通过 `Condition="'$(Configuration)' != 'Debug'"` 条件编译控制

---

## 6. 关键子模块文档索引

- **EventBus 详细设计** → [`PLAN-EventBus.md`](./PLAN-EventBus.md)
  - 含参考来源、与 FML 现状的偏移决策、§5 两处参考缺陷的修复方案、
    B1..B6 实施阶段拆分、Adapter 层 15 个游戏事件桥接清单、I18n 迁移路径
- **Register 一体化详细设计** → [`PLAN-Register.md`](./PLAN-Register.md)
  - 含现有抽象能力短板盘点、各模块旁路字典现状、`IRegistry<T>` 扩能契约、
    `ReverseLookupRegistry<T,TKey>` 设计、owner (modid) 与 EventBus 的协同语义、
    R1..R8 实施阶段拆分、Audio/Quests/Shop/Crafting/Items 五模块迁移细则
- EnemyUtils 三方案对比 → 本文 §3
- NodeCanvas 序列化补丁决定时机 → 本文 §3 末尾

---

## 7. 当前状态与下一步

### 已完成
- ✅ **Phase 0** — 仓库卫生（全部完成）
- ✅ **Phase 1** — 框架内核加固（EventBus B1–B6 + Register 一体化 R1–R8 + ModBehaviour 生命周期）
- ✅ **Phase 2** — 头部消费系统（Economy / Buffs / CustomOptions）
- ✅ **Phase 3** — 内容创作系统全部完成（Shop / Audio / Buildings / PerkTrees / EnemyUtils）

### 待启动
- **Phase 4** — Achievements / Weather / Fishing / Multi-Scene（视社区需求）
- **Phase 5** — NUnit 全量测试 + 示例 mod 子项目 + 中英双语 API 文档

---

*本 PLAN.md 仅为高层路线图。具体代码级执行计划在每个子模块文档单独产出
（目前已有：PLAN-EventBus.md、PLAN-Register.md）。*