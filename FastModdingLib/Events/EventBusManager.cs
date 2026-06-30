using FastModdingLib.Utils;

namespace FastModdingLib.Events
{
    /// <summary>
    /// EventBus 总管单例。承载同步与异步两条总线。
    /// 继承 <see cref="Singleton{T}"/>（纯 C# Lazy，无 GameObject）。
    /// </summary>
    public sealed class EventBusManager : Singleton<EventBusManager>
    {
        private readonly EventBus _sync;
        private readonly AsyncEventBus _async;

        private EventBusManager()
        {
            _sync = new EventBus();
            _async = new AsyncEventBus();
        }

        /// <summary>
        /// 同步事件总线。
        /// </summary>
        public EventBus Sync => _sync;

        /// <summary>
        /// 异步（协程）事件总线。需在 Bootstrap 阶段注入 EventBusRunner 后方可 Post。
        /// </summary>
        public AsyncEventBus Async => _async;

        /// <summary>
        /// 清空同步与异步总线的所有 handler。供 hot-reload / mod 卸载使用。
        /// </summary>
        public void Clear()
        {
            _sync.Clear();
            _async.Clear();
        }
    }
}