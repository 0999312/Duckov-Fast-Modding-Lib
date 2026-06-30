using FastModdingLib.Utils;

namespace FastModdingLib.Register
{
    /// <summary>
    /// Register 模块启动/卸载入口（PLAN-Register §6）。由 <c>ModBehaviour.OnAfterSetup</c>
    /// 调用 <see cref="Init"/> 把各模块 registry 注册到 <see cref="RegistryManager.Registry"/>
    /// 元表；<c>OnBeforeDeactivate</c> 调 <see cref="TearDown"/> 按当前 modid 批量卸载。
    /// </summary>
    /// <remarks>
    /// Audio / Crafting / Items 三个 registry 已在各自静态构造 / Singleton 构造中自注册到元表，
    /// 此处不重复注册（<see cref="NonAlterableSimpleRegistry{T}"/> 重复 key 抛异常）。
    /// 仅注册 Quests 与 Shop 两个未自注册的 registry。
    /// </remarks>
    public static class RegisterBootstrap
    {
        /// <summary>
        /// 把各模块 registry 注册到 <see cref="RegistryManager.Registry"/> 元表。
        /// 幂等：重复调用不会抛异常（使用 <see cref="NonAlterableSimpleRegistry{T}.SetIfAbsent"/>）。
        /// 各子模块 Init() 内部自注册到元表（Buff / Building / PerkTree 有独立 Init，均幂等）。
        /// </summary>
        public static void Init()
        {
            var meta = RegistryManager.Instance.Registry;

            // —— 各模块 Init（幂等，内部 SetIfAbsent） ——
            BuffUtils.Init();
            BuildingUtils.Init();
            PerkTreeUtils.Init();
            EnemyUtils.Init();

            // —— Quests / Shop 由本 bootstrap 注册（不自注册，原因见模块文档） ——
            meta.SetIfAbsent(
                new Identifier("fastmoddinglib", "quest"),
                QuestUtils.Registry,
                RegistryManager.CurrentModid);
            meta.SetIfAbsent(
                new Identifier("fastmoddinglib", "shop"),
                ShopUtils.Registry,
                RegistryManager.CurrentModid);
        }

        /// <summary>
        /// 遍历元表所有 registry，按 <paramref name="modid"/> 批量卸载其注册条目
        /// （各 registry 的 <c>OnRemoved</c> 回调完成 native 侧善后）。
        /// </summary>
        public static void TearDown(string modid)
        {
            RegistryManager.Instance.RemoveAllByOwner(modid);
        }
    }
}