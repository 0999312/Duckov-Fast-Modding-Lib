# Register 一体化开发计划 (PLAN-Register.md) 📦 已归档

> **状态**：✅ 全部完成，已归档。实施成果已合入 `FastModdingLib/Register/`。

---

## 0. 定位与动机

为 `FML` 引入**统一的注册表抽象**，作为所有"modder 注册自定义资源 → 游戏原生数据集合"
路径的中间层。当前痛点：

1. **抽象能力不足**：`IRegistry<T>` 仅 4 个方法（`this[]` / `Get` / `Set` / `TryGet`），
   无 `Remove` / `Clear` / 遍历 / `owner` 索引 / 反向查询 —— 无法支持 mod 卸载场景。
2. **模块旁路 Registry**：Quests / Shop / Crafting 三个模块**完全不走 Registry**，各自
   手维护 `Dictionary<...> + UnregisterAll(modid)` 风格代码；Quests 与 Crafting 各有
   7~9 行重复的 enumerate-and-remove 模板，Shop 连 modid 追踪都没做、卸载 API 完全缺失。
3. **半接入模块存在旁路字典**：Audio 走了 `SimpleRegistry<AudioData> dataRegistry` 但
   因抽象单向，被迫在 registry 之外另开 `Dictionary<string, Identifier> mapdata` 做
   "eventName → Identifier" 反向查询（`AudioObjectMixin` L21-23）—— 抽象不足以表达业务
   诉求，反而制造两份真相。
4. **写入但无人读**：`ItemUtils.RegisterItem` 写 `RegistryManager.Instance.ItemID.Set(id,
   item.TypeID)`，但全代码库 0 处 `ItemID.Get` / `TryGet` —— registry 写入了却没有任何
   读取消费方，注册没意义。
5. **Phase 1.A EventBus 的 `UnregisterAll(ownerMod)` 与本计划强相关**：EventBus 用
   ownerMod 索引 handler，Register 用 modid 索引 entry —— 二者其实映射同一概念（mod
   身份）。若两者抽象不一致，modder 卸载时需要触达两个 API、两套 key 命名，容易遗漏。

Register 一体化解决这五件：扩能抽象 / 收编旁路字典 / 补齐 Shop 卸载 / 让 ItemID 真正
被读 / 与 EventBus owner 语义对齐。

---

## 1. 现状盘点

### 1.1 抽象层（`FastModdingLib/Register/`，5 文件 / 99 LOC）

| 文件 | 角色 | LOC | 关键事实 |
|---|---|---|---|
| `ERegistry.cs` | 空标记接口 | 7 | 仅用作 `RegistryManager.Registry` 元注册表的类型参数 |
| `IRegistry<T>` | `IRegistry : ERegistry` | 12 | 4 方法：`this[]`get/set / `TryGet` / `Get` / `Set` |
| `SimpleRegistry<T>` | `Dictionary<Identifier,T>` 薄包装 | 41 | `protected Dictionary<Identifier,T> dict`，虚方法 |
| `NonAlterableSimpleRegistry<T>` | override `Set` 重复 key 抛 ArgumentException | 22 | 唯一附加语义"加完了不能改" |
| `RegistryManager` | `Singleton<RegistryManager>` 全局根 | 17 | 挂 2 个 child：`Registry`（元表 ERegistry）+ `ItemID`（Identifier→int） |

**能力短板清单**：

| 短板 | 现状 | 影响 |
|---|---|---|
| 无 `Remove` / `Clear` | `IRegistry<T>` 仅写不删 | mod 卸载完全无法走 registry |
| 无 owner 索引 | 无 `modid` 字段 | 无法回答"某 mod 注册了哪些资源" |
| 无反向索引 | 单向 `Identifier → T` | Audio eventName→Identifier 场景被迫旁路 |
| 无遍历 | 未实现 `IEnumerable` | 现存卸载代码只能 `foreach (var item in addedXxxs)` 走旁路字典 |
| 无 modify 语义 | `NonAlterableSimpleRegistry` 重写 `Set` 为 `TryAdd` | 后续 formula 编辑场景（Phase 1.B 修缮 Crafting）需另写带 owner 校验的 modify API |
| `ERegistry` 空接口 | 仅为类型分桶 | 反复 `IRegistry<T>` 泛型化反而增多 |

### 1.2 Identifier（`FastModdingLib/Utils/Identifier.cs`，97 LOC）

- `domain:path` 双字段，校验禁止 `:` / `\\` / `..` / 空；`domain` 禁止 `/`，`path` 允许 `/` 以支持子目录资源
- `IEquatable<Identifier>` + `GetHashCode`（`HashCode.Combine(Path, Domain)`）
- **未重写 `ToString()`** → 调试输出默认是类名，不便排查
- 设计健康，本计划仅补 `ToString()` 与 `Parse(string)` 静态方法（不破坏现有语义）

### 1.3 各模块消费情况

| 模块 | 走 Registry | 实际做法 | 旁路字典 |
|---|---|---|---|
| **Items** | ⚠️ 半走 | `ItemUtils.RegisterItem` 仅 `ItemID.Set(id, item.TypeID)` 一行；`addedItemIds = Dictionary<int,string>` 手维护 TypeID→modid；**ItemID 无人 `Get`**；`UnregisterAllItem(modid)` 走 `addedItemIds` 不查 `ItemID` | `addedItemIds` |
| **Audio** | ⚠️ 半走 | `dataRegistry` 注册到 `Registry` 元表 (`fastmoddinglib:audio`)；但 `AudioObjectMixin` 反查走 `mapdata` 字典 | `mapdata` (eventName→Identifier) |
| **Quests** | ❌ 完全未走 | `addedQuests = Dictionary<Quest,string>` 手维护 | `addedQuests` |
| **Shop** | ❌ 完全未走 | `ShopUtils.AddGoods` 直接 mutate `GameplayDataSettings.StockshopDatabase...entries`；**无 modid 追踪，无卸载 API** | 无（缺口本身） |
| **Crafting** | ❌ 完全未走 | 3 个并行字典：`addedFormulaIds` (string→modid) / `addedFormulaResults` (int→modid) / `addedDecomposeItemIds` (int→modid)；每个 `RemoveAllAddedXxx(modid)` 各写一遍 enumerate-and-remove | 3 个字典 |

### 1.4 hand-rolled 卸载模板（重复模式）

Quests/Crafting 各处出现同一模板（Crafting 各重复 2 次）：

```csharp
foreach (var item in addedXxxs.ToList()) {
    if (item.Value.Equals(modID)) {
        GameplayDataSettings.Xxx.Remove(item.Key);
        // ...cleanup native side...
        addedXxxs.Remove(item.Key);
    }
}
```

~25 LOC 重复 ×3 模块。Register 一体化把这段压缩到 `IRegistry<T>.RemoveAllByOwner(modid)` 一调用。

---

## 2. 偏移与扩展决策（针对现状 1.1 / 1.2）

| 维度 | 现状 | 决策 |
|---|---|---|
| 能力扩展 | `IRegistry` 4 方法 | 加 `Remove` / `Clear` / `IEnumerable` / owner 重载 / `GetAllByOwner` / `RemoveAllByOwner` / `TryGetOwner`（详见 §3） |
| 反向索引 | 单向 | 新增 `ReverseLookupRegistry<T, TKey>` 子类解决 Audio 类场景；不污染 `IRegistry<T>` 主契约（很多 registry 无 native key） |
| `ERegistry` 空接口 | 类型分桶用 | 保留作为"元注册表"标记，不变 |
| `NonAlterableSimpleRegistry` | `Set` 用 TryAdd 行为奇怪（写失败静默） | 调整为：默认 `Set` 抛异常；新增 `SetIfAbsent(id, value, modid)` 显式表达"加完了不改"语义；破坏性变更但现有调用点 1 处（`RegistryManager` 构造）将一并迁移 |
| Identifier | 无 `ToString` / `Parse` | 补 `ToString() => $"{Domain}:{Path}"`；补 `static Parse(string)` 与 `TryParse`（`string` 形式 `"domain:path"`） |
| owner 类型 | 各模块 `string modid` 默认值五花八门（`"FastModdingLib"` / `"old_fml_version"` / `"TopTierWeaponExpansion"`） | 保留 `string`，不强行改为 object；统一默认值为正在加载的 mod 的 Assembly 名称（由 `RegistryManager` 提供 `CurrentModid` 上下文 set 一次，详见 §3.4） |
| 集合变异与迭代 | 现状卸载走 `.ToList()` snapshot | `IRegistry<T>` 实现内部 snapshot，避免下游每次手 `.ToList()` |
| 可空性 | 未启用 | `<Nullable>enable</Nullable>`，所有新 API 加可空标注 |

---

## 3. 公共 API 落地契约

### 3.1 `IRegistry<T>`（扩能）

```csharp
namespace FastModdingLib.Register
{
    public interface IRegistry<T> : ERegistry, IEnumerable<KeyValuePair<Identifier, T>>
    {
        // —— 既有 ——
        T this[Identifier id] { get; set; }            // set 等价 Set(id, value, RegistryManager.CurrentModid)
        bool TryGet(Identifier id, out T value);
        T Get(Identifier id);
        void Set(Identifier id, T value);              // 默认 owner = CurrentModid

        // —— 新增：删除 / 清空 ——
        bool Remove(Identifier id);                    // 返回是否实际移除
        void Clear();                                  // 清全部（包含所有 owner）

        // —— 新增：显式带 owner 的写入 ——
        void Set(Identifier id, T value, string modid);
        bool Remove(Identifier id, out string? modidRemoved);

        // —— 新增：按 owner 索引 / 批量卸载 ——
        bool TryGetOwner(Identifier id, out string? modid);
        IReadOnlyList<Identifier> GetAllByOwner(string modid);
        int RemoveAllByOwner(string modid);            // 返回删条数；native 侧清理走回调（见 §3.3）

        // —— 新增：枚举 ——
        IEnumerator<KeyValuePair<Identifier, T>> GetEnumerator();
    }
}
```

### 3.2 `ReverseLookupRegistry<T, TKey>`（新增）

```csharp
namespace FastModdingLib.Register
{
    /// <summary>有 native 键（如 Audio eventName、Item TypeID）的 registry。</summary>
    public class ReverseLookupRegistry<T, TKey> : SimpleRegistry<T>
    {
        private readonly Dictionary<TKey, Identifier> _byNativeKey;
        private readonly Func<T, TKey> _nativeKeySelector;

        public ReverseLookupRegistry(Func<T, TKey> nativeKeySelector);

        // —— 写入时同步反向索引 ——
        public void Register(TKey nativeKey, Identifier id, T value, string modid);
        public new bool Remove(Identifier id);                             // 同步清反向 dict
        public new int RemoveAllByOwner(string modid);                     // 同步清反向 dict
        public bool TryGetIdentifier(TKey nativeKey, out Identifier id);
    }
}
```

### 3.3 模块注册时的卸载回调

很多模块在 registry 删除 entry 时还需对 native 侧清理（如 `Destroy(quest.gameObject)` /
`ItemAssetsCollection.RemoveDynamicEntry(item)`）。直接在 `Remove` 内部硬编码耦合到 native
不合适。引入**注册侧回调**：

```csharp
// 各模块自己的 registry 持有：
public QuestRegistry : SimpleRegistry<Quest>
{
    // 注册侧决定"删一条 = 哪些 native 善后"
    protected override void OnRemoved(Identifier id, Quest value, string? modid)
    {
        GameplayDataSettings.QuestCollection.Remove(value);
        UnityEngine.Object.Destroy(value.gameObject);
        GameplayDataSettings.QuestRelation.RemoveNode(
            GameplayDataSettings.QuestRelation.GetNode(value.ID));
    }
}
```

`SimpleRegistry<T>` 暴露 `protected virtual void OnRemoved(Identifier, T, string?)`，默认空实现。
`Remove` / `RemoveAllByOwner` 内部在删除 `_dict` 条目后调用 `OnRemoved`。这样：

- `IRegistry<T>` 不耦合 native 类型
- 每个模块自己的 registry 子类决定清理逻辑
- 模块 API（`QuestUtils.UnregisterAll(modID)`）退化为对 registry 的单调用

### 3.4 模块身份（modid）的统一

**问题**：现状各模块自带 `string modid = "old_fml_version"` 默认值，5 个模块 4 个默认值
不同（`"FastModdingLib"` / `"old_fml_version"` / `"TopTierWeaponExpansion"`）；modder
很容易忘记传 modid 而注册到错误的 namespace。

**决策**：

1. 引入 `RegistryManager.CurrentModid`（thread-static string，默认 `"fastmoddinglib"`）。
2. `FML.ModBehaviour.OnAfterSetup()` 在加载某个 mod 的 Assembly 时短暂 `using (
   RegistryManager.EnterModScope(modid))` set 之；退出时还原。
3. 各 `IRegistry<T>` 写入无 modid 重载时取 `RegistryManager.CurrentModid`。
4. 现存 `string modid = "old_fml_version"` 等默认值**移除**，改为必填或走 CurrentModid
   兜底；迁移期一次性 break 调用方（受影响方仅本仓库内 QuestUtils/CraftingUtils/ItemUtils），
   不存在外部 modder 调用兼容性约束（库未发布 v1）。

**与 `PLAN-EventBus.md` 协同**：EventBus §3 公约中采用 `object ownerMod`；本计划采用
`string modid`。两者映射同一 mod 身份。**统一决策**：

- 保留二者差异：EventBus 的 handler 在 mod unload 时整批 `UnregisterAll(ownerMod)`，
  Register 的 entry 同样按 modid 整批 `RemoveAllByOwner(modid)` —— 二者使用相同的 modid
  字符串值即可（即 modder 的 Assembly 名称 / `RegistryManager.CurrentModid`）。
- `PLAN-EventBus.md` 的 `EventBusManager.Clear()` 与本计划 `RegistryManager.Clear()`
  在 `ModBehaviour.OnModWillBeDeactivated` 中**按相同 modid 串行调用**。
- EventBus 后续修订时建议把 `object ownerMod` 放宽到 `string modid`（保持向后兼容），与
  Register 完全对齐 — 该修订留到 Phase 1.A EventBus 落地时一并做，不在本计划内强制。

---

## 4. 模块文件布局

```
FastModdingLib/
  Register/
    ERegistry.cs                            既有, 不动
    IRegistry.cs                            扩能 ~50 LOC
    SimpleRegistry.cs                       扩能 ~80 LOC (含 OnRemoved, owner dict, snapshot)
    NonAlterableSimpleRegistry.cs           行为修正 ~30 LOC
    ReverseLookupRegistry.cs                新增 ~50 LOC
    RegistryManager.cs                      扩能 ~50 LOC (CurrentModid, EnterModScope, modids 索引)
    ModScope.cs                              新增 ~20 LOC (IDisposable EnterModScope 返回类型)
  Utils/
    Identifier.cs                            补 ToString/Parse/TryParse ~120 LOC (97 → ~120)
  Audio/
    AudioUtil.cs                             迁移: 删 mapdata, 改 ReverseLookupRegistry<AudioData,string>
    AudioData.cs                             不动
    AudioObjectMixin.cs                      改走 RegistryManager.Audio.TryGetIdentifier(eventName)
  Quests/
    QuestRegistry.cs                         新增 ~50 LOC (SimpleRegistry<Quest> + OnRemoved)
    QuestUtils.cs                            收编: addedQuests 删除, Unregister 改调 registry
    QuestData.cs                             不动
    TaskKillCountFix.cs                      不动
  Shop/
    ShopRegistry.cs                          新增 ~40 LOC (ReverseLookupRegistry + OnRemoved)
    ShopUtils.cs                             接入 registry, 新增 UnregisterAll(modid)
    ShopGoodsData.cs                         不动
  CraftingUtils.cs                           收编: 3 字典 → 2 registry (CraftingFormula/Decompose)
  Items/
    ItemUtils.cs                             读 ItemID, modid 走 CurrentModid; ItemID 改为 ReverseLookupRegistry<int,int>? 评估
    ItemData.cs / BulletData.cs / ReturnItem.cs  不动
  Register/
    RegisterTest.cs                          新增 ~150 LOC  (首个非 Unity 依赖的纯 C# 测试)
```

---

## 5. 各模块迁移细则

### 5.1 Items

- 删 `addedItemIds = Dictionary<int, string>`（与 `ItemID` 重复）。
- `RegistryManager.ItemID` 升级为 `ReverseLookupRegistry<int, int>`（key Identifier →
  value ItemTypeID，反向 TypeID → Identifier），让 `ItemID.TryGetIdentifier(typeID, out id)`
  可用。
- `ItemUtils.RegisterItem` 中 `RegistryManager.Instance.ItemID.Set` 加入 modid 参数，
  `UnregisterAllItem` 改为 `ItemID.RemoveAllByOwner(modid)`，并通过 `OnRemoved` 调
  `ItemAssetsCollection.RemoveDynamicEntry(item)`。
- **新增一处真正读取 ItemID**：`ItemUtils.TryGetCustomItem(int typeID, out Item item)` ——
  否则 ItemID 注册无意义；至少 `Audio` 类查询（按 TypeID 反查）落地。

### 5.2 Audio

- 删 `AudioUtil.mapdata`（eventName→Identifier）旁路字典。
- `dataRegistry` 改为 `ReverseLookupRegistry<AudioData, string>`，`_nativeKeySelector =
  data => data.Eventname`。
- `AudioObjectMixin.Prefix` 改为 `AudioUtil.Instance.dataRegistry.TryGetIdentifier(eventName,
  out var id)`，去掉 `GetIdentifier(string)` 方法（合并）。
- `RegisterAudio(id, data)` 调 `Register(data.Eventname, id, data, CurrentModid)`。

### 5.3 Quests

- 新增 `QuestRegistry : SimpleRegistry<Quest>`，`OnRemoved` 完成三件事：
  `QuestCollection.Remove` / `Destroy(gameObject)` / `QuestRelation.RemoveNode`。
- `QuestUtils.addedQuests` 删除。
- `UnregisterQuest(int ID)` → 改走 registry：用 `TryGetOwner` 找到对应 mod 的 entry 再
  `Remove`。
- `UnregisterQuestAll(modID)` → `RemoveAllByOwner(modID)`。
- 注意 Quest 的反查是 native `Quest.ID`(int) 找到 `Quest` 对象本身——`QuestRegistry` 不需要
  反向索引（Quest 已经在 dict 里），用 `Where(p => p.Value.ID == targetID)` 枚举即可。
  与 ItemID 类的"反向到 Identifier"是两件事。

### 5.4 Shop

- 新增 `ShopRegistry : ReverseLookupRegistry<StockShopDatabase.ItemEntry, int>`，存
  Identifier→ItemEntry，反向 `typeID→Identifier`。
  - `OnRemoved` 调 `GameplayDataSettings.StockshopDatabase.GetMerchantProfile(merchantProfileID).entries.Remove(value)`。
- `ShopUtils.AddGoods` 增加 modid 参数（默认走 CurrentModid）；调用
  `ShopRegistry.Register(typeID, id, entry, modid)`。
- 新增 `ShopUtils.UnregisterAllGoods(modid)` 受 Fellow PLAN §6 "ModBehaviour 生命周期"
  统一调度。

### 5.5 Crafting

- 三个字典合并为两份 ReverseLookupRegistry：
  - `CraftingFormulaRegistry : SimpleRegistry<CraftingFormula>` —— string formulaId 作为
    Identifier.path；`Remove` 重载调 `CraftingFormulaCollection.Instance.list.Remove`。
  - `DecomposeRegistry : ReverseLookupRegistry<DecomposeFormula, int>` —— key Identifier →
    value DecomposeFormula，反向 itemId → Identifier；`OnRemoved` 调
    `DecomposeDatabase.Instance.Dic.Remove(itemId)` 并触发 `RebuildDictionary` 反射。
- `addedFormulaIds` / `addedDecomposeItemIds` / `addedFormulaResults` 三个字典全部删除。
- `RemoveAllAddedFormulas(modid)` / `RemoveAllAddedDecomposeFormulas(modid)` 各自缩为单行
  `Registry.RemoveAllByOwner(modid)`。
- `AddCraftingFormula` 内的 "Exist formula" 检查改走 `TryGet`，与 PLAN §1.B "Crafting 修缮
  per-formula Remove / 查询 / 编辑" 提前对齐 1 项。

---

## 6. ModBehaviour 集成

```csharp
// FastModdingLib/ModBehaviour.cs (扩展)
protected override void OnAfterSetup()
{
    var harmony = new Harmony("fastmoddinglib");
    harmony.PatchAll();

    // 当前 mod 身份上线（modder 的 Assembly 名称）
    RegistryManager.Instance.EnterModScope(Assembly.GetExecutingAssembly().GetName().Name);
    EventBusBootstrap.Init();
    RegisterBootstrap.Init();   // 创建 QuestRegistry / ShopRegistry / CraftingRegistry / DecomposeRegistry 并注册到元表
}

internal void OnModWillBeDeactivated()
{
    GameEventAdapters.TearDown();
    EventBusManager.Instance.Clear();
    RegistryManager.Instance.RemoveAllByOwner(RegistryManager.CurrentModid);  // 一次性收编
    EventBusManager.Instance.Clear();
}
```

`RegisterBootstrap.Init()` 内部把 5 个 registry 注册到 `RegistryManager.Registry` 元表
（Identifiers `fastmoddinglib:quest` / `:shop` / `:crafting_formula` / `:decompose` /
`:audio` / `:itemid`，Audio/ItemID 已在现状中注册）。

---

## 7. 实施阶段拆分与依赖

```
R1 ─┐
    ├── R2 ─┐
    │       ├── R3 ─┐
    │       │       ├── R5 (Shop) ─┐
    │       └── R4 ─┐               │
    │               ├── R6 (Items) ─┤
    │               └── R5.A (Quests)┤
    └── R7 (Crafting) ──────────────┤
                                     └── R8 (测试) — 跟进每阶段产出
```

| 阶段 | 范围 | 依赖 | 验收点 |
|---|---|---|---|
| **R1** | `IRegistry<T>` 扩能 + `SimpleRegistry`（含 `OnRemoved` / owner dict / snapshot）/ `NonAlterableSimpleRegistry` 行为修正 / `RegistryManager.CurrentModid` + `EnterModScope` + `ModScope` | 无 | 编译通过；既有 5 处 `Set` 调用点（RegistryManager 构造 / AudioUtil / ItemUtils）零破坏 — 移除默认参数 fallback 由 R3/R6/R7 兜底 |
| **R2** | `Identifier.ToString() / Parse / TryParse` + `RegisterTest` 基础用例 | R1（仅为共用测试框架） | Identifier ToString 输出 `"domain:path"`；Parse 往返一致 |
| **R3** | `ReverseLookupRegistry<T, TKey>` 新增 + 用例 | R1 | 反向索引在 `Register` / `Remove` / `RemoveAllByOwner` 三条路径都同步 |
| **R4** | Audio 迁移：删 `mapdata`，`AudioUtil` 改 `ReverseLookupRegistry`，`AudioObjectMixin` 改走 `TryGetIdentifier` | R3 | Audio 注册/反查/卸载全程走 registry；mapdata 字典消失 |
| **R5** | Quests 迁移：`QuestRegistry` + 收编 `addedQuests` | R1 | `UnregisterQuestAll(modID) ⇒ RemoveAllByOwner(modID)`；旧 dict 删除 |
| **R5.A** | Shop 接入：`ShopRegistry` + 新增 `UnregisterAllGoods` | R1, R3 | Shop 现在有了卸载 API（从无到有） |
| **R6** | Items 迁移：`ItemID` 升级 `ReverseLookupRegistry<int,int>`，新增 `ItemUtils.TryGetCustomItem(int)`，`UnregisterAllItem` 改走 registry | R3 | ItemID 终于有读取方；`addedItemIds` 删除 |
| **R7** | Crafting 迁移：3 字典 → 2 registry（CraftingFormula / Decompose），含 `RebuildDictionary` 反射路径保留 | R1, R3 | `RemoveAllAddedFormulas` / `RemoveAllAddedDecomposeFormulas` 各退化为单行 |
| **R8** | `RegisterTest` 全量 + 滚动跟进 | 各阶段 | DOD §8 全绿 |

> R1 必须先做（所有迁移都依赖扩能后的 `IRegistry<T>`）。
> R2 / R3 可在 R1 完成后并行。
> R4 / R5 / R5.A / R6 / R7 在 R3 完成后均可并行（彼此非关键路径）。
> R8 滚动跟进，每完成一个 R.. 阶段补一组用例。

---

## 8. 验收清单（DOD）

- [ ] `dotnet build` 通过；`FastModdingLib.csproj` 不新增第三方包引用
- [ ] `RegisterTest` 全绿，覆盖：
  - [ ] `Set` / `Get` / `TryGet` / `Remove` / `Clear` 单条路径
  - [ ] `Set` 带 modid 可正确 `GetAllByOwner(modid)` 返回
  - [ ] `RemoveAllByOwner(modid)` 触发 `OnRemoved` 回调正确、native 侧清理执行
  - [ ] `ReverseLookupRegistry` 在 `Register` / `Remove` / `RemoveAllByOwner` 后反向索引均同步
  - [ ] `EnterModScope(modid)` 期间无 modid 重载写入落到 CurrentModid；离开 scope 还原
  - [ ] `Identifier.ToString() / Parse` 往返一致；`TryParse` 失败不抛
- [ ] Audio：`mapdata` 字段从 `AudioUtil` 消失；`GetIdentifier(string)` 公开方法删除或退化为转调
- [ ] Quests：`addedQuests` 字段从 `QuestUtils` 消失；`UnregisterQuestAll(modID)` 行数收敛
- [ ] Crafting：`addedFormulaIds` / `addedFormulaResults` / `addedDecomposeItemIds` 三字段消失；
      `RemoveAllAddedFormulas` / `RemoveAllAddedDecomposeFormulas` 各缩为 ≤3 行
- [ ] Shop：`ShopUtils.UnregisterAllGoods(modid)` 可用，且真正能撤掉之前 `AddGoods` 的效果
- [ ] Items：出现至少 1 处 `ItemID.TryGet` / `TryGetCustomItem` 真实读路径
- [ ] `lsp_diagnostics` 干净
- [ ] PLAN §4 覆盖率矩阵：`Registry 抽象` 行从 ⚠️ 改 ✅，并标注本期落地

---

## 9. 不在本计划范围

- **不**引入多级 namespace（Identifier 仍 `domain:path` 两段冒号分隔；不做 `domain:path:sub`）。`path` 内部允许 `/` 子目录层次（如 `mymod:items/weapons/rifle`），这与多级 namespace 是不同维度。
- **不**做 registry 持久化（即"保存注册表"用于跨会话恢复——属于 SaveUtils 范畴）。
- **不**做跨 Assembly 的注册表远程 RPC（mod 间通信）— 留 future。
- **不**对 Unity 主线程做线程安全锁 — FML 单线程上下文，加锁徒增开销。但 `CurrentModid`
  实现 thread-static 以防子线程 async 链上误读。
- ~~**不**重构 `Identifier` 校验语义 — 现有 `:` / `/` / `\\` / `..` 禁令保持。~~ **已变更**：`path` 现允许 `/` 以支持子目录资源（如 `modid:items/weapons`），`domain` 仍禁止 `/`。
- **不**触碰仓库卫生遗留（DuckovPath env / README 拼写 / Tests 独立 csproj）—— 用户明确说不考虑。
- 🚫 **不修改 `0Harmony.dll` 引用方式（版本硬性锁定）**：vendored 二进制锁定
  **2.4.1.0**（`ProductVersion: 2.4.1.0+789df191bbaf6610232d50e7ef7dddc0d2812549`，
  2456064 字节，2025-11-03）。
  - **禁止改 `PackageReference Lib.Harmony`** —— NuGet 拉取版本可能漂移到 2.x 后续
    patch 或 3.x，与游戏运行时实际加载的 Harmony 版本错位 → `MissingMethodException`
    / Harmony patch 失效 / mod 加载崩溃。
  - 本计划所有改动的 csproj 仅可新增 `<Compile Include>` 项与 `Register/Register*.cs`
    等源文件，不得改动 `<Reference Include="0Harmony">` 或引入任何 Harmony 包引用。
  - 详见 `PLAN.md` §1 仓库卫生说明。
- **不**强制 EventBus 的 `object ownerMod` 与本计划 `string modid` 对齐 —— 仅在文档中
  注明二者映射关系（§3.4）；EventBus 自身的修订在 PLAN-EventBus 文档处理。

---

## 10. 后续可演进项（future，不纳入本期）

- **按类型分桶元注册表查询**：让 `RegistryManager.Registry.GetAllByOwner(modid)` 跨所有
  registry 聚合返回 — 目前需逐 registry 自调。
- **foreach-friendly 视图**：暴露 `IReadOnlyDictionary<Identifier, T>` 视图减少 snapshot
  开销。
- **Identifier 增量校验缓存**：构造时哈希目前每次新组合；高频构造路径可考虑缓存 raw 字符串。
- **Registry 事件**：在 `Set` / `Remove` 时发 `RegistryChangedEvent`，配合 EventBus 让
  下游模块订阅 — 跨 Phase 1.A / 1.B 协同时机再评估。
- **跨 EventBus ownerMod 类型对齐**：见 §3.4。

---

## 11. 风险与对策

| 风险 | 说明 | 对策 |
|---|---|---|
| `NonAlterableSimpleRegistry` 行为变更破坏现存 RegistryManager 初始化 | 现 `RegistryManager` 构造时 `Registry.Set(...)` 依赖 `TryAdd` 静默写行为；变更后若改为 `Set` 重复抛异常，加载即崩 | R1 同步更新 `RegistryManager` 构造，避免在单一实例化时对同一 key 重复 Set；加 `RegisterTest.RegisterManager_Boot_DoesNotThrow` 守门 |
| `RegistryManager.CurrentModid` 在子线程误读 | modder spawn 的 `Task.Run` 中调用注册 API 时拿到错误 modid | `CurrentModid` 用 `[ThreadStatic]`；`EnterModScope` 仅在主线程使用；异步链上的注册由 modder 显式传 modid |
| 模块迁移破坏 modder 调用兼容性 | 例：`AddGoods` 增加 modid 参数，外部 modder 调用方式变 | 库未发布 v1，无外部调用方；仅本仓内 5 处调用一次性 break；modder 文档随 Phase 5 一并更新 |
| Crafting `RebuildDictionary` 反射路径 | 现状已用 `?.Invoke`，但反射本身就是脆弱点 | 迁移保留原反射调用；未来若游戏版本更新方法名，集中在一处 | 
| `OnRemoved` 回调抛异常中断 `RemoveAllByOwner` 遍历 | 单条 entry native 善后失败导致后续 entries 不被清，半残留 | `RemoveAllByOwner` 内 try/catch 单条，记录失败项继续；最终 Debug.LogError 汇总 |
| snapshot 多次复制开销 | 大量 entries 时 `foreach` snapshot 复制 `List<>` | 卸载场景量在百级，可接受；真正 hot 路径若出现再优化 |
| `ReverseLookupRegistry` 反向索引失同步 | 派生类忘记 override 关键路径 | 关键方法用 `new`（非 virtual）+ sealed 杜绝二次继承错失；`OnRemoved` 是 `protected virtual` 唯一扩展点 |

---

*本计划为 Register 一体化模块的独立文档；实施时按 §7 阶段顺序进入编码。*