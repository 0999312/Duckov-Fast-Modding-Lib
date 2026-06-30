using FastModdingLib.Events.Adapters;
using UnityEngine;

namespace FastModdingLib.Events
{
    /// <summary>
    /// EventBus 模块启动/卸载入口。
    /// <see cref="Init"/> 创建协程宿主并桥接原生事件；
    /// <see cref="TearDown"/> 解除原生事件订阅并清空 handler 队列。
    /// </summary>
    /// <remarks>
    /// EventBusRunner GameObject 是游戏级单例（DontDestroyOnLoad），生命周期独立于单个 mod。
    /// 由 <see cref="FMLBootstrap"/> 统一管理 init/teardown 时机——FML 最先加载时创建，
    /// FML 最后卸载时销毁。
    /// </remarks>
    public static class EventBusBootstrap
    {
        private static EventBusRunner? _runner;
        private static GameObject? _runnerGo;

        /// <summary>
        /// 创建 <see cref="EventBusRunner"/> GameObject（DontDestroyOnLoad），注入
        /// <see cref="EventBusManager.Async"/>，并 <see cref="GameEventAdapters.WireUp"/>
        /// 桥接 15 个游戏原生事件。幂等：已初始化时直接返回。
        /// </summary>
        public static void Init()
        {
            if (_runner != null) return;

            _runnerGo = new GameObject("[FML EventBusRunner]");
            _runner = _runnerGo.AddComponent<EventBusRunner>();
            Object.DontDestroyOnLoad(_runnerGo);

            EventBusManager.Instance.Async.Init(_runner);
            GameEventAdapters.WireUp();
        }

        /// <summary>
        /// 销毁 EventBus 游戏级单例：停止协程、解除原生事件、清空 handler
        /// 队列、销毁 Runner GameObject。仅应在 FML 自身卸载时调用一次。
        /// </summary>
        public static void TearDown()
        {
            GameEventAdapters.TearDown();
            EventBusManager.Instance.Clear();

            if (_runner != null)
            {
                _runner.StopAll();
            }
            if (_runnerGo != null)
            {
                Object.Destroy(_runnerGo);
                _runnerGo = null;
            }
            _runner = null;
        }

        /// <summary>
        /// 仅清空 EventBus handler 队列 + 解除原生事件订阅，
        /// <b>不销毁</b> Runner GameObject（它是游戏级单例）。
        /// 用于 hot-reload 场景下重置状态而不重建 Runner。
        /// </summary>
        public static void Reset()
        {
            GameEventAdapters.TearDown();
            EventBusManager.Instance.Clear();

            if (_runner != null)
            {
                _runner.StopAll();
            }

            // 重新桥接（runner 仍存在）
            if (_runner != null)
            {
                EventBusManager.Instance.Async.Init(_runner);
                GameEventAdapters.WireUp();
            }
        }
    }
}
