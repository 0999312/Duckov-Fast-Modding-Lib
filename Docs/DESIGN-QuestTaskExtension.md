# 扩展 QuestTask 系统设计

> 不 Patch 原生 Task，新增 FML 子类实现标签/口径/耐久度匹配
> 最后更新：2026-07-01

---

## 1. 游戏侧 Task 架构

### 1.1 Task 基类

```csharp
// Duckov.Quests.Task (abstract MonoBehaviour)
public abstract class Task : MonoBehaviour, ISaveDataProvider
{
    public Quest Master { get; internal set; }
    public int ID { get; internal set; }
    public virtual string Description { get; }
    public virtual string[] ExtraDescriptsions { get; }

    // 子类必须实现
    protected abstract bool CheckFinished();
    public abstract object GenerateSaveData();
    public abstract void SetupSaveData(object data);

    // 已提供
    protected void ReportStatusChanged();  // 触发 onStatusChanged + 通知 Master
    protected virtual void OnInit();       // 初始化回调
}
```

### 1.2 现有 Task 子类

| 类 | 用途 | 关键字段 |
|----|------|---------|
| `QuestTask_KillCount` | 击杀计数 | `requireAmount`, `weaponTypeID`, `requireEnemyType`, `requireHeadShot` |
| `QuestTask_SubmitMoney` | 提交金钱 | `requireAmount` (money) |
| `QuestTask_ConstructBuilding` | 建造建筑 | `requireBuildingID`, `requireAmount` |
| `QuestTask_UseItem` | 使用物品 | `itemTypeID`, `requireAmount` |
| `QuestTask_ReachLocation` | 到达地点 | `requireLocation` |
| `QuestTask_Evacuate` | 撤离 | — |
| `QuestTask_UnlockPerk` | 解锁技能 | `perkID` |

**关键发现**：没有 `QuestTask_SubmitItem`（提交物品任务）——提交物品可能通过 `QuestTask_UseItem` 变体或直接在 Quest 层面处理。

### 1.3 任务注册流程

```
QuestData.tasks[] → Quest GameObject
  → AddComponent<QuestTask_Xxx>()
  → 反射设置序列化字段
  → task.Init() → OnInit() → OnEnable()
  → 订阅游戏事件（Health.OnDead 等）
```

---

## 2. 设计原则

**不 Patch 原生 Task**。原因：
1. Patch 影响所有已有任务（可能破坏游戏原生行为）
2. QuestTask_KillCount 是 sealed 或字段为 `[SerializeField] private`，外部扩展困难
3. 新增 FML Task 子类可以自由添加字段（如 `weaponTag`、`itemTag`、`durabilityCost`），不受原生约束
4. 新 Task 通过 QuestUtils.RegisterQuest 时自动识别并注入

---

## 3. 新增 Task 子类

### 3.1 `FMLTask_KillCountByTag`

```csharp
namespace FastModdingLib.Quests
{
    /// <summary>
    /// FML 扩展击杀任务：支持按武器标签匹配击杀。
    /// 继承自 Duckov.Quests.Task。
    /// </summary>
    public class FMLTask_KillCountByTag : Task
    {
        [SerializeField] private int requireAmount = 1;
        [SerializeField] private int amount;
        [SerializeField] private string weaponTag;       // 🆕 武器标签，如 "Pistol"
        [SerializeField] private string requireEnemyName; // 敌人 nameKey
        [SerializeField] private bool requireHeadShot;

        protected override bool CheckFinished()
            => amount >= requireAmount;

        public override object GenerateSaveData() => amount;
        public override void SetupSaveData(object data)
        { if (data is int n) amount = n; }

        protected override void OnInit()
        {
            Health.OnDead += OnEnemyDead;
        }

        private void OnDestroy()
        {
            Health.OnDead -= OnEnemyDead;
        }

        private void OnEnemyDead(Health health, DamageInfo info)
        {
            if (health.team == Teams.player) return;
            var fromChar = info.fromCharacter;
            if (fromChar == null || !fromChar.IsMainCharacter()) return;

            // 🆕 标签武器检查
            if (!string.IsNullOrEmpty(weaponTag))
            {
                var weapon = fromChar.PrimWeaponSlot()?.Content;
                if (weapon == null) return;
                if (!ItemUtils.HasTag(weapon, weaponTag)) return;
            }

            // 敌人类型检查
            if (!string.IsNullOrEmpty(requireEnemyName))
            {
                var victim = health.TryGetCharacter();
                if (victim?.characterPreset?.nameKey != requireEnemyName) return;
            }

            // 爆头检查
            if (requireHeadShot && info.crit <= 0) return;

            if (amount < requireAmount)
            {
                amount++;
                ReportStatusChanged();
            }
        }
    }
}
```

### 3.2 `FMLTask_SubmitItemByTag`

```csharp
namespace FastModdingLib.Quests
{
    /// <summary>
    /// FML 扩展提交物品任务：支持按标签匹配物品，支持耐久度折算。
    /// </summary>
    public class FMLTask_SubmitItemByTag : Task, IHasInteract
    {
        [SerializeField] private string itemTag;         // 🆕 物品标签
        [SerializeField] private int requireAmount;
        [SerializeField] private int? minQuality;        // 🆕 最低品质
        [SerializeField] private bool durabilityCost;    // 🆕 耐久度折算
        [SerializeField] private int submitted;

        // 交互：打开提交界面或直接检测背包
        // ... 实现省略 ...

        protected override bool CheckFinished()
            => submitted >= requireAmount;

        public override object GenerateSaveData() => submitted;
        public override void SetupSaveData(object data)
        { if (data is int n) submitted = n; }

        /// <summary>尝试从玩家背包提交物品。</summary>
        public bool TrySubmitFromInventory()
        {
            var inventory = CharacterMainControl.Main?.CharacterItem?.Inventory;
            if (inventory == null) return false;

            int needed = requireAmount - submitted;
            float submittedDurability = 0f;
            var candidates = new List<(Item item, float effective)>();

            foreach (var slot in inventory.AllSlots)
            {
                var item = slot.Content;
                if (item == null) continue;

                // 标签匹配
                if (!string.IsNullOrEmpty(itemTag) && !ItemUtils.HasTag(item, itemTag)) continue;
                if (minQuality.HasValue && item.Quality < minQuality.Value) continue;

                float effective = item.StackCount;
                if (durabilityCost)
                {
                    var stat = item.GetStat("Durability".GetHashCode());
                    if (stat != null) effective *= stat.Value / stat.BaseValue;
                }
                candidates.Add((item, effective));
            }

            // 按有效量排序（优先消耗低耐久度）
            candidates.Sort((a, b) => a.effective.CompareTo(b.effective));

            float accumulated = 0f;
            var toRemove = new List<Item>();
            foreach (var (item, effective) in candidates)
            {
                if (accumulated >= needed) break;
                accumulated += effective;
                toRemove.Add(item);
            }

            if (accumulated < needed) return false;

            // 扣除物品
            foreach (var item in toRemove)
            {
                item.StackCount--;
                if (item.StackCount <= 0) item.DestroyTree();
            }

            submitted += needed;
            ReportStatusChanged();
            return true;
        }
    }
}
```

### 3.3 `FMLTask_KillCountByTag` vs 原生 `QuestTask_KillCount` 对比

| 功能 | 原生 | FML 扩展 |
|------|------|---------|
| 武器匹配 | `weaponTypeID` (精确 typeID) | `weaponTag` (标签) |
| 敌人匹配 | `requireEnemyType` (CharacterRandomPreset 引用) | `requireEnemyName` (nameKey 字符串) |
| 爆头 | `requireHeadShot` | `requireHeadShot` |
| 场景限制 | `requireSceneID` | — (暂不支持) |
| 无需 Patch | — | ✅ 独立 Task 子类 |

---

## 4. QuestUtils 集成

### 4.1 任务数据扩展

```csharp
// QuestData 中新增 TaskData 子类
public class TaskKillByTagData : TaskData
{
    public int requireAmount;
    public string? weaponTag;        // 🆕
    public string? requireEnemyName;
    public bool requireHeadShot;
}

public class TaskSubmitItemByTagData : TaskData
{
    public string? itemTag;          // 🆕
    public int requireAmount;
    public int? minQuality;
    public bool durabilityCost;
}
```

### 4.2 注册流程

```csharp
// QuestUtils.RegisterQuest 内部：
foreach (var taskData in questData.tasks)
{
    Task task;
    if (taskData is TaskKillByTagData kt)
    {
        task = taskGo.AddComponent<FMLTask_KillCountByTag>();
        // 反射设置字段...
    }
    else if (taskData is TaskSubmitItemByTagData st)
    {
        task = taskGo.AddComponent<FMLTask_SubmitItemByTag>();
        // 反射设置字段...
    }
    else
    {
        // 原生 TaskData 处理（不变）
    }
}
```

### 4.3 modder 使用示例

```csharp
var quest = new QuestData
{
    ID = 6001,
    displayName = "quest_pistol_hunter",
    tasks = new List<TaskData>
    {
        new TaskKillByTagData           // 🆕 FML 扩展
        {
            id = 1,
            requireAmount = 15,
            weaponTag = "Pistol",       // 任意手枪标签
            requireEnemyName = "Character_Scav",
            requireHeadShot = false
        },
        new TaskSubmitItemByTagData     // 🆕 FML 扩展
        {
            id = 2,
            itemTag = "Drink",          // 任意饮品标签
            requireAmount = 5,
            durabilityCost = false
        }
    },
    rewards = new List<RewardData>
    {
        new RewardEXP { amount = 500 }
    }
};
QuestUtils.RegisterQuest(new Identifier("mymod", "drink_hunter"), quest);
```

---

## 5. 不采用 Patch 方案的原因

| 方案 | 优点 | 缺点 |
|------|------|------|
| **Patch 原生 Task** | 复用现有字段 | 影响所有已有任务；可能破坏游戏原生行为；扩展性差（无法新增字段） |
| **新增 FML Task 子类** ✅ | 独立安全；可自由扩展字段；不影响原生任务；可同时支持标签+口径+耐久度 | 需要 QuestUtils 内部适配 |

**结论**：采用新增 FML Task 子类方案。

---

*本设计的核心类型（`FMLTask_KillCountByTag`、`FMLTask_SubmitItemByTag`、`TaskKillByTagData`、`TaskSubmitItemByTagData`）已在 `FastModdingLib/Quests/` 中实现。完整的任务系统集成和验收标准待 Phase 5。*
