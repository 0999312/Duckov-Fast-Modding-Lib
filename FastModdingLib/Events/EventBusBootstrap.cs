using FastModdingLib.Events.Adapters;

namespace FastModdingLib.Events
{
    /// <summary>
    /// EventBus 模块启动/卸载入口。
    /// <see cref="Init"/> 桥接 15 个游戏原生事件到 FML EventBus；
    /// <see cref="TearDown"/> 解除原生事件订阅并清空 handler 队列。
    /// </summary>
    /// <remarks>
    /// 异步事件总线基于 UniTask PlayerLoop 调度，无需 MonoBehaviour 协程宿主。
    /// 由 <see cref="FMLBootstrap"/> 统一管理 init/teardown 时机。
    /// </remarks>
    public static class EventBusBootstrap
    {
        /// <summary>
        /// 桥接游戏原生事件。幂等：已初始化时直接返回。
        /// </summary>
        public static void Init()
        {
            GameEventAdapters.WireUp();
        }

        /// <summary>
        /// 解除原生事件订阅并清空 handler 队列。
        /// </summary>
        public static void TearDown()
        {
            GameEventAdapters.TearDown();
            EventBusManager.Instance.Clear();
        }

        /// <summary>
        /// 仅清空 EventBus handler 队列 + 解除原生事件订阅，
        /// 然后重新桥接。用于 hot-reload 场景下重置状态。
        /// </summary>
        public static void Reset()
        {
            GameEventAdapters.TearDown();
            EventBusManager.Instance.Clear();

            // 重新桥接
            GameEventAdapters.WireUp();
        }
    }
}
