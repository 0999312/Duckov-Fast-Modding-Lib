# FML 案例方案文档

> 基于 FML 框架的完整使用示例
> 最后更新：2026-07-01
>
> **关联设计文档**：
> - [捏脸系统集成](DESIGN-FaceCustomization.md)
> - [扩展 QuestTask 系统](DESIGN-QuestTaskExtension.md) — 不 Patch，新增 Task 子类
> - [标签+耐久度合成](DESIGN-TagCrafting.md) — 双层分离策略
> - [FIX-PLAN-v1](FIX-PLAN-v1.md) — 当前修复计划（Wave 1-3）

---

## 案例 1：饮品商人

**场景描述**：创建一个"饮品商人"模组——在基地建造咖啡机后生成商人 NPC，提供解锁饮品的任务。

### 1.1 创建饮品物品

```csharp
using FastModdingLib;
using FastModdingLib.Utils;

public class DrinkMod : Duckov.Modding.ModBehaviour, IHasModid
{
    string dllPath = Assembly.GetExecutingAssembly().Location;
    public string GetModid() => "drinkmod";

    protected override void OnAfterSetup()
    {
        ModPathResolver.Register(GetModid(), dllPath);
        I18n.InitI18n(GetModid());
        var harmony = new Harmony(GetModid());
        harmony.PatchAll(Assembly.GetExecutingAssembly());

        RegisterDrinkItems();
        RegisterRecipes();
        RegisterQuests();
        RegisterBuilding();
    }
}
```

#### 创建咖啡、茶、果汁等物品

```csharp
void RegisterDrinkItems()
{
    // 咖啡
    ItemUtils.CreateCustomItem(new Identifier("drinkmod", "coffee"), new ItemData
    {
        itemId = 200001, localizationKey = "item_coffee",
        weight = 0.3f, value = 500, maxStackCount = 5, quality = 3,
        spritePath = "coffee_icon.png",
        tags = new List<string> { "Drink", "Food" },
        usages = new UsageData
        {
            useTime = 1.5f,
            behaviors = new List<UsageBehaviorData>
            {
                new FoodData { energyValue = 20, waterValue = 15 }
            }
        }
    });

    // 茶
    ItemUtils.CreateCustomItem(new Identifier("drinkmod", "tea"), new ItemData
    {
        itemId = 200002, localizationKey = "item_tea",
        weight = 0.2f, value = 300, maxStackCount = 5, quality = 2,
        spritePath = "tea_icon.png",
        tags = new List<string> { "Drink", "Food" },
        usages = new UsageData
        {
            useTime = 1.5f,
            behaviors = new List<UsageBehaviorData>
            {
                new FoodData { energyValue = 15, waterValue = 10 }
            }
        }
    });

    // 果汁
    ItemUtils.CreateCustomItem(new Identifier("drinkmod", "juice"), new ItemData
    {
        itemId = 200003, localizationKey = "item_juice",
        weight = 0.3f, value = 400, maxStackCount = 3, quality = 3,
        spritePath = "juice_icon.png",
        tags = new List<string> { "Drink", "Food" },
        usages = new UsageData
        {
            useTime = 1f,
            behaviors = new List<UsageBehaviorData>
            {
                new FoodData { energyValue = 25, waterValue = 20 }
            }
        }
    });

    // 咖啡豆（材料）
    ItemUtils.CreateCustomItem(new Identifier("drinkmod", "coffee_beans"), new ItemData
    {
        itemId = 200010, localizationKey = "item_coffee_beans",
        weight = 0.1f, value = 100, maxStackCount = 20, quality = 1,
        spritePath = "coffee_beans_icon.png",
        tags = new List<string> { "Material", "Food" }
    });

    // 茶叶（材料）
    ItemUtils.CreateCustomItem(new Identifier("drinkmod", "tea_leaves"), new ItemData
    {
        itemId = 200011, localizationKey = "item_tea_leaves",
        weight = 0.05f, value = 80, maxStackCount = 20, quality = 1,
        spritePath = "tea_leaves_icon.png",
        tags = new List<string> { "Material", "Food" }
    });
}
```

### 1.2 创建合成配方（标签驱动）

```csharp
void RegisterRecipes()
{
    // 咖啡配方 —— 需要咖啡豆 + 任意水标签物品
    CraftingUtils.AddCraftingFormula(new CraftingFormulaData
    {
        Id = new Identifier("drinkmod", "coffee_recipe"),
        Money = 50,
        CostItems = new[] {
            ItemEntry.Of("drinkmod:coffee_beans", 3),
            ItemEntry.ByTag("Water", 1)   // 🆕 任意水标签物品
        },
        Result = ItemEntry.Of("drinkmod:coffee", 1),
        Tags = new[] { "WorkBenchAdvanced" },
        RequirePerk = "cooking"
    });

    // 茶配方
    CraftingUtils.AddCraftingFormula(new CraftingFormulaData
    {
        Id = new Identifier("drinkmod", "tea_recipe"),
        Money = 30,
        CostItems = new[] {
            ItemEntry.Of("drinkmod:tea_leaves", 2),
            ItemEntry.ByTag("Water", 1)
        },
        Result = ItemEntry.Of("drinkmod:tea", 1),
        Tags = new[] { "WorkBenchAdvanced" },
        RequirePerk = "cooking"
    });

    // 果汁配方 —— 任意水果标签物品
    CraftingUtils.AddCraftingFormula(new CraftingFormulaData
    {
        Id = new Identifier("drinkmod", "juice_recipe"),
        Money = 40,
        CostItems = new[] {
            ItemEntry.ByTag("Fruit", 3),   // 🆕 任意水果标签物品
            ItemEntry.ByTag("Water", 1)
        },
        Result = ItemEntry.Of("drinkmod:juice", 1),
        Tags = new[] { "WorkBenchAdvanced" },
        RequirePerk = "cooking"
    });
}
```

### 1.3 创建任务链

```csharp
void RegisterQuests()
{
    // 任务1：制作3杯咖啡
    QuestUtils.RegisterQuest(new Identifier("drinkmod", "coffee_master"), new QuestData
    {
        ID = 5001,
        displayName = "quest_coffee_master",
        description = "quest_coffee_master_desc",
        questGiver = QuestGiverID.Fence,
        requireLevel = 1,
        tasks = new List<TaskData>
        {
            new TaskRequireItem
            {
                id = 1,
                itemIdentifier = new Identifier("drinkmod", "coffee"),
                requiredAmount = 3
            }
        },
        rewards = new List<RewardData>
        {
            new RewardEXP { id = 1, amount = 300 },
            new RewardMoney { id = 2, amount = 2000 },
            new RewardUnlockItem { id = 3, itemIdentifier = new Identifier("drinkmod", "tea_recipe_bp") }
        }
    });

    // 任务2：用手枪击杀敌人
    QuestUtils.RegisterQuest(new Identifier("drinkmod", "pistol_hunter"), new QuestData
    {
        ID = 5002,
        displayName = "quest_pistol_hunter",
        description = "quest_pistol_hunter_desc",
        questGiver = QuestGiverID.Fence,
        requireLevel = 2,
        tasks = new List<TaskData>
        {
            new TaskKillByTagData
            {
                id = 1,
                requireAmount = 10,
                requireEnemyName = "Scav",
                // 🆕 使用标签匹配武器——任意手枪标签武器均计数
                weaponTag = "Pistol",
                requireHeadShot = false
            }
        },
        rewards = new List<RewardData>
        {
            new RewardEXP { id = 1, amount = 500 },
            new RewardMoney { id = 2, amount = 3000 }
        }
    });
}
```

### 1.4 注册建筑 + 建造后生成商人

```csharp
void RegisterBuilding()
{
    // 创建咖啡机建筑
    var buildingInfo = new BuildingInfo
    {
        id = "CoffeeMachine",
        prefabName = "Building_CoffeeMachine",
        maxAmount = 1,
        cost = new Cost(money: 3000),
        requireBuildings = new[] { "Workbench" },
        iconReference = ItemUtils.LoadSprite("coffee_machine_icon", 0)
    };

    var building = BuildingUtils.CreateSimpleBuilding(
        new Identifier("drinkmod", "coffee_machine"),
        new Vector2Int(2, 2),
        existingPrefabName: "Building_Workbench"  // 复用工作台结构
    );

    BuildingUtils.RegisterBuilding(
        new Identifier("drinkmod", "coffee_machine"), buildingInfo, building);

    // 🆕 建造完成后生成饮品商人 NPC
    BuildingUtils.OnBuildingBuilt(
        new Identifier("drinkmod", "coffee_machine"),
        (builtBuilding) =>
        {
            var pos = builtBuilding.transform.position + Vector3.right * 3;
            SpawnDrinkMerchant(pos);
        },
        "drinkmod");
}

void SpawnDrinkMerchant(Vector3 position)
{
    var merchantData = new EnemyPresetData
    {
        NameKey = "NPC_Drink_Merchant",
        NpcRole = NpcRole.Merchant,          // 🆕 友善商人角色
        Team = Teams.middle,                 // 中立阵营
        Health = 300,
        ShowHealthBar = false,
        DefaultWeaponOut = false,
        CanTalk = true,
        IsBoss = false,
        ShopProfile = "Merchant_Drink",      // 🆕 关联商店 profile
        Model = ModelRef.GamePrefab("CharacterModel_Duck_Jeff"),
        Loot = new LootConfig
        {
            DropBoxOnDead = false,
            HasCashChance = 0
        }
    };

    // 生成 NPC
    EnemyUtils.RegisterEnemy(
        new Identifier("drinkmod", "drink_merchant"),
        null,  // 商人不需要战斗 AI
        merchantData.ToNative());

    EnemyUtils.SpawnEnemy(
        new Identifier("drinkmod", "drink_merchant"),
        position);
}

// 注册商人商品
void RegisterMerchantShop()
{
    ShopUtils.CreateMerchantProfile("Merchant_Drink");

    ShopUtils.AddGoods(new ShopGoodsData
    {
        merchantProfileID = "Merchant_Drink",
        itemIdentifier = new Identifier("drinkmod", "coffee_beans"),
        maxStock = 10, priceFactor = 1.0f
    }, "drinkmod");

    ShopUtils.AddGoods(new ShopGoodsData
    {
        merchantProfileID = "Merchant_Drink",
        itemIdentifier = new Identifier("drinkmod", "tea_leaves"),
        maxStock = 10, priceFactor = 1.0f
    }, "drinkmod");

    ShopUtils.AddGoods(new ShopGoodsData
    {
        merchantProfileID = "Merchant_Drink",
        itemIdentifier = new Identifier("drinkmod", "coffee"),
        maxStock = 5, priceFactor = 2.0f
    }, "drinkmod");
}
```

### 1.5 本地化（`assets/lang/zh_cn.json`）

```json
{
    "item_coffee": "咖啡",
    "item_coffee_desc": "一杯热咖啡，恢复精力和水分。",
    "item_tea": "茶",
    "item_tea_desc": "一杯清茶，舒缓身心。",
    "item_juice": "果汁",
    "item_juice_desc": "鲜榨果汁，补充维生素。",
    "item_coffee_beans": "咖啡豆",
    "item_tea_leaves": "茶叶",
    "quest_coffee_master": "咖啡大师",
    "quest_coffee_master_desc": "制作3杯咖啡来证明你的手艺。",
    "quest_pistol_hunter": "手枪猎人",
    "quest_pistol_hunter_desc": "使用手枪击杀10名敌人。",
    "NPC_Drink_Merchant": "饮品商人"
}
```

---

## 案例 2：给已有敌人分配武器

**场景描述**：创建几把新武器，让游戏已有的 Scav 和 Boss 有概率使用这些武器。

### 2.1 创建武器和子弹

```csharp
public class WeaponMod : Duckov.Modding.ModBehaviour, IHasModid
{
    public string GetModid() => "weaponmod";

    protected override void OnAfterSetup()
    {
        ModPathResolver.Register(GetModid(), dllPath);
        var harmony = new Harmony(GetModid());
        harmony.PatchAll(Assembly.GetExecutingAssembly());

        RegisterWeapons();
        AssignToExistingEnemies();
    }
}

void RegisterWeapons()
{
    var bundle = AssetUtil.LoadBundle(new Identifier("weaponmod", "weapons"));

    // 自定义 AK（基于 AK 模板 originGunID=654，继承其子弹口径和标签）
    ItemUtils.RegisterGun(
        new Identifier("weaponmod", "ak_custom"),
        bundle, "AK_Custom_Prefab", originGunID: 654);

    // 自定义手枪（基于手枪模板 originGunID=151）
    ItemUtils.RegisterGun(
        new Identifier("weaponmod", "pistol_custom"),
        bundle, "Pistol_Custom_Prefab", originGunID: 151);

    // 自定义 SKS（基于 SKS 模板）
    ItemUtils.RegisterGun(
        new Identifier("weaponmod", "sks_custom"),
        bundle, "SKS_Custom_Prefab", originGunID: 916);

    // 配套子弹（自动从 originGunID 继承口径）
        // AK 使用 5.56，自动注册增强型子弹
    ItemUtils.CreateCustomBullet(new Identifier("weaponmod", "bullet_556_ap"), new BulletData
    {
        itemId = 300201,
        localizationKey = "bullet_556_ap",
        Caliber = "5.56x45",
        damageMultiplier = 1.3f,
        ArmorPiercingGain = 0.5f
    });
}

void AssignToExistingEnemies()
{
    // ⏳ Phase 5 API — 以下方法当前未实现，仅作设计参考
    // 给普通 Scav 20% 概率替换武器为自定义 AK
    EnemyUtils.AddWeaponToExistingPreset(
        "EnemyPreset_Scav",
        new WeaponInjection
        {
            WeaponPool = new[] {
                ItemEntry.Of("weaponmod:ak_custom", 1)
            },
            Chance = 0.2f,
            Qualities = new QualityRange { Min = 2, Max = 4 },
            Durability = new Vector2(0.3f, 0.6f),
            WithMatchingAmmo = true    // 🆕 自动生成匹配口径子弹
        },
        "weaponmod");

    // 给 Elete Scav 30% 概率使用自定义 SKS
    EnemyUtils.AddWeaponToExistingPreset(
        "EnemyPreset_Scav_Elete",
        new WeaponInjection
        {
            WeaponPool = new[] { ItemEntry.Of("weaponmod:sks_custom", 1) },
            Chance = 0.3f,
            Qualities = new QualityRange { Min = 3, Max = 5 },
            WithMatchingAmmo = true
        },
        "weaponmod");

    // 给 Boss Red 10% 概率掉落自定义武器
    EnemyUtils.AddLootToExistingPreset(
        "EnemyPreset_Boss_Red",
        ItemEntry.Of("weaponmod:ak_custom", 1),
        chance: 0.1f,
        "weaponmod");

    // 给所有 Scav 5% 概率使用自定义手枪
    EnemyUtils.AddWeaponToExistingPreset(
        "EnemyPreset_Scav",
        new WeaponInjection
        {
            WeaponPool = new[] { ItemEntry.Of("weaponmod:pistol_custom", 1) },
            Chance = 0.05f,
            Qualities = new QualityRange { Min = 1, Max = 3 },
            WithMatchingAmmo = true
        },
        "weaponmod");
}
```

### 2.2 `EnemyUtils` 新增 API（Phase 5 实现）

```csharp
// WeaponInjection —— 武器注入配置
public class WeaponInjection
{
    public ItemEntry[] WeaponPool;
    public float Chance;
    public QualityRange? Qualities;
    public Vector2 Durability;
    public bool WithMatchingAmmo;
}

// EnemyUtils 新增方法
public static void AddWeaponToExistingPreset(
    string presetAssetName,   // 如 "EnemyPreset_Scav"
    WeaponInjection injection,
    string modid)
{
    // 1. 通过 Resources/GameplayDataSettings 找到 CharacterRandomPreset
    // 2. 反射读取 itemsToGenerate 列表
    // 3. 追加武器条目（带 Chance 概率控制）
    // 4. 设置 bulletQualityDistribution 使子弹匹配
}

public static void AddLootToExistingPreset(
    string presetAssetName,
    ItemEntry lootItem,
    float chance,
    string modid)
{
    // 在 itemsToGenerate 末尾追加额外掉落条目
}
```

---

## 案例 3：标签驱动的合成与任务

**场景描述**：创建基于标签而非具体 typeID 的合成配方和击杀任务。

### 3.1 标签配方

```csharp
void RegisterTagBasedRecipes()
{
    // 弹药配方 —— 火药+金属
    CraftingUtils.AddCraftingFormula(new CraftingFormulaData
    {
        Id = new Identifier("weaponmod", "ammo_pack"),
        Money = 200,
        CostItems = new[] {
            ItemEntry.ByTag("Gunpowder", 2),   // 🆕 任意火药标签
            ItemEntry.ByTag("Metal", 5),        // 🆕 任意金属标签
        },
        Result = ItemEntry.Of("weaponmod:bullet_556_ap", 60),
        Tags = new[] { "WorkBenchAdvanced" }
    });
}
```

### 3.2 标签击杀任务

```csharp
void RegisterTagKillQuests()
{
    // 任务：使用任意手枪击杀 15 名敌人
    QuestUtils.RegisterQuest(new Identifier("weaponmod", "pistol_expert"), new QuestData
    {
        ID = 6001,
        displayName = "quest_pistol_expert",
        description = "quest_pistol_expert_desc",
        questGiver = QuestGiverID.Fence,
        requireLevel = 3,
        tasks = new List<TaskData>
        {
            new TaskKillByTagData
            {
                id = 1,
                requireAmount = 15,
                requireEnemyName = "Scav",
                weaponTag = "Pistol",          // 🆕 标签匹配——任意手枪
                requireHeadShot = false
            }
        },
        rewards = new List<RewardData>
        {
            new RewardEXP { id = 1, amount = 800 },
            new RewardMoney { id = 2, amount = 5000 }
        }
    });

    // 任务：使用任意狙击枪爆头击杀 5 名 Boss
    QuestUtils.RegisterQuest(new Identifier("weaponmod", "sniper_master"), new QuestData
    {
        ID = 6002,
        displayName = "quest_sniper_master",
        description = "quest_sniper_master_desc",
        questGiver = QuestGiverID.Fence,
        requireLevel = 5,
        tasks = new List<TaskData>
        {
            new TaskKillByTagData
            {
                id = 1,
                requireAmount = 5,
                requireEnemyName = "Boss",
                weaponTag = "Sniper",          // 🆕 任意狙击枪标签
                requireHeadShot = true
            }
        },
        rewards = new List<RewardData>
        {
            new RewardEXP { id = 1, amount = 1500 },
            new RewardGiveItem { id = 2, itemIdentifier = new Identifier("weaponmod", "sks_custom"), amount = 1 }
        }
    });
}
```

### 3.3 耐久度折算配方

```csharp
void RegisterDurabilityRecipes()
{
    // 修理配方 —— 消耗任意护甲（耐久度折算）
    CraftingUtils.AddCraftingFormula(new CraftingFormulaData
    {
        Id = new Identifier("weaponmod", "repair_kit_recipe"),
        Money = 300,
        CostItems = new[] {
            ItemEntry.ByTag("Armor", 1)
                .WithDurabilityCost(true),    // 🆕 耐久度折算
            ItemEntry.Of("Metal", 10)
        },
        Result = ItemEntry.Of("weaponmod:repair_kit", 1),
        Tags = new[] { "WorkBenchAdvanced" }
    });

    // 分解——回收任意武器（耐久度越高回收材料越多）
    CraftingUtils.AddDecomposeFormula(new DecomposeFormulaData
    {
        Id = new Identifier("weaponmod", "scrap_weapon"),
        SourceItemId = null,  // 不指定——modder 可右键任意武器
        Money = 100,
        ResultItems = new[] {
            ItemEntry.ByTag("Metal", 5),
            ItemEntry.ByTag("Gunpowder", 2)
        }
    });
}
```

### 3.4 耐久度折算逻辑（实现思路）

```csharp
// FML 内部实现（Phase 5）
// 当 ItemEntry.DurabilityCost == true 时：
// 1. 搜索玩家背包中匹配标签/口径的物品
// 2. 按耐久度从低到高排序（优先消耗低耐久物品）
// 3. 累计消耗量 = Amount / (当前耐久度 / 最大耐久度)
//    例如：需要1件护甲耐久度折算：
//      - 一件满耐久护甲 = 1.0 → 满足需求
//      - 一件20%耐久护甲 = 0.2 → 需要额外物品
//      - 两件50%耐久护甲 = 1.0 → 满足需求
```

---

## 附录：本例中使用的 FML API 总览

### 新实现（Wave 2）

| API | 案例 |
|-----|------|
| `EnemyPresetData` DTO | 案例1-步骤4 |
| `NpcRole.Merchant` / `.QuestGiver` | 案例1-步骤4 |
| `ModelRef.GamePrefab()` | 案例1-步骤4 |
| `BuildingUtils.CreateSimpleBuilding()` | 案例1-步骤4 |
| `BuildingUtils.OnBuildingBuilt()` 真回调 | 案例1-步骤4 |
| `ItemEntry.ByTag()` | 案例1-步骤2, 案例3 |
| `ItemEntry.WithDurabilityCost()` | 案例3 |

### 待实现（Phase 5）

| API | 案例 |
|-----|------|
| `EnemyUtils.AddWeaponToExistingPreset()` | 案例2-步骤2 |
| `EnemyUtils.AddLootToExistingPreset()` | 案例2-步骤2 |
| `TaskKillCount.weaponTag` | 案例1-步骤3, 案例3 |
| 耐久度折算逻辑 | 案例3 |

---

*这三个案例覆盖了 FML 框架 90% 的典型 modder 场景。具体 API 签名以实际代码为准。*
