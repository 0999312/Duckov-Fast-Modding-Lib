using FastModdingLib.Modding;
using FastModdingLib.Options;
using HarmonyLib;
using System.Reflection;

namespace FastModdingLib
{
    /// <summary>
    /// FML 自身的 mod 入口类。由游戏 ModManager 实例化并调用生命周期方法。
    /// </summary>
    /// <remarks>
    /// <para>本类负责 FML 作为前置模组的全部初始化：</para>
    /// <list type="bullet">
    /// <item>创建 Harmony 实例并 Patch FML 内置补丁</item>
    /// <item>应用 ModManager 排序/自激活补丁（全局生效）</item>
    /// <item>启动 Registry 元表 + EventBus 等游戏级单例（<see cref="FMLBootstrap"/>)</item>
    /// </list>
    /// <para><b>依赖 FML 的模组不应继承本类。</b>请直接继承
    /// <c>Duckov.Modding.ModBehaviour</c> 并实现 <see cref="IHasModid"/> 接口。详见文档。</para>
    /// </remarks>
    public class ModBehaviour : Duckov.Modding.ModBehaviour, IHasModid
    {
        private Harmony? _harmony;

        /// <summary>
        /// FML 自身的模组标识符，固定返回 <c>"FastModdingLib"</c>。
        /// </summary>
        public string GetModid() => "FastModdingLib";

        public void Awake() { }

        /// <summary>
        /// FML 初始化入口，由 ModManager 调用。
        /// 依次执行：Harmony 内置补丁 → ModManager 排序补丁 → 单例启动。
        /// </summary>
        protected override void OnAfterSetup()
        {
            _harmony = new Harmony(GetModid());

            // 仅 Patch FML 自身的 [HarmonyPatch]（AudioObjectMixin、OtherPatches 等）
            _harmony.PatchAll(Assembly.GetExecutingAssembly());

            // ModManager 排序/自激活补丁（全局生效，后续 Rescan 时接管 mod 加载顺序）
            ModManagerPatches.EnsurePatched();

            // Registry 元表 + EventBus 单例初始化（幂等：FML 最先加载时首次生效，后续子模组调用直接返回）
            FMLBootstrap.EnsureInit();
        }

        /// <summary>
        /// FML 卸载入口，由 ModManager 调用（依赖顺序确保 FML 最后卸载）。
        /// 依次：解除 Harmony → 清理 Registry 条目 → 销毁游戏级单例 → 面板/资源清理。
        /// </summary>
        protected override void OnBeforeDeactivate()
        {
            _harmony?.UnpatchAll();
            _harmony = null;

            FMLBootstrap.TearDownMod(GetModid());
            FMLBootstrap.TearDown();

            ModOptionsRegistry.UnregisterAllPanels();
            AssetUtil.UnloadAllBundles();
            ShopUtils.RemoveAllProfiles(GetModid());

            base.OnBeforeDeactivate();
        }
    }
}
