# Fast-Modding-Lib 迁移指南 / Migration Guide

_面向已有模组作者，帮助将基于旧版 FML 的模组迁移到最新 API。_

---

## 目录

1. [mod 主类迁移（ModBehaviour）](#1-mod-主类迁移modbehaviour)
2. [mod 身份标识（modid）](#2-mod-身份标识modid)
3. [Identifier 使用规范](#3-identifier-使用规范)
4. [物品系统迁移（ItemUtils）](#4-物品系统迁移itemutils)
5. [合成配方迁移（CraftingUtils）](#5-合成配方迁移craftingutils)
6. [任务系统迁移（QuestUtils）](#6-任务系统迁移questutils)
7. [商店系统迁移（ShopUtils）](#7-商店系统迁移shoputils)
8. [音频系统迁移（AudioUtil）](#8-音频系统迁移audioutil)
9. [事件总线（EventBus）](#9-事件总线eventbus)
10. [注册表系统（Registry）](#10-注册表系统registry)
11. [新增模块速览](#11-新增模块速览)
12. [完整迁移示例](#12-完整迁移示例)
13. [常见问题](#13-常见问题)

---

## 1. mod 主类迁移（ModBehaviour）

### 旧版
```csharp
public class MyMod : Duckov.Modding.ModBehaviour
{
    public void Awake()
    {
        I18n.InitI18n(dllPath);
    }
}
```

### 新版
```csharp
public class MyMod : Duckov.Modding.ModBehaviour, IHasModid
{
    string dllPath = Assembly.GetExecutingAssembly().Location;

    public string GetModid() => "MyModId";

    protected override void OnAfterSetup()
    {
        ModPathResolver.Register(GetModid(), dllPath);
        I18n.InitI18n(GetModid());

        // 自行管理 Harmony：
        var harmony = new Harmony(GetModid());
        harmony.PatchAll(Assembly.GetExecutingAssembly());

        // 其他自定义初始化...
    }
}
```

**变化说明**：
- 继承 **`Duckov.Modding.ModBehaviour`**（与旧版相同），**不再**继承 `FastModdingLib.ModBehaviour`
- 实现 **`IHasModid`** 接口 — FML 工具通过此接口获取你的 mod 身份
- **不再调用** `base.OnAfterSetup()` — FML 的单例（Registry / EventBus）由 `FMLBootstrap` 自动管理
- 自行创建 Harmony 实例并 `PatchAll` 自身程序集（不再自动）
- `GetModid()` 必须显式实现（不再自动从程序集名推导）

---

## 2. mod 身份标识（modid）

### 核心变化

| 旧版 | 新版 |
|------|------|
| `GetModid()` 自动从程序集名推导 | 实现 `IHasModid` 接口，显式返回自身 modid |
| 继承 `FastModdingLib.ModBehaviour` | 继承 `Duckov.Modding.ModBehaviour` + 实现 `IHasModid` |
| 各模块 `UnregisterAll` 需手动传 modid | `FMLBootstrap.TearDownMod(GetModid())` 自动按 modid 卸载 |

### 推荐用法

**方法 A — 不传 modid（自动使用当前 mod 身份）**
```csharp
// RegisterItem 不传 modid → 从 id.Domain 自动推导
ItemUtils.RegisterItem(new Identifier("mymod", "coffee"), coffeeItem);
```

**方法 B — 显式传 modid（旧式 API 仍保留）**
```csharp
CraftingUtils.AddCraftingFormula("coffee_recipe", 100, costItems, 200001, 1, modid: "MyWeaponPack");
```

**方法 C — 使用 EnterModScope**
```csharp
// 临时切换 mod 身份作用域（IDisposable，using 块退出后自动还原）
using (RegistryManager.EnterModScope("MyMod"))
{
    EventBusManager.Instance.Sync.Register<HurtEvent>(OnHurt);
    AudioUtil.Instance.RegisterAudio(id, audioData);
}
```

### 迁移操作
1. 搜索所有 `"old_fml_version"` 调用，替换为 `null` 或你的 mod 名称。
2. 搜索 `"TopTierWeaponExpansion"`，同样替换。
3. 搜索 `"FastModdingLib"`（某些模块的默认值），同样替换。

---

## 3. Identifier 使用规范

### 格式
```csharp
new Identifier("domain", "path")
// 字符串形式: "domain:path"
// 例如: new Identifier("mymod", "rifle_ak47") → "mymod:rifle_ak47"
```

### 校验规则（自动）
- 禁止 `:` / `\\` / `..` / 空字符串。`domain` 禁止 `/`，`path` 允许 `/` 以支持子目录。
- `ToString()` 输出 `"domain:path"` 格式，便于调试。

### 解析
```csharp
// 从字符串解析
Identifier id = Identifier.Parse("mymod:rifle_ak47");

// 安全解析
if (Identifier.TryParse("mymod:rifle_ak47", out var parsed))
{
    // parsed 可用
}
```

### 作为可空类型
```csharp
// 部分 API（如 TryGetIdentifier）使用 Identifier? 可空返回值
if (audioRegistry.TryGetIdentifier(eventName, out Identifier? id))
{
    // 使用 id
}
```

---

## 4. 物品系统迁移（ItemUtils）

### 核心变化

新版 `ItemUtils` 全部改为 **Identifier-first 模式**：所有注册方法第一个参数均为 `Identifier id`，
modid **不再作为方法参数**，而是从 `id.Domain` 自动推导。modPath 也不再需要——库会自动探测 mod 目录。

### 方法签名变化

| 方法 | 旧签名 | 新签名 |
|------|--------|--------|
| `CreateCustomItem` | `(string modPath, ItemData config, string modid)` | `(Identifier id, ItemData config)` |
| `GetCustomItem` | `(string modPath, ItemData config, string modid)` | `(Identifier id, ItemData config)` — 便捷重载：`(ItemData config)` 自动推导 modid |
| `CreateCustomBluePrint` | `(BlueprintData config, string modid)` | `(Identifier id, BlueprintData config)` |
| `RegisterItem` | `(Item item, string modid)` | `(Identifier id, Item item)` |
| `RegisterGun` | `(AssetBundle bundle, string name, int originGunID, string modid)` | `(Identifier id, AssetBundle bundle, string name, int originGunID = 654)` |
| `RegisterItemFromBundle` | `(AssetBundle bundle, string name, string modid)` | `(Identifier id, AssetBundle bundle, string name)` |
| `CreateCustomBullet` | `(BulletData config, string modPath, string modid)` | `(Identifier id, BulletData config)` |
| `UnregisterAllItem` | `(string modid)` | `(string? modid = null)` — 走 CurrentModid 兜底 |
| `TryGetCustomItem` | — | `(int typeID, out Item? item)` — 不变 |

### 新增 API
```csharp
// 仅构造 Item 实例，不注册到 Registry（适合需要手动设置属性的场景）
Item item = ItemUtils.GetCustomItem(itemData);

// 加载内嵌 Sprite（从 assets/textures/）
Sprite? sprite = ItemUtils.LoadSprite("icon_rifle", 150001);
```

### TypeID 冲突自动处理
```csharp
// 如果 typeID 与现有物品冲突，RegisterItem 会自动分配可用 ID
ItemUtils.CreateCustomItem(new Identifier("mymod", "coffee"), drinkData);
```

### 迁移步骤
1. 将 `CreateCustomItem(dllPath, data, "old_fml_version")` 改为 `CreateCustomItem(new Identifier("mymod", "coffee"), data)`。
2. 将 `RegisterItem(component, "old_fml_version")` 改为 `RegisterItem(new Identifier("mymod", "coffee"), component)`。
3. 删除所有 `modPath` 参数——库自动探测 mod 目录。
4. modid 从 `Identifier.Domain` 自动推导，无需额外传参。

---

## 5. 合成配方迁移（CraftingUtils）

### 方法签名变化

| 方法 | 旧签名 | 新签名 |
|------|--------|--------|
| `AddCraftingFormula` | `(..., string modid = "old_fml_version")` | `(string formulaId, long money, (int id, long amount)[] costItems, int resultItemId, int resultItemAmount, string[]? tags, string requirePerk, bool unlockByDefault, bool hideInIndex, bool lockInDemo, string? modid = null)` |
| `AddDecomposeFormula` | `(..., string modid = "old_fml_version")` | `(int itemId, long money, (int id, long amount)[] resultItems, string? modid = null)` |
| `RemoveAllAddedFormulas` | `(string modid = "old_fml_version")` | `(string? modid = null)` |
| `RemoveAllAddedDecomposeFormulas` | `(string modid = "old_fml_version")` | `(string? modid = null)` |

### 示例
```csharp
// 合成配方（带标签和前置技能）
CraftingUtils.AddCraftingFormula(
    "ammo_pack", 100,
    new[] { (1001, 5L), (1002, 2L) },
    resultItemId: 200001,
    resultItemAmount: 10,
    tags: new[] { "WorkBenchAdvanced" },
    requirePerk: "reloading_expert",
    unlockByDefault: false,
    modid: "MyMod"
);

// 分解配方
CraftingUtils.AddDecomposeFormula(
    itemId: 200001, money: 50,
    new[] { (1001, 3L) },
    modid: "MyMod"
);

// 卸载
CraftingUtils.RemoveAllAddedFormulas("MyMod");
CraftingUtils.RemoveAllAddedDecomposeFormulas("MyMod");
```

### 新增 per-formula 管理
```csharp
// CraftingFormulaRegistry / DecomposeRegistry 均为 SimpleRegistry 子类
// 支持 OnRemoved 回调自动清理 native 侧残留
if (craftingFormulaRegistry.TryGet(new Identifier("crafting", "ammo_pack"), out var formula))
{
    // 配方已存在
}
```

**迁移步骤**：将 `"old_fml_version"` 参数替换为你自己的 mod 名称或 `null`。

---

## 6. 任务系统迁移（QuestUtils）

### 方法签名变化

| 方法 | 变化 |
|------|------|
| `RegisterQuest(QuestData data, string modid)` | modid 默认值从 `"FastModdingLib"` 改为建议显式传入 |

**建议**：之前如果不传 modid，默认值是 `"FastModdingLib"`，所有任务会被注册到 FML 自身名下。现在请显式传入你的 mod 名称：
```csharp
QuestUtils.RegisterQuest(questData, "MyWeaponPack");
```

### Unregister 的变化
- `UnregisterQuestAll(modID)` 行为不变，但现在走 `QuestRegistry.RemoveAllByOwner`。
- 卸载时会自动从 `GameplayDataSettings.QuestCollection` 移除并 Destroy 任务 GameObject。

---

## 7. 商店系统迁移（ShopUtils）

### 旧版（仅 AddGoods，无卸载）
```csharp
// 旧版 ShopUtils 只有这个可用
ShopUtils.AddGoods(data);
// 但无法卸载，无法查询，无法编辑
```

### 新版（完整的 CRUD）
```csharp
// 1. 注册商品（modid 走 CurrentModid 兜底或显式传入）
ShopUtils.AddGoods(new ShopGoodsData
{
    merchantProfileID = "Merchant_Normal",
    typeID = 150001,
    maxStock = 10,
    priceFactor = 1.0f
}, "MyMod");

// 2. 查询商品
if (ShopUtils.TryGetGoods("Merchant_Normal", 150001, out var data))
{
    Debug.Log($"Current stock: {data.maxStock}");
}

// 3. 修改商品属性
ShopUtils.EditGoods("Merchant_Normal", 150001, new ShopGoodsData
{
    maxStock = 20,
    priceFactor = 1.5f
});

// 4. 移除单个商品
ShopUtils.RemoveGoods("Merchant_Normal", 150001);

// 5. 创建新商人
ShopUtils.CreateMerchantProfile("MyTrader");

// 6. 按商人批量移除
ShopUtils.RemoveAllGoods("Merchant_Normal");

// 7. 按 mod 批量卸载
ShopUtils.UnregisterAllGoods("MyMod");
```

**迁移步骤**：
1. `AddGoods` 调用增加 modid 参数（或去掉原有硬编码）。
2. 不再需要自行维护 `Dictionary` 来追踪商店条目（Registry 自动管理）。

---

## 8. 音频系统迁移（AudioUtil）

### SFX 注册 — Identifier 化
```csharp
// 新版：使用 Identifier + AudioData
AudioUtil.Instance.RegisterAudio(
    new Identifier("mymod", "gun_shot"),
    new AudioData
    {
        Path = "events/Weapons/GunShot",
        Eventname = "GunShot",
        MinDistance = 1f,
        MaxDistance = 500f
    }
);
```

### 新增 BGM API（之前只能用 SFX）
```csharp
// 播放内置 BGM
AudioUtil.PlayBGM("theme");

// 播放自定义 BGM 文件
AudioUtil.PlayCustomBGM("path/to/music.ogg");

// 停止 BGM
AudioUtil.StopBGM();

// 切换 BGM
AudioUtil.SwitchBGM("battle");

// 检查是否正在播放
bool isPlaying = AudioUtil.IsBGMPlaying();
```

### 新增音量总线控制（之前没有）
```csharp
// 总音量
AudioUtil.SetMasterVolume(0.8f);
float vol = AudioUtil.GetMasterVolume();
AudioUtil.SetMasterMute(true);

// 音乐音量
AudioUtil.SetMusicVolume(0.5f);
float musicVol = AudioUtil.GetMusicVolume();
AudioUtil.SetMusicMute(false);

// SFX 音量
AudioUtil.SetSFXVolume(1.0f);
float sfxVol = AudioUtil.GetSFXVolume();
AudioUtil.SetSFXMute(false);
```

### 内部变化（无需修改 mod 代码）
- 改用 `ReverseLookupRegistry<AudioData, string>`，以 `Eventname` 为 native key。
- 反向查询（eventName → Identifier）走 `TryGetIdentifier`。

---

## 9. 事件总线（EventBus）

### 新增功能（之前需要手动 hook 游戏事件）
```csharp
// 订阅玩家金钱变化
EventBusManager.Instance.Sync.Register<MoneyChangedEvent>(e =>
{
    Debug.Log($"Money: {e.OldMoney} → {e.NowMoney}");
});

// 订阅角色受伤
EventBusManager.Instance.Sync.Register<HurtEvent>(OnHurt);

// 订阅角色死亡
EventBusManager.Instance.Sync.Register<EntityDeathEvent>(OnDeath);

// 订阅关卡初始化
EventBusManager.Instance.Sync.Register<LevelInitializedEvent>(OnLevelInit);

// 订阅语言切换（I18n 已内部迁移至此）
EventBusManager.Instance.Sync.Register<LanguageChangedEvent>(OnLangChanged);
```

### 订阅与卸载
```csharp
// 以当前 mod 身份注册（自动关联卸载）
EventBusManager.Instance.Sync.Register<HurtEvent>(OnHurt, 0, RegistryManager.CurrentModid);

// mod 卸载时自动 UnregisterAll（无需手动处理）
```

### 15 个可订阅的游戏事件
| 事件类型 | 触发时机 | 可取消 |
|----------|----------|--------|
| `HurtEvent` | 角色受伤 | 可标记（effect 已应用） |
| `EntityDeathEvent` | 角色死亡 | 仅观察 |
| `LevelInitializedEvent` | 关卡初始化完成 | 仅观察 |
| `MoneyChangedEvent` | 金钱变化 | 仅观察 |
| `LanguageChangedEvent` | 游戏语言切换 | 仅观察 |
| `PlayerHearSoundEvent` | 玩家听到声音 | 仅观察 |
| `SoundSpawnedEvent` | 声音产生 | 仅观察 |
| `PlayerDeathEvent` | 玩家死亡 | 仅观察 |
| `ControllingCharacterChangedEvent` | 切换控制角色 | 仅观察 |
| `ItemUnlockStateChangedEvent` | 物品解锁状态变化 | 仅观察 |
| `ItemCraftedEvent` | 物品制作成功 | 仅观察 |
| `FormulaUnlockedEvent` | 配方解锁 | 仅观察 |
| `QuestTaskFinishedEvent` | 任务目标完成 | 仅观察 |
| `CollectSaveDataEvent` | 收集存档数据 | 仅观察 |
| `KillCountChangedEvent` | 击杀数变化 | 仅观察 |

---

## 10. 注册表系统（Registry）

### 核心抽象

所有模块现在使用统一的 `IRegistry<T>` 抽象，提供完整的 CRUD + owner 追踪：

| 方法 | 说明 |
|------|------|
| `Set(id, value)` | 写入（owner=CurrentModid） |
| `Set(id, value, modid)` | 写入（指定 owner） |
| `Get(id)` / `TryGet(id)` | 读取 |
| `Remove(id)` | 删除单条 |
| `Remove(id, out modid)` | 删除单条并返回 owner |
| `RemoveAllByOwner(modid)` | 批量卸载 |
| `GetAllByOwner(modid)` | 按 owner 查询 |
| `TryGetOwner(id, out modid)` | 查询 owner |
| `Clear()` | 清空全部 |
| `foreach` 遍历 | 支持 `IEnumerable` |

### 两种实现

| 实现 | 特点 | 使用场景 |
|------|------|----------|
| `SimpleRegistry<T>` | 完整 CRUD + owner 追踪 + `OnRemoved` 回调 | 常规模块（Quest / Buff / Building 等） |
| `NonAlterableSimpleRegistry<T>` | 写入后不可修改或覆盖 | 元注册表（`RegistryManager.Registry`） |
| `ReverseLookupRegistry<T, TKey>` | 在 SimpleRegistry 基础上维护 native key → Identifier 反向索引 | Audio / Items / Shop 需按游戏原生 key 反查的场景 |

### 反向查询（Audio / Decompose 类场景）
```csharp
// ReverseLookupRegistry 支持按 native key 反查 Identifier
if (audioRegistry.TryGetIdentifier(eventName, out Identifier? id))
{
    // 找到对应 Identifier
}

// DecomposeRegistry 自实现反向索引
if (decomposeRegistry.TryGetIdentifier(itemId, out Identifier? id))
{
    // 找到对应 Identifier
}
```

### 元注册表
```csharp
// 所有 registry 可通过 RegistryManager.Registry 元表遍历
var meta = RegistryManager.Instance.Registry;

// 手动注册自定义 registry（NonAlterableSimpleRegistry 不允许覆盖已有 key）
meta.Set(new Identifier("mymod", "myregistry"), myRegistry, "MyMod");
// 或使用 SetIfAbsent（幂等安全）
meta.SetIfAbsent(new Identifier("mymod", "myregistry"), myRegistry, "MyMod");
```

### OnRemoved 生命周期
```csharp
// 子类可 override OnRemoved 来自动清理 native 侧资源
public class MyRegistry : SimpleRegistry<MyType>
{
    protected override void OnRemoved(Identifier id, MyType value, string? modid)
    {
        // 自动清理 native 侧（如 Destroy GameObject、移除列表条目）
        Destroy(value.gameObject);
    }
}
```

---

## 11. 新增模块速览

### EconomyUtils（全新）
```csharp
EconomyUtils.AddMoney(1000);
EconomyUtils.RemoveMoney(500);
EconomyUtils.SetMoney(5000);
long money = EconomyUtils.GetMoney();
EconomyUtils.UnlockItem(itemTypeId);
bool unlocked = EconomyUtils.IsItemUnlocked(itemTypeId);

// 订阅金钱变化
EconomyUtils.OnMoneyChanged(handler);
EconomyUtils.RegisterMoneyChangedCallback((old, now) => { ... });
```

### BuffUtils（全新）
```csharp
// 注册 Buff（modid 从 id.Domain 自动推导）
BuffUtils.RegisterBuff(new Identifier("mymod", "mybuff"), buffPrefab);

// 按 ID 查找 Buff
Buff buff = BuffUtils.FindBuff(buffID);

// 批量卸载
BuffUtils.UnregisterAllBuffs("MyMod");

// 移除单个
BuffUtils.UnregisterBuff(new Identifier("mymod", "mybuff"));
```

### ModOptionsRegistry（全新）
```csharp
ModOptionsRegistry.RegisterPanel("mymod", "My Mod", builder =>
{
    builder.AddToggle("feature", true, "Enable Feature");
    builder.AddSlider("difficulty", 1f, 0.5f, 3f, "Difficulty");
    builder.AddDropdown("mode", new[] {"Easy", "Normal"}, 0, "Mode");
    builder.AddButton("Reset", () => ResetStuff());
});
```

### PerkTreeUtils（全新）
```csharp
Perk perk = PerkTreeUtils.AddPerk(
    new Identifier("mymod", "ExtraHealth"), req, icon);

PerkTreeUtils.ConnectPerks(
    new Identifier("mymod", "ExtraHealth"),
    new Identifier("mymod", "IronWill"));

PerkTreeUtils.ForceUnlock(new Identifier("mymod", "ExtraHealth"));
PerkTreeUtils.RemoveAllPerks("mymod");
```

### BuildingUtils（全新）
```csharp
// 注册建筑（modid 从 id.Domain 自动推导）
BuildingUtils.RegisterBuilding(
    new Identifier("mymod", "workbench"),
    buildingInfo, prefab
);

// 按 Identifier 查询 BuildingInfo
BuildingInfo? info = BuildingUtils.GetBuildingInfo(
    new Identifier("mymod", "workbench"));

// 获取所有建筑 Identifier
IReadOnlyList<Identifier> allIds = BuildingUtils.GetAllBuildingIds();

// 放置建筑
BuildingUtils.PlaceBuilding(
    new Identifier("base", "area1"),
    new Identifier("mymod", "workbench"),
    new Vector2Int(2, 2), BuildingRotation.Rot0);

// 批量卸载
BuildingUtils.UnregisterAllBuildings("MyMod");

// 移除单个
BuildingUtils.UnregisterBuilding(new Identifier("mymod", "workbench"));
```

---

## 12. 完整迁移示例

### 旧版 mod（使用旧 FML）
```csharp
public class MyGunMod : Duckov.Modding.ModBehaviour
{
    string dllPath = Assembly.GetExecutingAssembly().Location;
    private static List<int> _addedItems = new List<int>();

    public void Awake()
    {
        var harmony = new Harmony("mygunmod");
        harmony.PatchAll();
        I18n.InitI18n(dllPath);
        RegisterGuns();
    }

    private void RegisterGuns()
    {
        var itemData = new ItemData
        {
            itemId = 200001,
            localizationKey = "my_rifle",
            weight = 3.5f,
            value = 5000,
        };
        ItemUtils.CreateCustomItem(dllPath, itemData, "old_fml_version");
        _addedItems.Add(200001);
    }

    public void OnModUnload()
    {
        foreach (var id in _addedItems)
        {
            ItemAssetsCollection.RemoveDynamicEntry(...);
        }
    }
}
```

### 新版 mod（使用最新 FML）
```csharp
public class MyGunMod : Duckov.Modding.ModBehaviour, IHasModid
{
    string dllPath = Assembly.GetExecutingAssembly().Location;

    public string GetModid() => "mygunmod";

    protected override void OnAfterSetup()
    {
        ModPathResolver.Register(GetModid(), dllPath);
        I18n.InitI18n(GetModid());
        var harmony = new Harmony(GetModid());
        harmony.PatchAll(Assembly.GetExecutingAssembly());
        RegisterGuns();
    }

    private void RegisterGuns()
    {
        var itemData = new ItemData
        {
            itemId = 200001,
            localizationKey = "my_rifle",
            weight = 3.5f,
            value = 5000,
        };
        // Identifier 自动推导 modid，modPath 自动探测
        ItemUtils.CreateCustomItem(new Identifier("mygunmod", "rifle"), itemData);
        // 或直接从 AssetBundle 注册：
        // ItemUtils.RegisterGun(new Identifier("mygunmod", "rifle"), bundle, "Rifle_Prefab");
    }

    // 不需要 OnModUnload —— 基类 OnBeforeDeactivate 自动执行：
    // 1. GameEventAdapters.TearDown() 解除原生事件
    // 2. EventBusManager.Clear() 清空所有 handler
    // 3. RegistryManager.RemoveAllByOwner("MyGunMod") 批量卸载全部资源
}
```

---

## 13. 常见问题

### Q: 迁移后编译报错找不到某些方法？
**A**: 检查是否还在使用 `"old_fml_version"` 默认参数。新版大部分 API 已改为 Identifier-first 模式——方法签名已变。
例如 `RegisterItem(item, modid)` 现为 `RegisterItem(Identifier id, Item item)`——需要传入 `Identifier` 作为第一参数。

### Q: 为什么不用传 `modPath`（DLL 路径）了？
**A**: 请在 `OnAfterSetup` 中调用 `ModPathResolver.Register(GetModid(), dllPath)` 显式注册 mod 路径。之后 `CreateCustomItem`、`I18n.InitI18n`、`AssetUtil.LoadBundle` 等的便捷重载会通过 `ModPathResolver.ResolveDirectory(modid)` 自动解析 mod 目录，不再需要每次都传路径参数。

### Q: `Identifier` 和之前用的 `string` 类型 key 有什么区别？
**A**: `Identifier` 强制 `domain:path` 双段命名空间，禁止特殊字符，自带 `ToString`/`Parse`/`TryParse`，且所有 Registry 以它为键。建议把之前零散的 `string` key 统一为 `Identifier` 格式。

### Q: 迁移后还要自己维护 `Dictionary` 来追踪注册的资源吗？
**A**: 不需要。所有模块的 Registry 会自动按 modid 追踪 owner。基类 `OnBeforeDeactivate` 会通过 `FMLBootstrap.TearDownMod(GetModid())` 自动按 modid 清理本模组注册的全部条目。

### Q: `RegisterItem` 现在没有 modid 参数了，那怎么指定 owner？
**A**: modid 从 `Identifier.Domain` 自动推导。例如 `new Identifier("mymod", "rifle")` 的 owner 为 `"mymod"`。

### Q: 我的 mod 用了老版 `ShopUtils.AddGoods` 但没传 modid？
**A**: 新版的 `AddGoods` 支持 `string? modid = null` 自动走 `RegistryManager.CurrentModid`。建议改为你的 mod 名称以便正确卸载。

### Q: 我之前用了 `Harmony.PatchAll`，现在还需要吗？
**A**: 需要。请在你的 `OnAfterSetup` 中显式调用 `new Harmony(GetModid()).PatchAll(Assembly.GetExecutingAssembly())` 来 patch 自身 `[HarmonyPatch]`。FML 不再自动 patch 子模组程序集。

### Q: 我的 mod 之前手动管理卸载逻辑，现在有冲突吗？
**A**: 不冲突。`FMLBootstrap.TearDownMod(GetModid())` 会按你的 modid 清理 Registry 条目，你可以在 `OnBeforeDeactivate` 中同时执行自定义卸载逻辑。

### Q: fml.json 是什么？我需要创建吗？
**A**: `fml.json` 是模组根目录下的声明式配置文件。可声明 modid、优先级、依赖关系、自激活策略。非必需，不创建时 FML 使用默认值（最低优先级、无依赖、不自动激活）。详见 [USAGE.md §2.1](USAGE.md#21-fmljson--声明式模组配置)。

### Q: fml.json 的 modid 必须和 info.ini 一致吗？
**A**: **必须**。`fml.json` 中的 `modid` 必须与 `info.ini` 的 `name` 字段完全一致，否则整个 fml.json 会被忽略（日志给出警告）。

### Q: 新版 `IRegistry<T>` 和旧版有什么不同？
**A**: 旧版只有基本的读写，新版完整支持 CRUD + owner 追踪 + 批量卸载 + 遍历。所有模块（Quest/Shop/Buff/Building/Perk）均统一使用此接口。

### Q: `TryGetCustomItem` 的签名变了吗？
**A**: 没变——仍然是 `TryGetCustomItem(int typeID, out Item? item)`。内部实现改为走 `RegistryManager.Instance.ItemID`（`ReverseLookupRegistry`），但对外接口保持一致。

---

*如有其他迁移问题，请在 GitHub Issues 中提出。*
