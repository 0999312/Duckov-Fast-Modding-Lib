# FML Harmony Patch 设计方案

> 标签驱动合成配方 & 标签击杀任务需要 Harmony Patch 解决
> 最后更新：2026-07-01

---

## 0. 问题背景

游戏原生的合成配方和击杀任务**仅支持精确 typeID 匹配**，不支持标签/口径筛选。

### 游戏侧源码分析

**合成系统** (`CraftingManager.cs:103-125`):
```csharp
// Craft() 方法 —— 核心流程
private async UniTask<List<Item>> Craft(CraftingFormula formula)
{
    if (!formula.cost.Enough) return null;     // ← EconomyManager.IsEnough(this)
    if (!formula.cost.Pay()) return null;       // ← EconomyManager.Pay(this)
    // ... 生成产物 ...
}
```

`Cost.Enough` → `EconomyManager.IsEnough(Cost)` — 按 typeID 检查玩家背包  
`Cost.Pay()` → `EconomyManager.Pay(Cost)` — 按 typeID 移除物品  
`Cost.ItemEntry` = `{ int id, long amount }` — 仅支持精确 typeID

**击杀任务** (`QuestTask_KillCount.cs:295`):
```csharp
// Health_OnDead() —— 击杀检测
if (withWeapon && info.fromWeaponItemID != weaponTypeID)
    return;  // ← 仅比较 typeID
```

`weaponTypeID` 是 `[ItemTypeID] int` 字段，仅支持精确 typeID。

---

## 1. 合成标签 Patch

### 1.1 策略

**不直接 Patch `EconomyManager.IsEnough/Pay`**（影响面太广，可能干扰游戏原生逻辑），而是：

1. FML 维护一个 `Dictionary<string, TagCostEntry>` — formulaId → 标签成本配置
2. Harmony Prefix 拦截 `CraftingManager.Craft(CraftingFormula)` 
3. 检查该 formula 是否有标签成本注册
4. 如果有：先执行 FML 的成本验证和扣除，成功后再放行原方法（让原方法处理剩余的标准成本）

### 1.2 新增文件

```
FastModdingLib/Crafting/
├── TagCostRegistry.cs              (~60 LOC)  标签成本注册表
├── Patches/
│   └── CraftingManagerPatch.cs     (~80 LOC)  Craft 方法 Prefix + 标签成本验证
```

### 1.3 `TagCostRegistry.cs`

```csharp
namespace FastModdingLib
{
    /// <summary>标签成本条目：每个 formula 的标签匹配成本。</summary>
    public class TagCostEntry
    {
        /// <summary>配方 ID（与 CraftingFormula.id 对应）。</summary>
        public string FormulaId;
        /// <summary>标签成本列表。</summary>
        public TagItemCost[] Costs;
        /// <summary>注册者 modid。</summary>
        public string Modid;
    }

    /// <summary>单个标签物品成本。</summary>
    public struct TagItemCost
    {
        public string Tag;          // 物品标签，如 "Food"、"Metal"
        public int Amount;          // 数量
        public int? MinQuality;     // 可选：最低品质
        public bool DurabilityCost; // 是否耐久度折算
    }

    /// <summary>标签成本注册表。</summary>
    public sealed class TagCostRegistry : SimpleRegistry<TagCostEntry>
    {
        private static TagCostRegistry? _instance;
        public static TagCostRegistry Instance => _instance ??= new TagCostRegistry();

        /// <summary>查询 formula 是否有标签成本。</summary>
        public bool TryGetCosts(string formulaId, out TagItemCost[] costs)
        {
            foreach (var kvp in this)
            {
                if (kvp.Value.FormulaId == formulaId)
                {
                    costs = kvp.Value.Costs;
                    return true;
                }
            }
            costs = default!;
            return false;
        }
    }
}
```

### 1.4 `CraftingManagerPatch.cs`

```csharp
[HarmonyPatch(typeof(CraftingManager), "Craft")]
class CraftingManagerPatch
{
    /// <summary>
    /// Prefix: 在原生 Craft 之前检查标签成本。
    /// 返回 false 表示拦截（标签成本不满足时阻止合成）。
    /// 返回 true 表示放行（无标签成本或标签成本已扣除）。
    /// </summary>
    static bool Prefix(CraftingFormula formula)
    {
        // 检查是否有标签成本注册
        if (!TagCostRegistry.Instance.TryGetCosts(formula.id, out var tagCosts))
            return true;  // 无标签成本，放行原生逻辑

        // 验证标签成本
        foreach (var cost in tagCosts)
        {
            if (!HasEnoughByTag(cost.Tag, cost.Amount, cost.MinQuality, cost.DurabilityCost))
            {
                Debug.Log($"[FML] Tag cost not satisfied: tag={cost.Tag}, amount={cost.Amount}");
                return false;  // 成本不满足，阻止合成
            }
        }

        // 扣除标签物品
        foreach (var cost in tagCosts)
        {
            ConsumeItemsByTag(cost.Tag, cost.Amount, cost.MinQuality, cost.DurabilityCost);
        }

        return true;  // 放行原生逻辑（处理标准 Cost 中的 typeID 成本）
    }
}
```

### 1.5 标签物品搜索/扣除实现

```csharp
/// <summary>检查玩家背包中是否有足够的标签匹配物品。</summary>
static bool HasEnoughByTag(string tag, int amount, int? minQuality, bool durabilityCost)
{
    var inventory = CharacterMainControl.Main?.CharacterItem?.Inventory;
    if (inventory == null) return false;

    float totalAvailable = 0f;
    foreach (var slot in inventory.AllSlots)
    {
        var item = slot.Content;
        if (item == null) continue;

        // 检查物品标签
        if (!HasTag(item, tag)) continue;

        // 品质检查
        if (minQuality.HasValue && item.Quality < minQuality.Value) continue;

        // 计算可用量
        float available = item.StackCount;
        if (durabilityCost)
        {
            // 耐久度折算：当前耐久度 / 最大耐久度
            var durabilityStat = item.GetStat("Durability".GetHashCode());
            if (durabilityStat != null)
                available *= durabilityStat.Value / durabilityStat.BaseValue;
        }

        totalAvailable += available;
        if (totalAvailable >= amount) return true;
    }
    return totalAvailable >= amount;
}

/// <summary>从玩家背包中扣除标签匹配物品（优先低耐久度）。</summary>
static void ConsumeItemsByTag(string tag, int amount, int? minQuality, bool durabilityCost)
{
    var inventory = CharacterMainControl.Main?.CharacterItem?.Inventory;
    if (inventory == null) return;

    // 收集所有匹配物品，按耐久度升序排列
    var candidates = new List<(Item item, float effectiveAmount)>();
    foreach (var slot in inventory.AllSlots)
    {
        var item = slot.Content;
        if (item == null || !HasTag(item, tag)) continue;
        if (minQuality.HasValue && item.Quality < minQuality.Value) continue;

        float effective = item.StackCount;
        if (durabilityCost)
        {
            var stat = item.GetStat("Durability".GetHashCode());
            if (stat != null) effective *= stat.Value / stat.BaseValue;
        }
        candidates.Add((item, effective));
    }
    candidates.Sort((a, b) => a.effectiveAmount.CompareTo(b.effectiveAmount));

    // 扣除
    float remaining = amount;
    foreach (var (item, effective) in candidates)
    {
        if (remaining <= 0) break;
        int toRemove = Mathf.CeilToInt(Mathf.Min(remaining, item.StackCount));
        item.StackCount -= toRemove;
        if (item.StackCount <= 0) item.DestroyTree();
        remaining -= toRemove;
    }
}

/// <summary>检查物品是否有指定标签。</summary>
static bool HasTag(Item item, string tag)
{
    // 通过 ItemAssetsCollection 或 ItemMetaData 查询标签
    var meta = ItemAssetsCollection.GetMetaData(item.TypeID);
    return meta?.Tags?.Any(t => t.name == tag) ?? false;
}
```

---

## 2. 击杀任务武器标签 ⚠️ 设计调整 — 改用 FMLTask 子类方案

> **2026-07-02 决策**：击杀任务武器标签匹配**不使用 Harmony Patch 实现**，
> 改为通过 `FMLTask_KillCountByTag` 子类方案（详见 `DESIGN-QuestTaskExtension.md`）。
> 该子类已在 `FastModdingLib/Quests/FMLTask_KillCountByTag.cs` 中实现。
> 本节保留原始 Patch 设计方案供参考，**不会实际实施**。

### 2.1 策略（已废弃）

`QuestTask_KillCount.Health_OnDead` 第 295 行检查武器 typeID。我们需要在 FML 内部维护一个 `weaponTag → taskId` 映射表，在 Prefix 中拦截并替换检查逻辑。

### 2.2 新增文件

```
FastModdingLib/Quests/Patches/
└── KillCountWeaponTagPatch.cs     (~60 LOC)  Health_OnDead 方法 Prefix
```

### 2.3 `KillCountWeaponTagPatch.cs`

```csharp
[HarmonyPatch(typeof(QuestTask_KillCount), "Health_OnDead")]
class KillCountWeaponTagPatch
{
    // FML 维护：task 实例 → weaponTag 的映射
    private static readonly Dictionary<QuestTask_KillCount, string> _weaponTagMap = new();

    /// <summary>注册武器标签映射。</summary>
    internal static void RegisterWeaponTag(QuestTask_KillCount task, string weaponTag)
    {
        _weaponTagMap[task] = weaponTag;
    }

    /// <summary>Prefix: 拦截武器检查，用标签匹配替换 typeID 匹配。</summary>
    static bool Prefix(
        QuestTask_KillCount __instance,
        Health health, DamageInfo info)
    {
        // 检查是否有武器标签注册
        if (!_weaponTagMap.TryGetValue(__instance, out var weaponTag))
            return true;  // 无标签注册，走原生逻辑

        // 基础检查（复用原生逻辑）
        if (health.team == Teams.player) return false;
        var fromCharacter = info.fromCharacter;
        if (fromCharacter == null || !info.fromCharacter.IsMainCharacter()) return false;

        // 🆕 标签武器检查：替换原生的 weaponTypeID 检查
        var weaponItem = fromCharacter.PrimWeaponSlot()?.Content;
        if (weaponItem == null) return false;
        if (!HasWeaponTag(weaponItem, weaponTag)) return false;

        // 其他检查（场景、爆头、Buff——复用原生逻辑）
        if (!__instance.SceneRequirementSatisfied) return false;
        // ... (其他检查省略，通过反射调用私有字段)

        // 调用原生 AddCount（通过反射）
        typeof(QuestTask_KillCount)
            .GetMethod("AddCount", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.Invoke(__instance, null);

        return false;  // 已处理，阻止原生逻辑
    }

    private static bool HasWeaponTag(Item weapon, string tag)
    {
        var meta = ItemAssetsCollection.GetMetaData(weapon.TypeID);
        return meta?.Tags?.Any(t => t.name == tag) ?? false;
    }
}
```

---

## 3. 图标循环动画（可选优化）⏳ 设计阶段，未实现

### 3.1 问题

Building 图标和 Merchant 图标在游戏 UI 中可能需要循环播放动画（如呼吸灯、旋转效果）。

### 3.2 实现方案

```csharp
// BuildingUtils 新增：
/// <summary>为 Sprite 添加循环旋转动画。</summary>
public static void AnimateIcon(SpriteRenderer renderer, float speed = 30f)
{
    var go = renderer.gameObject;
    var anim = go.GetComponent<IconAnimator>() ?? go.AddComponent<IconAnimator>();
    anim.RotationSpeed = speed;
}

/// <summary>为 Sprite 添加呼吸缩放动画。</summary>
public static void AnimateIconPulse(SpriteRenderer renderer, float speed = 2f, float scale = 0.1f)
{
    var go = renderer.gameObject;
    var anim = go.GetComponent<IconAnimator>() ?? go.AddComponent<IconAnimator>();
    anim.PulseSpeed = speed;
    anim.PulseScale = scale;
}

// IconAnimator MonoBehaviour（内部）
internal class IconAnimator : MonoBehaviour
{
    public float RotationSpeed;
    public float PulseSpeed;
    public float PulseScale;
    private Vector3 _baseScale;

    void Start() { _baseScale = transform.localScale; }
    void Update()
    {
        if (RotationSpeed > 0)
            transform.Rotate(0, 0, RotationSpeed * Time.deltaTime);
        if (PulseSpeed > 0)
            transform.localScale = _baseScale * (1 + Mathf.Sin(Time.time * PulseSpeed) * PulseScale);
    }
}
```

---

## 4. FML API 整合

### 4.1 CraftingUtils 扩展

```csharp
// 现有签名添加标签支持：
CraftingUtils.AddCraftingFormula(new CraftingFormulaData
{
    Id = new Identifier("mymod", "coffee"),
    Money = 50,
    CostItems = new[] {
        ItemEntry.Of("mymod:coffee_beans", 3),
        ItemEntry.ByTag("Water", 1)        // 🆕 标签成本
    },
    Result = ItemEntry.Of("mymod:coffee", 1),
    Tags = new[] { "WorkBenchAdvanced" }
});
// FML 内部：
// 1. 将 ItemEntry.ByTag 的成本分离到 TagCostRegistry
// 2. 将标准 typeID 成本保留在 CraftingFormula.cost 中
// 3. Harmony Prefix 自动拦截
```

### 4.2 QuestUtils 扩展

```csharp
// 注册任务时使用 FMLTask_KillCountByTag 子类：
QuestUtils.RegisterQuest(id, new QuestData
{
    tasks = new List<TaskData>
    {
        new TaskKillByTagData              // 🆕 FML 子类方案
        {
            weaponTag = "Pistol",
            requireAmount = 10,
            requireEnemy = "Scav"
        }
    }
});
// FML 内部：
// 1. QuestUtils.RegisterQuest 检测到 TaskKillByTagData
// 2. 自动创建 FMLTask_KillCountByTag 实例（继承 QuestTask_KillCount）
// 3. FMLTask_KillCountByTag.OnInitialize 覆写 weaponTypeID 匹配逻辑
```

---

## 5. 文件总览

| 文件 | 用途 | 预估 LOC |
|------|------|----------|
| `Crafting/TagCostRegistry.cs` | 标签成本注册表 | ~60 |
| `Crafting/Patches/CraftingManagerPatch.cs` | 合成标签 Harmony Patch | ~100 |
| `Quests/Patches/KillCountWeaponTagPatch.cs` | ~~击杀武器标签 Harmony Patch~~ ❌ 不实现（改用 `FMLTask_KillCountByTag` 子类） | — |
| `Buildings/IconAnimator.cs` | 图标循环动画 | ~30 |

---

## 6. 验收标准

- [ ] `ItemEntry.ByTag("Water", 3)` 配方在玩家背包有任意水标签物品时可合成
- [ ] 合成后正确扣除匹配的物品（优先低耐久度）
- [ ] `ItemEntry.WithDurabilityCost(true)` 按耐久度比例折算消耗
- [x] ~~`TaskKillCount.weaponTag = "Pistol"`~~ 已改用 `TaskKillByTagData` + `FMLTask_KillCountByTag` 子类方案，不使用 Patch
- [ ] 无标签注册时原生逻辑不受影响（零回归）
- [ ] `dotnet build` 通过（0 错误）
