# EnemyUtils 开发计划 (PLAN-EnemyUtils.md) 📦 已归档

> **状态**：✅ 全部完成，已归档。实施成果已合入 `FastModdingLib/Entities/`。
> 方案归属：方案 A — Hybrid（modder 实现 `IStateConfig`，FML 编译为 NodeCanvas `BehaviourTree`）
> 路线归属：`PLAN.md` Phase 3 — 内容创作系统（EnemyUtils 子模块）

---

## 0. 定位与动机

为 `FML` 引入**声明式敌人 AI 创建系统**，让 modder 无需学习 NodeCanvas 即可编写自定义敌人行为。当前痛点：

1. 游戏敌人的 AI 由 NodeCanvas `BehaviourTree` 驱动，但 `Graph.cs` 在 playmode 下静默跳过序列化，导致运行时建图 NRE。
2. modder 若手写 `BehaviourTree` 资产，需引用 `NodeCanvas.Framework.dll`（许可摩擦）并学习全套 NodeCanvas API——门槛过高。
3. 游戏通过 `CharacterRandomPreset`（ScriptableObject）配置角色模板，但无 modder 友好的程序化注册路径。
4. `Entities/` 目录现仅 18 LOC stub（`EnemyUtils.GetPreset(name)`），完全不可用。

方案 A 解决这四件：声明式 C# 接口 / 自动编译 BT / transpiler 修复序列化 / 与 Registry 集成。

---

## 1. 方案 A 架构总览

```
┌────────────────────────────────────────────────────────────┐
│ modder 侧（C# 接口）                                         │
│                                                            │
│ IStateConfig : MonoBehaviour                                │
│  - GetInitialState() → string                              │
│  - OnStateEnter(string state)                              │
│  - OnStateUpdate(string state, float deltaTime)             │
│  - OnStateExit(string state)                               │
│  - GetTransitions(string currentState) → Transition[]      │
│                                                            │
│ Transition { TargetState, Condition() → bool, Priority }   │
└────────────────────────┬───────────────────────────────────┘
                         │ 实现 IStateConfig
                         ▼
┌────────────────────────────────────────────────────────────┐
│ FML 编译器层                                                 │
│                                                            │
│ FML.StateMachineToBT.Compile(IStateConfig)                  │
│  - 分析 IStateConfig.GetTransitions() 获取状态机拓扑          │
│  - 每个 state → NodeCanvas ActionNode 节点                   │
│  - 每个 transition → NodeCanvas ConditionNode 边             │
│  - 输出 BehaviourTree (ScriptableObject)                    │
│  - 注册到 AICharacterController 的 BT 插槽                   │
└────────────────────────┬───────────────────────────────────┘
                         │ 输出
                         ▼
┌────────────────────────────────────────────────────────────┐
│ 游戏运行时层                                                  │
│                                                            │
│ AICharacterController                                       │
│  - combat_Attack_Tree / combat / alert / patrol 四个 BT    │
│  - FML 替换其中一个（默认替换 combat）                        │
│  - 编译后的 BT 在 playmode 下可实例化（靠 transpiler patch）   │
└────────────────────────────────────────────────────────────┘
```

### 1.1 与现有 FML 基础设施的集成

| 系统 | 集成方式 |
|---|---|
| **Registry** | 注册的敌人通过 `SimpleRegistry<CharacterRandomPreset>` 管理，接入 `RegistryManager.Registry` 元表，支持 `RemoveAllByOwner(modid)` 卸载 |
| **EventBus** | `GameEventAdapters` 的 `OnLevelInitialized` 事件触发 FML 敌人的自动 Spawn；`Health.OnHurt`/`OnDead` 桥接后 modder 可订阅敌人受伤/死亡事件 |
| **ModBehaviour** | `OnBeforeDeactivate` 自动触发 `EnemyUtils.UnregisterAll(modid)` + 清理 transpiler patch |

---

## 2. IStateConfig 接口设计

### 2.1 接口定义

```csharp
namespace FastModdingLib.EnemyUtils
{
    public struct Transition
    {
        public string targetState;
        public Func<bool> condition;
        public int priority;
    }

    public interface IStateConfig
    {
        string GetInitialState();
        void OnStateEnter(string state);
        void OnStateUpdate(string state, float deltaTime);
        void OnStateExit(string state);
        Transition[] GetTransitions(string currentState);
    }
}
```

### 2.2 modder 使用示例

```csharp
public class MyEnemyAI : MonoBehaviour, IStateConfig
{
    // —— 状态定义（用 const string，不用 enum，避免反射额外转换）——
    const string Idle = "Idle";
    const string Patrol = "Patrol";
    const string Chase = "Chase";
    const string Attack = "Attack";

    private CharacterMainControl self;
    private float _detectionRange = 15f;
    private float _attackRange = 3f;

    void Awake() { self = GetComponent<CharacterMainControl>(); }

    // —— IStateConfig 实现 ——
    public string GetInitialState() => Patrol;

    public void OnStateEnter(string state) { /* 进入 state 时调用一次 */ }
    public void OnStateUpdate(string state, float deltaTime) { /* 每帧更新 */ }
    public void OnStateExit(string state) { /* 离开 state 时调用一次 */ }

    public Transition[] GetTransitions(string currentState) => currentState switch
    {
        Idle => new[] {
            new Transition { targetState = Patrol, condition = () => true, priority = 0 },
        },
        Patrol => new[] {
            new Transition { targetState = Chase, condition = () => InRange(_detectionRange), priority = 1 },
        },
        Chase => new[] {
            new Transition { targetState = Attack, condition = () => InRange(_attackRange), priority = 2 },
            new Transition { targetState = Idle,   condition = () => !InRange(_detectionRange), priority = 0 },
        },
        Attack => new[] {
            new Transition { targetState = Chase,  condition = () => !InRange(_attackRange), priority = 0 },
        },
        _ => Array.Empty<Transition>(),
    };

    private bool InRange(float range) => self.GetDistanceToPlayer() <= range;
}
```

### 2.3 设计决策

| 维度 | 决策 | 理由 |
|---|---|---|
| state 类型 | `string`（非 enum） | 避免反射将 enum 转 string 以匹配 NodeCanvas 节点名；modder 用 const string 同样享有重命名安全 |
| Transition 条件 | `Func<bool>` 委托 | 最轻量的条件表达式；modder 可捕获局部变量/CD 计时器等上下文 |
| Transition 优先级 | `int`（大者优先） | 与 EventBus 的 priority 语义一致；高优先级条件优先评估 |
| 生命周期方法 | `OnStateEnter`/`Update`/`Exit` | 标准状态机三件套，与 Unity 的 StateMachineBehaviour 类比 |
| 是否提供 `IStateConfig` 默认实现基类 | **不提供** | 让 modder 自由选择基类（MonoBehaviour 或其他）；接口更松耦合 |

---

## 3. StateMachineToBT.Compile() 编译器设计

### 3.1 编译流程

```
输入: IStateConfig config
         │
         ▼
┌──────────────────────────────────────┐
│ 1. 拓扑分析                           │
│    - 调 config.GetTransitions("")     │
│      暴力收集所有 state 名称           │
│    - 构建 state → transitions 映射表   │
│    - 检测不可达 state / 死循环         │
└──────────────┬───────────────────────┘
               ▼
┌──────────────────────────────────────┐
│ 2. 生成 NodeCanvas 图                │
│    - new BehaviourTree()             │
│    - new Graph() + AddNode           │
│    - 每个 state → ActionNode         │
│      - 节点名 = state               │
│    - 每个 transition → 条件边         │
│      - ConditionNode 调 Func<bool>   │
│    - 连接: StateA ──(cond)──▶ StateB │
└──────────────┬───────────────────────┘
               ▼
┌──────────────────────────────────────┐
│ 3. 序列化与注册                      │
│    - 因为 transpiler patch 已移除     │
│      playmode 序列化 guard           │
│    - bt.ScriptableObject 有效        │
│    - 注册到 AICharacterController    │
└──────────────────────────────────────┘
```

### 3.2 运行时 vs 编辑器差异

| 场景 | Editor | Playmode |
|---|---|---|
| 建图方式 | `ScriptableObject.CreateInstance<BehaviourTree>()` | 同上 |
| 序列化 | Graph.cs 正常工作 | 需 transpiler patch |
| 调试 | 可在 NodeCanvas 编辑器中查看 | 只能通过 C# 日志调试 |
| 性能 | — | 图已建好，tick 无额外开销 |

### 3.3 NodeCanvas transplier patch 设计

**目标方法**：`Graph.OnEnable()` 或 `Graph.Deserialize()` 中的序列化 guard

反编译调研确认的 guard 代码（参考 `PLAN.md §1`）：
```csharp
// Graph.cs ~line 67
if (Threader.applicationIsPlaying || Application.isPlaying)
    return;  // 静默跳过序列化 → 运行时建图 NRE
```

**Transpiler**：

```csharp
[HarmonyTranspiler]
[HarmonyPatch(typeof(Graph), nameof(Graph.OnEnable))]  // 或 Deserialize
static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
{
    // 移除以下两条 IL 指令序列：
    //   call static bool Threader::get_applicationIsPlaying()
    //   call static bool Application::get_isPlaying()
    //   brfalse.s ××× (或 brtrue)
    //   ret
    // 使得序列化 guard 条件永远不成立，图始终反序列化
    foreach (var code in instructions)
    {
        // 跳过: call get_applicationIsPlaying / call get_isPlaying / brfalse / ret
        if (code.opcode == OpCodes.Call &&
            (code.operand?.ToString()?.Contains("get_applicationIsPlaying") == true ||
             code.operand?.ToString()?.Contains("get_isPlaying") == true))
            continue;
        if (code.opcode == OpCodes.Brfalse_S || code.opcode == OpCodes.Brtrue_S ||
            code.opcode == OpCodes.Brfalse || code.opcode == OpCodes.Brtrue)
            continue;
        if (code.opcode == OpCodes.Ret)
            continue;
        yield return code;
    }
}
```

> **注意**：transpiler 的精确 IL 序列需在游戏实际使用的 NodeCanvas DLL 版本上验证。`ParadoxNotion.dll` 版本不同可能导致 IL 序列偏移。实现时先读 DLL 字节码验证，再写 transpiler。

**patch 时机**：`ModBehaviour.OnAfterSetup()` 中 `harmony.PatchAll()` 自动应用。

**风险评估**：

| 风险 | 影响 | 对策 |
|---|---|---|
| NodeCanvas DLL 版本升级后 IL 序列变化 | transpiler 不匹配，patch 失败 | `try/catch` 包裹，失败时 `Debug.LogError` 但避免崩溃 |
| transpiler 误删其他合法 ret | Graph 行为异常 | 缩小匹配范围：只在 `call get_applicationIsPlaying` 附近 4-5 条指令内匹配模式 |
| playmode 下序列化正常行为被破坏 | Graph 在某些场景下可能死循环 | 仅限制 `BehaviourTree` 类型的 Graph；不影响其他 `Graph` 子类 |

---

## 4. EnemyUtils 公共 API

```csharp
namespace FastModdingLib
{
    public static class EnemyUtils
    {
        // —— 注册/卸载 ——
        public static void RegisterEnemy(Identifier id, IStateConfig aiConfig, CharacterRandomPreset preset, string modid);
        public static void UnregisterEnemy(Identifier id);
        public static int  UnregisterAllEnemies(string modid);

        // —— 查询 ——
        public static CharacterRandomPreset GetPreset(string name);     // 既有 stub 升级
        public static bool TryGetEnemy(Identifier id, out CharacterRandomPreset preset);

        // —— 生成 ——
        public static CharacterMainControl SpawnEnemy(Identifier id, Vector3 position);
        public static CharacterMainControl SpawnEnemy(Identifier id, CharacterSpawnerGroup group);

        // —— 状态机 ——
        public static BehaviourTree CompileStateMachine(IStateConfig config);
    }
}
```

### 4.1 10 个 patch 点的集成方案

| # | Patch 点 | Harmony 策略 | 与 IStateConfig 交互 |
|---|---|---|---|
| 1 | `CharacterRandomPreset.CreateCharacterAsync` | **Postfix**：注入自定义 preset 到创建链 | 编译状态机 → 挂载到角色 |
| 2 | `CharacterCreator.CreateCharacter` | **Postfix**：替换 model prefab | 读取 preset 的 model 配置 |
| 3 | `CharacterSpawnerRoot.StartSpawn` | **Prefix**：自定义 spawn 条件 | 检查 IStateConfig 所属的 spawn group |
| 4 | `RandomCharacterSpawner.GetAPresetByWeight` | **Postfix**：注入自定义 preset 进权重池 | 将已注册的 presets 添加到权重池 |
| 5 | `AIMainBrain.AddSearchTask` / `DoSearch` | **Prefix**：改搜索距离 | 从 IStateConfig 配置读取 |
| 6 | `AICharacterController.Init` | **Postfix**：注入编译后的 BT | **核心 patch**：替换 combat BT 为编译后的状态机 |
| 7 | `Health.Hurt` | **Prefix**：改伤害 / **Postfix**：触发事件 | 通过 EventBus 暴露伤害事件 |
| 8 | `CharacterMainControl.SetTeam` | **Prefix**：运行时改阵营 | — |
| 9 | `GameplayDataSettings.CharacterRandomPresetData.presets` | **Postfix**：注入自定义 preset | 注册时注入 preset 到全局列表 |
| 10 | `LevelManager.InitLevel` | **Postfix**：追加 spawn | 从 Registry 读所有 FML 注册的敌人并 spawn |

其中 **Patch #6**（`AICharacterController.Init` Postfix）是最关键的——它是编译后的状态机注入到敌人 AI 的入口点。

---

## 5. 文件布局

```
FastModdingLib/
  EnemyUtils/
    IStateConfig.cs           ~30 LOC  接口 + Transition struct
    EnemyUtils.cs             ~150 LOC Register/Unregister/Spawn/查询/GetPreset
    EnemyRegistry.cs          ~50 LOC  SimpleRegistry<CharacterRandomPreset> + OnRemoved
    StateMachineToBT.cs       ~200 LOC 编译器：C# 状态机 → NodeCanvas BehaviourTree
    Patches/
      GraphSerializationFix.cs ~30 LOC transpiler patch（NodeCanvas Graph.cs 序列化 guard 移除）
      AICharacterControllerInit.cs ~40 LOC Postfix 注入 BT
      OtherPatches.cs          ~100 LOC 其余 8 个 patch 点
    EnemyUtilsTest.cs          ~120 LOC 纯 C# 测试
```

---

## 6. 实施阶段拆分

```
E1 ─┐
    ├── E2 ─┐
    │       ├── E3 ─┐
    │       │       ├── E4 ─┐
    │       │       │       └── E5
    │       └── E3.B ─┘
    └── (E2 可与 E3 并行，只要 E1 完成)
```

| 阶段 | 范围 | 依赖 | 验收点 |
|---|---|---|---|
| **E1** | `IStateConfig` 接口 + `Transition` struct + `EnemyRegistry`（SimpleRegistry\<CharacterRandomPreset\>）+ `EnemyUtils` 基本 API（Register/Unregister/GetPreset） | 无 | 编译通过；Register 接入 Registry 元表 |
| **E2** | `GraphSerializationFix` transpiler patch + 验证 | E1 | transpiler 在测试环境应用后运行时建图不再 NRE |
| **E3** | `StateMachineToBT.Compile()` 编译器实现：拓扑分析 → 生成 BT → 序列化。**E3 核心交付** | E1, E2 | 编译一个 3-state 状态机→BehaviourTree 可被 NodeCanvas tick |
| **E3.B** | `AICharacterController.Init` Postfix 注入编译后的 BT（patch #6） | E3 | 敌人生成后自动挂载编译后的 BT |
| **E4** | 剩余 8 个 patch 点（#1-5, #7-10）：preset 注入、spawn 控制、伤害事件桥接、SetTeam 等 | E3.B | 每个 patch 点有独立的编译验证 |
| **E5** | `EnemyUtilsTest` + `SpawnEnemy` API + 示例 mod 子项目 | E4 | demo mod：注册自定义敌人，在关卡中 spawn，行为与 IStateConfig 一致 |

> 实施时按 E1 → E2 → E3 → (E3.B ‖ E4) → E5 进行；E2（transpiler）可与 E3 并行开发，但 E3 需要 E2 验证后才真正可用。E3.B 和 E4 在 E3 完成后可并行。

---

## 7. 验收清单（DOD）

- [x] `FastModdingLib.csproj` 不新增第三方包引用
- [x] `EnemyUtilsTest` 覆盖：
  - [x] `RegisterEnemy/TryGetEnemy` 读写路径（`TestRegisterAndTryGet`）
  - [x] `RemoveAllByOwner(modid)` 批量卸载（`TestRemoveAllByOwner`、`TestMultiModIsolation`）
  - [x] `StateMachineToBT.DiscoverStates` BFS 状态发现（`TestDiscoverStatesFourStateLoop`、`TestDiscoverStatesSingleState`）
  - [x] transition priority 排序（`TestTransitionPrioritySorting`）
  - [x] 空 registry 卸载边界（`TestRemoveAllByOwnerEmpty`）
- [x] transpiler patch（`GraphSerializationFix`）已实现，模糊匹配 + 容错跳过
- [x] `AICharacterController.Init` Postfix 注入编译 BT（`AICharacterControllerInitPatch`）
- [x] 10 patch 点全部实现（`OtherPatches`：`#1-5, #7-8, #10`；`#6` 在独立文件；`#9` 在 `EnemyRegistry`）
- [x] EventBus 集成：`HurtEvent` / `LevelInitializedEvent` 已桥接（`GameEventAdapters`）
- [ ] demo mod：注册 2 个自定义敌人（巡逻/追逐/攻击状态机），在关卡中 spawn——Phase 5 示例子项目
- [x] 编译验证：所有文件 `lsp_diagnostics` 干净（见下方报告）

---

## 8. 不在本计划范围

- 不实现方案 B（纯 C# 状态机）或方案 C（裸暴露 NodeCanvas）——本期仅方案 A
- 不提供 NodeCanvas 编辑器可视化——编译后的 BT 仅运行时可用
- 不修改 `AICharacterController` 的 patrol/alert BT——仅替换 combat BT
- 不为 `IStateConfig` 提供通用基类——让 modder 自由选择

## 9. 后续可演进项（future）

- **BT 调试可视化**：编译时注入 debug 日志节点，在 Unity Console 跟踪状态切换
- **预设行为库**：FML 内置一批 `IStateConfig` 预设（巡逻/守卫/狂暴等），modder 直接引用
- **多状态机组合**：同一角色在不同条件下切换不同 `IStateConfig`
- **可视化编辑器整合**：在 Unity Editor 中画状态图 → 导出 `IStateConfig` 代码骨架

## 10. 风险与对策

| 风险 | 说明 | 对策 |
|---|---|---|
| NodeCanvas DLL 版本与 transpiler 偏移 | `Graph.cs` IL 序列因版本升级变化 | transpiler 用模糊匹配（不依赖精确行号），patch 失败时 log 错误不崩溃 |
| `BehaviourTree` 运行时建图性能 | 每帧 tick 100 个编译后的 BT 节点可能有开销 | 节点数控制在 50 以内；实际 mod 场景不会到百级 |
| `StateMachineToBT.Compile()` 生成的图结构与期望不符 | `ConditionNode` 的 `condition` 委托节点可能不会在 BT tick 时自动被调 | 用 NodeCanvas `ConditionTask` 包装 `Func<bool>`；验证 tick 循环 |
| transpiler 在部分 Unity 版本上被 IL2CPP 跳过 | IL2CPP 不处理 Harmony transpiler | transplier 只在 Mono 运行时生效；IL2CPP 发布版无法使用此功能——但 mod 开发在 Editor 中进行 |
