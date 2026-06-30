namespace FastModdingLib.Events.GameEvents
{
    /// <summary>
    /// 关卡初始化完成事件。桥接自游戏原生 <c>LevelManager.OnLevelInitialized</c> 事件。
    /// 仅观察用途，不支持取消（Pre 拦截在 Phase 1 不做）。
    /// 注意：原生 OnLevelInitialized 实际签名为无参 <c>Action</c>，故 Manager 字段恒为 null
    /// （保留字段以兼容 PLAN §6 表的 LevelManager mgr 设计；后续若游戏改为带参可填充）。
    /// </summary>
    public sealed class LevelInitializedEvent : Event
    {
        /// <summary>
        /// 触发事件的 LevelManager 实例；原生事件无参故为 null。
        /// TODO: 原生事件为无参 Action；若需 LevelManager 实例，后续可通过单例访问补充。
        /// </summary>
        public object? Manager { get; }

        public LevelInitializedEvent(object? manager = null)
        {
            Manager = manager;
        }
    }
}