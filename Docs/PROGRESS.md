# 项目进度文档 (PROGRESS.md)

> 最后更新：2026-07-01

---

## Phase 0 — 仓库与工程基础整理 ✅ 已完成

**完成时间**: 2026-06-20
**耗时**: 约 2 小时

### 文件变更清单
| 操作 | 文件路径 | 改动摘要 |
|---|---|---|
| 新建 | `.gitignore` | 写入根 gitignore，覆盖 `DecompiledDLL/`、`.vs/` 等 |
| 修改 | `README.md` | 全面更新，反映全模块 API |
| 修改 | `FastModdingLib/DuckovPath.targets` | 新增 `$(DUCKOV_PATH)` 环境变量优先 |
| 修改 | `Tests/Tests.csproj` | 通过 `Condition` 控制 Debug 配置排除 |
| 删除 | 嵌套 `.sln` | 删除子目录的重复 sln 文件 |

### 遗留问题
- 无

---

## Phase 1 — 框架内核加固 ✅ 已完成

**完成时间**: 2026-06-25
**耗时**: 约 6 小时

### 文件变更清单
| 操作 | 文件路径 | 改动摘要 |
|---|---|---|
| 新建 | `FastModdingLib/Events/` | EventBus 核心 + AsyncEventBus + 15 个游戏事件桥接 |
| 新建 | `FastModdingLib/Events/EventBusTest.cs` | EventBus 7 个单元测试用例 |
| 新建 | `FastModdingLib/Register/` | Register 一体化：IRegistry、SimpleRegistry、ReverseLookupRegistry、RegistryManager、ModScope |
| 新建 | `FastModdingLib/Register/RegisterTest.cs` | 15 个 Register 测试用例 |
| 修改 | `FastModdingLib/ModBehaviour.cs` | 生命周期：OnAfterSetup 调 EventBus + Register bootstrap |
| 修改 | 多个模块 | Audio/Quests/Shop/Items/Crafting 五模块旁路字典收编到 Registry |

### 遗留问题
- 无

---

## Phase 2 — 头部消费系统 ✅ 已完成

**完成时间**: 2026-06-27
**耗时**: 约 3 小时

### 文件变更清单
| 操作 | 文件路径 | 改动摘要 |
|---|---|---|
| 新建 | `FastModdingLib/EconomyUtils.cs` | Money 增删查、SetMoney、物品解锁/确认/查询 |
| 新建 | `FastModdingLib/BuffUtils.cs` + `BuffRegistry.cs` | Buff 注册/查询/卸载 |
| 新建 | `FastModdingLib/Options/` | ModOptionsBuilder + ModOptionsRegistry（Toggle/Slider/Dropdown/Button） |

### 遗留问题
- 无

---

## Phase 3 — 内容创作系统 ✅ 已完成

**完成时间**: 2026-06-29
**耗时**: 约 5 小时

### 文件变更清单
| 操作 | 文件路径 | 改动摘要 |
|---|---|---|
| 新建 | `FastModdingLib/Shop/ShopUtils.cs` + `ShopRegistry.cs` | 完整 A-Z 商店 API |
| 新建 | `FastModdingLib/Audio/AudioUtil.cs` | BGM 控制 + FMOD 总线音量 |
| 新建 | `FastModdingLib/PerkTrees/PerkTreeUtils.cs` + `PerkTreeRegistry.cs` | 基础 API：AddPerk、ConnectPerks、ForceUnlock |
| 新建 | `FastModdingLib/Buildings/BuildingUtils.cs` + `BuildingRegistry.cs` | 基础 API：RegisterBuilding、PlaceBuilding（占位） |
| 新建 | `FastModdingLib/Entities/` | EnemyUtils、IStateConfig、StateMachineToBT、EnemyRegistry、3 个 Patch 文件 |

### 遗留问题
- PlaceBuilding 抛 NotSupportedException（Phase 4 B1 修复）
- ConnectPerks 用 try/catch 反射包装，脆弱（Phase 4 P1 重写）
- Endowment 完全缺失（Phase 4 E1 从零实现）

---

## Phase 4 — Building / PerkTree / Endowment / UI 深化 ✅ 已完成

**完成时间**: 2026-07-01
**耗时**: 约 3 小时
**审查修复完成**: 2026-07-01

### 设计原则
所有新增/修改的 public API 统一使用 `Identifier` 作为资源标识符，
游戏原生数字 ID（如 `EndowmentIndex` 枚举值）由 FML 内部自动分配，对 modder 完全透明。

### 文件变更清单

#### B1/B2 — Building 深化
| 操作 | 文件路径 | 改动摘要 |
|---|---|---|
| 新建 | `FastModdingLib/Buildings/Patches/BuildingCollectionPatch.cs` | 3 个 Harmony Postfix（GetInfo/GetPrefab/GetBuildingsToDisplay） |
| 修改 | `FastModdingLib/Buildings/BuildingUtils.cs` | PlaceBuilding 反射实现 + Identifier 化；GetBuildingInfo(Identifier) 新增；GetAllBuildingIds() 返回 IReadOnlyList\<Identifier\>；[Obsolete] string 重载保留 |
| 修改 | `FastModdingLib/Buildings/BuildingRegistry.cs` | 新增 GetAllInfos() 供 Patch 层遍历 |

#### P1 — PerkTree 稳健化
| 操作 | 文件路径 | 改动摘要 |
|---|---|---|
| 修改 | `FastModdingLib/PerkTrees/PerkTreeUtils.cs` | ConnectPerks 重写（去 try/catch + NodeCanvas 直接 API）；AddPerk(Identifier) Identifier 化；新增 AddPerkBehaviour\<T\>；新增 RegisterPerkTree 完整创建自定义树；ForceUnlock(Identifier) Identifier 化；保留 [Obsolete] string 重载 |
| 新建 | `FastModdingLib/PerkTrees/Patches/PerkTreeEnablePatch.cs` | LevelConfig.IsPerkTreeEnabled Prefix——自定义 treeId 返回 true |
| 新建 | `FastModdingLib/PerkTrees/Patches/PerkTreeCollectGuard.cs` | PerkTree.Collect Prefix——跳过 FML 树的 Collect |

#### E1 — Endowment 完整实现
| 操作 | 文件路径 | 改动摘要 |
|---|---|---|
| 新建 | `FastModdingLib/Endowment/EndowmentUtils.cs` | 完整 API：RegisterEndowment/UnregisterEndowment/SelectEndowment/IsEndowmentUnlocked/UnlockEndowment/GetCurrentSelection——全部走 Identifier |
| 新建 | `FastModdingLib/Endowment/EndowmentRegistry.cs` | SimpleRegistry\<EndowmentEntry\> + Identifier→EndowmentIndex 内部映射（≥10） |
| 新建 | `FastModdingLib/Endowment/Patches/EndowmentManagerPatch.cs` | Awake Postfix 注入 + SelectIndex Prefix |

#### U1 — UI 交互辅助
| 操作 | 文件路径 | 改动摘要 |
|---|---|---|
| 新建 | `FastModdingLib/UI/InteractTemplates.cs` | 三个 InteractableBase 子类模板（Building/PerkTree/Endowment） |
| 修改 | `FastModdingLib/Register/RegisterBootstrap.cs` | 新增 EndowmentUtils.Init() 调用 |

### 设计偏离
- **ModifierDescription 类型不可编译引用**：`EndowmentUtils.RegisterEndowment` 便捷重载改用 `object[]` 参数代替 `ModifierDescription[]`，避免对游戏内部类型的编译期依赖。运行时通过反射设置到 EndowmentEntry.modifiers 字段。
- **PerkTree.ConnectTo 接口存疑**：`PerkRelationNode.ConnectTo()` 在编译期不可用（NodeCanvas 版本差异），回退为反射调用 + graph.ConnectNodes 双重兜底方案。
- **EndowmentManager API 签名差异**：`IsUnlocked` 和 `UnlockEndowment` 通过反射调用，以兼容 static/instance 两种可能的签名。

### 验证结果
- [x] `dotnet build` 通过（0 错误，25 警告——均为预存警告，无新增）
- [x] 所有新增 public API 使用 Identifier（无裸 string/数字 ID）
- [x] 不修改 0Harmony.dll 引用方式

### 审查修复记录（2026-07-01 二次审查）
- **`PerkTreeUtils.ResolveTreeId`**：原本只返回 domain，现改为先查已注册 PerkTree 再查原生 treeId
- **`PerkTreeUtils.RegisterPerkTree`**：原本用 `null` 作为 registry value，现改用 HashSet 跟踪 treeId + 正确 cleanup
- **`PerkTreeUtils.RemoveAllPerks`**：新增自定义 PerkTree 的 GameObject 销毁和 PerkTreeManager 列表清理
- **`PerkTreeEnablePatch`/`PerkTreeCollectGuard`**：从名称前缀检测改为 `IsFMLTree()` 注册表检测
- **PLAN.md "Stub / 空缺" 部分**：Endowment/Building/PerkTree 状态从 ❌/⚠️ 更新为 ✅
- **PLAN-Phase4.md 验收清单**：所有 `[ ]` 更新为 `[x]`，添加 ✅ 状态标记

### 遗留问题
- 无（OnBuildingBuilt 已通过 Wave 2 修复）

### 未实现的 PLAN-Phase4 设计项
以下组件在 `PLAN-Phase4-Building-Perk-Endowment-UI.md` §14-17 中有详细设计但未在 Phase 4 中实现，
已移至后续 Phase 5 或更晚处理：

| 组件 | 设计章节 | 状态 |
|------|---------|------|
| `EnemyPresetData` DTO + `ModelRef` | §14 | ✅ 已完成（Wave 2 补实现） |
| `CreateSimpleBuilding()` | §15.1 | ✅ 已完成（Wave 2 补实现） |
| `SetBuildingModel()` | §15.2 | ✅ 已完成（Wave 2 补实现） |
| `OnBuildingBuilt` 真回调 | §13-A.4 | ✅ 已完成（Wave 2 补实现） |
| `SimpleViewBuilder` | §16.2 | ✅ 已完成（Wave 3 补实现） |
| UI 注入辅助 | §16.1 | ⏳ 待 Phase 5 |
| `ItemEntry.ByTag()` + `WithDurabilityCost()` | §13-C.3 | ✅ 已完成（Wave 2 补实现） |

### Wave 修复记录（2026-07-01 文档&代码修复）
- **Wave 1（文档）**：MIGRATION.md API 签名修正、PLAN.md 索引/矩阵/日期更新、PROGRESS.md 补充未实现项、USAGE.md 注释修正
- **Wave 2（代码）**：`EnemyPresetData.cs` + `ModelRef` 新建；`BuildingUtils.CreateSimpleBuilding`/`SetBuildingModel`/反射事件订阅；`CraftingData.ItemEntry` 扩展 `ByTag`/`WithDurabilityCost`
- **Wave 3（代码）**：`SimpleViewBuilder.cs` 新建；USAGE.md 补充文档

### Wave 遗漏模块补录（2026-07-02 审计发现）
以下模块已在代码中实现但未在 Wave 2/3 记录中列出，存在已实现代码无对应进度记录的问题：

| 模块 | 文件路径 | 状态 |
|------|---------|------|
| `TagCostRegistry` + `TagCostValidator` + `CraftingManagerPatch` | `FastModdingLib/Crafting/` | ✅ 已实现（标签合成 Patch 系统） |
| `FMLTask_KillCountByTag` + `FMLTask_SubmitItemByTag` | `FastModdingLib/Quests/` | ✅ 已实现（任务扩展子类） |
| `TaskKillByTagData` + `TaskSubmitItemByTagData` | `FastModdingLib/Quests/QuestData.cs` | ✅ 已实现（任务数据 DTO） |
| `FaceRef` + `FacePartIds` + `FaceRefMode` + `NpcRole` | `FastModdingLib/Entities/FaceRef.cs` + `EnemyPresetData.cs` | ✅ 已实现（捏脸引用类型） |

---

## Phase 5 — 长尾幂等系统 ⏳ 待启动

**计划内容**（详见 PLAN.md §7）：
- Achievements（成就系统）
- Weather / Seasons（天气/季节）
- Fishing（钓鱼）
- Multi-Scene（多场景支持）
- 友善 NPC 交互（FIX-PLAN-v1.md 附录 A.2）
- UI 注入辅助（FIX-PLAN-v1.md 附录 A.3）
- 标签驱动的物品需求（FIX-PLAN-v1.md 附录 A.4）

**已完成的前置工作**（可在 Phase 5 启用）：
- `FaceRef` / `FacePartIds` / `NpcRole` 类型已就绪
- `FMLTask_KillCountByTag` / `FMLTask_SubmitItemByTag` 类型已就绪
- `TagCostRegistry` / `TagCostValidator` / `CraftingManagerPatch` 已就绪

### 遗留问题
- 待 Phase 5 正式启动时补充详细计划文档（PLAN-Phase5-*.md）

---

## Phase 6 — 质量 ⏳ 待启动

**计划内容**（详见 PLAN.md §7）：
- NUnit 单元测试体系建设
- 示例 mod 项目（完整可运行 demo）
- 中英双语 API 文档完善
- CI/CD 流水线搭建

### 遗留问题
- 待 Phase 6 正式启动时补充详细计划文档（PLAN-Phase6-*.md）
