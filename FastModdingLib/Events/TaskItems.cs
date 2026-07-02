using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace FastModdingLib.Events
{
    /// <summary>
    /// 所有 handler 包装的基类。持有 <see cref="Priority"/> 与全局自增的
    /// <see cref="RegistrationId"/>，后者作为 <see cref="CompareTo"/> 的二级键，
    /// 修复参考设计中"同优先级 handler 在 SortedSet 中相互覆盖"的缺陷（PLAN §5.1）。
    /// </summary>
    public abstract class TaskItemBase : IComparable<TaskItemBase?>
    {
        private static long _nextId;

        /// <summary>
        /// 注册时指定的优先级。数值越大越先执行。
        /// </summary>
        public int Priority { get; }

        /// <summary>
        /// 全局自增注册 ID，作为同优先级 handler 的稳定二级排序键，
        /// 保证 SortedSet 不会因 CompareTo == 0 而丢弃 handler。
        /// </summary>
        internal long RegistrationId { get; }

        /// <summary>
        /// 注册时关联的 ownerMod（Assembly / ModBehaviour 实例 / 自定义 tag），
        /// 用于 <see cref="EventBus.UnregisterAll"/> 批量卸载。
        /// </summary>
        public object? OwnerMod { get; }

        protected TaskItemBase(int priority, object? ownerMod)
        {
            Priority = priority;
            RegistrationId = Interlocked.Increment(ref _nextId);
            OwnerMod = ownerMod;
        }

        /// <summary>
        /// 比较顺序：先按 Priority 降序意图（这里用升序比较，Post 时按升序遍历，
        /// 数值大者先执行需在调用方处理），再按 RegistrationId 升序保证稳定且不丢 handler。
        /// </summary>
        public int CompareTo(TaskItemBase? other)
        {
            if (other is null) return 1;
            int p = Priority.CompareTo(other.Priority);
            return p != 0 ? p : RegistrationId.CompareTo(other.RegistrationId);
        }

        /// <summary>
        /// 派发事件给被包装的 handler。
        /// </summary>
        public abstract void Delegate(Event evt);
    }

    /// <summary>
    /// 同步 handler 包装。<typeparamref name="T"/> 为目标事件类型。
    /// </summary>
    public sealed class TaskItem<T> : TaskItemBase where T : Event
    {
        private readonly Action<T> _handler;

        public TaskItem(Action<T> handler, int priority, object? ownerMod)
            : base(priority, ownerMod)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public override void Delegate(Event evt)
        {
            if (evt is T typed)
            {
                _handler(typed);
            }
        }

        /// <summary>
        /// 暴露内部 delegate 引用，供 <see cref="EventBus.Unregister{T}"/> 精确匹配。
        /// </summary>
        internal Action<T> Handler => _handler;
    }

    /// <summary>
    /// 异步（UniTask）handler 包装基类。
    /// 同步 <see cref="Delegate(Event)"/> 不应被调用，调用即抛 <see cref="NotSupportedException"/>。
    /// </summary>
    public abstract class AsyncTaskItemBase : TaskItemBase
    {
        protected AsyncTaskItemBase(int priority, object? ownerMod)
            : base(priority, ownerMod)
        {
        }

        /// <summary>
        /// 同步派发对异步 handler 无意义；调用即抛 <see cref="NotSupportedException"/>。
        /// 异步总线应调用 <see cref="DelegateAsync"/>。
        /// </summary>
        public override void Delegate(Event evt)
        {
            throw new NotSupportedException(
                "AsyncTaskItemBase.Delegate is not supported; use DelegateAsync via AsyncEventBus.");
        }

        /// <summary>
        /// 派发事件并返回 <see cref="UniTask"/>；由 <see cref="AsyncEventBus"/> 按序 await。
        /// </summary>
        public abstract UniTask DelegateAsync(Event evt);
    }

    /// <summary>
    /// 异步（UniTask）handler 包装。
    /// </summary>
    public sealed class AsyncTaskItem<T> : AsyncTaskItemBase where T : Event
    {
        private readonly Func<T, UniTask> _handler;

        public AsyncTaskItem(Func<T, UniTask> handler, int priority, object? ownerMod)
            : base(priority, ownerMod)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public override UniTask DelegateAsync(Event evt)
        {
            if (evt is T typed)
            {
                return _handler(typed);
            }
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 暴露内部 delegate 引用，供 <see cref="AsyncEventBus.Unregister{T}"/> 精确匹配。
        /// </summary>
        internal Func<T, UniTask> Handler => _handler;
    }
}