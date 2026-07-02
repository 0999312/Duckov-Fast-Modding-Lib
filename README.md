# Fast-Modding-Lib 快速模组开发库

_用于高效开发《逃离鸭科夫》模组。_

---

## 文档 / Documentation

详细文档请参阅 `Docs/` 目录：

| 文档 | 说明 | 适用人群 |
|------|------|----------|
| [Docs/USAGE.md](Docs/USAGE.md) | 完整使用指南 — 快速开始、各模块 API、项目结构参考 | **新项目开发者** |
| [Docs/MIGRATION.md](Docs/MIGRATION.md) | 迁移指南 — 从旧版 FML 迁移到最新 API | **已有模组开发者** |

---

## 配置 C# 工程 / Configuring C# Project

**注意：在上传 Steam Workshop 的时候，会覆写 info.ini。info.ini 中原有的信息可能会因此丢失。所以不建议在 info.ini 中存储除以上项目之外的其他信息。**

1. 在电脑上准备好《逃离鸭科夫》本体。
2. 通过 Visual Studio 软件创建一个 .NET 类库（Class Library）。
3. 配置工程参数。
   1) 框架（Target Framework）
      - **TargetFramework 建议设置为 .NET Standard 2.1**。
      - 注意删除 TargetFramework 不支持的功能，比如 `<ImplicitUsings>`。
   2) 添加引用（Reference Include）
      - 将《逃离鸭科夫》的 `\Duckov_Data\Managed\*.dll` 添加到引用中。
      - 可通过 `DUCKOV_PATH` 环境变量指定游戏路径，或直接在 csproj 中设置 `DuckovPath`。
      - 示例：
      ```xml
        <ItemGroup>
          <Reference Include="$(DuckovPath)\Duckov_Data\Managed\TeamSoda.*" />
          <Reference Include="$(DuckovPath)\Duckov_Data\Managed\ItemStatsSystem.dll" />
          <Reference Include="$(DuckovPath)\Duckov_Data\Managed\Unity*" />
          <Reference Include="$(DuckovPath)\Duckov_Data\Managed\Newtonsoft.Json.dll" />
          <Reference Include="$(DuckovPath)\Duckov_Data\Managed\FMODUnity.dll" />
          <Reference Include="$(DuckovPath)\Duckov_Data\Managed\ParadoxNotion.dll" />
          <Reference Include="$(DuckovPath)\Duckov_Data\Managed\UniTask*" />
        </ItemGroup>
      ```

4. 工程配置完成！现在在你 Mod 工程的 Namespace 中编写一个 `ModBehaviour` 主类（继承 `FastModdingLib.ModBehaviour`）。
5. 手动导入本项目构建完成的 dll（可在 Steam 创意工坊或 Release 中获取）。
6. 构建工程，即可得到你的 mod 的主要 dll。然后整理好文件夹结构，即可开始本地测试。

---

## 快速上手 / Quick Start

若您使用本库，请在配置完项目之后，在您的主类（`ModBehaviour`）里添加以下成员：

```csharp
string dllPath = Assembly.GetExecutingAssembly().Location;
```

用于在后续使用中正确调取模组目录。

> 完整 API 文档请参阅 **[Docs/USAGE.md](Docs/USAGE.md)**。

---

## 模组功能速览 / Module Overview

以下是 FML 提供的各模块快速索引。**完整 API 文档请参阅 [Docs/USAGE.md](Docs/USAGE.md)**。

| 模块 | 示例 | 文档章节 |
|------|------|----------|
| **物品** `ItemUtils` | `CreateCustomItem(id, data)` | [§4](Docs/USAGE.md#4-物品系统itemutils) |
| **合成** `CraftingUtils` | `AddCraftingFormula(id, money, cost, result, ...)` | [§5](Docs/USAGE.md#5-合成配方craftingutils) |
| **任务** `QuestUtils` | `RegisterQuest(data, modid)` | [§6](Docs/USAGE.md#6-任务系统questutils) |
| **商店** `ShopUtils` | `AddGoods(data, modid)` | [§7](Docs/USAGE.md#7-商店系统shoputils) |
| **音频** `AudioUtil` | `AudioUtil.Instance.RegisterAudio(id, data)` | [§8](Docs/USAGE.md#8-音频系统audioutil) |
| **本地化** `I18n` | `I18n.InitI18n()` | [§9](Docs/USAGE.md#9-本地化i18n) |
| **事件总线** `EventBus` | `EventBusManager.Instance.Sync.Register<T>(h)` | [§10](Docs/USAGE.md#10-事件总线eventbus) |
| **经济** `EconomyUtils` | `AddMoney(1000)` | [§11](Docs/USAGE.md#11-经济系统economyutils) |
| **Buff** `BuffUtils` | `RegisterBuff(id, prefab)` | [§12](Docs/USAGE.md#12-buff-状态效果buffutils) |
| **建筑** `BuildingUtils` | `RegisterBuilding(id, info, prefab)` | [§13](Docs/USAGE.md#13-建筑系统buildingutils) |
| **Perk** `PerkTreeUtils` | `AddPerk(id, req, icon)` | [§14](Docs/USAGE.md#14-perk-技能树perktreeutils) |
| **天赋** `EndowmentUtils` | `RegisterEndowment(id, entry)` | [§15](Docs/USAGE.md#15-天赋系统endowmentutils) |
| **敌人** `EnemyUtils` | `RegisterEnemy(id, aiConfig, preset)` | [§16](Docs/USAGE.md#16-敌人系统enemyutils) |
| **设置面板** `ModOptionsRegistry` | `RegisterPanel(modId, name, builder)` | [§17](Docs/USAGE.md#17-自定义设置面板modoptionsregistry) |
| **AssetBundle** `AssetUtil` | `LoadBundle("weapons")` | [§18](Docs/USAGE.md#18-assetbundle-加载assetutil) |

### 卸载生命周期

模组卸载时，`OnBeforeDeactivate` 自动执行：

```
GameEventAdapters.TearDown()    → 解除原生事件
EventBusManager.Clear()         → 清空 handler
RegistryManager.RemoveAllByOwner() → 批量卸载全部资源
```

无需手动处理卸载逻辑。详见 [Docs/USAGE.md §20](Docs/USAGE.md#20-模组卸载生命周期)。

---

## 迁移指南 / Migration Guide

已有模组从旧版 FML 迁移到最新 API，请参阅 **[Docs/MIGRATION.md](Docs/MIGRATION.md)**。

---

## 示例项目 / Sample Projects

1. [Duckov FML Gun Example](https://github.com/0999312/Duckov-FML-Gun-Example) — 自定义武器示例
