using FastModdingLib.Utils;

namespace FastModdingLib
{
    // ═══════════════════════════════════════════════════════════════
    //  ItemEntry — 单个物品引用（消耗材料 / 产物 / 分解结果通用）
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 配方中的单个物品条目。支持 Identifier 或 int typeID 两种引用方式。
    /// <see cref="ItemId"/> 有值时优先解析为 typeID，解析失败回退到 <see cref="ItemTypeId"/>。
    /// </summary>
    public struct ItemEntry
    {
        /// <summary>可选：物品 Identifier。设置后优先解析。</summary>
        public Identifier? ItemId;

        /// <summary>物品 typeID。ItemId 解析失败时的回退值。</summary>
        public int ItemTypeId;

        /// <summary>数量。</summary>
        public int Amount;

        // ── 工厂方法 ──

        /// <summary>从 int typeID 创建。</summary>
        public static ItemEntry Of(int typeId, int amount) => new ItemEntry()
        {
            ItemTypeId = typeId,
            Amount = amount
        };

        /// <summary>从 Identifier 创建。</summary>
        public static ItemEntry Of(Identifier id, int amount) => new ItemEntry()
        {
            ItemId = id,
            Amount = amount
        };

        /// <summary>从 "domain:path" 字符串创建。</summary>
        public static ItemEntry Of(string idString, int amount) => new ItemEntry()
        {
            ItemId = Identifier.Parse(idString),
            Amount = amount
        };

        /// <summary>解析为 int typeID。</summary>
        internal int ResolveTypeId() => ItemUtils.ResolveItemRef(ItemId, ItemTypeId);
    }

    // ═══════════════════════════════════════════════════════════════
    //  CraftingFormulaData
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 合成配方数据。
    /// </summary>
    /// <example>
    /// <code>
    /// CraftingUtils.AddCraftingFormula(new CraftingFormulaData
    /// {
    ///     Id = new Identifier("mymod", "coffee"),
    ///     Money = 100,
    ///     CostItems = new[] { ItemEntry.Of(1001, 5), ItemEntry.Of("mymod:beans", 2) },
    ///     Result = ItemEntry.Of("mymod:coffee", 10),
    ///     Tags = new[] { "WorkBenchAdvanced" },
    ///     RequirePerk = "cooking"
    /// });
    ///
    /// // Builder 方式
    /// CraftingUtils.AddCraftingFormula(
    ///     CraftingFormulaData.Builder
    ///         .Create("mymod:coffee")
    ///         .Money(100)
    ///         .AddCost(1001, 5)
    ///         .AddCost("mymod:beans", 2)
    ///         .Result("mymod:coffee", 10)
    ///         .Tags("WorkBenchAdvanced")
    ///         .Build());
    /// </code>
    /// </example>
    public struct CraftingFormulaData
    {
        /// <summary>配方 Identifier（domain = modid）。必填。</summary>
        public Identifier Id;

        /// <summary>所需金钱。</summary>
        public long Money;

        /// <summary>消耗材料列表。</summary>
        public ItemEntry[] CostItems;

        /// <summary>产物。</summary>
        public ItemEntry Result;

        /// <summary>工作台标签。默认 ["WorkBenchAdvanced"]。</summary>
        public string[] Tags;

        /// <summary>前置技能（空字符串 = 无）。</summary>
        public string RequirePerk;

        /// <summary>是否默认解锁。默认 true。</summary>
        public bool UnlockByDefault;

        /// <summary>是否在配方索引中隐藏。默认 false。</summary>
        public bool HideInIndex;

        /// <summary>是否在 Demo 中锁定。默认 false。</summary>
        public bool LockInDemo;

        // ── 便捷工厂 ──

        /// <summary>快速创建（仅 Id）。其余字段用对象初始化器设置。</summary>
        public static CraftingFormulaData Create(Identifier id) => new CraftingFormulaData()
        {
            Id = id,
            UnlockByDefault = true,
            Tags = new[] { "WorkBenchAdvanced" }
        };

        /// <summary>快速创建（从 "domain:path" 字符串）。</summary>
        public static CraftingFormulaData Create(string idString) => Create(Identifier.Parse(idString));

        // ── Builder ──

        /// <summary>获取 Builder 以流式构建。</summary>
        public static BuilderType Builder => default;

        /// <summary>流式 Builder。</summary>
        public struct BuilderType
        {
            /// <summary>从 "domain:path" 字符串开始构建。</summary>
            public CraftingFormulaBuilder Create(string idString) => new CraftingFormulaBuilder(Identifier.Parse(idString));

            /// <summary>从 Identifier 开始构建。</summary>
            public CraftingFormulaBuilder Create(Identifier id) => new CraftingFormulaBuilder(id);
        }

        public class CraftingFormulaBuilder
        {
            private CraftingFormulaData _data;

            internal CraftingFormulaBuilder(Identifier id)
            {
                _data = new CraftingFormulaData
                {
                    Id = id,
                    UnlockByDefault = true,
                    Tags = new[] { "WorkBenchAdvanced" }
                };
            }

            public CraftingFormulaBuilder Money(long money) { _data.Money = money; return this; }

            public CraftingFormulaBuilder AddCost(int typeId, int amount)
            {
                _data.CostItems = Append(_data.CostItems, ItemEntry.Of(typeId, amount));
                return this;
            }

            public CraftingFormulaBuilder AddCost(Identifier id, int amount)
            {
                _data.CostItems = Append(_data.CostItems, ItemEntry.Of(id, amount));
                return this;
            }

            public CraftingFormulaBuilder AddCost(string idString, int amount)
            {
                _data.CostItems = Append(_data.CostItems, ItemEntry.Of(idString, amount));
                return this;
            }

            public CraftingFormulaBuilder CostItems(ItemEntry[] items) { _data.CostItems = items; return this; }

            public CraftingFormulaBuilder Result(int typeId, int amount) { _data.Result = ItemEntry.Of(typeId, amount); return this; }
            public CraftingFormulaBuilder Result(Identifier id, int amount) { _data.Result = ItemEntry.Of(id, amount); return this; }
            public CraftingFormulaBuilder Result(string idString, int amount) { _data.Result = ItemEntry.Of(idString, amount); return this; }

            public CraftingFormulaBuilder Tags(params string[] tags) { _data.Tags = tags; return this; }
            public CraftingFormulaBuilder RequirePerk(string perk) { _data.RequirePerk = perk; return this; }
            public CraftingFormulaBuilder UnlockByDefault(bool value) { _data.UnlockByDefault = value; return this; }
            public CraftingFormulaBuilder HideInIndex(bool value) { _data.HideInIndex = value; return this; }
            public CraftingFormulaBuilder LockInDemo(bool value) { _data.LockInDemo = value; return this; }

            public CraftingFormulaData Build() => _data;

            private static ItemEntry[] Append(ItemEntry[]? existing, ItemEntry item)
            {
                if (existing == null) return new[] { item };
                var result = new ItemEntry[existing.Length + 1];
                System.Array.Copy(existing, result, existing.Length);
                result[existing.Length] = item;
                return result;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  DecomposeFormulaData
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 分解配方数据。
    /// </summary>
    /// <example>
    /// <code>
    /// CraftingUtils.AddDecomposeFormula(new DecomposeFormulaData
    /// {
    ///     Id = new Identifier("mymod", "scrap_old_gun"),
    ///     SourceItemId = new Identifier("mymod", "old_gun"),
    ///     Money = 50,
    ///     ResultItems = new[] { ItemEntry.Of(1001, 3), ItemEntry.Of(1002, 1) }
    /// });
    /// </code>
    /// </example>
    public struct DecomposeFormulaData
    {
        /// <summary>配方 Identifier（domain = modid）。必填。</summary>
        public Identifier Id;

        /// <summary>可选：被分解物品的 Identifier。设置后优先解析为 typeID。</summary>
        public Identifier? SourceItemId;

        /// <summary>被分解物品的 typeID（SourceItemId 解析失败时回退）。</summary>
        public int SourceItemTypeId;

        /// <summary>返还金钱。</summary>
        public long Money;

        /// <summary>分解产物列表。</summary>
        public ItemEntry[] ResultItems;

        // ── 便捷工厂 ──

        /// <summary>快速创建（分解已知 typeID 的物品）。</summary>
        public static DecomposeFormulaData Create(Identifier id, int sourceTypeId) => new DecomposeFormulaData()
        {
            Id = id,
            SourceItemTypeId = sourceTypeId
        };

        /// <summary>快速创建（分解由 Identifier 引用的物品）。</summary>
        public static DecomposeFormulaData Create(Identifier id, Identifier sourceId) => new DecomposeFormulaData()
        {
            Id = id,
            SourceItemId = sourceId
        };

        // ── Builder ──

        public static DecomposeBuilderType Builder => default;

        public struct DecomposeBuilderType
        {
            public DecomposeFormulaBuilder Create(Identifier id) => new DecomposeFormulaBuilder(id);
            public DecomposeFormulaBuilder Create(string idString) => new DecomposeFormulaBuilder(Identifier.Parse(idString));
        }

        public class DecomposeFormulaBuilder
        {
            private DecomposeFormulaData _data;

            internal DecomposeFormulaBuilder(Identifier id)
            {
                _data = new DecomposeFormulaData { Id = id };
            }

            public DecomposeFormulaBuilder SourceItem(int typeId) { _data.SourceItemTypeId = typeId; return this; }
            public DecomposeFormulaBuilder SourceItem(Identifier id) { _data.SourceItemId = id; return this; }
            public DecomposeFormulaBuilder SourceItem(string idString) { _data.SourceItemId = Identifier.Parse(idString); return this; }

            public DecomposeFormulaBuilder Money(long money) { _data.Money = money; return this; }

            public DecomposeFormulaBuilder AddResult(int typeId, int amount)
            {
                _data.ResultItems = Append(_data.ResultItems, ItemEntry.Of(typeId, amount));
                return this;
            }

            public DecomposeFormulaBuilder AddResult(Identifier id, int amount)
            {
                _data.ResultItems = Append(_data.ResultItems, ItemEntry.Of(id, amount));
                return this;
            }

            public DecomposeFormulaBuilder AddResult(string idString, int amount)
            {
                _data.ResultItems = Append(_data.ResultItems, ItemEntry.Of(idString, amount));
                return this;
            }

            public DecomposeFormulaBuilder ResultItems(ItemEntry[] items) { _data.ResultItems = items; return this; }

            public DecomposeFormulaData Build() => _data;

            private static ItemEntry[] Append(ItemEntry[]? existing, ItemEntry item)
            {
                if (existing == null) return new[] { item };
                var result = new ItemEntry[existing.Length + 1];
                System.Array.Copy(existing, result, existing.Length);
                result[existing.Length] = item;
                return result;
            }
        }
    }
}
