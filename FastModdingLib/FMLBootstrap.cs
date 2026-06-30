using FastModdingLib.Events;
using FastModdingLib.Register;

namespace FastModdingLib
{
    /// <summary>
    /// FML 全局单例生命周期入口。
    /// Registry 元表、EventBus Runner 等游戏级单例应在整个游戏会话中只初始化一次、
    /// 只销毁一次。由于 FML 作为其他模组的前置依赖，天然最先加载、最后卸载，
    /// 因此由 FML 自身的 <see cref="ModBehaviour"/> 实例承载单例的 init/teardown。
    /// </summary>
    /// <remarks>
    /// <see cref="EnsureInit"/> 幂等，其他继承 <see cref="ModBehaviour"/> 的模组
    /// 在 <c>base.OnAfterSetup()</c> 中重复调用安全（直接返回）。
    /// <see cref="TearDown"/> 仅在 FML 自身卸载时由 <see cref="ModBehaviour.OnBeforeDeactivate"/>
    /// 调用——因为依赖顺序保证 FML 最后卸载。
    /// </remarks>
    public static class FMLBootstrap
    {
        private static bool _initialized;

        /// <summary>
        /// 确保游戏级单例已初始化。幂等，重复调用安全。
        /// </summary>
        public static void EnsureInit()
        {
            if (_initialized) return;
            _initialized = true;

            RegisterBootstrap.Init();
            EventBusBootstrap.Init();
        }

        /// <summary>
        /// 卸载指定 mod 在 Registry 元表中注册的全部条目。
        /// 不销毁 EventBus Runner（它是游戏级单例，生命周期独立于单个 mod）。
        /// </summary>
        public static void TearDownMod(string modid)
        {
            RegisterBootstrap.TearDown(modid);
        }

        /// <summary>
        /// 销毁所有游戏级单例（Registry 元表清空 + EventBus Runner 销毁 + 原生事件解除）。
        /// 仅应在 FML 自身卸载时调用一次。
        /// </summary>
        public static void TearDown()
        {
            EventBusBootstrap.TearDown();
            _initialized = false;
        }
    }
}
