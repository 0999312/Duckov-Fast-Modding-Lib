# 标签+耐久度合成系统设计

> FML 扩展 `CraftingUtils` 支持标签/口径/耐久度驱动的合成配方
> 最后更新：2026-07-01

---

## 1. 现状分析

### 1.1 游戏原生合成流程

```
CraftingManager.Craft(formula)
  ├── formula.cost.Enough        ← EconomyManager.IsEnough(Cost)
  │     └── 按 typeID 检查玩家背包
  ├── formula.cost.Pay()         ← EconomyManager.Pay(Cost)
  │     └── 按 typeID 从背包移除物品
  └── cost.Return(...)           ← 生成产物到背包
```

`CraftingFormula.cost` 是 `Cost` 结构体：
```csharp
public struct Cost {
    public long money;
    public ItemEntry[] items;    // { int id, long amount }[]  — 仅 typeID
}
```

**原生不支持**：
- 标签匹配消耗（如 "任意 水标签物品 ×2"）
- 口径匹配消耗（如 "任意 5.56 口径子弹 ×30"）
- 耐久度折算（如 "消耗总耐久度 ≥100% 的任意护甲 ×1"）

### 1.2 FML 现有能力

`CraftingUtils.AddCraftingFormula` 接受 `CraftingFormulaData`：
```csharp
CraftingFormulaData {
    ItemEntry[] CostItems;  // ItemEntry { Identifier? ItemId; int ItemTypeId; int Amount }
    ItemEntry Result;
    // ...
}
```

`ItemEntry` 已在 Wave 2 中扩展了 `ItemTag`、`MinQuality`、`DurabilityCost` 字段。

---

## 2. 设计策略

### 2.1 核心思路：双层分离

FML 将合成配方的成本分为两层处理：

```
┌──────────────────────────────────────────────────┐
│ FML TagCostLayer (标签/口径/耐久度)                │
│   - 在 CraftingManager.Craft 之前执行              │
│   - 验证标签/口径匹配的物品是否充足                 │
│   - 扣除匹配的物品                                 │
│   - 失败则阻止合成                                 │
├──────────────────────────────────────────────────┤
│ Native CostLayer (标准 typeID)                     │
│   - 原生的 CraftingFormula.cost 流程               │
│   - 仅包含精确 typeID 的成本项                     │
│   - 标签成本已从 native cost 中移除                │
└──────────────────────────────────────────────────┘
```

### 2.2 为什么不 Patch Cost 而是分两层？

| 方案 | 评估 |
|------|------|
| Patch `EconomyManager.IsEnough/Pay` | 影响所有 Economy 操作（购买、修理等），风险太大 |
| Patch `CraftingManager.Craft` Prefix | ✅ 仅影响合成，且只在有标签成本注册时生效 |

---

## 3. 实现设计

### 3.1 `TagCostRegistry`

```csharp
namespace FastModdingLib
{
    /// <summary>标签成本注册表。存储每个 FML 注册配方的标签成本。</summary>
    internal static class TagCostRegistry
    {
        private static readonly Dictionary<string, TagCostEntry> _entries = new();

        /// <summary>注册配方的标签成本。</summary>
        public static void Register(string formulaId, TagCostEntry entry)
        {
            _entries[formulaId] = entry;
        }

        /// <summary>查询配方是否有标签成本。</summary>
        public static bool TryGet(string formulaId, out TagCostEntry entry)
            => _entries.TryGetValue(formulaId, out entry);

        /// <summary>移除配方的标签成本。</summary>
        public static bool Remove(string formulaId)
            => _entries.Remove(formulaId);

        /// <summary>按 modid 批量移除。</summary>
        public static int RemoveAllByOwner(string modid)
            => _entries.RemoveAll(kvp => kvp.Value.Modid == modid);
    }

    /// <summary>单个配方的标签成本条目。</summary>
    public class TagCostEntry
    {
        public string FormulaId;
        public TagItemCost[] Costs;
        public string Modid;
    }

    /// <summary>单个标签物品成本。</summary>
    public struct TagItemCost
    {
        public string? Tag;           // 物品标签
        public int Amount;            // 数量
        public int? MinQuality;       // 最低品质
        public bool DurabilityCost;   // 是否耐久度折算
    }
}
```

### 3.2 `CraftingManagerPatch` — 合成拦截

```csharp
namespace FastModdingLib.Crafting.Patches
{
    /// <summary>
    /// Harmony Prefix 拦截 CraftingManager.Craft。
    /// 仅在有 FML 标签成本注册时生效；无注册则完全放行原生逻辑。
    /// </summary>
    [HarmonyPatch(typeof(CraftingManager), "Craft")]
    internal static class CraftingManagerPatch
    {
        /// <returns>true=放行原生逻辑；false=阻止合成</returns>
        static bool Prefix(CraftingFormula formula)
        {
            if (!TagCostRegistry.TryGet(formula.id, out var entry))
                return true;  // 无标签成本，放行

            // 验证标签成本
            if (!TagCostValidator.Validate(entry.Costs))
            {
                Debug.Log($"[FML] Tag cost validation failed for formula '{formula.id}'");
                return false;  // 阻止合成
            }

            // 扣除标签物品
            TagCostValidator.Consume(entry.Costs);

            return true;  // 放行原生 cost 处理
        }
    }
}
```

### 3.3 `TagCostValidator` — 标签验证与扣除

```csharp
namespace FastModdingLib.Crafting
{
    /// <summary>标签成本验证器：搜索、验证、扣除标签匹配物品。</summary>
    internal static class TagCostValidator
    {
        /// <summary>验证玩家背包中是否有足够的标签匹配物品。</summary>
        public static bool Validate(TagItemCost[] costs)
        {
            var inventory = CharacterMainControl.Main?.CharacterItem?.Inventory;
            if (inventory == null) return false;

            foreach (var cost in costs)
            {
                float available = CountAvailable(inventory, cost);
                float required = cost.DurabilityCost ? cost.Amount : cost.Amount;
                if (available < required) return false;
            }
            return true;
        }

        /// <summary>从玩家背包中扣除标签匹配物品。</summary>
        public static void Consume(TagItemCost[] costs)
        {
            var inventory = CharacterMainControl.Main?.CharacterItem?.Inventory;
            if (inventory == null) return;

            foreach (var cost in costs)
            {
                ConsumeFromInventory(inventory, cost);
            }
        }

        private static float CountAvailable(
            /* IInventory */ object inventory, TagItemCost cost)
        {
            float total = 0f;
            foreach (var item in EnumerateItems(inventory))
            {
                if (!Matches(item, cost)) continue;
                total += GetEffectiveAmount(item, cost.DurabilityCost);
            }
            return total;
        }

        private static void ConsumeFromInventory(
            object inventory, TagItemCost cost)
        {
            // 收集所有匹配物品，按有效量升序（优先消耗低耐久度）
            var candidates = new List<(object item, float effective)>();
            foreach (var item in EnumerateItems(inventory))
            {
                if (!Matches(item, cost)) continue;
                candidates.Add((item, GetEffectiveAmount(item, cost.DurabilityCost)));
            }
            candidates.Sort((a, b) => a.effective.CompareTo(b.effective));

            // 扣除
            float remaining = cost.Amount;
            foreach (var (item, effective) in candidates)
            {
                if (remaining <= 0) break;
                int toRemove = Mathf.CeilToInt(Mathf.Min(remaining, GetStackCount(item)));
                SetStackCount(item, GetStackCount(item) - toRemove);
                remaining -= toRemove;
            }
        }

        private static bool Matches(object item, TagItemCost cost)
        {
            if (!string.IsNullOrEmpty(cost.Tag) && !HasTag(item, cost.Tag)) return false;
            if (cost.MinQuality.HasValue && GetQuality(item) < cost.MinQuality.Value) return false;
            return true;
        }

        private static float GetEffectiveAmount(object item, bool durabilityCost)
        {
            float stack = GetStackCount(item);
            if (!durabilityCost) return stack;

            float durability = GetDurability(item);
            float maxDurability = GetMaxDurability(item);
            if (maxDurability <= 0) return stack;
            return stack * (durability / maxDurability);
        }

        // 辅助方法（通过反射或 Publicizer 访问游戏原生 API）
        private static IEnumerable<object> EnumerateItems(object inventory) { /* ... */ }
        private static bool HasTag(object item, string tag) { /* ItemMetaData.Tags */ }
        private static int GetQuality(object item) { /* item.Quality */ }
        private static float GetDurability(object item) { /* item.GetStat("Durability") */ }
        private static float GetMaxDurability(object item) { /* stat.BaseValue */ }
        private static int GetStackCount(object item) { /* item.StackCount */ }
        private static void SetStackCount(object item, int count) { /* item.StackCount = count */ }
    }
}
```

---

## 4. CraftingUtils 集成

### 4.1 `AddCraftingFormula` 内部处理

```csharp
public static void AddCraftingFormula(CraftingFormulaData data, string? modid = null)
{
    // 1. 分离标签成本与标准成本
    var tagCosts = new List<TagItemCost>();
    var standardCosts = new List<(int, long)>();

    foreach (var entry in data.CostItems)
    {
        if (entry.IsTagMatch)
        {
            tagCosts.Add(new TagItemCost
            {
                Tag = entry.ItemTag,
                Amount = entry.Amount,
                MinQuality = entry.MinQuality,
                DurabilityCost = entry.DurabilityCost
            });
        }
        else
        {
            int typeId = entry.ResolveTypeId();
            standardCosts.Add((typeId, entry.Amount));
        }
    }

    // 2. 注册标签成本（如果有）
    if (tagCosts.Count > 0)
    {
        TagCostRegistry.Register(data.Id.Path, new TagCostEntry
        {
            FormulaId = data.Id.Path,
            Costs = tagCosts.ToArray(),
            Modid = modid ?? data.Id.Domain
        });
    }

    // 3. 原生注册（仅标准成本）
    // 调用现有的原生 CraftingUtils.AddCraftingFormula 逻辑
    // ... (保留现有代码)
}
```

### 4.2 modder 使用示例

```csharp
// 饮品配方：标签成本
CraftingUtils.AddCraftingFormula(new CraftingFormulaData
{
    Id = new Identifier("mymod", "coffee"),
    Money = 50,
    CostItems = new[] {
        ItemEntry.Of("mymod:coffee_beans", 3),  // 精确物品
        ItemEntry.ByTag("Water", 1)              // 🆕 任意水标签
    },
    Result = ItemEntry.Of("mymod:coffee", 1),
    Tags = new[] { "WorkBenchAdvanced" }
});

// 弹药配方：火药+金属
CraftingUtils.AddCraftingFormula(new CraftingFormulaData
{
    Id = new Identifier("mymod", "ammo_pack"),
    Money = 200,
    CostItems = new[] {
        ItemEntry.ByTag("Gunpowder", 2),         // 🆕 任意火药
        ItemEntry.ByTag("Metal", 5)               // 🆕 任意金属
    },
    Result = ItemEntry.Of("mymod:bullet_custom", 60),
    Tags = new[] { "WorkBenchAdvanced" }
});

// 修理配方：耐久度折算
CraftingUtils.AddCraftingFormula(new CraftingFormulaData
{
    Id = new Identifier("mymod", "repair_kit"),
    Money = 300,
    CostItems = new[] {
        ItemEntry.ByTag("Armor", 1)
            .WithDurabilityCost(true),            // 🆕 耐久度折算
        ItemEntry.Of("Metal", 10)
    },
    Result = ItemEntry.Of("mymod:repair_kit", 1),
    Tags = new[] { "WorkBenchAdvanced" }
});
```

---

## 5. 与原生配方的关系

### 5.1 混合配方

FML 配方可以混合使用标签成本和标准成本：

```csharp
CostItems = new[] {
    ItemEntry.ByTag("Water", 1),           // 标签成本 → TagCostRegistry
    ItemEntry.Of("mymod:coffee_beans", 3), // 标准成本 → CraftingFormula.cost
    ItemEntry.ByTag("Fuel", 2)             // 标签成本 → TagCostRegistry
}
```

内部处理：
- 标签成本项 → `TagCostRegistry.Register()`
- 标准成本项 → `CraftingFormula.cost.items[]`

合成时：
1. Harmony Prefix 检查/扣除标签成本
2. 原生 `cost.Pay()` 处理标准成本

### 5.2 纯标签配方

如果所有成本项都是标签匹配，则 `CraftingFormula.cost` 只包含金钱，`items` 为空。

---

## 6. 文件布局

```
FastModdingLib/
├── Crafting/
│   ├── TagCostRegistry.cs           (~60 LOC)  标签成本注册表
│   ├── TagCostValidator.cs          (~100 LOC) 标签验证与扣除引擎
│   └── Patches/
│       └── CraftingManagerPatch.cs  (~30 LOC)  Harmony Prefix
├── CraftingUtils.cs                 (修改)      AddCraftingFormula 内部分离逻辑
└── CraftingData.cs                  (已修改)    ItemEntry 扩展字段
```

---

## 7. 验收标准

- [x] `ItemEntry.ByTag("Water", 3)` 字段扩展已在 `CraftingData.cs` 中实现
- [ ] 合成后正确扣除匹配物品（优先低耐久度）— 需运行时验证
- [ ] `ItemEntry.WithDurabilityCost(true)` 耐久度折算正确 — 需运行时验证
- [ ] 混合配方（标签+标准成本）同时生效 — 需运行时验证
- [ ] 无标签成本的配方不受影响（零回归）— 需运行时验证
- [x] 卸载 mod 时 `TagCostRegistry.RemoveAllByOwner(modid)` 清理已实现
- [x] `dotnet build` 通过（0 错误）

---

*本设计的核心模块（`TagCostRegistry`、`TagCostValidator`、`CraftingManagerPatch`）及 `ItemEntry.ByTag()`/`WithDurabilityCost()` 字段已在 `FastModdingLib/Crafting/` 中实现。运行时行为验收（合成消耗逻辑、耐久度折算）留待 Phase 5。*
