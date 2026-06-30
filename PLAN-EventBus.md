# EventBus 开发计划 (PLAN-EventBus.md)

> 参考来源：`DecompiledDLL/Event/`（个人其他项目的反编译产物）
> 路线归属：补入 `PLAN.md` Phase 1 — 框架内核加固（跨模块基础设施）
> 声明：本计划**不走 GSD 工作流**；按本文直接进入实现。

---

## 0. 定位与动机

为 `FML` 引入**统一的跨模块事件总线**，作为 modder 与游戏原生 C# 事件之间的中间层。当前痛点：

1. PLAN.md §1 已列出十余个游戏侧可订阅事件（`Health.OnHurt` / `EconomyManager.OnMoneyChanged`
   / `LevelManager.OnLevelInitialized` 等），但 modder 需要各自 hook、各自处理 unsubscribe，
   无统一生命周期、无统一可取消语义、无统一优先级。
2. I18n 已 hand-roll hook `LocalizationManager.OnSetLanguage`；后续 Audio/Crafting/Economy
   等模块都会需要订阅各自原生事件 — 缺一个公用骨架会持续重复实现。
3. Phase 1 计划的 SaveUtils / ModBehaviour 卸载钩子**强依赖**于一个可被 modder 统一
   unregister 的总线，否则 mod 卸载时回调泄漏会引发 NRE。

EventBus 解决这三件：统一 API / 优先级 / 可取消 / 可批量卸载。

---

## 1. 参考设计要点（`DecompiledDLL/Event/`）

| 类 | 职责 | 行数 |
|---|---|---|
| `Event` | 事件基类，`Cancelled`+`IsCancelable()`（查 `[Cancelable]` 特性）+ `SetCancelled(bool)` | 24 |
| `CancelableAttribute` | 标记类级可取消事件 | 9 |
| `EventBus` | 同步总线；`SortedSet<TaskItemBase>` 按 Priority 排序；`Post(evt)` 一路广播、遇 `Cancelled` 即停 | 31 |
| `AsyncEventBus` | 协程总线；handler 为 `Func<T, IEnumerator>`，`Post(evt, callback)` 返回 `IEnumerator` | 39 |
| `TaskItemBase`/`TaskItem<T>` | handler 包装；`TaskItem<T>` 在 invoke 时做 `evt is T` 类型过滤后调用强类型 delegate | 26/18 |
| `AsyncTaskItemBase`/`AsyncTaskItem<T>` | 同上，但内含 `Func<Event, IEnumerator>` | 27/21 |
| `EventBusManager : SingletonMonoBehaviour` | 单例宿主，静态暴露 `Sync` / `Async` | 27 |
| `LanguageChangedEvent` | 示例事件类型（构造注入 `LangCode`） | 13 |

核心发布循环：

```csharp
foreach (TaskItemBase task in Handler) {           // Handler 全局一个集合，不按 T 分桶
    task.Delegate(evt);
    if (evt.Cancelled) return true;                // 可取消事件被中途 SetCancelled ⇒ 停止后续 handler
}
return evt.Cancelled;
```

---

## 2. 与 `FML` 现状的契合点 / 偏移点

| 维度 | 参考写法 | FML 现状 | 偏移决策 |
|---|---|---|---|
| 单例风格 | `SingletonMonoBehaviour<T>`（Unity GameObject 承载） | `FastModdingLib.Utils.Singleton<T>`（`Lazy<T>` 纯 C#，无 GameObject） | **采用 FML 的 `Singleton<T>`**；EventBus 同步部分不需要场景对象 |
| 协程宿主 | `SingletonMonoBehaviour` 自带 `StartCoroutine` | 无 GameObject | **新增 `EventBusRunner : MonoBehaviour`**，由 `ModBehaviour.OnAfterSetup` 一次创建；`AsyncEventBus` 调它跑协程 |
| 可空性 | 未启用 | `<Nullable>enable</Nullable>` | 公开 API 全部加可空标注；`Post` 返回 `bool`，handler 默认 `Action<T>` 非 null |
| 卸载支持 | **无 Unregister**（参考设计已知缺陷） | PLAN.md §1 明确要求 "modder 卸载时的 unregister 回调" | **必须新增 `Unregister<T>` / `UnregisterAll(ownerMod)`** |
| 同优先级 handler 顺序 | `SortedSet<TaskItemBase>` + `CompareTo` 仅比 `Priority` ⇒ 同优先级相互覆盖（**参考实现 bug**） | —— | **必须修复**：`TaskItemBase` 增二级键 `RegistrationId`（自增 long），`CompareTo` 优先比 Priority、再比 RegistrationId，保证稳定且不丢 handler |
| `SetCancelled` 语义 | `Cancelled = Cancelled \|\| cancelled`（单向 OR，置 true 后不可逆） | —— | 保留语义；文档中显式说明 handler 间通过 priority 协作：高优先级先执行，决定是否叫停后续 |
| 命名空间 | `MinecraftStyleFramework.Events` | `FastModdingLib.<Module>` 多数用单复数命名 | 采用 **`FastModdingLib.Events`**（与参考一致，避免与单数 `Event` 混淆） |

---

## 3. 公共 API 落地契约

```csharp
namespace FastModdingLib.Events
{
    public abstract class Event
    {
        public bool Cancelled { get; private set; }
        public bool IsCancelable();
        public void SetCancelled(bool cancelled);   // non-cancelable ⇒ NotSupportedException
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CancelableAttribute : Attribute { }

    public sealed class EventBus   // 同步总线
    {
        public void Register<T>(Action<T> handler) where T : Event;
        public void Register<T>(Action<T> handler, int priority) where T : Event;
        public bool Unregister<T>(Action<T> handler) where T : Event;
        public int  UnregisterAll(object ownerMod);              // 返回移除条数；handler 注册时关联 ownerMod
        public void Register<T>(Action<T> handler, int priority, object ownerMod);
        public bool Post(Event evt);                            // 返回 evt.Cancelled
    }

    public sealed class AsyncEventBus
    {
        public void Register<T>(Func<T, IEnumerator> handler) where T : Event;
        public void Register<T>(Func<T, IEnumerator> handler, int priority, object ownerMod) where T : Event;
        public bool Unregister<T>(Func<T, IEnumerator> handler) where T : Event;
        public int  UnregisterAll(object ownerMod);
        public IEnumerator Post(Event evt, Action<bool>? onComplete = null);
    }

    public sealed class EventBusManager : Singleton<EventBusManager>
    {
        public EventBus       Sync  { get; }
        public AsyncEventBus  Async { get; }
        public void Clear();                                     // 视为 UnregisterAll 全量
    }

    public sealed class EventBusRunner : MonoBehaviour   // 私有 internal；仅供 AsyncEventBus 内部使用
    {
        public void Run(IEnumerator coroutine);
    }
}
```

`ownerMod` 形参在 modder 用法上等价于 `Assembly.GetExecutingAssembly()` 或 modder 自定义 tag；FML 内部统一传 `ModBehaviour` 实例以便卸载时整批清理。

---

## 4. 模块文件布局

```
FastModdingLib/
  Events/
    Event.cs                       ~30 LOC
    CancelableAttribute.cs        ~10 LOC
    EventBus.cs                   ~90 LOC  (Register/Unregister/Post，含 ownerMod 索引)
    AsyncEventBus.cs              ~60 LOC  (协程版同结构)
    EventBusManager.cs            ~30 LOC  (Singleton<EventBusManager>)
    EventBusRunner.cs             ~30 LOC  (MonoBehaviour 协程宿主，internal)
    Adapters/
      GameEventAdapters.cs       ~150 LOC  (桥接游戏原生事件 → FML Event 一次性)
      GameEvents/                ~200 LOC  (各事件类型文件，每个 ≤15 LOC)
        HurtEvent.cs
        EntityDeathEvent.cs
        LevelInitializedEvent.cs
        PlayerHearSoundEvent.cs
        SoundSpawnedEvent.cs
        PlayerDeathEvent.cs
        ControllingCharacterChangedEvent.cs
        MoneyChangedEvent.cs
        ItemUnlockStateChangedEvent.cs
        ItemCraftedEvent.cs
        FormulaUnlockedEvent.cs
        QuestTaskFinishedEvent.cs
        CollectSaveDataEvent.cs
        KillCountChangedEvent.cs
        LanguageChangedEvent.cs
    EventBusTest.cs              ~120 LOC  (首个可独立运行的真单元测试；不依赖 Unity)
```

---

## 5. 对参考设计发现的两个缺陷的修复（不照抄）

### 5.1 同优先级 handler 相互覆盖（必修复）

**问题**：参考 `EventBus.Handler` 为 `SortedSet<TaskItemBase>`，而 `TaskItemBase.CompareTo`
仅比较 `Priority`。`SortedSet` 把 `CompareTo == 0` 视为同一对象 ⇒ 两个同 priority 的
handler 注册后只保留 1 个。

**修复**：`TaskItemBase` 增

```csharp
private static long _nextId;
internal long RegistrationId { get; }
public int CompareTo(TaskItemBase? other)
{
    if (other is null) return 1;
    int p = Priority.CompareTo(other.Priority);
    return p != 0 ? p : RegistrationId.CompareTo(other.RegistrationId);
}
```

（构造函数赋 `RegistrationId = Interlocked.Increment(ref _nextId)`）

### 5.2 缺少 Unregister（必新增）

**问题**：参考 API 仅可 `Register`，无 `Unregister`。mod 卸载后已注册 handler 仍被 native
事件触发、对已释放对象回调 ⇒ NRE。

**修复**：`EventBus` / `AsyncEventBus` 维护 `Dictionary<object, List<TaskItemBase>> _byOwner`
（ownerMod → 注册的 handler 列表），提供：

- `Unregister<T>(handler)` — 按 delegate 引用精确移除
- `UnregisterAll(ownerMod)` — mod 卸载时一次性清理（`ModBehaviour.OnModWillBeDeactivated`
  调用）
- `Clear()` — 全量清理，供 hot-reload 测试

---

## 6. 桥接游戏原生事件（Adapter 层）

PLAN.md §1 "关键事件钩子" 表逐一接入 EventBus。每个 adapter 把 native C# event 的 +=
改为在 `GameEventAdapters.WireUp()` 一次性订阅、再 `Publish` 到 `EventBusManager.Sync`：
（Cancel 语义在 native 事件无对应物的场景下仅可取消后续 FML handler，不影响 native 流）

| 游戏 API 原生事件 | FML 事件类型 | 可取消语义 |
|---|---|--|
| `Health.OnHurt` | `HurtEvent(CharacterMainControl target, DamageInfo info)` | 可标记，但 effect 已应用 |
| `Health.OnDead` | `EntityDeathEvent(CharacterMainControl victim)` | 仅观察 |
| `AIMainBrain.OnSoundSpawned` | `SoundSpawnedEvent(...)` | 仅观察 |
| `AIMainBrain.OnPlayerHearSound` | `PlayerHearSoundEvent(...)` | 仅观察 |
| `LevelManager.OnLevelInitialized` | `LevelInitializedEvent(LevelManager mgr)` | 仅观察（Pre 在 Phase 1 不做） |
| `LevelManager.OnMainCharacterDead` | `PlayerDeathEvent(...)` | 仅观察 |
| `LevelManager.OnControllingCharacterChanged` | `ControllingCharacterChangedEvent(...)` | 仅观察 |
| `EconomyManager.OnMoneyChanged` | `MoneyChangedEvent(long old, long now)` | 仅观察 |
| `EconomyManager.OnItemUnlockStateChanged` | `ItemUnlockStateChangedEvent(ItemID, bool unlocked)` | 仅观察 |
| `CraftingManager.OnItemCrafted` | `ItemCraftedEvent(ItemData, int count)` | 仅观察 |
| `CraftingManager.OnFormulaUnlocked` | `FormulaUnlockedEvent(CraftingFormula)` | 仅观察 |
| `QuestManager.OnTaskFinishedEvent` | `QuestTaskFinishedEvent(QuestTask)` | 仅观察 |
| `SavesSystem.OnCollectSaveData` | `CollectSaveDataEvent(...)` | 仅观察；Phase 1 SaveUtils 也基于它 |
| `SavesCounter.OnKillCountChanged` | `KillCountChangedEvent(int total)` | 仅观察 |
| `LocalizationManager.OnSetLanguage` | `LanguageChangedEvent(string langCode)` | 仅观察 |

`[Cancelable]` 仅施加于真正能截断游戏行为的少数事件（如 `OnHurt` 可作为后续 spell-chain
的 gated pre/post，留待具体落地时确认）。

---

## 7. 与 `I18n` 现状的迁移

**现状**：`I18n` 直接 `+=` 到 `LocalizationManager.OnSetLanguage` 完成多语言重读。

**迁移目标**：

1. `GameEventAdapters.WireUp()` 把 `LocalizationManager.OnSetLanguage` 桥到 `EventBusManager.Sync`
   ，publish `LanguageChangedEvent`。
2. `I18n` 改为 `EventBusManager.Sync.Register<LanguageChangedEvent>(OnLangChanged)` 中订阅。
3. **保持 `I18n` 公共 API 不变**；仅为内部实现迁移 — modder 现有调用零回归。
4. 在 `GameEventAdapters.TearDown()` 中 `-=` 原生订阅，避免 mod 卸载泄漏。

该迁移属 B4 段（见 §9），独立可回滚。

---

## 8. `ModBehaviour` 集成点

```csharp
// FastModdingLib/ModBehaviour.cs  (扩展，不改 ModBehaviour 主结构)
protected override void OnAfterSetup()
{
    var harmony = new Harmony("fastmoddinglib");
    harmony.PatchAll();

    EventBusBootstrap.Init();   // 创建 EventBusRunner GameObject + 触发 GameEventAdapters.WireUp()
}

// 与 PLAN.md Phase 1 "ModBehaviour 生命周期 OnModWillBeDeactivated" 一并实现：
internal void OnModWillBeDeactivated()
{
    GameEventAdapters.TearDown();
    EventBusManager.Instance.Clear();
}
```

`EventBusBootstrap.Init()` 内部：

- `new GameObject("[FML EventBusRunner]").AddComponent<EventBusRunner>()`；`DontDestroyOnLoad`
- `GameEventAdapters.WireUp(EventBusRunner)` — native 事件 += 桥、AsyncEventBus 注入 runner

---

## 9. 实施阶段拆分与依赖

```
B1 ─┐
    ├── B2 ─┐
    │       ├── B3 ─┐
    │       │       ├── B5 ─┐
    │       │       │       └── B4 (I18n 迁移，依赖 B3 的 LanguageChanged event 已发布)
    │       └── (B3 可与 B4 并行，只要 B2 完成给出 Event 基类)
    └── B6 (测试) — 与 B1..B4 其中任一完成后即可并行
```

| 阶段 | 范围 | 依赖 | 验收点 |
|---|---|---|---|
| **B1** | Event + CancelableAttribute + EventBus + AsyncEventBus + EventBusManager + EventBusRunner + TaskItem*（**含 §5 两处修复**） | 无 | `dotnet build` 通过；EventBusTest 优先级 / 同优先级不互相覆盖 / Cancel 拦断 / Unregister 四类用例绿 |
| **B2** | `LanguageChangedEvent` 示例事件 + 配套测试 | B1 | 手动构造一个 `EventBus` 订阅 + post 一个 LanguageChangedEvent，handler 收到正确 LangCode |
| **B3** | `GameEventAdapters.WireUp/TearDown` + §6 第一批 5 个核心事件桥（OnHurt/OnDead/OnLevelInitialized/OnMoneyChanged/OnSetLanguage） | B1 + B2 | demo mod 订阅 `LevelInitializedEvent` 在新关卡开打时回调被调用一次 |
| **B4** | `I18n` 内部迁移：从直接 hook 改走 EventBus | B3 (LanguageChangedEvent 已就绪) | 切语言时现有加载行为不回归 |
| **B5** | §6 剩余 10 个事件桥接补齐 | B3 | 每个事件至少有 1 个最小 reachable test 或 canned demo |
| **B6** | `EventBusTest` 全量用例 + Article 级 README/示例 | 滚动 | Phase 5 验收时已就绪可纳入统一测试套件 |

> 实施时按 B1 → B2 → B3 → (B4 \|\| B5) → B6 进行；CPU 上每段均可独立 commit。

---

## 10. 验收清单（DOD）

- [ ] `dotnet build` 通过；`FastModdingLib.csproj` 不新增第三方包引用
- [ ] `EventBusTest` 全绿，覆盖：
  - [ ] priority 排序正确（高 → 低）
  - [ ] 同优先级两 handler **均被调用**（验证 §5.1 修复）
  - [ ] `[Cancelable]` 事件被 SetCancelled 后低优先级 handler 不被调
  - [ ] non-cancelable 事件 SetCancelled 抛 `NotSupportedException`
  - [ ] `Unregister<T>` 后 handler 不再触发
  - [ ] `UnregisterAll(mod)` 批次移除且计数正确
- [ ] 启动游戏 / 切换语言 → I18n 现有加载行为不回归（B4）
- [ ] demo mod：订阅 `LevelInitializedEvent` 在每关开始打印日志
- [ ] 模拟 mod unload：`UnregisterAll` 后触发 native 事件 / post Event 不再回调已释放对象
- [ ] `lsp_diagnostics` 干净（无 warning 当 error 留尾）
- [ ] PLAN §5 覆盖率矩阵新增一行：`| EventBus / 事件桥接 | ✅ | 完备（Phase 1 子模块） |`

---

## 11. 不在本计划范围

- 不修改 PLAN.md 主 phase 编号；本计划作为 Phase 1 的可选子模块独立审查。
- 不引入第三方依赖（Krafs.Publicizer / Harmony 已有）。
- 🚫 **不修改 `0Harmony.dll` 引用方式（版本硬性锁定 2.4.1.0）**：本计划所有改动
  的 csproj 仅可新增 `<Compile Include>` 项与 `Events/*.cs` 等源文件，**不得改动**
  `<Reference Include="0Harmony"><HintPath>0Harmony.dll</HintPath></Reference>`
  现状，也**不得引入** `PackageReference Lib.Harmony`。NuGet 拉取的版本可能与游戏运行
  时实际加载的 Harmony 版本错位，引发 `MissingMethodException` / patch 失效。
  详见 `PLAN.md` §1 仓库卫生说明。
- 不实现 "事件 pre/post 替换 ref 参数"、"事件录制回放"、"跨 EventBus 域广播"、
  "按 typeof(T) 分桶性能优化" — 留 future。
- 不为 EventBusRunner 提供编辑器可视化 / playmode 序列化（mod 制作期编辑器诉求与 NodeCanvas 不同列）。
- 不为 native 事件做 Pre-Harmony patch（Pre 改写参数语义需单独评估；本期仅 Post 观察式桥接）。

---

## 12. 后续可演进项（future，不纳入本期）

- **按 typeof(T) 分桶**：当前 `Post`对所有 handler 走一次 `evt is T` 检查。Handler 量在 mod 场景
  不会大（百级），但若某个 hot 事件高频触发，可按事件类型缓存专桶 lookup。
- **跨 EventBus 域广播**：配合 `RegistryManager` 给每个 mod 一个独立子域 EventBus + 主总线
  中继扩展，便于 mod 间隔离。
- **事件回放**：开发期录制事件序列、回放贴回 EventBus；调试 native 事件时序问题。
- **动态 priority 调整**：对 Register 返回 handle 句柄、支持重新排序（mod 调试用）。

---

## 13. 风险与对策

| 风险 | 说明 | 对策 |
|---|---|---|
| Native 事件被 wire 后无法 tear down | Unity `+=` 静态事件泄漏经典坑 | `GameEventAdapters.WireUp/TearDown` 严格配对；`ModBehaviour.OnModWillBeDeactivated` 强制调用 |
| `SortedSet` 修改引发迭代异常 | `Unregister` 时机若在 `Post` 协程中段，集合被改 ⇒ `InvalidOperationException` | `Post` 内 snapshot 一份 `ToArray()` 再迭代；hot 路径 N 小可接受 |
| `ownerMod` 区分粒度过粗 | 当前以 `Assembly` / ModBehaviour 实例判定 | 先以 ModBehaviour 实例为粒度；后续若需"同一 mod 内分模块卸载"再扩展 |
| `SetCancelled` 单向 OR 语义对 pre-hook 限制 | 某些场景希望高优先级"覆盖"取消决定 | 不改语义；若有需求在 `Event` 加 `ClearCancelled()`（受保护），future 评估 |
| `AsyncEventBus` 协程中断后 handler 残留 | mod unload 时正在跑的协程不会自动 cancel | `EventBusRunner` 持 `List<IEnumerator>` active 表；`Clear()` 时 `StopAllCoroutines()` |

---

*本计划为 EventBus 模块的独立文档；实施时按 §9 阶段顺序进入编码。*